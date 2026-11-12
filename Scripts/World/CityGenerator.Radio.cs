// Scripts/World/CityGenerator.Radio.cs
// Rádio Frequência 96.6 FM (v0.9): estúdio com letreiro, sala de locução por dentro (com o rádio no ar)
// e a torre de transmissão treliçada, pintada de vermelho e branco, com luzes piscando no alto.
// Também monta o aparelho de rádio usado no escritório e nos apartamentos.
using Godot;

namespace DeadFrequency;

public partial class CityGenerator
{
    [ExportGroup("Rádio")]
    [Export] public Vector2I RadioStationBlock { get; set; } = new(1, 2);

    /// <summary>Cilindro esticado entre dois pontos (vigas da treliça, cabos).</summary>
    private MeshInstance3D AddBeam(Node3D parent, Vector3 a, Vector3 b, float radius, Material material)
    {
        Vector3 dir = b - a;
        float len = dir.Length();
        if (len < 0.001f)
            return null;
        Vector3 up = dir / len;
        Vector3 side = Mathf.Abs(up.Dot(Vector3.Forward)) > 0.95f ? Vector3.Right : Vector3.Forward;
        Vector3 x = up.Cross(side).Normalized();
        Vector3 z = x.Cross(up).Normalized();
        var mi = new MeshInstance3D
        {
            Mesh = _unitCylinder,
            MaterialOverride = material,
            Transform = new Transform3D(new Basis(x * radius * 2f, up * len, z * radius * 2f), (a + b) * 0.5f),
        };
        parent.AddChild(mi);
        return mi;
    }

