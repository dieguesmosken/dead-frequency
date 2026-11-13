// Scripts/Vehicle/VanDetails.cs
// Modelo da caminhonete de entrega (v0.9): estilo "Bongo" anos 90 — cabine avançada (cab-over) branca com
// faixa verde da empresa, para-brisa grande, carroceria de madeira com guardas laterais, grade de proteção
// atrás da cabine, para-lamas, tanque, retrovisores de caminhão, giroflex e lanternas.
// Monta tudo em código e esconde as caixas antigas da cena (van.tscn). As colisões continuam as mesmas
// (cabine na frente, caçamba atrás com laterais de 45 cm). Frente = +Z. Só visual.
using Godot;

namespace DeadFrequency;

public static class VanDetails
{
    private static readonly string[] OldNodes =
    {
        "Body/ChassisMesh", "Body/BedWallLMesh", "Body/BedWallRMesh", "Body/TailgateMesh",
        "Cabin/Roof", "Cabin/BackWall", "Cabin/DoorL", "Cabin/DoorR", "Cabin/PillarFL", "Cabin/PillarFR",
        "Cabin/PillarBL", "Cabin/PillarBR", "Cabin/Hood", "Cabin/HeadlightL", "Cabin/HeadlightR",
        "Cabin/TaillightL", "Cabin/TaillightR",
    };

    /// <summary>Medidas da caminhonete atual (para-choque a para-choque e largura da caçamba).</summary>
    private const float TruckLength = 4.85f, TruckWidth = 2.0f;
    /// <summary>Meia-largura da porta da cabine do modelo do Meshy (a largura total inclui os retrovisores).</summary>
    private const float CabHalfWidth = 0.86f;

