// Scripts/World/CityGenerator.Depot.cs
// Galpão do depósito por dentro: paredes com o portão de carga e a porta de pessoas abertos,
// prateleiras porta-paletes com produtos, paletes no chão, empilhadeira, luzes industriais
// e o escritório com o PC que mostra os ganhos (OfficeComputer).
// Também estaciona a frota (caminhões e moto da Kenney, CC0; táxi e van de viagem do CarModels) no pátio.
//
// Coordenadas locais do quarteirão do depósito (centro = origem). O galpão ocupa
// x -13..13 e z -17..-5, com a fachada (portões) em z = -5 virada para +Z.
using Godot;

namespace DeadFrequency;

public partial class CityGenerator
{
    private const float ShedX = 13f, ShedBack = -17f, ShedFront = -5f, ShedH = 9f, WallT = 0.3f;
    private const float LoadDoorX = -4f, LoadDoorW = 7f, LoadDoorH = 6f;
    private const float PeopleDoorX = 7f, PeopleDoorW = 1.4f, PeopleDoorH = 2.4f;

    private StandardMaterial3D _rackOrange, _rackBlue, _wood, _cardboard, _cardboardDark, _wrap, _floorPaint,
        _safetyYellow, _forkYellow, _officeWall, _officeFloor, _deskWood, _chairFabric, _tube, _metalGrey;
    private RandomNumberGenerator _depotRng;
    /// <summary>Texturas do depósito (v0.10, Assets/Textures/Depot/depot_textures.py.txt): caixas, etiquetas, muro e quadros.</summary>
    private const string DepotTex = "res://Assets/Textures/Depot/";
    private StandardMaterial3D[] _boxMats;
    private StandardMaterial3D _muro;
    private readonly System.Collections.Generic.Dictionary<string, StandardMaterial3D> _depotTexMats = new();

    /// <summary>Material com textura do depósito, sem filtro (estilo PS1). Nulo se o arquivo não existir.</summary>
    private StandardMaterial3D DepotMaterial(string file, float roughness = 0.85f)
    {
        if (_depotTexMats.TryGetValue(file, out StandardMaterial3D mat))
            return mat;
        string path = DepotTex + file;
        mat = ResourceLoader.Exists(path)
            ? new StandardMaterial3D { AlbedoTexture = GD.Load<Texture2D>(path), Roughness = roughness, TextureFilter = BaseMaterial3D.TextureFilterEnum.NearestWithMipmaps }
            : null;
        _depotTexMats[file] = mat;
        return mat;
    }

    /// <summary>Adesivo/quadro: um retângulo com imagem, virado para +Z antes de girar rotY. Sem a imagem, não faz nada.</summary>
    private bool DepotQuad(Node3D parent, string file, Vector3 pos, Vector2 size, float rotY = 0f)
    {
        StandardMaterial3D mat = DepotMaterial(file);
        if (mat == null)
            return false;
        parent.AddChild(new MeshInstance3D
        {
            Mesh = new QuadMesh { Size = size },
            Position = pos,
            Rotation = new Vector3(0f, rotY, 0f),
            MaterialOverride = mat,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        });
        return true;
    }