    private void BuildRadioStation(Node3D block, Vector3 center)
    {
        var st = new Node3D { Name = "Radio966", Position = center + new Vector3(0f, CurbHeight, 0f) };
        block.AddChild(st);

        StandardMaterial3D gravel = Flat(new Color(0.3f, 0.29f, 0.27f));
        StandardMaterial3D wall = Flat(new Color(0.16f, 0.17f, 0.19f), 0.7f);
        StandardMaterial3D green = Emissive(new Color(0.35f, 1f, 0.45f), 1.4f);
        StandardMaterial3D red = Flat(new Color(0.8f, 0.1f, 0.08f), 0.6f);
        StandardMaterial3D white = Flat(new Color(0.92f, 0.92f, 0.9f), 0.6f);
        StandardMaterial3D floor = Flat(new Color(0.32f, 0.3f, 0.3f), 0.8f);
        StandardMaterial3D foam = Flat(new Color(0.42f, 0.26f, 0.55f), 1f);
        StandardMaterial3D inner = Flat(new Color(0.55f, 0.55f, 0.58f), 0.8f);

        AddBox(st, new Vector3(0f, 0.01f, 0f), new Vector3(30f, 0.02f, 30f), gravel, false);

        // ---------------- estúdio (enterável). Frente em z = 9, porta no meio.
        const float x0 = -13f, x1 = 1f, z0 = -2f, z1 = 9f, h = 5f, t = 0.25f;
        float cx = (x0 + x1) * 0.5f, cz = (z0 + z1) * 0.5f;
        AddBox(st, new Vector3(cx, 0.03f, cz), new Vector3(x1 - x0, 0.04f, z1 - z0), floor, false);
        AddBox(st, new Vector3(cx, h, cz), new Vector3(x1 - x0 + 0.4f, 0.3f, z1 - z0 + 0.4f), wall);
        AddBox(st, new Vector3(cx, h * 0.5f, z0), new Vector3(x1 - x0, h, t), wall);
        AddBox(st, new Vector3(x0, h * 0.5f, cz), new Vector3(t, h, z1 - z0), wall);
        AddBox(st, new Vector3(x1, h * 0.5f, cz), new Vector3(t, h, z1 - z0), wall);
        const float doorA = -7f, doorB = -5.4f;
        AddBox(st, new Vector3((x0 + doorA) * 0.5f, h * 0.5f, z1), new Vector3(doorA - x0, h, t), wall);
        AddBox(st, new Vector3((doorB + x1) * 0.5f, h * 0.5f, z1), new Vector3(x1 - doorB, h, t), wall);
        AddBox(st, new Vector3((doorA + doorB) * 0.5f, (2.4f + h) * 0.5f, z1), new Vector3(doorB - doorA, h - 2.4f, t), wall);
        // Vitrine do estúdio e letreiro aceso.
        AddBox(st, new Vector3(-1.8f, 2.0f, z1), new Vector3(4.2f, 1.6f, 0.3f), _glass, false);
        AddBox(st, new Vector3(-10.2f, 2.0f, z1), new Vector3(4.2f, 1.6f, 0.3f), _glass, false);
        TexQuad(st, "res://Assets/Textures/Stores/sign_radio966.png", new Vector3(cx, h - 0.9f, z1 + 0.16f), new Vector2(10.5f, 1.5f), 0f, 1.2f);
        AddBox(st, new Vector3(cx, h - 0.9f, z1 + 0.1f), new Vector3(10.9f, 1.7f, 0.05f), _darkMetal, false);
        // Luz "NO AR" em cima da porta.
        AddBox(st, new Vector3((doorA + doorB) * 0.5f, 2.75f, z1 + 0.18f), new Vector3(1.1f, 0.35f, 0.06f), Emissive(new Color(1f, 0.1f, 0.05f), 2.5f), false);
        st.AddChild(new Label3D
        {
            Text = "NO AR",
            FontSize = 48,
            PixelSize = 0.004f,
            Modulate = Colors.White,
            OutlineSize = 0,
            Position = new Vector3((doorA + doorB) * 0.5f, 2.75f, z1 + 0.22f),
        });

        // Por dentro: espuma acústica, mesa de som, microfones, discos e o rádio no ar.
        for (float x = x0 + 0.8f; x < x1 - 0.5f; x += 1.2f)
            AddBox(st, new Vector3(x, 2.6f, z0 + 0.21f), new Vector3(1.0f, 2.4f, 0.08f), foam, false);
        AddBox(st, new Vector3(-6f, 0.45f, z0 + 1.6f), new Vector3(5f, 0.9f, 1.2f), wall);
        RadioConsolePoint = st.Position + new Vector3(-6f, 0f, z0 + 1.6f);
        AddBox(st, new Vector3(-6f, 0.93f, z0 + 1.55f), new Vector3(4.6f, 0.06f, 0.9f), _darkMetal, false);
        for (int i = 0; i < 18; i++)
        {
            float x = -8.1f + i * 0.25f;
            AddBox(st, new Vector3(x, 1.0f, z0 + 1.5f + (i % 3) * 0.05f), new Vector3(0.05f, 0.08f, 0.12f), i % 4 == 0 ? red : white, false);
        }
        AddBox(st, new Vector3(-6f, 1.35f, z0 + 1.15f), new Vector3(1.8f, 0.8f, 0.1f), green, false); // VU / monitor
        foreach (float mx in new[] { -7.6f, -4.4f })
        {
            AddCylinder(st, new Vector3(mx, 1.25f, z0 + 2.0f), 0.015f, 0.6f, _darkMetal);
            AddCylinder(st, new Vector3(mx, 1.6f, z0 + 2.0f), 0.06f, 0.18f, _darkMetal);
            AssetProps.Spawn(st, AssetProps.Furniture + "chair_C.gltf", new Vector3(mx, 0.03f, z0 + 2.8f), Mathf.Pi, 0.75f, new Vector3(0.55f, 0.9f, 0.55f));
        }
        // Estante de discos.
        AddBox(st, new Vector3(x1 - 0.5f, 1.2f, cz), new Vector3(0.6f, 2.4f, 5f), _trunk);
        for (int i = 0; i < 30; i++)
            AddBox(st, new Vector3(x1 - 0.8f, 0.4f + (i % 5) * 0.45f, cz - 2.2f + (i / 5) * 0.8f), new Vector3(0.05f, 0.32f, 0.32f), Flat(WallPalette[i % WallPalette.Length] * 1.6f, 0.5f), false);
        st.AddChild(new OmniLight3D { Position = new Vector3(cx, h - 0.6f, cz), LightColor = new Color(0.9f, 0.8f, 1f), LightEnergy = 2.2f, OmniRange = 11f });
        st.AddChild(new OmniLight3D { Position = new Vector3(-6f, 2.6f, z0 + 2.5f), LightColor = new Color(1f, 0.85f, 0.7f), LightEnergy = 1.5f, OmniRange = 6f });
        // Revestimento claro por dentro (as paredes de fora são escuras).
        AddBox(st, new Vector3(cx, h * 0.5f, z0 + 0.14f), new Vector3(x1 - x0 - 0.4f, h - 0.1f, 0.02f), inner, false);
        AddBox(st, new Vector3(x0 + 0.14f, h * 0.5f, cz), new Vector3(0.02f, h - 0.1f, z1 - z0 - 0.4f), inner, false);
        AddBox(st, new Vector3(x1 - 0.14f, h * 0.5f, cz), new Vector3(0.02f, h - 0.1f, z1 - z0 - 0.4f), inner, false);
        AddBox(st, new Vector3(cx, h - 0.16f, cz), new Vector3(x1 - x0 - 0.4f, 0.02f, z1 - z0 - 0.4f), inner, false);
        AddBox(st, new Vector3(cx, h - 0.2f, cz), new Vector3(2.4f, 0.05f, 0.3f), Emissive(new Color(1f, 0.95f, 1f), 1.8f), false);
        BuildRadioProp(st, new Vector3(-6f, 0.96f, z0 + 1.9f), 0f, true, -2f);
        // Caixa de som na fachada: quem passa na calçada ouve a rádio.
        st.AddChild(new RadioPlayer { Name = "FacadeSpeaker", On = true, VolumeDb = -8f, UnitSize = 5f, MaxDistance = 28f, Position = new Vector3((doorA + doorB) * 0.5f, 3.4f, z1 + 0.4f) });
        // Parabólica no telhado.
        AddCylinder(st, new Vector3(-10f, h + 0.9f, 2f), 0.06f, 1.5f, _darkMetal);
        MeshInstance3D dish = AddCylinder(st, new Vector3(-10f, h + 1.8f, 2.2f), 1.1f, 0.12f, white);
        dish.Rotation = new Vector3(-1.0f, 0f, 0f);

        // ---------------- torre de transmissão
        BuildTransmissionTower(st, new Vector3(8f, 0f, -6f), 62f, red, white);

        // Muro em volta do terreno com grade de metal em cima (v0.9.9). Portão de carro na frente e,
        // do lado +X, um portãozinho de pedestre (só passa gente, não carro).
        const float muroH = 1.0f, gateZ = 6f, gateW = 1.1f;
        float g0 = gateZ - gateW * 0.5f, g1 = gateZ + gateW * 0.5f;
        foreach ((Vector3 p, Vector3 s) in new[]
        {
            (new Vector3(0f, muroH * 0.5f, -14.5f), new Vector3(29f, muroH, 0.2f)),
            (new Vector3(-14.5f, muroH * 0.5f, 0f), new Vector3(0.2f, muroH, 29f)),
            (new Vector3(14.5f, muroH * 0.5f, (-14.5f + g0) * 0.5f), new Vector3(0.2f, muroH, g0 + 14.5f)),
            (new Vector3(14.5f, muroH * 0.5f, (g1 + 14.5f) * 0.5f), new Vector3(0.2f, muroH, 14.5f - g1)),
            (new Vector3(-9f, muroH * 0.5f, 14.5f), new Vector3(11f, muroH, 0.2f)),
            (new Vector3(7f, muroH * 0.5f, 14.5f), new Vector3(15f, muroH, 0.2f)),
        })
            WallWithRailing(st, p, s, wall, 1.0f);
        BuildPedestrianGate(st, new Vector3(14.5f, 0f, gateZ), gateW, muroH + 1.0f);
        RegisterObstacle(st.Position + new Vector3(-6.2f, 0f, 15f));
        Poster(st, new Vector3(-12f, 0.7f, 14.55f), 0f, 0.9f);
        BuildTeuzinBillboard(st);
    }

