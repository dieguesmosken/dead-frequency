// Scripts/World/CityGenerator.Services.cs
// Serviços da cidade (v0.8): postos de gasolina paródia (Petrobraz, Ipiranguinha, Shello) com loja de
// conveniência e totem de preços, o Lava-Jato Molhadinho, a caixa d'água municipal numa praça e
// outdoors na beira da cidade, iluminados de baixo para cima à noite (o anúncio muda a cada partida).
// Também guarda os obstáculos das calçadas para os pedestres desviarem.
using System.Collections.Generic;
using Godot;

namespace DeadFrequency;

public partial class CityGenerator
{
    [ExportGroup("Serviços")]
    [Export] public Vector2I WaterTowerBlock { get; set; } = new(-1, -1);
    [Export] public Vector2I CarWashBlock { get; set; } = new(2, 1);

    private static readonly (Vector2I Block, string Id)[] GasStations =
    {
        (new Vector2I(2, 1), "petrobraz"),
        (new Vector2I(-2, -2), "ipiranguinha"),
        (new Vector2I(-3, 1), "shello"),
    };

    private static readonly string[] AdIds =
        { "energetico", "radio966", "pizza", "omoo", "guarana", "fisk", "casas", "correrios", "deadfreq", "petrobraz" };

    /// <summary>Postes, placas, orelhões etc. na calçada (os pedestres desviam).</summary>
    public List<Vector3> SidewalkObstacles { get; } = new();

    private readonly Dictionary<string, StandardMaterial3D> _texMaterials = new();
    /// <summary>Anúncios sorteados nesta partida (outdoors e ônibus usam a mesma lista).</summary>
    private static List<string> _runAds;

    public void RegisterObstacle(Vector3 position) => SidewalkObstacles.Add(position);

    // ---- pontos usados pelas profissões (v0.9)
    /// <summary>Caçambas de lixo (gari).</summary>
    public List<Vector3> DumpsterSpots { get; } = new();
    /// <summary>Vaga entre as bombas de cada posto e a marca (frentista).</summary>
    public List<(Vector3 Position, string Brand)> GasSpots { get; } = new();
    /// <summary>Prateleiras do supermercado: posição da prateleira e rotação (repositor).</summary>
    public List<(Vector3 Position, float RotY)> MarketShelfSlots { get; } = new();
    public Vector3 MarketStockPoint { get; private set; }
    public Vector3 MarketEntrance { get; private set; }
    /// <summary>Mesa de som da rádio (locutor). Zero = fase sem rádio.</summary>
    public Vector3 RadioConsolePoint { get; private set; }

    /// <summary>Lista embaralhada de anúncios, diferente a cada vez que o jogo abre.</summary>
    public static List<string> RunAds()
    {
        if (_runAds != null)
            return _runAds;
        var rng = new RandomNumberGenerator();
        rng.Randomize();
        _runAds = new List<string>(AdIds);
        for (int i = _runAds.Count - 1; i > 0; i--)
        {
            int j = rng.RandiRange(0, i);
            (_runAds[i], _runAds[j]) = (_runAds[j], _runAds[i]);
        }
        return _runAds;
    }

    public static string AdTexturePath(string id) => id == "teuzin" ? "res://Assets/Textures/Ads/ad_teuzin.svg" : $"res://Assets/Textures/Ads/ad_{id}.png";

    /// <summary>Material com textura (opcionalmente acesa), com cache por arquivo.</summary>
    private StandardMaterial3D TexMaterial(string path, float glow = 0f)
    {
        string key = path + "|" + glow;
        if (_texMaterials.TryGetValue(key, out StandardMaterial3D mat))
            return mat;
        if (!ResourceLoader.Exists(path))
            return null;
        var tex = GD.Load<Texture2D>(path);
        mat = new StandardMaterial3D { AlbedoTexture = tex, Roughness = 0.4f, TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmapsAnisotropic };
        if (glow > 0f)
        {
            mat.EmissionEnabled = true;
            mat.EmissionTexture = tex;
            mat.EmissionEnergyMultiplier = glow;
            _signFaces.Add(mat); // acende mais à noite junto com os letreiros
        }
        _texMaterials[key] = mat;
        return mat;
    }

