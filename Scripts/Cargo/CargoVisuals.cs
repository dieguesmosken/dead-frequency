// Scripts/Cargo/CargoVisuals.cs
// Aparência de cada encomenda (pelo CargoData.CargoId). Troca a caixa marrom genérica por um modelo
// que mostra o que é: monitor de tubo, bateria, maleta de documentos, botijão, bolo, aquário...
// Ajusta também a caixa de colisão para o tamanho do objeto.
// Caixote de tomates e panela de feijoada usam modelos CC0 da KayKit (Restaurant Bits).
using Godot;

namespace DeadFrequency;

public static class CargoVisuals
{
    private static StandardMaterial3D M(Color c, float rough = 0.8f, float metal = 0f) => new() { AlbedoColor = c, Roughness = rough, Metallic = metal };

    private static readonly StandardMaterial3D Cardboard = M(new Color(0.58f, 0.43f, 0.27f), 0.9f);
    private static readonly StandardMaterial3D Tape = M(new Color(0.72f, 0.6f, 0.4f), 0.4f);
    private static readonly StandardMaterial3D Beige = M(new Color(0.78f, 0.74f, 0.64f));
    private static readonly StandardMaterial3D Dark = M(new Color(0.06f, 0.06f, 0.07f), 0.6f);
    private static readonly StandardMaterial3D Wood = M(new Color(0.55f, 0.4f, 0.24f), 0.9f);
    private static readonly StandardMaterial3D WoodDark = M(new Color(0.35f, 0.24f, 0.14f), 0.9f);
    private static readonly StandardMaterial3D White = M(new Color(0.92f, 0.92f, 0.9f));
    private static readonly StandardMaterial3D Red = M(new Color(0.8f, 0.08f, 0.06f));
    private static readonly StandardMaterial3D Metal = M(new Color(0.62f, 0.64f, 0.66f), 0.3f, 0.8f);
    private static readonly StandardMaterial3D Yellow = M(new Color(0.95f, 0.75f, 0.1f), 0.5f);

    public static void Apply(CargoItem item)
    {
        string id = item.Data?.CargoId ?? "";
        var root = new Node3D { Name = "Visual" };
        Vector3? collider = id switch
        {
            "fitas_magneticas" => Fitas(root),
            "monitor_crt" => Crt(root),
            "suprimentos" => Medical(root),
            "pecas_radio" => Crate(root, "PEÇAS DE RÁDIO", false),
            "valvulas" => Crate(root, "VÁLVULAS · FRÁGIL", true),
            "documentos" => Briefcase(root),
            "bateria" => Battery(root),
            "gerador_portatil" => Generator(root),
            "botijao" => GasCylinder(root),
            "bolo" => Cake(root),
            "aquario" => Aquarium(root),
            "parabolica" => Dish(root),
            "tomates" => Kay(root, "crate_tomatoes.gltf", 0.27f, new Vector3(0.54f, 0.26f, 0.54f)),
            "racao" => FeedSack(root),
            "feijoada" => Kay(root, "pot_A_stew.gltf", 0.36f, new Vector3(0.5f, 0.2f, 0.36f)),
            _ => null,
        };
        if (collider == null)
        {
            root.Free();
            return;
        }
        item.AddChild(root);
        if (item.GetNodeOrNull<MeshInstance3D>("MeshInstance3D") is { } oldMesh)
            oldMesh.Visible = false;
        if (item.GetNodeOrNull<CollisionShape3D>("CollisionShape3D") is { } shape)
            shape.Shape = new BoxShape3D { Size = collider.Value }; // forma nova: a da cena é compartilhada
    }

    private static MeshInstance3D Box(Node3D p, Vector3 pos, Vector3 size, Material m)
    {
        var mi = new MeshInstance3D { Mesh = new BoxMesh { Size = size }, Position = pos, MaterialOverride = m };
        p.AddChild(mi);
        return mi;
    }