    /// <summary>
    /// Portãozinho de pedestre num muro em x fixo (vão ao longo de Z): pilares, verga com grade e a folha
    /// de grade aberta para dentro (decorativa, sem colisão: dá para passar a pé).
    /// </summary>
    private void BuildPedestrianGate(Node3D parent, Vector3 center, float width, float height)
    {
        EnsureRailingResources();
        float z0 = center.Z - width * 0.5f, z1 = center.Z + width * 0.5f;
        foreach (float z in new[] { z0 - 0.12f, z1 + 0.12f })
            AddBox(parent, new Vector3(center.X, height * 0.5f + 0.1f, z), new Vector3(0.3f, height + 0.2f, 0.24f), _concrete);
        // Verga em arco simples por cima do vão (cara de portão de casa).
        AddBox(parent, new Vector3(center.X, height + 0.12f, center.Z), new Vector3(0.12f, 0.08f, width + 0.24f), _railMetal, false);
        // Folha aberta ~75° para dentro, presa no pilar de z0.
        var leaf = new Node3D { Position = new Vector3(center.X - 0.04f, 0f, z0), Rotation = new Vector3(0f, Mathf.DegToRad(-75f), 0f) };
        parent.AddChild(leaf);
        // Na folha local, o vão vai de z = 0 até z = width.
        BuildRailingVisual(leaf, new Vector3(0f, 0f, 0.02f), new Vector3(0f, 0f, width - 0.04f), 0.05f, height - 0.15f);
        AddBox(leaf, new Vector3(0f, 0.08f, width * 0.5f), new Vector3(0.05f, 0.05f, width - 0.04f), _railMetal, false);
        AddBox(leaf, new Vector3(0f, height * 0.45f, width - 0.06f), new Vector3(0.04f, 0.12f, 0.04f), _darkMetal, false); // trinco
    }