    private void TexQuad(Node3D parent, string path, Vector3 position, Vector2 size, float rotY = 0f, float glow = 0.6f)
    {
        StandardMaterial3D mat = TexMaterial(path, glow);
        if (mat == null)
            return;
        parent.AddChild(new MeshInstance3D
        {
            Mesh = new QuadMesh { Size = size },
            Position = position,
            Rotation = new Vector3(0f, rotY, 0f),
            MaterialOverride = mat,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        });
    }

    /// <summary>Chamado pelo BuildBlock. true = o quarteirão virou um serviço especial.</summary>
    private bool TryBuildServiceBlock(Node3D block, Vector3 center, int bx, int bz)
    {
        if (bx == RadioStationBlock.X && bz == RadioStationBlock.Y)
        {
            BuildRadioStation(block, center);
            return true;
        }
        if (bx == MarketBlock.X && bz == MarketBlock.Y)
        {
            BuildSupermarket(block, center);
            return true;
        }
        if (bx == ApartmentBlock.X && bz == ApartmentBlock.Y)
        {
            BuildApartmentBuilding(block, center);
            return true;
        }
        if (bx == DealerBlock.X && bz == DealerBlock.Y)
        {
            BuildDealer(block, center);
            return true;
        }
        if (bx == WaterTowerBlock.X && bz == WaterTowerBlock.Y)
        {
            BuildWaterTowerPlaza(block, center);
            return true;
        }
        foreach ((Vector2I b, string id) in GasStations)
        {
            if (b.X == bx && b.Y == bz)
            {
                BuildGasStation(block, center, id, bx == CarWashBlock.X && bz == CarWashBlock.Y);
                return true;
            }
        }
        return false;
    }

    // ------------------------------------------------------------------ posto de gasolina