    private void BuildDepotResources()
    {
        _rackOrange = Flat(new Color(0.85f, 0.38f, 0.08f), 0.6f);
        _rackBlue = Flat(new Color(0.12f, 0.24f, 0.55f), 0.6f);
        _wood = Flat(new Color(0.55f, 0.4f, 0.24f));
        _cardboard = Flat(new Color(0.62f, 0.47f, 0.3f));
        _cardboardDark = Flat(new Color(0.48f, 0.35f, 0.21f));
        // Caixas com textura (fita, setas e taça impressas) no lugar das cores lisas.
        _boxMats = new[]
        {
            DepotMaterial("caixa_papelao.png") ?? _cardboard, DepotMaterial("caixa_papelao_escuro.png") ?? _cardboardDark,
            DepotMaterial("caixa_branca.png") ?? _signWhite, DepotMaterial("caixa_azul.png") ?? _rackBlue, DepotMaterial("caixa_vermelha.png") ?? _stopRed,
        };
        _wrap = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.9f, 0.92f, 0.95f, 0.28f),
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            Roughness = 0.2f,
        };
        _floorPaint = Flat(new Color(0.32f, 0.34f, 0.33f), 0.8f);
        _safetyYellow = Flat(new Color(0.95f, 0.78f, 0.1f), 0.7f);
        _forkYellow = Flat(new Color(0.95f, 0.65f, 0.05f), 0.5f);
        _officeWall = Flat(new Color(0.78f, 0.76f, 0.7f));
        _officeFloor = Flat(new Color(0.42f, 0.3f, 0.2f), 0.6f);
        _deskWood = Flat(new Color(0.45f, 0.32f, 0.2f), 0.5f);
        _chairFabric = Flat(new Color(0.15f, 0.2f, 0.32f));
        _metalGrey = Flat(new Color(0.45f, 0.47f, 0.5f), 0.4f);
        _tube = Flat(new Color(0.95f, 0.98f, 1f));
        _tube.EmissionEnabled = true;
        _tube.Emission = new Color(0.9f, 0.95f, 1f);
        _tube.EmissionEnergyMultiplier = 2.5f;
        // Gerador próprio: decorar o depósito não muda nada no resto da cidade.
        _depotRng = new RandomNumberGenerator { Seed = (ulong)Seed * 31UL + 7UL };
    }

    /// <summary>Galpão oco com interior. Substitui o bloco maciço de versões antigas.</summary>
    private void BuildWarehouse(Node3D depot)
    {
        BuildDepotResources();
        float midZ = (ShedBack + ShedFront) * 0.5f;
        float depth = ShedFront - ShedBack;

        // Paredes externas (a de trás e as laterais inteiras).
        AddBox(depot, new Vector3(0f, ShedH * 0.5f, ShedBack + WallT * 0.5f), new Vector3(ShedX * 2f, ShedH, WallT), _warehouse);
        AddBox(depot, new Vector3(-ShedX + WallT * 0.5f, ShedH * 0.5f, midZ), new Vector3(WallT, ShedH, depth), _warehouse);
        AddBox(depot, new Vector3(ShedX - WallT * 0.5f, ShedH * 0.5f, midZ), new Vector3(WallT, ShedH, depth), _warehouse);

        // Fachada com dois vãos: portão de carga e porta de pessoas.
        float fz = ShedFront - WallT * 0.5f;
        float l0 = LoadDoorX - LoadDoorW * 0.5f, l1 = LoadDoorX + LoadDoorW * 0.5f;
        float p0 = PeopleDoorX - PeopleDoorW * 0.5f, p1 = PeopleDoorX + PeopleDoorW * 0.5f;
        FrontSegment(depot, -ShedX, l0, 0f, ShedH, fz);
        FrontSegment(depot, l1, p0, 0f, ShedH, fz);
        FrontSegment(depot, p1, ShedX, 0f, ShedH, fz);
        FrontSegment(depot, l0, l1, LoadDoorH, ShedH, fz);
        FrontSegment(depot, p0, p1, PeopleDoorH, ShedH, fz);

        // Portão de enrolar recolhido no alto + faixas zebradas nas laterais do vão.
        AddBox(depot, new Vector3(LoadDoorX, LoadDoorH - 0.35f, fz - 0.45f), new Vector3(LoadDoorW + 0.2f, 0.7f, 0.6f), _darkMetal, false);
        foreach (float x in new[] { l0 - 0.12f, l1 + 0.12f })
            for (int i = 0; i < 6; i++)
                AddBox(depot, new Vector3(x, 0.5f + i * 1f, ShedFront + 0.02f), new Vector3(0.22f, 0.5f, 0.04f), i % 2 == 0 ? _safetyYellow : _darkMetal, false);
        // Batente da porta de pessoas.
        AddBox(depot, new Vector3(PeopleDoorX, PeopleDoorH + 0.06f, ShedFront + 0.03f), new Vector3(PeopleDoorW + 0.2f, 0.12f, 0.06f), _darkMetal, false);
        var doorSign = new Label3D
        {
            Text = "ESCRITÓRIO",
            FontSize = 48,
            PixelSize = 0.005f,
            Modulate = new Color(0.95f, 0.95f, 0.9f),
            Position = new Vector3(PeopleDoorX, PeopleDoorH + 0.35f, ShedFront + 0.07f),
        };
        depot.AddChild(doorSign);

        // Piso pintado por dentro + faixas de segurança marcando o corredor do portão.
        AddBox(depot, new Vector3(0f, 0.006f, midZ), new Vector3(ShedX * 2f - 0.6f, 0.012f, depth - 0.6f), _floorPaint, false);
        foreach (float x in new[] { l0 + 0.15f, l1 - 0.15f })
            AddBox(depot, new Vector3(x, 0.014f, -10f), new Vector3(0.12f, 0.01f, 9.4f), _safetyYellow, false);
        AddBox(depot, new Vector3(LoadDoorX, 0.014f, -14.7f), new Vector3(LoadDoorW - 0.3f, 0.01f, 0.12f), _safetyYellow, false);

        // Treliças do telhado (vistas de dentro).
        for (float x = -10f; x <= 10.1f; x += 5f)
            AddBox(depot, new Vector3(x, ShedH - 0.5f, midZ), new Vector3(0.25f, 0.35f, depth - 0.6f), _darkMetal, false);

        // Luminárias industriais penduradas (sempre acesas: o galpão é escuro mesmo de dia).
        foreach (Vector3 p in new[] { new Vector3(-9f, 0f, -11f), new Vector3(-3f, 0f, -11f), new Vector3(3f, 0f, -13.5f), new Vector3(9f, 0f, -14f) })
        {
            AddBox(depot, new Vector3(p.X, ShedH - 1.2f, p.Z), new Vector3(0.03f, 1.6f, 0.03f), _darkMetal, false);
            AddCylinder(depot, new Vector3(p.X, ShedH - 2.05f, p.Z), 0.45f, 0.25f, _darkMetal);
            AddCylinder(depot, new Vector3(p.X, ShedH - 2.2f, p.Z), 0.32f, 0.04f, _tube);
            depot.AddChild(new OmniLight3D
            {
                Position = new Vector3(p.X, ShedH - 2.6f, p.Z),
                LightColor = new Color(0.95f, 0.97f, 1f),
                LightEnergy = 1.6f,
                OmniRange = 11f,
                OmniAttenuation = 0.9f,
                ShadowEnabled = false,
            });
        }

        BuildRacks(depot);
        BuildFloorPallets(depot);
        BuildForklift(depot, new Vector3(1.6f, 0f, -12.2f), Mathf.DegToRad(-30f));
        BuildPalletJack(depot, new Vector3(-8.6f, 0f, -6.4f), Mathf.DegToRad(20f));
        BuildOffice(depot);

        // Aviso pintado na parede de trás.
        depot.AddChild(new Label3D
        {
            Text = "USE EPI  ·  CARGA FRÁGIL: SEM PRESSA  ·  BOA ENTREGA",
            FontSize = 64,
            PixelSize = 0.008f,
            Modulate = new Color(0.95f, 0.8f, 0.2f),
            Position = new Vector3(-3f, 7.2f, ShedBack + WallT + 0.02f),
        });
    }

    private void FrontSegment(Node3D depot, float x0, float x1, float y0, float y1, float z)
    {
        if (x1 - x0 < 0.01f || y1 - y0 < 0.01f)
            return;
        AddBox(depot, new Vector3((x0 + x1) * 0.5f, (y0 + y1) * 0.5f, z), new Vector3(x1 - x0, y1 - y0, WallT), _warehouse);
    }

    // ------------------------------------------------------------------ prateleiras

    private void BuildRacks(Node3D depot)
    {
        // Fileira encostada na parede de trás (setores A, B, C) e uma atrás do escritório (D).
        string[] sectors = { "A", "B", "C", "D" };
        float[] xs = { -9.6f, -3.8f, 2.0f, 9.3f };
        for (int i = 0; i < xs.Length; i++)
            BuildRack(depot, new Vector3(xs[i], 0f, ShedBack + WallT + 0.7f), 5.4f, 0f, sectors[i]);
        // Uma ao longo da parede esquerda.
        BuildRack(depot, new Vector3(-ShedX + WallT + 0.7f, 0f, -10.6f), 6.0f, Mathf.Pi * 0.5f, "E");
    }

    /// <summary>Porta-paletes: colunas azuis, longarinas laranja, 3 níveis com paletes e caixas.</summary>
    private void BuildRack(Node3D depot, Vector3 pos, float length, float rotY, string sector)
    {
        const float depth = 1.1f, height = 5.6f;
        var rack = new Node3D { Position = pos, Rotation = new Vector3(0f, rotY, 0f) };
        depot.AddChild(rack);

        // Colisão única (o jogador não escala a prateleira).
        var body = new StaticBody3D { CollisionLayer = 1, CollisionMask = 0, Position = new Vector3(0f, height * 0.5f, 0f) };
        body.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(length, height, depth) } });
        rack.AddChild(body);

        int bays = Mathf.Max(1, Mathf.RoundToInt(length / 2.7f));
        float bayW = length / bays;
        for (int b = 0; b <= bays; b++)
        {
            float x = -length * 0.5f + b * bayW;
            foreach (float z in new[] { -depth * 0.5f + 0.05f, depth * 0.5f - 0.05f })
                AddBox(rack, new Vector3(x, height * 0.5f, z), new Vector3(0.09f, height, 0.09f), _rackBlue, false);
            // Travessas diagonais simplificadas.
            AddBox(rack, new Vector3(x, 1.2f, 0f), new Vector3(0.04f, 0.04f, depth - 0.1f), _rackBlue, false);
            AddBox(rack, new Vector3(x, 3.4f, 0f), new Vector3(0.04f, 0.04f, depth - 0.1f), _rackBlue, false);
        }

        float[] levels = { 0.15f, 2.0f, 3.85f };
        foreach (float y in levels)
        {
            if (y > 0.2f)
            {
                foreach (float z in new[] { -depth * 0.5f + 0.05f, depth * 0.5f - 0.05f })
                    AddBox(rack, new Vector3(0f, y - 0.07f, z), new Vector3(length, 0.12f, 0.06f), _rackOrange, false);
            }
            for (int b = 0; b < bays; b++)
            {
                float cx = -length * 0.5f + (b + 0.5f) * bayW;
                foreach (float off in new[] { -0.6f, 0.6f })
                {
                    if (_depotRng.Randf() < 0.18f)
                        continue; // vaga vazia
                    BuildPallet(rack, new Vector3(cx + off, y, 0f), _depotRng.RandiRange(1, 3), y < 0.2f ? 1.5f : 1.45f, false);
                }
            }
        }

        rack.AddChild(new Label3D
        {
            Text = "SETOR " + sector,
            FontSize = 64,
            PixelSize = 0.006f,
            Modulate = new Color(1f, 1f, 1f),
            OutlineSize = 8,
            Position = new Vector3(-length * 0.5f + 0.6f, height + 0.25f, depth * 0.5f),
        });
    }

    // ------------------------------------------------------------------ paletes e produtos

    private void BuildFloorPallets(Node3D depot)
    {
        Vector3[] spots =
        {
            new(-10.2f, 0f, -7.2f), new(-10.2f, 0f, -8.6f), new(-9.0f, 0f, -12.6f),
            new(2.6f, 0f, -6.6f), new(3.9f, 0f, -6.6f), new(3.2f, 0f, -8.4f),
            new(-4.0f, 0f, -14.2f), new(10.5f, 0f, -12.6f), new(8.3f, 0f, -12.6f),
        };
        foreach (Vector3 s in spots)
        {
            var root = new Node3D { Position = s, Rotation = new Vector3(0f, _depotRng.RandfRange(-0.08f, 0.08f), 0f) };
            depot.AddChild(root);
            BuildPallet(root, Vector3.Zero, _depotRng.RandiRange(1, 4), 3f, true);
        }
    }

    /// <summary>Palete de madeira com caixas empilhadas (às vezes envolvidas em filme).</summary>
    private void BuildPallet(Node3D parent, Vector3 basePos, int layers, float maxHeight, bool collide)
    {
        const float pw = 1.1f, pd = 1.0f, ph = 0.14f;
        // Tábuas de cima + três "pés".
        for (int i = 0; i < 5; i++)
            AddBox(parent, basePos + new Vector3(-pw * 0.5f + 0.11f + i * 0.22f, ph - 0.02f, 0f), new Vector3(0.14f, 0.03f, pd), _wood, false);
        foreach (float z in new[] { -pd * 0.42f, 0f, pd * 0.42f })
            AddBox(parent, basePos + new Vector3(0f, (ph - 0.03f) * 0.5f, z), new Vector3(pw, ph - 0.03f, 0.1f), _cardboardDark, false);

        float box = 0.5f;
        float boxH = _depotRng.RandfRange(0.38f, 0.5f);
        var frontBoxes = new System.Collections.Generic.List<Vector3>();
        layers = Mathf.Min(layers, Mathf.Max(1, (int)((maxHeight - ph) / boxH)));
        float y = ph;
        for (int l = 0; l < layers; l++)
        {
            for (int ix = 0; ix < 2; ix++)
                for (int iz = 0; iz < 2; iz++)
                {
                    if (l == layers - 1 && _depotRng.Randf() < 0.2f)
                        continue;
                    Material m = ProductMaterial();
                    var p = basePos + new Vector3((ix - 0.5f) * (box + 0.02f), y + boxH * 0.5f, (iz - 0.5f) * (box - 0.02f));
                    AddBox(parent, p, new Vector3(box, boxH - 0.01f, box - 0.04f), m, false);
                    if (iz == 1 && l == 0)
                        frontBoxes.Add(p);
                    // Fita adesiva no topo.
                    AddBox(parent, p + new Vector3(0f, boxH * 0.5f, 0f), new Vector3(0.08f, 0.006f, box - 0.03f), _cardboardDark, false);
                }
            y += boxH;
        }

        bool wrapped = layers >= 2 && _depotRng.Randf() < 0.45f;
        if (wrapped)
            AddBox(parent, basePos + new Vector3(0f, ph + (y - ph) * 0.5f, 0f), new Vector3(pw + 0.04f, y - ph + 0.04f, pd + 0.02f), _wrap, false);

        // Etiqueta colada numa caixa da frente (adesivo com imagem; por cima do filme, se tiver).
        if (_depotRng.Randf() < 0.45f && frontBoxes.Count > 0)
        {
            string[] tags = { "fragil", "lado_cima", "casas_baixa", "correrios", "marcianas", "devolucao", "luzia" };
            Vector3 b = frontBoxes[_depotRng.RandiRange(0, frontBoxes.Count - 1)];
            float z = wrapped ? basePos.Z + pd * 0.5f + 0.014f : b.Z + (box - 0.04f) * 0.5f + 0.004f;
            DepotQuad(parent, "etq_" + tags[_depotRng.RandiRange(0, tags.Length - 1)] + ".png", new Vector3(b.X, b.Y, z), new Vector2(0.34f, 0.17f));
        }

        if (collide)
        {
            var body = new StaticBody3D { CollisionLayer = 1, CollisionMask = 0, Position = basePos + new Vector3(0f, y * 0.5f, 0f) };
            body.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(pw, y, pd) } });
            parent.AddChild(body);
        }
    }

    private Material ProductMaterial()
    {
        float r = _depotRng.Randf();
        return _boxMats[r < 0.55f ? 0 : r < 0.72f ? 1 : r < 0.82f ? 2 : r < 0.9f ? 3 : 4];
    }

    // ------------------------------------------------------------------ máquinas

    private void BuildForklift(Node3D depot, Vector3 pos, float rotY)
    {
        var fork = new Node3D { Position = pos, Rotation = new Vector3(0f, rotY, 0f) };
        depot.AddChild(fork);
        var body = new StaticBody3D { CollisionLayer = 1, CollisionMask = 0, Position = new Vector3(0f, 1.1f, 0.2f) };
        body.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(1.2f, 2.2f, 2.4f) } });
        fork.AddChild(body);

        // v0.10.1: empilhadeira do Meshy com rodas de verdade (o Meshy faz sem rodas).
        if (MeshyVehicles.HasProp("empilhadeira"))
        {
            MeshyVehicles.BuildWheeledProp(fork, "empilhadeira", new Vector3(1.15f, 2.4f, 2.6f), -1, 0.08f, 0.27f, 0.22f, new[] { -0.12f, 0.3f }, 0f);
            return;
        }

        // Frente (garfos) = -Z.
        AddBox(fork, new Vector3(0f, 0.55f, 0.35f), new Vector3(1.1f, 0.7f, 1.7f), _forkYellow, false); // chassi
        AddBox(fork, new Vector3(0f, 0.6f, 1.25f), new Vector3(1.1f, 0.8f, 0.3f), _darkMetal, false); // contrapeso
        AddBox(fork, new Vector3(0f, 1.0f, 0.55f), new Vector3(0.5f, 0.12f, 0.5f), _darkMetal, false); // banco
        AddBox(fork, new Vector3(0f, 1.3f, 0.8f), new Vector3(0.5f, 0.5f, 0.1f), _darkMetal, false);
        AddBox(fork, new Vector3(0f, 1.05f, -0.15f), new Vector3(0.6f, 0.3f, 0.3f), _forkYellow, false); // painel
        AddCylinder(fork, new Vector3(0f, 1.3f, -0.2f), 0.16f, 0.04f, _darkMetal); // volante
        // Gaiola de proteção.
        foreach (float x in new[] { -0.5f, 0.5f })
            foreach (float z in new[] { -0.25f, 1.05f })
                AddBox(fork, new Vector3(x, 1.55f, z), new Vector3(0.06f, 2.0f, 0.06f), _darkMetal, false);
        AddBox(fork, new Vector3(0f, 2.55f, 0.4f), new Vector3(1.1f, 0.06f, 1.4f), _darkMetal, false);
        // Torre e garfos.
        foreach (float x in new[] { -0.35f, 0.35f })
        {
            AddBox(fork, new Vector3(x, 1.4f, -0.6f), new Vector3(0.1f, 2.8f, 0.12f), _metalGrey, false);
            AddBox(fork, new Vector3(x, 0.08f, -1.25f), new Vector3(0.12f, 0.05f, 1.2f), _metalGrey, false);
        }
        AddBox(fork, new Vector3(0f, 0.45f, -0.68f), new Vector3(0.9f, 0.8f, 0.05f), _metalGrey, false);
        // Rodas.
        foreach (float x in new[] { -0.5f, 0.5f })
            foreach (float z in new[] { -0.3f, 0.95f })
            {
                var w = AddCylinder(fork, new Vector3(x, 0.25f, z), 0.25f, 0.22f, _darkMetal);
                w.Rotation = new Vector3(0f, 0f, Mathf.Pi * 0.5f);
            }
        // Giroflex.
        AddCylinder(fork, new Vector3(0f, 2.65f, 0.4f), 0.08f, 0.12f, _redBeacon ?? _stopRed);
    }

    private void BuildPalletJack(Node3D depot, Vector3 pos, float rotY)
    {
        var jack = new Node3D { Position = pos, Rotation = new Vector3(0f, rotY, 0f) };
        depot.AddChild(jack);
        // v0.10: paleteira do Meshy (garfos para -Z, como a antiga); sem o modelo, fica a de caixas.
        if (MeshyVehicles.HasProp("paleteira"))
        {
            var holder = new Node3D { Position = new Vector3(0f, 0f, -0.25f) };
            jack.AddChild(holder);
            MeshyVehicles.BuildProp(holder, "paleteira", new Vector3(0.62f, 1.22f, 1.55f), -1);
            var body = new StaticBody3D { CollisionLayer = 1, CollisionMask = 0, Position = new Vector3(0f, 0.3f, -0.25f) };
            body.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(0.6f, 0.6f, 1.5f) } });
            jack.AddChild(body);
            return;
        }
        foreach (float x in new[] { -0.25f, 0.25f })
            AddBox(jack, new Vector3(x, 0.06f, -0.6f), new Vector3(0.16f, 0.07f, 1.15f), _stopRed, false);
        AddBox(jack, new Vector3(0f, 0.18f, 0.05f), new Vector3(0.6f, 0.3f, 0.2f), _stopRed, false);
        AddBox(jack, new Vector3(0f, 0.7f, 0.25f), new Vector3(0.05f, 1.0f, 0.05f), _darkMetal, false, 0f).RotationDegrees = new Vector3(-20f, 0f, 0f);
        AddBox(jack, new Vector3(0f, 1.18f, 0.42f), new Vector3(0.36f, 0.05f, 0.05f), _darkMetal, false);
    }

    // ------------------------------------------------------------------ escritório

    private void BuildOffice(Node3D depot)
    {
        const float x0 = 6.0f, x1 = ShedX - WallT, z0 = -10.5f, z1 = ShedFront - WallT, h = 3.0f;
        float cx = (x0 + x1) * 0.5f, cz = (z0 + z1) * 0.5f;
        float w = x1 - x0, d = z1 - z0;

        // Piso de taco, forro e paredes de divisória.
        AddBox(depot, new Vector3(cx, 0.02f, cz), new Vector3(w, 0.02f, d), _officeFloor, false);
        AddBox(depot, new Vector3(cx, h + 0.06f, cz), new Vector3(w + 0.1f, 0.12f, d + 0.1f), _officeWall, false);

        // Parede do fundo (z0) com porta para o galpão (x 8.6..9.8).
        const float doorA = 8.6f, doorB = 9.8f;
        AddBox(depot, new Vector3((x0 + doorA) * 0.5f, h * 0.5f, z0), new Vector3(doorA - x0, h, 0.12f), _officeWall);
        AddBox(depot, new Vector3((doorB + x1) * 0.5f, h * 0.5f, z0), new Vector3(x1 - doorB, h, 0.12f), _officeWall);
        AddBox(depot, new Vector3((doorA + doorB) * 0.5f, (2.2f + h) * 0.5f, z0), new Vector3(doorB - doorA, h - 2.2f, 0.12f), _officeWall);
        // Parede lateral (x0) com janela de vidro para vigiar o galpão.
        AddBox(depot, new Vector3(x0, 0.5f, cz), new Vector3(0.12f, 1.0f, d), _officeWall);
        AddBox(depot, new Vector3(x0, (2.3f + h) * 0.5f, cz), new Vector3(0.12f, h - 2.3f, d), _officeWall);
        AddBox(depot, new Vector3(x0, 1.65f, cz), new Vector3(0.04f, 1.3f, d), _glass);
        for (float z = z0 + 1.3f; z < z1 - 0.2f; z += 1.3f)
            AddBox(depot, new Vector3(x0, 1.65f, z), new Vector3(0.1f, 1.3f, 0.06f), _darkMetal, false);

        // Luz fluorescente do teto.
        AddBox(depot, new Vector3(cx, h - 0.02f, cz), new Vector3(1.4f, 0.05f, 0.3f), _tube, false);
        depot.AddChild(new OmniLight3D
        {
            Position = new Vector3(cx, h - 0.4f, cz),
            LightColor = new Color(0.92f, 1f, 0.95f),
            LightEnergy = 1.2f,
            OmniRange = 6.5f,
            ShadowEnabled = false,
        });

        // Mesa encostada na parede direita, virada para o meio da sala.
        float deskX = x1 - 0.45f, deskZ = -7.4f;
        AddBox(depot, new Vector3(deskX, 0.75f, deskZ), new Vector3(0.85f, 0.05f, 1.8f), _deskWood, false);
        AddBox(depot, new Vector3(deskX, 0.37f, deskZ - 0.85f), new Vector3(0.8f, 0.74f, 0.05f), _deskWood);
        AddBox(depot, new Vector3(deskX, 0.37f, deskZ + 0.85f), new Vector3(0.8f, 0.74f, 0.05f), _deskWood);
        AddBox(depot, new Vector3(deskX + 0.38f, 0.4f, deskZ), new Vector3(0.04f, 0.6f, 1.7f), _deskWood, false);
        // Gaveteiro.
        AddBox(depot, new Vector3(deskX, 0.36f, deskZ + 0.45f), new Vector3(0.7f, 0.7f, 0.6f), _deskWood);
        for (int i = 0; i < 3; i++)
            AddBox(depot, new Vector3(deskX - 0.36f, 0.15f + i * 0.22f, deskZ + 0.45f), new Vector3(0.02f, 0.03f, 0.18f), _metalGrey, false);

        // O computador (tela virada para -X, ou seja, para quem está na sala).
        var pc = new OfficeComputer
        {
            Name = "OfficeComputer",
            Position = new Vector3(deskX - 0.05f, 0.775f, deskZ - 0.2f),
            Rotation = new Vector3(0f, -Mathf.Pi * 0.5f, 0f),
        };
        depot.AddChild(pc);

        // Cadeira (KayKit Furniture, CC0); se o modelo não carregar, usa a cadeira de caixas.
        float chairX = deskX - 0.95f, chairZ = deskZ - 0.15f;
        if (AssetProps.Spawn(depot, AssetProps.Furniture + "chair_C.gltf", new Vector3(chairX, 0f, chairZ), Mathf.Pi * 0.5f, 0.75f, new Vector3(0.55f, 0.9f, 0.55f)) == null)
        {
            AddCylinder(depot, new Vector3(chairX, 0.25f, chairZ), 0.04f, 0.45f, _darkMetal);
            AddBox(depot, new Vector3(chairX, 0.5f, chairZ), new Vector3(0.5f, 0.08f, 0.5f), _chairFabric);
            AddBox(depot, new Vector3(chairX - 0.25f, 0.85f, chairZ), new Vector3(0.07f, 0.6f, 0.46f), _chairFabric, false);
        }

        // Mobília do escritório (KayKit Furniture Bits, CC0).
        AssetProps.Spawn(depot, AssetProps.Furniture + "rug_rectangle_stripes_A.gltf", new Vector3(cx - 0.4f, 0.03f, cz), Mathf.Pi * 0.5f, 0.75f);
        AssetProps.Spawn(depot, AssetProps.Furniture + "lamp_standing.gltf", new Vector3(x1 - 0.45f, 0.03f, z1 - 0.45f), 0f, 0.7f, new Vector3(0.4f, 1.7f, 0.4f));
        AssetProps.Spawn(depot, AssetProps.Furniture + "cactus_medium_A.gltf", new Vector3(deskX + 0.1f, 0.775f, deskZ - 0.72f), 0f, 0.28f);
        AssetProps.Spawn(depot, AssetProps.Furniture + "pictureframe_large_A.gltf", new Vector3(11.2f, 1.9f, z0 + 0.1f), 0f, 0.6f);
        AssetProps.Spawn(depot, AssetProps.Furniture + "shelf_B_large_decorated.gltf", new Vector3(11.6f, 2.05f, z1 - 0.18f), Mathf.Pi, 0.6f);
        AssetProps.Spawn(depot, AssetProps.Furniture + "couch_pillows.gltf", new Vector3(x0 + 0.6f, 0.03f, -8.2f), Mathf.Pi * 0.5f, 0.6f, new Vector3(1.0f, 0.75f, 1.8f));
        AssetProps.Spawn(depot, AssetProps.Furniture + "table_low.gltf", new Vector3(x0 + 1.75f, 0.03f, -8.2f), Mathf.Pi * 0.5f, 0.45f, new Vector3(0.7f, 0.25f, 1.05f));
        AssetProps.Spawn(depot, AssetProps.Furniture + "book_set.gltf", new Vector3(x0 + 1.75f, 0.33f, -8.2f), 0.4f, 0.35f);

        // Arquivo de aço, bebedouro e quadro de avisos.
        AddBox(depot, new Vector3(x1 - 0.3f, 0.7f, z0 + 0.45f), new Vector3(0.55f, 1.4f, 0.6f), _metalGrey);
        for (int i = 0; i < 4; i++)
            AddBox(depot, new Vector3(x1 - 0.59f, 0.25f + i * 0.33f, z0 + 0.45f), new Vector3(0.02f, 0.03f, 0.2f), _darkMetal, false);
        Poster(depot, new Vector3(7.3f, 1.6f, z0 + 0.09f), 0f);
        // Rádio em cima do arquivo, sempre na 96.6 (E desliga).
        BuildRadioProp(depot, new Vector3(x1 - 0.32f, 1.4f, z0 + 0.45f), -Mathf.Pi * 0.5f, true, -7f);
        AddBox(depot, new Vector3(x0 + 0.4f, 0.5f, z0 + 0.4f), new Vector3(0.35f, 1.0f, 0.35f), _signWhite);
        AddCylinder(depot, new Vector3(x0 + 0.4f, 1.2f, z0 + 0.4f), 0.15f, 0.4f, _rackBlue).Transparency = 0.3f;
        // Quadro de avisos, "funcionário do mês" e a paisagem do vale: imagens (antes eram textos soltos que saíam do quadro).
        if (!DepotQuad(depot, "quadro_avisos.png", new Vector3(x1 - 0.015f, 1.7f, -9.0f), new Vector2(1.2f, 0.8f), -Mathf.Pi * 0.5f))
            AddBox(depot, new Vector3(x1 - 0.02f, 1.7f, -9.0f), new Vector3(0.02f, 0.8f, 1.2f), _cardboard, false);
        if (!DepotQuad(depot, "funcionario_mes.png", new Vector3(9.8f, 1.8f, z1 - 0.015f), new Vector2(0.7f, 0.5f), Mathf.Pi))
            AddBox(depot, new Vector3(9.8f, 1.8f, z1 - 0.02f), new Vector3(0.7f, 0.5f, 0.02f), _signWhite, false);
        if (DepotQuad(depot, "quadro_vale.png", new Vector3(10.45f, 1.9f, z0 + 0.095f), new Vector2(0.42f, 0.56f)))
            AddBox(depot, new Vector3(10.45f, 1.9f, z0 + 0.075f), new Vector3(0.48f, 0.62f, 0.03f), _deskWood, false); // moldura
    }

    // ------------------------------------------------------------------ frota (Kenney, CC0)

    /// <summary>
    /// Caminhões e moto do pátio (v0.9.3: dá para usar). A moto faz entregas com o baú; os caminhões
    /// são emprestados para ir para casa. Montados pelo VehicleFactory; frente = +Z.
    /// </summary>
    private void BuildDepotFleet(Node3D depot)
    {
        // v0.9.9: caminhões mais para a frente do pátio, liberando o portão novo do lado -X (z -5..5).
        (VehicleKind Kind, Vector3 Pos, float RotDeg)[] fleet =
        {
            (VehicleKind.TruckYellow, new Vector3(-13.6f, 0.3f, 9f), 90f),
            (VehicleKind.TruckRed, new Vector3(-13.6f, 0.3f, 14.5f), 90f),
            (VehicleKind.Moto, new Vector3(11.2f, 0.3f, -2.6f), 20f),
        };
        foreach ((VehicleKind kind, Vector3 pos, float rot) in fleet)
            Park(VehicleFactory.Build(kind), pos, rot);

        // Táxi da cooperativa e van de viagem, lado a lado no canto da frente encostados no muro +X,
        // de bico para o portão desse lado (o orelhão do pátio fica em x 8, z 16,6: longe do caminho).
        Taxi = Park(VehicleFactory.BuildTaxi(), new Vector3(15.4f, 0.3f, 13f), 180f);
        TravelVan = Park(VehicleFactory.BuildTravelVan(), new Vector3(12.2f, 0.3f, 13f), 180f);

        VanController Park(VanController v, Vector3 pos, float rotDeg)
        {
            v.Position = depot.Position + pos;
            v.RotationDegrees = new Vector3(0f, rotDeg, 0f);
            AddChild(v); // fora do Static: é um corpo que se mexe
            return v;
        }
    }

    /// <summary>Táxi amarelo do pátio (o TaxiJob só faz corrida com ele).</summary>
    public VanController Taxi { get; private set; }
    /// <summary>Van de viagem anos 80 do Modo História.</summary>
    public VanController TravelVan { get; private set; }
}