    public static void Build(VanController van)
    {
        foreach (string path in OldNodes)
            if (van.GetNodeOrNull<Node3D>(path) is Node3D old)
                old.Visible = false;

        var root = new Node3D { Name = "Model" };
        van.AddChild(root);

        StandardMaterial3D white = Mat(new Color(0.9f, 0.9f, 0.87f), 0.45f);
        StandardMaterial3D green = Mat(new Color(0.1f, 0.42f, 0.2f), 0.5f);
        StandardMaterial3D dark = Mat(new Color(0.06f, 0.06f, 0.06f), 0.9f);
        StandardMaterial3D steel = new() { AlbedoColor = new Color(0.45f, 0.46f, 0.48f), Metallic = 0.6f, Roughness = 0.45f };
        StandardMaterial3D chrome = new() { AlbedoColor = new Color(0.7f, 0.7f, 0.72f), Metallic = 0.85f, Roughness = 0.25f };
        StandardMaterial3D wood = Mat(new Color(0.5f, 0.33f, 0.17f), 0.85f);
        StandardMaterial3D woodDark = Mat(new Color(0.36f, 0.23f, 0.12f), 0.9f);
        StandardMaterial3D plate = Mat(new Color(0.85f, 0.86f, 0.82f), 0.7f);
        StandardMaterial3D glass = new()
        {
            AlbedoColor = new Color(0.55f, 0.7f, 0.75f, 0.18f), Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            Roughness = 0.05f, Metallic = 0.3f, CullMode = BaseMaterial3D.CullModeEnum.Disabled,
        };
        StandardMaterial3D head = Glow(new Color(1f, 0.95f, 0.8f), 1.6f);
        StandardMaterial3D tail = Glow(new Color(0.85f, 0.05f, 0.04f), 0.9f);
        StandardMaterial3D amber = Glow(new Color(1f, 0.55f, 0.1f), 0.7f);
        StandardMaterial3D beacon = new()
        {
            AlbedoColor = new Color(1f, 0.55f, 0.1f, 0.85f), Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            EmissionEnabled = true, Emission = new Color(1f, 0.45f, 0.05f), EmissionEnergyMultiplier = 1.2f,
        };

        // v0.10: carroceria do Meshy (rodas, calotas, caçamba e cabine num modelo só). As colisões da cena
        // continuam; daqui só sobe o interior (assoalho, volante, rádio) e os letreiros por cima.
        Vector3 shell = MeshyVehicles.HasTruck ? MeshyVehicles.BuildTruck(root, TruckLength, TruckWidth) : Vector3.Zero;
        bool meshy = shell != Vector3.Zero;
        if (meshy && van.GetNodeOrNull<Node3D>("Cabin/Dashboard") is Node3D dash)
        {
            // A frente do modelo novo é inclinada e a cabine é mais estreita que os retrovisores:
            // o painel antigo (1,8 m) passava para fora. Encolhe e recua um pouco.
            dash.Scale = new Vector3(0.84f, 1f, 0.8f);
            dash.Position = new Vector3(0f, 1.4f, 2.0f);
        }
        if (meshy)
            foreach (string w in new[] { "WheelFL", "WheelFR", "WheelRL", "WheelRR" })
                if (van.GetNodeOrNull<Node3D>(w + "/Mesh") is Node3D wheelMesh)
                    wheelMesh.Visible = false; // as rodas (com calota) já vêm no modelo

        if (!meshy)
        {
            // ---------------- chassi e carroceria de madeira (z -2,3 .. 0,9)
            Box(root, new Vector3(0.45f, 0.72f, 0f), new Vector3(0.12f, 0.2f, 4.5f), dark);
            Box(root, new Vector3(-0.45f, 0.72f, 0f), new Vector3(0.12f, 0.2f, 4.5f), dark);
            Box(root, new Vector3(0f, 0.92f, -0.7f), new Vector3(1.95f, 0.12f, 3.2f), steel);   // base
            for (int i = 0; i < 8; i++)                                                           // assoalho em tábuas
                Box(root, new Vector3(-0.85f + i * 0.243f, 1.0f, -0.7f), new Vector3(0.23f, 0.04f, 3.15f), i % 2 == 0 ? wood : woodDark);
            foreach (float side in new[] { -1f, 1f })
            {
                // Guardas laterais: 3 tábuas + postes de aço.
                for (int r = 0; r < 3; r++)
                    Box(root, new Vector3(side * 0.93f, 1.08f + r * 0.14f, -0.7f), new Vector3(0.06f, 0.12f, 3.2f), r == 1 ? woodDark : wood);
                foreach (float z in new[] { -2.25f, -1.2f, -0.2f, 0.85f })
                    Box(root, new Vector3(side * 0.95f, 1.23f, z), new Vector3(0.07f, 0.5f, 0.07f), steel);
                Box(root, new Vector3(side * 0.95f, 1.47f, -0.7f), new Vector3(0.08f, 0.04f, 3.22f), steel);
                // Para-lamas traseiros e lameiros.
                Box(root, new Vector3(side * 0.85f, 0.9f, -1.5f), new Vector3(0.3f, 0.06f, 0.95f), dark);
                Box(root, new Vector3(side * 0.85f, 0.42f, -2.05f), new Vector3(0.28f, 0.32f, 0.02f), dark);
            }
            // Tampa traseira (madeira) com dobradiças.
            for (int r = 0; r < 3; r++)
                Box(root, new Vector3(0f, 1.08f + r * 0.14f, -2.27f), new Vector3(1.9f, 0.12f, 0.06f), r == 1 ? woodDark : wood);
            foreach (float x in new[] { -0.6f, 0.6f })
                Box(root, new Vector3(x, 1.0f, -2.31f), new Vector3(0.12f, 0.04f, 0.04f), steel);
            // Grade de proteção atrás da cabine.
            Box(root, new Vector3(0f, 2.15f, 0.88f), new Vector3(1.95f, 0.06f, 0.06f), steel);
            for (int i = 0; i < 9; i++)
                Box(root, new Vector3(-0.9f + i * 0.225f, 1.6f, 0.88f), new Vector3(0.03f, 1.1f, 0.03f), steel);
            // Tanque de combustível e estepe embaixo.
            Box(root, new Vector3(-0.72f, 0.62f, 0.2f), new Vector3(0.3f, 0.3f, 0.7f), steel);
            root.AddChild(new MeshInstance3D
            {
                Mesh = new CylinderMesh { TopRadius = 0.33f, BottomRadius = 0.33f, Height = 0.22f, RadialSegments = 12 },
                MaterialOverride = dark,
                Position = new Vector3(0f, 0.5f, -1.95f),
            });
        }

        // ---------------- chão da cabine e raios do volante (v0.9.6)
        Box(root, new Vector3(0f, 0.53f, 1.6f), new Vector3(1.86f, 0.05f, 1.3f), dark);   // assoalho de borracha
        Box(root, new Vector3(0f, 0.75f, 2.2f), new Vector3(1.86f, 0.42f, 0.12f), dark);  // parede dos pedais
        if (van.GetNodeOrNull<Node3D>("Cabin/SteeringWheel") is Node3D wheel)
        {
            // Raios em "T" e o miolo: sem eles o aro redondo girava sem dar para ver.
            Box(wheel, Vector3.Zero, new Vector3(0.34f, 0.035f, 0.025f), dark);
            Box(wheel, new Vector3(0f, -0.085f, 0f), new Vector3(0.035f, 0.17f, 0.025f), dark);
            Box(wheel, new Vector3(0f, 0f, 0.01f), new Vector3(0.08f, 0.08f, 0.04f), green);
            Box(wheel, new Vector3(0f, 0.17f, 0.012f), new Vector3(0.04f, 0.02f, 0.03f), amber); // marca do "meio"
        }

        // ---------------- cabine avançada (z 0,92 .. 2,3)
        const float cz0 = 0.92f, cz1 = 2.3f, cy1 = 2.42f, hw = 0.97f;
        float cmid = (cz0 + cz1) * 0.5f, clen = cz1 - cz0;
        if (!meshy)
        {
            Box(root, new Vector3(0f, cy1, cmid), new Vector3(hw * 2f, 0.08f, clen + 0.04f), white);           // teto
            Box(root, new Vector3(0f, 1.15f, cz0), new Vector3(hw * 2f, 0.9f, 0.06f), white);                  // traseira baixa
            Box(root, new Vector3(0f, 2.3f, cz0), new Vector3(hw * 2f, 0.24f, 0.06f), white);                  // traseira alta
            Box(root, new Vector3(0f, 1.85f, cz0), new Vector3(1.3f, 0.66f, 0.03f), glass);                    // vidro traseiro
            foreach (float side in new[] { -1f, 1f })
            {
                float x = side * hw;
                Box(root, new Vector3(x, 1.05f, cmid), new Vector3(0.05f, 0.75f, clen), white);                // porta (baixo)
                Box(root, new Vector3(x, 0.98f, cmid), new Vector3(0.055f, 0.14f, clen), green);               // faixa da empresa
                Box(root, new Vector3(x, 1.8f, cmid), new Vector3(0.02f, 0.85f, clen - 0.1f), glass);          // janela
                Box(root, new Vector3(x, 1.8f, cz0 + 0.03f), new Vector3(0.06f, 0.85f, 0.08f), white);         // coluna B
                Box(root, new Vector3(x, 1.8f, cz1 - 0.04f), new Vector3(0.06f, 0.85f, 0.08f), white);         // coluna A
                Box(root, new Vector3(x, 1.44f, cmid), new Vector3(0.06f, 0.04f, clen), white);                // frisos da janela
                Box(root, new Vector3(x * 1.0f + side * 0.02f, 1.32f, cmid + 0.35f), new Vector3(0.03f, 0.04f, 0.16f), chrome); // maçaneta
                Box(root, new Vector3(side * 0.88f, 0.55f, cmid), new Vector3(0.22f, 0.04f, 0.5f), dark);     // estribo
                // Retrovisor de caminhão.
                Box(root, new Vector3(side * 1.12f, 1.9f, cz1 - 0.1f), new Vector3(0.3f, 0.025f, 0.025f), dark);
                Box(root, new Vector3(side * 1.27f, 1.85f, cz1 - 0.1f), new Vector3(0.04f, 0.36f, 0.2f), dark);
                Box(root, new Vector3(side * 1.25f, 1.85f, cz1 - 0.1f), new Vector3(0.005f, 0.32f, 0.17f), chrome);
                // Para-lamas dianteiros.
                Box(root, new Vector3(side * 0.85f, 0.95f, 1.5f), new Vector3(0.32f, 0.08f, 0.9f), dark);
            }
            // Frente: painel baixo com grade, faróis, para-brisa grande inclinado.
            Box(root, new Vector3(0f, 1.0f, cz1), new Vector3(hw * 2f, 0.7f, 0.06f), white);
            Box(root, new Vector3(0f, 0.72f, cz1 + 0.01f), new Vector3(hw * 2f, 0.14f, 0.06f), green);
            Box(root, new Vector3(0f, 1.0f, cz1 + 0.035f), new Vector3(0.9f, 0.28f, 0.02f), dark);
            for (int i = -2; i <= 2; i++)
                Box(root, new Vector3(0f, 1.0f + i * 0.05f, cz1 + 0.05f), new Vector3(0.92f, 0.018f, 0.015f), chrome);
            root.AddChild(new Label3D
            {
                Text = "DF", FontSize = 40, PixelSize = 0.004f, Modulate = new Color(0.1f, 0.42f, 0.2f), OutlineSize = 0,
                Position = new Vector3(0f, 1.24f, cz1 + 0.035f),
            });
            foreach (float side in new[] { -1f, 1f })
            {
                Box(root, new Vector3(side * 0.67f, 1.0f, cz1 + 0.035f), new Vector3(0.34f, 0.2f, 0.02f), chrome);
                Box(root, new Vector3(side * 0.67f, 1.0f, cz1 + 0.05f), new Vector3(0.3f, 0.16f, 0.02f), head);
                Box(root, new Vector3(side * 0.9f, 1.0f, cz1 + 0.05f), new Vector3(0.1f, 0.14f, 0.02f), amber);
                Box(root, new Vector3(side * 0.95f, 1.86f, cz1 - 0.02f), new Vector3(0.06f, 0.9f, 0.06f), white); // moldura do para-brisa
            }
            var windshield = new MeshInstance3D
            {
                Mesh = new BoxMesh { Size = new Vector3(hw * 2f - 0.1f, 0.95f, 0.02f) },
                MaterialOverride = glass,
                Position = new Vector3(0f, 1.86f, cz1 - 0.05f),
                Rotation = new Vector3(-0.08f, 0f, 0f),
            };
            root.AddChild(windshield);
            Box(root, new Vector3(0f, 2.36f, cz1 - 0.02f), new Vector3(hw * 2f, 0.1f, 0.07f), white);
            Box(root, new Vector3(0f, 1.36f, cz1 - 0.02f), new Vector3(hw * 2f, 0.06f, 0.07f), white);
            // Limpadores.
            foreach (float x in new[] { -0.35f, 0.4f })
                Box(root, new Vector3(x, 1.5f, cz1 + 0.01f), new Vector3(0.02f, 0.3f, 0.015f), dark, 0.5f);
            // Para-choque, placa, lanternas de cabine no teto e giroflex.
            Box(root, new Vector3(0f, 0.6f, 2.38f), new Vector3(2.0f, 0.18f, 0.14f), dark);
            Box(root, new Vector3(0f, 0.6f, 2.46f), new Vector3(0.5f, 0.13f, 0.02f), plate);
            for (int i = -1; i <= 1; i++)
                Box(root, new Vector3(i * 0.25f, cy1 + 0.06f, cz1 - 0.06f), new Vector3(0.12f, 0.05f, 0.06f), amber);
            Box(root, new Vector3(0f, cy1 + 0.07f, 1.4f), new Vector3(0.5f, 0.06f, 0.18f), dark);
            root.AddChild(new MeshInstance3D
            {
                Mesh = new CylinderMesh { TopRadius = 0.08f, BottomRadius = 0.1f, Height = 0.14f, RadialSegments = 10 },
                Position = new Vector3(0f, cy1 + 0.17f, 1.4f),
                MaterialOverride = beacon,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            });
            Box(root, new Vector3(-0.85f, 2.85f, 2.1f), new Vector3(0.015f, 0.8f, 0.015f), dark); // antena do rádio

            // Traseira: para-choque, lanternas e placa.
            Box(root, new Vector3(0f, 0.6f, -2.36f), new Vector3(2.0f, 0.16f, 0.12f), dark);
            Box(root, new Vector3(0f, 0.82f, -2.33f), new Vector3(0.5f, 0.13f, 0.02f), plate);
            foreach (float side in new[] { -1f, 1f })
            {
                Box(root, new Vector3(side * 0.75f, 0.82f, -2.33f), new Vector3(0.24f, 0.14f, 0.04f), tail);
                Box(root, new Vector3(side * 0.92f, 0.82f, -2.33f), new Vector3(0.08f, 0.14f, 0.04f), amber);
            }

        }

        // Textos: empresa nas portas, placas e aviso na tampa.
        foreach (float side in new[] { -1f, 1f })
        {
            van.AddChild(new Label3D
            {
                Text = "DEAD FREQUENCY\nENTREGAS · 96.6",
                FontSize = 44,
                PixelSize = meshy ? 0.0034f : 0.0042f,
                Modulate = new Color(0.1f, 0.42f, 0.2f),
                OutlineSize = 0,
                Position = new Vector3(side * (meshy ? CabHalfWidth + 0.02f : hw + 0.03f), meshy ? 0.86f : 1.18f, meshy ? 1.45f : cmid),
                Rotation = new Vector3(0f, side * Mathf.Pi * 0.5f, 0f),
            });
        }
        // Placas: a caminhonete da empresa é DFQ-1996; as outras ganham a sua (Plates).
        string plateCode = van.NetId == "van" ? "DFQ-1996" : Plates.For(van.NetId);
        float plateZ = meshy ? shell.Z * 0.5f + 0.03f : 2.475f;
        Plates.Attach(van, plateCode, new Vector3(0f, meshy ? 0.55f : 0.6f, plateZ), true);
        Plates.Attach(van, plateCode, new Vector3(0f, meshy ? 0.75f : 0.82f, meshy ? -plateZ : -2.345f), false);
        van.AddChild(new Label3D
        {
            Text = "CARGA FRÁGIL · MANTENHA DISTÂNCIA",
            FontSize = 40,
            PixelSize = meshy ? 0.0024f : 0.0032f, // cabe na tampa da caçamba
            Modulate = new Color(0.95f, 0.85f, 0.2f),
            OutlineSize = 6,
            OutlineModulate = new Color(0.05f, 0.05f, 0.05f),
            Position = new Vector3(0f, 1.22f, meshy ? -shell.Z * 0.5f - 0.03f : -2.31f),
            Rotation = new Vector3(0f, Mathf.Pi, 0f),
        });

        // Rádio no painel (aceso quando ligado).
        Box(root, new Vector3(0f, 1.45f, 1.95f), new Vector3(0.36f, 0.1f, 0.1f), dark);
        van.AddChild(new Label3D
        {
            Name = "RadioDisplay",
            Text = "96.6",
            FontSize = 24,
            PixelSize = 0.0016f,
            Modulate = new Color(0.4f, 1f, 0.5f),
            OutlineSize = 0,
            Position = new Vector3(0f, 1.46f, 1.894f),
            Visible = false,
            Rotation = new Vector3(0f, Mathf.Pi, 0f),
        });
    }

    private static StandardMaterial3D Mat(Color c, float roughness) => new() { AlbedoColor = c, Roughness = roughness };

    private static StandardMaterial3D Glow(Color c, float energy) =>
        new() { AlbedoColor = c, EmissionEnabled = true, Emission = c, EmissionEnergyMultiplier = energy };

    private static void Box(Node3D parent, Vector3 pos, Vector3 size, Material mat, float rotZ = 0f) =>
        parent.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = size }, Position = pos, MaterialOverride = mat, Rotation = new Vector3(0f, 0f, rotZ) });
}