    private void BuildGasStation(Node3D block, Vector3 center, string brand, bool withCarWash)
    {
        // Frente do posto para a avenida (+Z). y = topo da calçada.
        var st = new Node3D { Name = "Posto_" + brand, Position = center + new Vector3(0f, CurbHeight, 0f) };
        block.AddChild(st);
        Color brandColor = brand switch
        {
            "petrobraz" => new Color(0.08f, 0.43f, 0.2f),
            "ipiranguinha" => new Color(0.98f, 0.78f, 0.08f),
            _ => new Color(0.98f, 0.82f, 0.08f),
        };
        StandardMaterial3D paint = Flat(brandColor, 0.5f);
        StandardMaterial3D white = Flat(new Color(0.93f, 0.93f, 0.9f), 0.6f);
        StandardMaterial3D asphalt = Flat(new Color(0.16f, 0.16f, 0.17f), 0.9f);

        // Pátio de concreto escuro por cima da calçada.
        AddBox(st, new Vector3(0f, 0.01f, 3f), new Vector3(30f, 0.02f, 24f), asphalt, false);

        // Cobertura com faixa da marca nos quatro lados e luz embaixo.
        const float cw = 16f, cd = 10f, ch = 5.2f;
        var canopyPos = new Vector3(0f, ch, 5f);
        AddBox(st, canopyPos, new Vector3(cw, 0.6f, cd), white, false);
        AddBox(st, canopyPos + new Vector3(0f, -0.31f, 0f), new Vector3(cw - 1f, 0.02f, cd - 1f), _lampHead, false);
        string fascia = $"res://Assets/Textures/Stores/fascia_{brand}.png";
        TexQuad(st, fascia, canopyPos + new Vector3(0f, 0.1f, cd * 0.5f + 0.01f), new Vector2(cw, 1f));
        TexQuad(st, fascia, canopyPos + new Vector3(0f, 0.1f, -cd * 0.5f - 0.01f), new Vector2(cw, 1f), Mathf.Pi);
        TexQuad(st, fascia, canopyPos + new Vector3(cw * 0.5f + 0.01f, 0.1f, 0f), new Vector2(cd, 0.62f), Mathf.Pi * 0.5f);
        TexQuad(st, fascia, canopyPos + new Vector3(-cw * 0.5f - 0.01f, 0.1f, 0f), new Vector2(cd, 0.62f), -Mathf.Pi * 0.5f);
        foreach (float x in new[] { -5f, 5f })
        {
            AddBox(st, new Vector3(x, ch * 0.5f, 5f), new Vector3(0.5f, ch, 0.5f), paint);
            _lightsRoot.AddChild(new OmniLight3D
            {
                Position = st.Position + canopyPos + new Vector3(x, -0.8f, 0f),
                LightColor = new Color(0.95f, 0.98f, 1f),
                LightEnergy = 1.6f,
                OmniRange = 11f,
                ShadowEnabled = false,
            });
        }

        GasSpots.Add((st.Position + new Vector3(0f, 0f, 5f), brand));
        // Ilhas de bombas.
        foreach (float x in new[] { -3f, 3f })
        {
            AddBox(st, new Vector3(x, 0.12f, 5f), new Vector3(1.3f, 0.24f, 4.2f), white);
            foreach (float z in new[] { 3.8f, 6.2f })
            {
                AddBox(st, new Vector3(x, 1.0f, z), new Vector3(0.7f, 1.6f, 0.45f), paint);
                AddBox(st, new Vector3(x, 1.45f, z + 0.235f), new Vector3(0.5f, 0.3f, 0.02f), _storeWindow, false);
                AddBox(st, new Vector3(x + 0.38f, 0.9f, z), new Vector3(0.06f, 0.5f, 0.12f), _darkMetal, false); // mangueira
                st.AddChild(new Label3D
                {
                    Text = brand == "petrobraz" ? "0,89" : brand == "ipiranguinha" ? "0,87" : "0,91",
                    FontSize = 40, PixelSize = 0.004f, Modulate = new Color(1f, 0.35f, 0.2f), OutlineSize = 0,
                    Position = new Vector3(x, 1.45f, z + 0.25f),
                });
            }
        }

        // Loja de conveniência no fundo.
        var shop = new Vector3(0f, 0f, -9.5f);
        AddBox(st, shop + new Vector3(0f, 2f, 0f), new Vector3(13f, 4f, 8f), white);
        AddBox(st, shop + new Vector3(0f, 4.15f, 0f), new Vector3(13.4f, 0.3f, 8.4f), paint, false);
        AddBox(st, shop + new Vector3(-2f, 1.3f, 4.02f), new Vector3(7f, 1.9f, 0.05f), _storeWindow, false);
        AddBox(st, shop + new Vector3(3.8f, 1.1f, 4.03f), new Vector3(1.4f, 2.2f, 0.06f), _doorDark, false);
        AddBox(st, shop + new Vector3(0f, 3.2f, 4.14f), new Vector3(9.4f, 1.48f, 0.2f), _doorDark, false);
        TexQuad(st, $"res://Assets/Textures/Stores/sign_conv_{brand}.png", shop + new Vector3(0f, 3.2f, 4.25f), new Vector2(9.2f, 1.3f));

        Poster(st, shop + new Vector3(6.52f, 1.6f, 1.5f), Mathf.Pi * 0.5f);
        // Totem de preços na esquina da avenida.
        var totem = new Vector3(13.5f, 0f, 14f);
        AddBox(st, totem + new Vector3(0f, 1.6f, 0f), new Vector3(0.4f, 3.2f, 0.4f), _darkMetal);
        AddBox(st, totem + new Vector3(0f, 5.2f, 0f), new Vector3(2.6f, 6.2f, 0.4f), paint, false);
        string totemTex = $"res://Assets/Textures/Stores/totem_{brand}.png";
        TexQuad(st, totemTex, totem + new Vector3(0f, 5.2f, 0.21f), new Vector2(2.4f, 6f));
        TexQuad(st, totemTex, totem + new Vector3(0f, 5.2f, -0.21f), new Vector2(2.4f, 6f), Mathf.Pi);

        // Calibrador e compressor de ar.
        AddBox(st, new Vector3(11f, 0.7f, 2f), new Vector3(0.5f, 1.4f, 0.4f), Flat(new Color(0.8f, 0.1f, 0.1f)));

        if (withCarWash)
            BuildCarWash(st, new Vector3(-11f, 0f, -6f));

        // Calçada rebaixada na entrada (direita) e na saída (esquerda): rampa do meio-fio até a rua,
        // com o meio-fio pintado de amarelo e preto dos lados.
        StandardMaterial3D rampMat = Flat(new Color(0.55f, 0.54f, 0.5f), 0.9f);
        rampMat.CullMode = BaseMaterial3D.CullModeEnum.Disabled;
        StandardMaterial3D yellow = Flat(new Color(0.95f, 0.78f, 0.1f), 0.6f);
        float edge = BlockSize * 0.5f;
        foreach ((float x0, float x1, string label) in new[] { (7f, 17f, "ENTRADA"), (-18f, -8f, "SAÍDA") })
        {
            AddRamp(st, (x0 + x1) * 0.5f, x1 - x0, edge, edge + 1.6f, 0f, -CurbHeight + 0.01f, rampMat);
            AddBox(st, new Vector3((x0 + x1) * 0.5f, 0.005f, edge - 1.2f), new Vector3(x1 - x0, 0.012f, 2.4f), rampMat, false);
            for (int i = 0; i < 4; i++)
            {
                AddBox(st, new Vector3(x0 - 0.15f, -CurbHeight * 0.5f + 0.002f, edge - 0.3f - i * 0.6f), new Vector3(0.3f, CurbHeight + 0.01f, 0.3f), i % 2 == 0 ? yellow : _darkMetal, false);
                AddBox(st, new Vector3(x1 + 0.15f, -CurbHeight * 0.5f + 0.002f, edge - 0.3f - i * 0.6f), new Vector3(0.3f, CurbHeight + 0.01f, 0.3f), i % 2 == 0 ? yellow : _darkMetal, false);
            }
            // Seta pintada no chão do posto.
            st.AddChild(new Label3D
            {
                Text = label,
                FontSize = 64,
                PixelSize = 0.01f,
                Modulate = new Color(0.95f, 0.95f, 0.9f),
                OutlineSize = 0,
                Position = new Vector3((x0 + x1) * 0.5f, 0.03f, edge - 2.6f),
                Rotation = new Vector3(-Mathf.Pi * 0.5f, 0f, 0f),
            });
        }

        RegisterObstacle(center + new Vector3(13.5f, 0f, 14f));
        MarkPoi(st, "G", brandColor);
    }