    private static MeshInstance3D Cyl(Node3D p, Vector3 pos, float r, float h, Material m, int seg = 14)
    {
        var mi = new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = r, BottomRadius = r, Height = h, RadialSegments = seg }, Position = pos, MaterialOverride = m };
        p.AddChild(mi);
        return mi;
    }

    private static void Text(Node3D p, string text, Vector3 pos, float rotY, Color color, float pixel = 0.0016f)
    {
        p.AddChild(new Label3D
        {
            Text = text, FontSize = 48, PixelSize = pixel, Modulate = color, OutlineSize = 0,
            Position = pos, Rotation = new Vector3(0f, rotY, 0f),
        });
    }

    // ---------------- Modelos ----------------

    /// <summary>Saco de ráfia de ração do Agro-Tudão (v0.9.9): branco, faixa verde, boca costurada.</summary>
    private static Vector3 FeedSack(Node3D r)
    {
        var raffia = M(new Color(0.88f, 0.86f, 0.78f), 0.95f);
        var green = M(new Color(0.12f, 0.48f, 0.18f), 0.8f);
        Box(r, new Vector3(0f, -0.02f, 0f), new Vector3(0.48f, 0.26f, 0.72f), raffia);
        Box(r, new Vector3(0f, 0.12f, 0f), new Vector3(0.44f, 0.04f, 0.66f), raffia); // barriga cheia
        Box(r, new Vector3(0f, -0.02f, 0.37f), new Vector3(0.4f, 0.2f, 0.04f), raffia); // boca costurada
        Box(r, new Vector3(0f, 0.145f, -0.08f), new Vector3(0.445f, 0.006f, 0.24f), green);
        Text(r, "AGRO-TUDÃO\nRAÇÃO 25 kg", new Vector3(0f, 0.15f, -0.08f), 0f, Colors.White, 0.0014f);
        r.GetChild<Label3D>(r.GetChildCount() - 1).Rotation = new Vector3(-Mathf.Pi * 0.5f, 0f, 0f);
        return new Vector3(0.48f, 0.3f, 0.74f);
    }

    private static Vector3 Fitas(Node3D r)
    {
        Box(r, Vector3.Zero, new Vector3(0.5f, 0.5f, 0.5f), Cardboard);
        Box(r, new Vector3(0f, 0.251f, 0f), new Vector3(0.1f, 0.004f, 0.5f), Tape);
        Box(r, new Vector3(0f, 0f, 0.251f), new Vector3(0.1f, 0.5f, 0.004f), Tape);
        Text(r, "FITAS MAGNÉTICAS\nVHS · K7", new Vector3(0f, 0.05f, 0.255f), 0f, new Color(0.15f, 0.1f, 0.05f));
        Text(r, "FITAS MAGNÉTICAS\nVHS · K7", new Vector3(0f, 0.05f, -0.255f), Mathf.Pi, new Color(0.15f, 0.1f, 0.05f));
        // Fitas aparecendo por uma aba aberta.
        Box(r, new Vector3(0.12f, 0.27f, 0.08f), new Vector3(0.19f, 0.04f, 0.11f), Dark);
        return new Vector3(0.5f, 0.5f, 0.5f);
    }

    private static Vector3 Crt(Node3D r)
    {
        Box(r, new Vector3(0f, 0.02f, 0.06f), new Vector3(0.5f, 0.42f, 0.36f), Beige);
        Box(r, new Vector3(0f, 0.04f, -0.2f), new Vector3(0.36f, 0.3f, 0.2f), Beige);
        Box(r, new Vector3(0f, 0.04f, 0.245f), new Vector3(0.42f, 0.33f, 0.01f), Dark);
        Box(r, new Vector3(0f, 0.04f, 0.251f), new Vector3(0.37f, 0.28f, 0.004f), M(new Color(0.08f, 0.12f, 0.1f), 0.1f, 0.3f));
        Box(r, new Vector3(0.18f, -0.15f, 0.245f), new Vector3(0.05f, 0.02f, 0.01f), M(new Color(0.2f, 0.9f, 0.3f)));
        Box(r, new Vector3(0f, -0.21f, 0f), new Vector3(0.3f, 0.04f, 0.28f), Beige);
        return new Vector3(0.5f, 0.46f, 0.5f);
    }

    private static Vector3 Medical(Node3D r)
    {
        Box(r, Vector3.Zero, new Vector3(0.5f, 0.4f, 0.5f), White);
        foreach ((Vector3 n, float rot) in new[] { (Vector3.Back, 0f), (Vector3.Forward, Mathf.Pi) })
        {
            Box(r, n * 0.252f, new Vector3(0.22f, 0.07f, 0.004f), Red);
            Box(r, n * 0.252f, new Vector3(0.07f, 0.22f, 0.004f), Red);
            Text(r, "SUPRIMENTOS MÉDICOS", n * 0.254f + new Vector3(0f, -0.15f, 0f), rot, new Color(0.7f, 0.05f, 0.05f), 0.0012f);
        }
        Box(r, new Vector3(0f, 0.201f, 0f), new Vector3(0.22f, 0.004f, 0.07f), Red);
        Box(r, new Vector3(0f, 0.201f, 0f), new Vector3(0.07f, 0.004f, 0.22f), Red);
        return new Vector3(0.5f, 0.4f, 0.5f);
    }

    private static Vector3 Crate(Node3D r, string label, bool tubes)
    {
        Box(r, Vector3.Zero, new Vector3(0.46f, 0.46f, 0.46f), WoodDark);
        // Ripas em volta.
        for (int i = -1; i <= 1; i++)
        {
            float y = i * 0.17f;
            Box(r, new Vector3(0f, y, 0.235f), new Vector3(0.5f, 0.1f, 0.03f), Wood);
            Box(r, new Vector3(0f, y, -0.235f), new Vector3(0.5f, 0.1f, 0.03f), Wood);
            Box(r, new Vector3(0.235f, y, 0f), new Vector3(0.03f, 0.1f, 0.5f), Wood);
            Box(r, new Vector3(-0.235f, y, 0f), new Vector3(0.03f, 0.1f, 0.5f), Wood);
        }
        Text(r, label, new Vector3(0f, 0f, 0.252f), 0f, tubes ? new Color(0.75f, 0.05f, 0.05f) : new Color(0.1f, 0.05f, 0.02f), 0.0014f);
        if (tubes)
        {
            var glow = new StandardMaterial3D { AlbedoColor = new Color(1f, 0.6f, 0.25f, 0.7f), Transparency = BaseMaterial3D.TransparencyEnum.Alpha, EmissionEnabled = true, Emission = new Color(1f, 0.45f, 0.1f), EmissionEnergyMultiplier = 0.8f };
            foreach (float x in new[] { -0.12f, 0f, 0.12f })
                Cyl(r, new Vector3(x, 0.27f, 0f), 0.035f, 0.12f, glow, 8);
        }
        else
        {
            Box(r, new Vector3(0.1f, 0.25f, 0.05f), new Vector3(0.18f, 0.06f, 0.1f), Dark); // rádio no topo
        }
        return new Vector3(0.5f, 0.48f, 0.5f);
    }

    private static Vector3 Briefcase(Node3D r)
    {
        StandardMaterial3D alu = M(new Color(0.7f, 0.72f, 0.74f), 0.25f, 0.85f);
        Box(r, Vector3.Zero, new Vector3(0.48f, 0.34f, 0.13f), alu);
        Box(r, new Vector3(0f, 0f, 0f), new Vector3(0.49f, 0.02f, 0.14f), Dark); // junta
        Box(r, new Vector3(0f, 0.2f, 0f), new Vector3(0.14f, 0.03f, 0.03f), Dark); // alça
        foreach (float x in new[] { -0.15f, 0.15f })
            Box(r, new Vector3(x, 0.13f, 0.066f), new Vector3(0.04f, 0.03f, 0.01f), Yellow); // fechos
        Text(r, "CONFIDENCIAL", new Vector3(0f, -0.06f, 0.067f), 0f, new Color(0.7f, 0.05f, 0.05f), 0.0012f);
        return new Vector3(0.48f, 0.4f, 0.14f);
    }

    private static Vector3 Battery(Node3D r)
    {
        Box(r, Vector3.Zero, new Vector3(0.34f, 0.22f, 0.2f), Dark);
        Box(r, new Vector3(0f, 0.115f, 0f), new Vector3(0.34f, 0.01f, 0.2f), M(new Color(0.15f, 0.15f, 0.16f)));
        Cyl(r, new Vector3(-0.12f, 0.135f, 0f), 0.022f, 0.04f, Red, 8);
        Cyl(r, new Vector3(0.12f, 0.135f, 0f), 0.022f, 0.04f, Metal, 8);
        Box(r, new Vector3(0f, 0.02f, 0.101f), new Vector3(0.24f, 0.09f, 0.004f), Yellow);
        Text(r, "12V · 60Ah", new Vector3(0f, 0.02f, 0.104f), 0f, new Color(0.05f, 0.05f, 0.05f), 0.0011f);
        return new Vector3(0.34f, 0.28f, 0.2f);
    }

    private static Vector3 Generator(Node3D r)
    {
        foreach (float x in new[] { -0.28f, 0.28f })
            foreach (float z in new[] { -0.19f, 0.19f })
                Box(r, new Vector3(x, 0f, z), new Vector3(0.03f, 0.44f, 0.03f), Dark);
        foreach (float y in new[] { -0.21f, 0.21f })
        {
            Box(r, new Vector3(0f, y, 0.19f), new Vector3(0.59f, 0.03f, 0.03f), Dark);
            Box(r, new Vector3(0f, y, -0.19f), new Vector3(0.59f, 0.03f, 0.03f), Dark);
        }
        Box(r, new Vector3(0f, 0.12f, 0f), new Vector3(0.44f, 0.16f, 0.3f), Yellow); // tanque
        Box(r, new Vector3(-0.08f, -0.08f, 0f), new Vector3(0.28f, 0.22f, 0.26f), M(new Color(0.2f, 0.2f, 0.22f))); // motor
        Box(r, new Vector3(0.18f, -0.08f, 0.1f), new Vector3(0.14f, 0.16f, 0.06f), M(new Color(0.85f, 0.85f, 0.85f))); // tomadas
        Cyl(r, new Vector3(0.1f, 0.22f, 0.05f), 0.04f, 0.03f, Dark, 8);
        return new Vector3(0.6f, 0.46f, 0.42f);
    }

    private static Vector3 GasCylinder(Node3D r)
    {
        // Botijão P13 "Gás do Zé" (v0.9.6): corpo torneado com a costura no meio, pé com recortes,
        // colarinho de proteção em volta da válvula e o regulador.
        StandardMaterial3D paint = M(new Color(0.92f, 0.42f, 0.08f), 0.45f);
        // Perfil (raio, altura) de baixo para cima: pé, ombro arredondado, corpo, cúpula.
        Vector2[] body =
        {
            new(0.0f, -0.24f), new(0.12f, -0.245f), new(0.155f, -0.225f), new(0.172f, -0.19f), new(0.178f, -0.14f),
            new(0.178f, 0.06f), new(0.172f, 0.12f), new(0.155f, 0.17f), new(0.12f, 0.205f), new(0.07f, 0.222f), new(0.0f, 0.226f),
        };
        r.AddChild(new MeshInstance3D { Mesh = Lathe(body, 20), MaterialOverride = paint });
        // Costura soldada no meio.
        r.AddChild(new MeshInstance3D { Mesh = new TorusMesh { InnerRadius = 0.176f, OuterRadius = 0.184f, Rings = 20, RingSegments = 4 }, Position = new Vector3(0f, -0.04f, 0f), MaterialOverride = paint });
        // Pé (anel) com quatro recortes escuros.
        Cyl(r, new Vector3(0f, -0.275f, 0f), 0.15f, 0.07f, paint, 18);
        for (int i = 0; i < 4; i++)
        {
            float a = i * Mathf.Pi * 0.5f;
            Box(r, new Vector3(Mathf.Cos(a) * 0.151f, -0.275f, Mathf.Sin(a) * 0.151f), new Vector3(0.05f, 0.035f, 0.05f), Dark);
        }
        // Colarinho (alça) em volta da válvula: anel alto com duas "janelas" para a mão.
        Vector2[] collar = { new(0.105f, 0.2f), new(0.11f, 0.2f), new(0.11f, 0.33f), new(0.1f, 0.34f), new(0.1f, 0.2f) };
        r.AddChild(new MeshInstance3D { Mesh = Lathe(collar, 20), MaterialOverride = paint });
        foreach (float side in new[] { -1f, 1f })
            Box(r, new Vector3(side * 0.106f, 0.29f, 0f), new Vector3(0.02f, 0.05f, 0.09f), Dark);
        // Válvula e regulador.
        Cyl(r, new Vector3(0f, 0.25f, 0f), 0.025f, 0.07f, Metal, 10);
        Cyl(r, new Vector3(0f, 0.3f, 0f), 0.045f, 0.035f, M(new Color(0.85f, 0.85f, 0.82f), 0.4f), 12);
        Box(r, new Vector3(0.06f, 0.3f, 0f), new Vector3(0.07f, 0.02f, 0.02f), Metal);
        Text(r, "GÁS DO ZÉ", new Vector3(0f, 0.02f, 0.181f), 0f, Colors.White, 0.0013f);
        Text(r, "P13 · 13 kg", new Vector3(0f, -0.1f, 0.181f), 0f, Colors.White, 0.0009f);
        return new Vector3(0.36f, 0.64f, 0.36f);
    }

    /// <summary>Malha de revolução em volta do eixo Y a partir de um perfil (raio, altura).</summary>
    private static ArrayMesh Lathe(Vector2[] profile, int segments)
    {
        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);
        for (int i = 0; i < profile.Length - 1; i++)
        {
            Vector2 p0 = profile[i], p1 = profile[i + 1];
            for (int k = 0; k < segments; k++)
            {
                float a0 = Mathf.Tau * k / segments, a1 = Mathf.Tau * (k + 1) / segments;
                Vector3 v00 = new(Mathf.Cos(a0) * p0.X, p0.Y, Mathf.Sin(a0) * p0.X), v01 = new(Mathf.Cos(a1) * p0.X, p0.Y, Mathf.Sin(a1) * p0.X);
                Vector3 v10 = new(Mathf.Cos(a0) * p1.X, p1.Y, Mathf.Sin(a0) * p1.X), v11 = new(Mathf.Cos(a1) * p1.X, p1.Y, Mathf.Sin(a1) * p1.X);
                st.AddVertex(v00); st.AddVertex(v11); st.AddVertex(v10);
                st.AddVertex(v00); st.AddVertex(v01); st.AddVertex(v11);
            }
        }
        st.GenerateNormals();
        return st.Commit();
    }

    private static Vector3 Cake(Node3D r)
    {
        StandardMaterial3D icing = M(new Color(0.98f, 0.95f, 0.9f), 0.6f);
        StandardMaterial3D pink = M(new Color(0.95f, 0.55f, 0.7f), 0.6f);
        Box(r, new Vector3(0f, -0.23f, 0f), new Vector3(0.44f, 0.02f, 0.44f), M(new Color(0.85f, 0.75f, 0.3f), 0.3f, 0.6f)); // bandeja
        float y = -0.22f;
        foreach ((float rad, float h) in new[] { (0.2f, 0.16f), (0.15f, 0.13f), (0.1f, 0.11f) })
        {
            Cyl(r, new Vector3(0f, y + h * 0.5f, 0f), rad, h, icing, 18);
            Cyl(r, new Vector3(0f, y + h - 0.01f, 0f), rad + 0.008f, 0.02f, pink, 18);
            y += h;
        }
        Box(r, new Vector3(-0.025f, y + 0.05f, 0f), new Vector3(0.02f, 0.1f, 0.02f), Dark); // noivinhos
        Box(r, new Vector3(0.025f, y + 0.05f, 0f), new Vector3(0.02f, 0.1f, 0.02f), White);
        return new Vector3(0.44f, 0.52f, 0.44f);
    }

    private static Vector3 Aquarium(Node3D r)
    {
        var glass = new StandardMaterial3D { AlbedoColor = new Color(0.8f, 0.95f, 1f, 0.22f), Transparency = BaseMaterial3D.TransparencyEnum.Alpha, Roughness = 0.05f };
        var water = new StandardMaterial3D { AlbedoColor = new Color(0.2f, 0.55f, 0.75f, 0.5f), Transparency = BaseMaterial3D.TransparencyEnum.Alpha, Roughness = 0.05f };
        Box(r, Vector3.Zero, new Vector3(0.5f, 0.34f, 0.3f), glass);
        Box(r, new Vector3(0f, -0.03f, 0f), new Vector3(0.48f, 0.26f, 0.28f), water);
        Box(r, new Vector3(0f, -0.155f, 0f), new Vector3(0.48f, 0.03f, 0.28f), M(new Color(0.75f, 0.68f, 0.5f))); // cascalho
        Box(r, new Vector3(0f, 0.175f, 0f), new Vector3(0.52f, 0.02f, 0.32f), Dark); // tampa
        StandardMaterial3D fish = M(new Color(1f, 0.5f, 0.1f), 0.4f);
        Box(r, new Vector3(-0.08f, 0f, 0.02f), new Vector3(0.07f, 0.04f, 0.02f), fish);
        Box(r, new Vector3(0.1f, -0.05f, -0.04f), new Vector3(0.06f, 0.035f, 0.02f), fish);
        Cyl(r, new Vector3(0.18f, -0.08f, 0.05f), 0.012f, 0.12f, M(new Color(0.2f, 0.6f, 0.2f)), 6); // plantinha
        return new Vector3(0.52f, 0.36f, 0.32f);
    }

    private static Vector3 Dish(Node3D r)
    {
        StandardMaterial3D dishMat = M(new Color(0.85f, 0.85f, 0.82f), 0.4f, 0.3f);
        dishMat.CullMode = BaseMaterial3D.CullModeEnum.Disabled;
        // Prato (calota esférica achatada) virado para +Z.
        r.AddChild(new MeshInstance3D
        {
            Mesh = new SphereMesh { Radius = 0.3f, Height = 0.3f, IsHemisphere = true, RadialSegments = 18, Rings = 4 },
            Position = new Vector3(0f, 0.05f, -0.02f),
            Rotation = new Vector3(-Mathf.Pi * 0.5f, 0f, 0f),
            Scale = new Vector3(1f, 0.35f, 1f),
            MaterialOverride = dishMat,
        });
        Box(r, new Vector3(0f, -0.08f, 0.12f), new Vector3(0.02f, 0.02f, 0.3f), Metal).Rotation = new Vector3(0.45f, 0f, 0f); // braço
        Box(r, new Vector3(0f, -0.02f, 0.25f), new Vector3(0.05f, 0.05f, 0.07f), Dark); // LNB
        Box(r, new Vector3(0f, -0.2f, -0.06f), new Vector3(0.05f, 0.2f, 0.05f), Metal); // suporte
        return new Vector3(0.6f, 0.6f, 0.26f);
    }

    private static Vector3 Kay(Node3D r, string file, float scale, Vector3 collider)
    {
        Node3D model = AssetProps.Spawn(r, "res://Assets/KayKit/Restaurant/" + file, new Vector3(0f, -collider.Y * 0.5f, 0f), 0f, scale);
        if (model == null)
            Box(r, Vector3.Zero, collider, Cardboard);
        return collider;
    }
}