    private void BuildTransmissionTower(Node3D parent, Vector3 basePos, float height, Material red, Material white)
    {
        var tower = new StaticBody3D { Name = "TorreTransmissao", Position = basePos, CollisionLayer = 1, CollisionMask = 0 };
        parent.AddChild(tower);
        tower.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(3.2f, 4f, 3.2f) }, Position = new Vector3(0f, 2f, 0f) });
        tower.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(1.4f, height, 1.4f) }, Position = new Vector3(0f, height * 0.5f, 0f) });

        const float baseHalf = 1.6f, topHalf = 0.35f, segment = 5f;
        Vector3 Leg(int corner, float y)
        {
            float half = Mathf.Lerp(baseHalf, topHalf, y / height);
            float sx = corner is 0 or 3 ? -1f : 1f, sz = corner < 2 ? -1f : 1f;
            return new Vector3(sx * half, y, sz * half);
        }
        int n = Mathf.CeilToInt(height / segment);
        for (int i = 0; i < n; i++)
        {
            float ya = i * segment, yb = Mathf.Min(height, ya + segment);
            Material paint = (i / 2) % 2 == 0 ? red : white; // faixas de 10 m vermelho/branco
            for (int c = 0; c < 4; c++)
            {
                int c2 = (c + 1) % 4;
                AddBeam(tower, Leg(c, ya), Leg(c, yb), 0.07f, paint);
                AddBeam(tower, Leg(c, ya), Leg(c2, ya), 0.035f, paint);
                AddBeam(tower, Leg(c, ya), Leg(c2, yb), 0.03f, paint); // diagonal (treliça em X)
                AddBeam(tower, Leg(c2, ya), Leg(c, yb), 0.03f, paint);
            }
        }
        // Mastro, antenas de painel e parabólicas.
        AddBeam(tower, new Vector3(0f, height, 0f), new Vector3(0f, height + 8f, 0f), 0.08f, white);
        for (int i = 0; i < 4; i++)
        {
            float ang = i * Mathf.Pi * 0.5f;
            var dir = new Vector3(Mathf.Sin(ang), 0f, Mathf.Cos(ang));
            AddBox(tower, dir * 0.55f + new Vector3(0f, height - 6f, 0f), new Vector3(0.35f, 2.2f, 0.12f), white, false, ang);
        }
        MeshInstance3D d1 = AddCylinder(tower, new Vector3(0f, height * 0.55f, 1.4f), 0.9f, 0.15f, white);
        d1.Rotation = new Vector3(Mathf.Pi * 0.5f, 0f, 0f);
        MeshInstance3D d2 = AddCylinder(tower, new Vector3(-1.2f, height * 0.4f, 0f), 0.7f, 0.15f, white);
        d2.Rotation = new Vector3(0f, 0f, Mathf.Pi * 0.5f);
        // Base de concreto e casinha dos transmissores.
        AddBox(tower, new Vector3(0f, 0.15f, 0f), new Vector3(4.2f, 0.3f, 4.2f), _concrete, false);
        AddBox(parent, basePos + new Vector3(-4.5f, 1.3f, 2f), new Vector3(3f, 2.6f, 3.4f), _warehouse);

        // Luzes de balizamento piscando (de dia também, como as de verdade).
        Vector3 world = parent.Position + basePos;
        foreach (float y in new[] { height * 0.33f, height * 0.66f, height + 8.2f })
        {
            AddBox(tower, new Vector3(0f, y, 0f), new Vector3(0.4f, 0.4f, 0.4f), _redBeacon, false);
            var beacon = new OmniLight3D { Position = world + new Vector3(0f, y, 0f), LightColor = new Color(1f, 0.1f, 0.05f), LightEnergy = 3f, OmniRange = 18f };
            ActiveLightsRoot.AddChild(beacon);
            Tween blink = CreateTween().SetLoops();
            blink.TweenProperty(beacon, "light_energy", 0.0, 0.7);
            blink.TweenProperty(beacon, "light_energy", 3.0, 0.1);
            blink.TweenInterval(0.5);
        }
    }

    /// <summary>Radinho de mesa sintonizado na 96.6. E liga/desliga.</summary>
    private RadioPlayer BuildRadioProp(Node3D parent, Vector3 position, float rotY, bool on, float volumeDb = -8f)
    {
        var body = new RadioInteractable
        {
            Name = "Radio",
            Position = position,
            Rotation = new Vector3(0f, rotY, 0f),
            CollisionLayer = 4, // Interactables
            CollisionMask = 0,
        };
        body.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(0.46f, 0.26f, 0.18f) }, Position = new Vector3(0f, 0.13f, 0f) });
        parent.AddChild(body);
        StandardMaterial3D case_ = Flat(new Color(0.35f, 0.22f, 0.12f), 0.5f);
        AddBox(body, new Vector3(0f, 0.13f, 0f), new Vector3(0.44f, 0.24f, 0.16f), case_, false);
        AddBox(body, new Vector3(-0.1f, 0.13f, 0.081f), new Vector3(0.2f, 0.18f, 0.01f), _darkMetal, false); // alto-falante
        AddBox(body, new Vector3(0.11f, 0.17f, 0.081f), new Vector3(0.16f, 0.06f, 0.01f), Emissive(new Color(0.4f, 1f, 0.5f), 1.2f), false);
        AddCylinder(body, new Vector3(0.07f, 0.07f, 0.09f), 0.025f, 0.02f, _signWhite).Rotation = new Vector3(Mathf.Pi * 0.5f, 0f, 0f);
        AddCylinder(body, new Vector3(0.15f, 0.07f, 0.09f), 0.025f, 0.02f, _signWhite).Rotation = new Vector3(Mathf.Pi * 0.5f, 0f, 0f);
        AddBeam(body, new Vector3(0.18f, 0.25f, -0.04f), new Vector3(0.28f, 0.6f, -0.04f), 0.005f, _darkMetal);
        body.AddChild(new Label3D
        {
            Text = "96.6",
            FontSize = 24,
            PixelSize = 0.0018f,
            Modulate = new Color(0.05f, 0.2f, 0.05f),
            OutlineSize = 0,
            Position = new Vector3(0.11f, 0.17f, 0.088f),
        });
        var radio = new RadioPlayer { Name = "Player", On = on, VolumeDb = volumeDb, UnitSize = 3.5f, MaxDistance = 20f, Position = new Vector3(0f, 0.13f, 0f) };
        body.AddChild(radio);
        body.Radio = radio;
        return radio;
    }
}