    /// <summary>Rampa (cunha) de largura width ao longo de X, descendo de (zTop, yTop) até (zBottom, yBottom). Com colisão.</summary>
    private void AddRamp(Node3D parent, float x, float width, float zTop, float zBottom, float yTop, float yBottom, Material mat)
    {
        float hw = width * 0.5f;
        Vector3[] v =
        {
            new(x - hw, yTop, zTop), new(x + hw, yTop, zTop), new(x + hw, yBottom, zBottom), new(x - hw, yBottom, zBottom),
            new(x - hw, yBottom, zTop), new(x + hw, yBottom, zTop),
        };
        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);
        void Tri(Vector3 a, Vector3 b, Vector3 c)
        {
            Vector3 n = (c - a).Cross(b - a).Normalized();
            foreach (Vector3 p in new[] { a, b, c })
            {
                st.SetNormal(n);
                st.AddVertex(p);
            }
        }
        Tri(v[0], v[1], v[2]); Tri(v[0], v[2], v[3]);   // rampa
        Tri(v[0], v[3], v[4]); Tri(v[1], v[5], v[2]);   // laterais
        var body = new StaticBody3D { CollisionLayer = 1, CollisionMask = 0 };
        body.AddChild(new MeshInstance3D { Mesh = st.Commit(), MaterialOverride = mat });
        body.AddChild(new CollisionShape3D { Shape = new ConvexPolygonShape3D { Points = v } });
        parent.AddChild(body);
    }

    private void BuildCarWash(Node3D parent, Vector3 pos)
    {
        var wash = new Node3D { Name = "LavaJato", Position = pos };
        parent.AddChild(wash);
        StandardMaterial3D blue = Flat(new Color(0.05f, 0.55f, 0.8f), 0.5f);
        StandardMaterial3D white = Flat(new Color(0.93f, 0.93f, 0.9f), 0.6f);
        // Túnel aberto na frente e atrás (a van passa por dentro).
        foreach (float x in new[] { -2.7f, 2.7f })
            AddBox(wash, new Vector3(x, 2f, 0f), new Vector3(0.3f, 4f, 9f), white);
        AddBox(wash, new Vector3(0f, 4.15f, 0f), new Vector3(5.8f, 0.3f, 9.2f), blue, false);
        AddBox(wash, new Vector3(0f, 4.95f, 4.5f), new Vector3(6.4f, 1.15f, 0.25f), _doorDark, false);
        TexQuad(wash, "res://Assets/Textures/Stores/sign_molhadinho.png", new Vector3(0f, 4.95f, 4.64f), new Vector2(6.2f, 0.95f));
        // Escovões girando.
        var brushMat = Flat(new Color(0.15f, 0.35f, 0.85f), 0.9f);
        foreach (float x in new[] { -1.7f, 1.7f })
        {
            var brush = new Spinner { Position = new Vector3(x, 2f, -1f), Axis = Vector3.Up, Speed = x > 0 ? 3f : -3f };
            wash.AddChild(brush);
            AddCylinder(brush, Vector3.Zero, 0.45f, 3.4f, brushMat, new CylinderMesh { TopRadius = 0.5f, BottomRadius = 0.5f, Height = 1f, RadialSegments = 7 });
        }
        var top = new Spinner { Position = new Vector3(0f, 3.4f, 1.5f), Axis = Vector3.Right, Speed = 2.5f };
        wash.AddChild(top);
        AddCylinder(top, Vector3.Zero, 0.4f, 4.6f, brushMat).Rotation = new Vector3(0f, 0f, Mathf.Pi * 0.5f);
        // Chão molhado e baldes.
        AddBox(wash, new Vector3(0f, 0.02f, 0f), new Vector3(5f, 0.02f, 9f), Flat(new Color(0.1f, 0.12f, 0.15f), 0.08f), false);
        AddCylinder(wash, new Vector3(3.6f, 0.25f, 4f), 0.25f, 0.5f, Flat(new Color(0.9f, 0.2f, 0.1f)));
    }

    // ------------------------------------------------------------------ caixa d'água municipal

    private void BuildWaterTowerPlaza(Node3D block, Vector3 center)
    {
        var plaza = new Node3D { Name = "CaixaDagua", Position = center + new Vector3(0f, CurbHeight, 0f) };
        block.AddChild(plaza);
        // Gramado com árvores nos cantos e bancos.
        AddBox(plaza, new Vector3(0f, 0.02f, 0f), new Vector3(24f, 0.04f, 24f), _grass, false);
        GrassTufts.Fill(plaza, new Vector3(0f, 0.04f, 0f), new Vector2(23f, 23f), 0.6f, new Color(0.8f, 0.95f, 0.75f));
        foreach (Vector3 c in new[] { new Vector3(-9f, 0f, -9f), new Vector3(9f, 0f, -9f), new Vector3(-9f, 0f, 9f), new Vector3(9f, 0f, 9f) })
        {
            AddCylinder(plaza, c + new Vector3(0f, 1.2f, 0f), 0.2f, 2.4f, _trunk);
            AddCylinder(plaza, c + new Vector3(0f, 3.4f, 0f), 1.8f, 3.4f, _leaves, _treeTop);
            AssetProps.Spawn(plaza, AssetProps.City + "bench.gltf", c * 0.6f, Mathf.Atan2(-c.X, -c.Z), 5f, new Vector3(2f, 0.5f, 0.7f));
        }

        StandardMaterial3D concrete = Flat(new Color(0.62f, 0.6f, 0.56f), 0.85f);
        StandardMaterial3D tankMat = Flat(new Color(0.88f, 0.86f, 0.8f), 0.6f);
        StandardMaterial3D band = Flat(new Color(0.1f, 0.35f, 0.6f), 0.6f);
        const float legH = 22f, tankR = 6f, tankH = 7f;
        // Quatro pilares de concreto com travamentos.
        foreach (float x in new[] { -3.6f, 3.6f })
            foreach (float z in new[] { -3.6f, 3.6f })
                AddBox(plaza, new Vector3(x, legH * 0.5f, z), new Vector3(0.9f, legH, 0.9f), concrete);
        for (float y = 5f; y < legH; y += 5.5f)
        {
            AddBox(plaza, new Vector3(0f, y, -3.6f), new Vector3(7.2f, 0.5f, 0.5f), concrete, false);
            AddBox(plaza, new Vector3(0f, y, 3.6f), new Vector3(7.2f, 0.5f, 0.5f), concrete, false);
            AddBox(plaza, new Vector3(-3.6f, y, 0f), new Vector3(0.5f, 0.5f, 7.2f), concrete, false);
            AddBox(plaza, new Vector3(3.6f, y, 0f), new Vector3(0.5f, 0.5f, 7.2f), concrete, false);
        }
        // Reservatório, faixa azul, telhado e escada.
        var cyl = new CylinderMesh { TopRadius = 0.5f, BottomRadius = 0.5f, Height = 1f, RadialSegments = 24 };
        AddCylinder(plaza, new Vector3(0f, legH + tankH * 0.5f, 0f), tankR, tankH, tankMat, cyl);
        AddCylinder(plaza, new Vector3(0f, legH + 1.3f, 0f), tankR + 0.05f, 1.2f, band, cyl);
        plaza.AddChild(new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = 0.4f, BottomRadius = tankR + 0.4f, Height = 2.2f, RadialSegments = 24 },
            Position = new Vector3(0f, legH + tankH + 1.1f, 0f),
            MaterialOverride = Flat(new Color(0.35f, 0.38f, 0.42f), 0.5f),
        });
        AddBox(plaza, new Vector3(0f, (legH + tankH) * 0.5f, 4.1f), new Vector3(0.6f, legH + tankH, 0.08f), _darkMetal, false);
        AddBox(plaza, new Vector3(0f, legH - 0.2f, 0f), new Vector3(tankR * 2f + 1.4f, 0.25f, tankR * 2f + 1.4f), _darkMetal, false); // passarela
        AddBox(plaza, new Vector3(0f, legH + tankH + 2.6f, 0f), new Vector3(0.5f, 0.5f, 0.5f), _redBeacon, false);
        var beacon = new OmniLight3D { Position = plaza.Position + new Vector3(0f, legH + tankH + 3f, 0f), LightColor = new Color(1f, 0.1f, 0.05f), LightEnergy = 2.5f, OmniRange = 14f };
        _lightsRoot.AddChild(beacon);

        // Nome pintado em volta do tanque (quatro lados).
        for (int i = 0; i < 4; i++)
        {
            float a = i * Mathf.Pi * 0.5f;
            var dir = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
            plaza.AddChild(new Label3D
            {
                Text = "CAIXA D'ÁGUA MUNICIPAL\nZONA MORTA · 1962",
                FontSize = 64,
                PixelSize = 0.012f,
                Modulate = new Color(0.1f, 0.3f, 0.55f),
                OutlineSize = 0,
                Position = dir * (tankR + 0.06f) + new Vector3(0f, legH + 4.6f, 0f),
                Rotation = new Vector3(0f, a, 0f),
            });
            // Refletores no chão apontando para o tanque (só à noite).
            _lightsRoot.AddChild(new SpotLight3D
            {
                Position = plaza.Position + dir * 9f + new Vector3(0f, 0.4f, 0f),
                Basis = Basis.LookingAt((new Vector3(0f, legH + 3f, 0f) - (dir * 9f)).Normalized(), Vector3.Up),
                LightColor = new Color(0.85f, 0.92f, 1f),
                LightEnergy = 4f,
                SpotRange = 40f,
                SpotAngle = 16f,
                ShadowEnabled = false,
            });
        }
        MarkPoi(plaza, "A", new Color(0.3f, 0.6f, 0.95f));
    }

    // ------------------------------------------------------------------ outdoors

    private void BuildBillboards()
    {
        float roadEdge = Radius * Period + Period * 0.5f + RoadWidth * 0.5f;
        float dist = roadEdge + 8f;
        List<string> ads = RunAds();
        int n = 0;
        Vector3[] outward = { Vector3.Forward, Vector3.Right, Vector3.Back, Vector3.Left };
        foreach (Vector3 o in outward)
        {
            Vector3 along = new(-o.Z, 0f, o.X);
            foreach (float t in new[] { -62f, 58f })
            {
                Vector3 pos = o * dist + along * t;
                // O primeiro outdoor é sempre o do teuzin0dev (twitch e github).
                BuildBillboard(pos, -o, n == 0 ? TeuzinAd : ads[n % ads.Count]);
                n++;
            }
        }
    }

    private void BuildBillboard(Vector3 pos, Vector3 facing, string adId)
    {
        var bb = new Node3D { Name = "Outdoor_" + adId, Position = pos, Rotation = new Vector3(0f, Mathf.Atan2(facing.X, facing.Z), 0f) };
        _staticRoot.AddChild(bb);
        const float w = 9f, h = 3f, baseY = 6.5f;
        foreach (float x in new[] { -2.8f, 2.8f })
            AddBox(bb, new Vector3(x, (baseY + h) * 0.5f, -0.3f), new Vector3(0.35f, baseY + h, 0.35f), _darkMetal);
        AddBox(bb, new Vector3(0f, baseY + h * 0.5f, -0.12f), new Vector3(w + 0.4f, h + 0.4f, 0.2f), _darkMetal, false);
        TexQuad(bb, AdTexturePath(adId), new Vector3(0f, baseY + h * 0.5f, 0.0f), new Vector2(w, h), 0f, 0f); // sem brilho próprio: à noite só os refletores iluminam
        // Passarela e refletores embaixo, apontando para cima.
        AddBox(bb, new Vector3(0f, baseY - 0.25f, 0.5f), new Vector3(w, 0.08f, 1.0f), _darkMetal, false);
        foreach (float x in new[] { -2.6f, 2.6f })
        {
            AddBox(bb, new Vector3(x, baseY - 0.1f, 0.85f), new Vector3(0.5f, 0.25f, 0.35f), _lampHead, false);
            var spot = new SpotLight3D
            {
                Position = new Vector3(x, baseY - 0.05f, 0.95f),
                RotationDegrees = new Vector3(78f, 0f, 0f),
                LightColor = new Color(1f, 0.95f, 0.85f),
                LightEnergy = 6f,
                SpotRange = 6f,
                SpotAngle = 52f,
                ShadowEnabled = false,
                DistanceFadeEnabled = true,
                DistanceFadeBegin = 120f,
                DistanceFadeLength = 40f,
            };
            // A luz fica na lista das luzes noturnas, mas presa ao outdoor (posição global).
            _lightsRoot.AddChild(spot);
            spot.GlobalTransform = bb.Transform * spot.Transform;
        }
    }
}
