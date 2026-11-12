// Scripts/World/CarModels.cs
// Modelos de carro low-poly montados com caixas e cilindros (estilo PS1).
// Inspirados nos carros brasileiros dos anos 80/90 (com nomes e logos paródia):
// Opalinha (sedã, Chevrolé), Gou quadrado (hatch, Folksvagem), F-Mil (picape, Fórdi), Kombe (Folksvagem)
// e Fuska (Folksvagem). Frente do carro = -Z, origem no chão.
// v0.9.3: a "estufa" (vidros) é um prisma fechado com colunas pintadas, sem frestas entre os vidros.
using System.Collections.Generic;
using Godot;

namespace DeadFrequency;

public enum CarType
{
    Sedan,
    Hatch,
    Pickup,
    Kombi,
    Fusca,
    /// <summary>Van de viagem anos 80 do Modo História (não aparece estacionada na rua).</summary>
    Van80,
}

public static class CarModels
{
    private static BoxMesh _box;
    private static CylinderMesh _wheel;
    private static StandardMaterial3D _glass, _rubber, _chrome, _head, _tail, _plate, _interior, _grille, _blinker;
    private static readonly Dictionary<Color, StandardMaterial3D> Paint = new();

    private static void EnsureResources()
    {
        if (_box != null)
            return;
        _box = new BoxMesh { Size = Vector3.One };
        _wheel = new CylinderMesh { TopRadius = 0.5f, BottomRadius = 0.5f, Height = 1f, RadialSegments = 10, Rings = 1 };
        _glass = new StandardMaterial3D { AlbedoColor = new Color(0.08f, 0.11f, 0.13f), Roughness = 0.1f, Metallic = 0.2f };
        _rubber = new StandardMaterial3D { AlbedoColor = new Color(0.05f, 0.05f, 0.05f), Roughness = 1f };
        _chrome = new StandardMaterial3D { AlbedoColor = new Color(0.6f, 0.6f, 0.62f), Roughness = 0.3f, Metallic = 0.8f };
        _interior = new StandardMaterial3D { AlbedoColor = new Color(0.12f, 0.1f, 0.09f) };
        _head = Emissive(new Color(1f, 0.95f, 0.8f), 1.2f);
        _tail = Emissive(new Color(0.8f, 0.05f, 0.04f), 0.8f);
        _plate = new StandardMaterial3D { AlbedoColor = new Color(0.85f, 0.85f, 0.8f) };
        _grille = new StandardMaterial3D { AlbedoColor = new Color(0.04f, 0.04f, 0.045f), Roughness = 0.9f };
        _blinker = Emissive(new Color(1f, 0.55f, 0.1f), 0.5f);
    }

    private static StandardMaterial3D Emissive(Color c, float energy) =>
        new() { AlbedoColor = c, EmissionEnabled = true, Emission = c, EmissionEnergyMultiplier = energy };

    public static StandardMaterial3D PaintFor(Color color)
    {
        if (!Paint.TryGetValue(color, out StandardMaterial3D mat))
        {
            mat = new StandardMaterial3D { AlbedoColor = color, Roughness = 0.35f, Metallic = 0.35f };
            Paint[color] = mat;
        }
        return mat;
    }

    public static MeshInstance3D Box(Node3D parent, Vector3 pos, Vector3 size, Material mat, float tiltX = 0f)
    {
        EnsureResources();
        var mesh = new MeshInstance3D { Mesh = _box, Position = pos, Scale = size, MaterialOverride = mat, Rotation = new Vector3(tiltX, 0f, 0f) };
        parent.AddChild(mesh);
        return mesh;
    }

    private static void Wheel(Node3D parent, Vector3 pos, float radius, float width)
    {
        var tire = new MeshInstance3D
        {
            Mesh = _wheel,
            Position = pos,
            Scale = new Vector3(radius * 2f, width, radius * 2f),
            Rotation = new Vector3(0f, 0f, Mathf.Pi * 0.5f),
            MaterialOverride = _rubber,
        };
        parent.AddChild(tire);
        var hub = new MeshInstance3D
        {
            Mesh = _wheel,
            Position = pos + new Vector3(Mathf.Sign(pos.X) * width * 0.52f, 0f, 0f),
            Scale = new Vector3(radius * 1.1f, 0.02f, radius * 1.1f),
            Rotation = new Vector3(0f, 0f, Mathf.Pi * 0.5f),
            MaterialOverride = _chrome,
        };
        parent.AddChild(hub);
    }

    /// <summary>Faróis, lanternas, para-choques, grade e placa, comuns a todos.
    /// Cada peça fica numa "camada" própria à frente da carroceria (nenhuma face coincide com outra),
    /// senão as duas superfícies brigam pelo mesmo pixel e a grade pisca (z-fighting).</summary>
    private static void Details(Node3D car, float width, float length, float lightY, float bumperY)
    {
        float hz = length * 0.5f;
        // Para-choques: saem 9 cm da carroceria.
        Box(car, new Vector3(0f, bumperY, -hz - 0.03f), new Vector3(width + 0.06f, 0.18f, 0.12f), _rubber);
        Box(car, new Vector3(0f, bumperY, hz + 0.03f), new Vector3(width + 0.06f, 0.18f, 0.12f), _rubber);

        // Grade: fundo escuro rente à frente + 3 frisos cromados mais à frente.
        float grilleW = width * 0.36f;
        Box(car, new Vector3(0f, lightY, -hz - 0.015f), new Vector3(grilleW, 0.16f, 0.03f), _grille);
        for (int i = -1; i <= 1; i++)
            Box(car, new Vector3(0f, lightY + i * 0.05f, -hz - 0.04f), new Vector3(grilleW + 0.02f, 0.018f, 0.02f), _chrome);

        foreach (float side in new[] { -1f, 1f })
        {
            float x = side * width * 0.34f;
            // Farol: moldura cromada + lente acesa um pouco à frente.
            Box(car, new Vector3(x, lightY, -hz - 0.012f), new Vector3(0.4f, 0.17f, 0.024f), _chrome);
            Box(car, new Vector3(x, lightY, -hz - 0.035f), new Vector3(0.34f, 0.12f, 0.02f), _head);
            // Pisca laranja ao lado do farol.
            Box(car, new Vector3(side * (width * 0.5f - 0.07f), lightY, -hz - 0.02f), new Vector3(0.1f, 0.1f, 0.03f), _blinker);
            // Lanterna traseira.
            Box(car, new Vector3(x, lightY, hz + 0.02f), new Vector3(0.34f, 0.14f, 0.04f), _tail);
            // Retrovisor.
            Box(car, new Vector3(side * (width * 0.5f + 0.08f), lightY + 0.4f, -hz * 0.36f), new Vector3(0.1f, 0.09f, 0.14f), _rubber);
        }
        // Placas (frente no para-choque, trás acima dele).
        Box(car, new Vector3(0f, bumperY, -hz - 0.1f), new Vector3(0.46f, 0.12f, 0.02f), _plate);
        Box(car, new Vector3(0f, bumperY + 0.19f, hz + 0.02f), new Vector3(0.46f, 0.12f, 0.03f), _plate);
        // Maçanetas.
        foreach (float side in new[] { -1f, 1f })
            Box(car, new Vector3(side * (width * 0.5f + 0.01f), lightY + 0.12f, -0.1f), new Vector3(0.03f, 0.04f, 0.16f), _chrome);
    }

    private static readonly Dictionary<string, StandardMaterial3D> Logos = new();

    private static StandardMaterial3D LogoMaterial(string brand)
    {
        if (Logos.TryGetValue(brand, out StandardMaterial3D m))
            return m;
        string path = $"res://Assets/Textures/Vehicles/car_logo_{brand}.png";
        m = ResourceLoader.Exists(path)
            ? new StandardMaterial3D { AlbedoTexture = GD.Load<Texture2D>(path), Transparency = BaseMaterial3D.TransparencyEnum.AlphaScissor, AlphaScissorThreshold = 0.5f, Roughness = 0.3f, Metallic = 0.5f }
            : null;
        Logos[brand] = m;
        return m;
    }

    /// <summary>Logo da marca na frente (grade) e atrás, mais o nome do modelo na tampa traseira,
    /// ao lado do logo e acima das lanternas (v0.9.7: antes o nome caía em cima da lanterna).</summary>
    private static void Badges(Node3D car, string brand, string model, float length, float frontY, float rearY, float size = 0.16f)
    {
        float hz = length * 0.5f;
        BadgeAt(car, brand, new Vector3(0f, frontY, -hz - 0.055f), Vector3.Forward, size);
        BadgeAt(car, brand, new Vector3(0f, rearY, hz + 0.045f), Vector3.Back, size * 0.8f);
        LabelAt(car, model, new Vector3(size * 0.5f + 0.05f, rearY, hz + 0.046f), Vector3.Back);
    }

    /// <summary>Logo colado numa superfície (normal = para onde a face aponta).</summary>
    private static void BadgeAt(Node3D car, string brand, Vector3 pos, Vector3 normal, float size)
    {
        StandardMaterial3D logo = LogoMaterial(brand);
        if (logo == null)
            return;
        car.AddChild(new MeshInstance3D { Mesh = new QuadMesh { Size = new Vector2(size, size) }, MaterialOverride = logo, Transform = new Transform3D(Facing(normal), pos) });
    }

    /// <summary>Nome do modelo; pos = canto esquerdo (visto de trás), centro na altura.</summary>
    private static void LabelAt(Node3D car, string text, Vector3 pos, Vector3 normal)
    {
        car.AddChild(new Label3D
        {
            Text = text,
            FontSize = 24,
            PixelSize = 0.0022f,
            HorizontalAlignment = HorizontalAlignment.Left,
            Modulate = new Color(0.85f, 0.85f, 0.88f),
            OutlineSize = 3,
            OutlineModulate = new Color(0.1f, 0.1f, 0.1f),
            Transform = new Transform3D(Facing(normal), pos),
        });
    }

    /// <summary>Base com +Z apontando para a normal (QuadMesh e Label3D olham para +Z).</summary>
    private static Basis Facing(Vector3 normal)
    {
        Vector3 z = normal.Normalized();
        Vector3 x = Vector3.Up.Cross(z).Normalized();
        return new Basis(x, z.Cross(x), z);
    }

    /// <summary>Caixa orientada entre dois pontos (colunas inclinadas).</summary>
    private static void Strut(Node3D parent, Vector3 a, Vector3 b, float tx, float tz, Material mat)
    {
        Vector3 up = b - a;
        float len = up.Length();
        Vector3 y = up / len;
        Vector3 x = Vector3.Right;
        Vector3 z = x.Cross(y).Normalized();
        parent.AddChild(new MeshInstance3D
        {
            Mesh = _box,
            MaterialOverride = mat,
            Transform = new Transform3D(new Basis(x * tx, y * len, z * tz), (a + b) * 0.5f),
        });
    }

    /// <summary>
    /// Estufa fechada: prisma de vidro com perfil trapézio (vista de lado) entre a linha de cintura e o teto,
    /// teto pintado e colunas A, B e C pintadas — sem buracos entre para-brisa, laterais e vidro traseiro.
    /// zFB/zFT = base e topo do para-brisa; zBT/zBB = topo e base do vidro traseiro (frente = -Z).
    /// </summary>
    private static void Greenhouse(Node3D car, float width, float yBelt, float yRoof, float zFB, float zFT, float zBT, float zBB, Material paint, bool bPillar = true)
    {
        float hw = width * 0.5f;
        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);
        Vector3 fbl = new(-hw, yBelt, zFB), fbr = new(hw, yBelt, zFB), ftl = new(-hw + 0.06f, yRoof, zFT), ftr = new(hw - 0.06f, yRoof, zFT);
        Vector3 btl = new(-hw + 0.06f, yRoof, zBT), btr = new(hw - 0.06f, yRoof, zBT), bbl = new(-hw, yBelt, zBB), bbr = new(hw, yBelt, zBB);
        void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            Vector3 n = (b - a).Cross(c - a).Normalized();
            foreach (Vector3 v in new[] { a, b, c, a, c, d })
            {
                st.SetNormal(n);
                st.AddVertex(v);
            }
        }
        Quad(fbl, ftl, ftr, fbr); // para-brisa
        Quad(bbr, btr, btl, bbl); // vidro traseiro
        Quad(fbl, bbl, btl, ftl); // lateral esquerda
        Quad(fbr, ftr, btr, bbr); // lateral direita
        car.AddChild(new MeshInstance3D { Mesh = st.Commit(), MaterialOverride = _glassDouble });
        // Teto e colunas.
        float roofLen = zBT - zFT;
        Box(car, new Vector3(0f, yRoof + 0.035f, (zFT + zBT) * 0.5f), new Vector3(width - 0.08f, 0.07f, roofLen + 0.08f), paint);
        foreach (float side in new[] { -1f, 1f })
        {
            float xb = side * (hw + 0.005f), xt = side * (hw - 0.055f);
            Strut(car, new Vector3(xb, yBelt, zFB), new Vector3(xt, yRoof, zFT), 0.07f, 0.08f, paint);  // coluna A
            Strut(car, new Vector3(xb, yBelt, zBB), new Vector3(xt, yRoof, zBT), 0.07f, 0.14f, paint);  // coluna C
            if (bPillar)
            {
                float zb = Mathf.Lerp(zFB, zBB, 0.48f);
                Strut(car, new Vector3(xb, yBelt, zb), new Vector3(xt, yRoof, Mathf.Lerp(zFT, zBT, 0.5f)), 0.06f, 0.1f, paint); // coluna B
            }
            // Friso da janela (linha de cintura).
            Box(car, new Vector3(side * (hw + 0.006f), yBelt + 0.02f, (zFB + zBB) * 0.5f), new Vector3(0.02f, 0.04f, zBB - zFB), _chrome);
        }
        // Moldura de borracha embaixo do para-brisa e do vidro traseiro.
        Box(car, new Vector3(0f, yBelt + 0.015f, zFB), new Vector3(width, 0.04f, 0.05f), _rubber);
        Box(car, new Vector3(0f, yBelt + 0.015f, zBB), new Vector3(width, 0.04f, 0.05f), _rubber);
    }

    private static StandardMaterial3D _glassDouble;
    private static SphereMesh _fender;

    /// <summary>Interpolação suave (Hermite) por pontos de controle.</summary>
    private static float Smooth(float[] xs, float[] ys, float x)
    {
        if (x <= xs[0])
            return ys[0];
        for (int i = 1; i < xs.Length; i++)
        {
            if (x <= xs[i])
            {
                float t = (x - xs[i - 1]) / (xs[i] - xs[i - 1]);
                t = t * t * (3f - 2f * t);
                return Mathf.Lerp(ys[i - 1], ys[i], t);
            }
        }
        return ys[^1];
    }

    /// <summary>
    /// Malha "torneada" ao longo de Z: em cada z a seção é uma superelipse (meia-largura, centro e meia-altura).
    /// th0..th1 escolhe o pedaço da volta (0 = lado direito, π/2 = topo); scale afasta a casca (vidros por fora).
    /// </summary>
    private static void Loft(Node3D parent, float z0, float z1, System.Func<float, (float hw, float yc, float hh)> section, float n,
        float th0, float th1, float scale, Material material, int segZ = 18, int segT = 28, bool caps = false)
    {
        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);
        int tCount = Mathf.Max(3, Mathf.CeilToInt(segT * (th1 - th0) / Mathf.Tau) + 1);
        Vector3 P(float z, float th)
        {
            (float hw, float yc, float hh) = section(z);
            float c = Mathf.Cos(th), sn = Mathf.Sin(th);
            float x = hw * scale * Mathf.Sign(c) * Mathf.Pow(Mathf.Abs(c), 2f / n);
            float y = yc + hh * scale * Mathf.Sign(sn) * Mathf.Pow(Mathf.Abs(sn), 2f / n);
            return new Vector3(x, y, z);
        }
        for (int i = 0; i < segZ; i++)
        {
            float za = Mathf.Lerp(z0, z1, (float)i / segZ), zb = Mathf.Lerp(z0, z1, (float)(i + 1) / segZ);
            for (int k = 0; k < tCount; k++)
            {
                float ta = Mathf.Lerp(th0, th1, (float)k / tCount), tb = Mathf.Lerp(th0, th1, (float)(k + 1) / tCount);
                Vector3 a = P(za, ta), b = P(zb, ta), c = P(zb, tb), d = P(za, tb);
                Vector3 nrm = (b - a).Cross(d - a).Normalized();
                foreach (Vector3 v in new[] { a, b, c, a, c, d })
                {
                    st.SetNormal(-nrm);
                    st.AddVertex(v);
                }
            }
        }
        if (caps)
        {
            foreach (float z in new[] { z0, z1 })
            {
                (float _, float yc, float _) = section(z);
                var center = new Vector3(0f, yc, z);
                for (int k = 0; k < tCount; k++)
                {
                    float ta = Mathf.Lerp(th0, th1, (float)k / tCount), tb = Mathf.Lerp(th0, th1, (float)(k + 1) / tCount);
                    Vector3 nz = new(0f, 0f, z < 0 ? -1f : 1f);
                    foreach (Vector3 v in new[] { center, P(z, ta), P(z, tb) })
                    {
                        st.SetNormal(nz);
                        st.AddVertex(v);
                    }
                }
            }
        }
        st.Index();
        Mesh mesh = st.Commit();
        Material mat = material;
        if (material is StandardMaterial3D std && std.CullMode != BaseMaterial3D.CullModeEnum.Disabled)
        {
            // A orientação dos triângulos varia com o pedaço da volta: desenha dos dois lados.
            mat = _loftMats.TryGetValue(std, out StandardMaterial3D dbl) ? dbl : (_loftMats[std] = (StandardMaterial3D)std.Duplicate());
            ((StandardMaterial3D)mat).CullMode = BaseMaterial3D.CullModeEnum.Disabled;
        }
        parent.AddChild(new MeshInstance3D { Mesh = mesh, MaterialOverride = mat });
    }

    private static readonly System.Collections.Generic.Dictionary<StandardMaterial3D, StandardMaterial3D> _loftMats = new();

    /// <summary>Monta o carro como filho de parent. Retorna o tamanho (largura, altura, comprimento) para colisão.</summary>
    public static Vector3 Build(Node3D parent, CarType type, Color color)
    {
        EnsureResources();
        _glassDouble ??= new StandardMaterial3D { AlbedoColor = new Color(0.08f, 0.11f, 0.13f), Roughness = 0.1f, Metallic = 0.25f, CullMode = BaseMaterial3D.CullModeEnum.Disabled };
        StandardMaterial3D paint = PaintFor(color);
        switch (type)
        {
            case CarType.Hatch: // "Gou" quadrado (Folksvagem)
            {
                const float w = 1.62f, l = 3.8f;
                Box(parent, new Vector3(0f, 0.58f, 0f), new Vector3(w, 0.56f, l), paint);
                Box(parent, new Vector3(0f, 0.87f, -1.3f), new Vector3(w - 0.04f, 0.04f, 1.1f), paint); // capô reto
                Greenhouse(parent, w - 0.02f, 0.86f, 1.36f, -0.72f, -0.18f, 1.25f, 1.62f, paint);
                Box(parent, new Vector3(0f, 0.6f, 1.9f), new Vector3(w - 0.04f, 0.5f, 0.02f), paint);
                Box(parent, new Vector3(0f, 0.47f, 0f), new Vector3(w + 0.012f, 0.06f, l - 0.6f), _rubber); // friso lateral
                Details(parent, w, l, 0.68f, 0.4f);
                Badges(parent, "folksvagem", "GOU", l, 0.68f, 0.8f);
                foreach (float x in new[] { -0.77f, 0.77f })
                    foreach (float z in new[] { -1.2f, 1.25f })
                        Wheel(parent, new Vector3(x, 0.3f, z), 0.3f, 0.2f);
                return new Vector3(w, 1.4f, l);
            }
            case CarType.Pickup: // "F-Mil" (Fórdi)
            {
                const float w = 1.95f, l = 5.0f;
                Box(parent, new Vector3(0f, 0.68f, 0f), new Vector3(w, 0.62f, l), paint);
                Box(parent, new Vector3(0f, 1.0f, -1.75f), new Vector3(w - 0.04f, 0.04f, 1.4f), paint); // capô longo
                Greenhouse(parent, w - 0.06f, 0.99f, 1.62f, -1.02f, -0.62f, 0.15f, 0.25f, paint, bPillar: false);
                Box(parent, new Vector3(0f, 1.3f, 0.27f), new Vector3(w - 0.06f, 0.62f, 0.06f), paint); // parede da cabine
                // Caçamba com assoalho e laterais.
                Box(parent, new Vector3(0f, 1.0f, 1.45f), new Vector3(w - 0.2f, 0.05f, 2.2f), _interior);
                Box(parent, new Vector3(w * 0.5f - 0.05f, 1.17f, 1.4f), new Vector3(0.1f, 0.36f, 2.3f), paint);
                Box(parent, new Vector3(-w * 0.5f + 0.05f, 1.17f, 1.4f), new Vector3(0.1f, 0.36f, 2.3f), paint);
                Box(parent, new Vector3(0f, 1.17f, 2.46f), new Vector3(w, 0.36f, 0.08f), paint);
                // Para-lamas saltados.
                foreach (float x in new[] { -1f, 1f })
                    foreach (float z in new[] { -1.6f, 1.55f })
                        Box(parent, new Vector3(x * (w * 0.5f + 0.03f), 0.78f, z), new Vector3(0.08f, 0.12f, 0.95f), _rubber);
                Details(parent, w, l, 0.82f, 0.45f);
                Badges(parent, "fordi", "F-MIL", l, 0.82f, 1.17f, 0.22f);
                foreach (float x in new[] { -0.9f, 0.9f })
                    foreach (float z in new[] { -1.6f, 1.55f })
                        Wheel(parent, new Vector3(x, 0.38f, z), 0.38f, 0.26f);
                return new Vector3(w, 1.7f, l);
            }
            case CarType.Kombi: // "Kombe" (Folksvagem), saia-e-blusa
            {
                const float w = 1.75f, l = 4.3f;
                StandardMaterial3D cream = PaintFor(new Color(0.9f, 0.88f, 0.82f));
                Box(parent, new Vector3(0f, 0.72f, 0f), new Vector3(w, 0.86f, l), paint);
                Box(parent, new Vector3(0f, 1.98f, 0.02f), new Vector3(w - 0.06f, 0.1f, l - 0.12f), cream); // teto
                // Faixa de janelas com colunas (frente: para-brisa bipartido).
                Box(parent, new Vector3(0f, 1.55f, 0f), new Vector3(w - 0.02f, 0.78f, l - 0.04f), _glassDouble);
                Box(parent, new Vector3(0f, 1.17f, 0f), new Vector3(w + 0.004f, 0.06f, l + 0.004f), cream); // cintura
                foreach (float side in new[] { -1f, 1f })
                {
                    foreach (float z in new[] { -2.1f, -1.25f, -0.4f, 0.45f, 1.3f, 2.1f })
                        Box(parent, new Vector3(side * (w * 0.5f + 0.005f), 1.55f, z), new Vector3(0.03f, 0.78f, 0.1f), cream);
                }
                Box(parent, new Vector3(0f, 1.55f, -l * 0.5f - 0.005f), new Vector3(0.08f, 0.78f, 0.03f), cream); // coluna do meio do para-brisa
                Box(parent, new Vector3(0f, 1.55f, l * 0.5f + 0.005f), new Vector3(w, 0.3f, 0.03f), cream);
                // "V" na frente, sobre a lata creme.
                Box(parent, new Vector3(0f, 0.95f, -l * 0.5f - 0.004f), new Vector3(w - 0.1f, 0.5f, 0.01f), cream);
                Strut(parent, new Vector3(-0.55f, 1.15f, -l * 0.5f - 0.012f), new Vector3(0f, 0.75f, -l * 0.5f - 0.012f), 0.08f, 0.01f, paint);
                Strut(parent, new Vector3(0.55f, 1.15f, -l * 0.5f - 0.012f), new Vector3(0f, 0.75f, -l * 0.5f - 0.012f), 0.08f, 0.01f, paint);
                Details(parent, w, l, 0.62f, 0.36f);
                Badges(parent, "folksvagem", "KOMBE", l, 1.0f, 1.05f, 0.24f);
                foreach (float x in new[] { -0.8f, 0.8f })
                    foreach (float z in new[] { -1.4f, 1.4f })
                        Wheel(parent, new Vector3(x, 0.32f, z), 0.32f, 0.2f);
                return new Vector3(w, 2.05f, l);
            }
            case CarType.Fusca: // "Fuska" (Folksvagem): carroceria e cabine "torneadas" (malha própria)
            {
                const float w = 1.55f, l = 4.05f;
                // Carroceria: seção superelíptica que afina na frente (capô) e atrás (motor).
                (float hw, float yc, float hh) Body(float z)
                {
                    float t = (z + 2.0f) / 4.0f; // 0 = frente, 1 = traseira
                    float top = Smooth(new[] { 0f, 0.1f, 0.3f, 0.7f, 0.9f, 1f }, new[] { 0.6f, 0.8f, 0.94f, 0.96f, 0.82f, 0.62f }, t);
                    float half = Smooth(new[] { 0f, 0.12f, 0.5f, 0.88f, 1f }, new[] { 0.5f, 0.66f, 0.74f, 0.68f, 0.56f }, t);
                    const float bottom = 0.3f;
                    return (half, (top + bottom) * 0.5f, (top - bottom) * 0.5f);
                }
                Loft(parent, -2.0f, 2.0f, Body, 3.2f, 0f, Mathf.Tau, 1f, paint, caps: true);
                // Cabine: meia-cúpula sobre a linha de cintura (0,9 m); para-brisa inclinado e traseira caída.
                const float belt = 0.9f;
                (float hw, float yc, float hh) Cabin(float z)
                {
                    float t = (z + 0.85f) / 2.15f; // de -0,85 a 1,3
                    float roof = Smooth(new[] { 0f, 0.28f, 0.4f, 0.68f, 1f }, new[] { 0.93f, 1.38f, 1.46f, 1.44f, 0.95f }, t);
                    float half = Smooth(new[] { 0f, 0.3f, 0.7f, 1f }, new[] { 0.6f, 0.64f, 0.64f, 0.58f }, t);
                    return (half, belt, Mathf.Max(0.02f, roof - belt));
                }
                Loft(parent, -0.85f, 1.3f, Cabin, 2.6f, 0f, Mathf.Pi, 1f, paint);
                // Vidros colados na própria cabine (um pouquinho por fora): para-brisa, traseiro e laterais.
                Loft(parent, -0.8f, -0.28f, Cabin, 2.6f, 0.42f, Mathf.Pi - 0.42f, 1.012f, _glassDouble);
                Loft(parent, 0.78f, 1.22f, Cabin, 2.6f, 0.5f, Mathf.Pi - 0.5f, 1.012f, _glassDouble);
                foreach ((float a0, float a1) in new[] { (0.06f, 0.6f), (Mathf.Pi - 0.6f, Mathf.Pi - 0.06f) })
                {
                    Loft(parent, -0.16f, 0.3f, Cabin, 2.6f, a0, a1, 1.012f, _glassDouble);   // janela da porta
                    Loft(parent, 0.42f, 0.74f, Cabin, 2.6f, a0, a1, 1.012f, _glassDouble);   // vidrinho de trás
                }
                foreach (float side in new[] { -1f, 1f })
                {
                    // Para-lamas redondos, faróis em cima dos da frente, lanternas e estribo.
                    parent.AddChild(new MeshInstance3D { Mesh = _fender ??= new SphereMesh { Radius = 0.5f, Height = 1f, RadialSegments = 14, Rings = 8 }, Position = new Vector3(side * 0.62f, 0.5f, -1.2f), Scale = new Vector3(0.36f, 0.5f, 0.9f), MaterialOverride = paint });
                    parent.AddChild(new MeshInstance3D { Mesh = _fender, Position = new Vector3(side * 0.62f, 0.5f, 1.25f), Scale = new Vector3(0.36f, 0.5f, 0.9f), MaterialOverride = paint });
                    parent.AddChild(new MeshInstance3D { Mesh = _wheel, Position = new Vector3(side * 0.6f, 0.72f, -1.62f), Scale = new Vector3(0.17f, 0.05f, 0.17f), Rotation = new Vector3(Mathf.Pi * 0.5f, 0f, 0f), MaterialOverride = _head });
                    Box(parent, new Vector3(side * 0.52f, 0.66f, 1.94f), new Vector3(0.1f, 0.14f, 0.04f), _tail);
                    Box(parent, new Vector3(side * 0.76f, 0.26f, 0.02f), new Vector3(0.12f, 0.04f, 1.45f), _rubber); // estribo
                }
                Box(parent, new Vector3(0f, 0.32f, -2.02f), new Vector3(w + 0.05f, 0.08f, 0.08f), _chrome); // para-choques cromados
                Box(parent, new Vector3(0f, 0.32f, 2.02f), new Vector3(w + 0.05f, 0.08f, 0.08f), _chrome);
                Box(parent, new Vector3(0f, 0.44f, 2.04f), new Vector3(0.4f, 0.11f, 0.02f), _plate);
                // Tampa do motor e capô são curvos: acha onde a superfície passa em cada altura
                // (pelo topo da seção) e cola grade, logo e nome ali, inclinados junto (v0.9.7).
                (Vector3 pos, Vector3 normal) OnSurface(float y, float x, bool rear)
                {
                    float lo = rear ? 0.6f : -2.0f, hi = rear ? 2.0f : -0.6f;
                    float Top(float z) { var b = Body(z); return b.yc + b.hh; }
                    for (int k = 0; k < 24; k++)
                    {
                        float mid = (lo + hi) * 0.5f;
                        bool above = Top(mid) > y;
                        if (above == rear) lo = mid; else hi = mid;
                    }
                    float zs = (lo + hi) * 0.5f;
                    float slope = (Top(zs + 0.01f) - Top(zs - 0.01f)) / 0.02f; // dy/dz da superfície
                    Vector3 n = new Vector3(0f, -slope, 1f).Normalized() * (rear ? 1f : -1f);
                    if (!rear) n = new Vector3(0f, Mathf.Abs(n.Y), -Mathf.Abs(n.Z)).Normalized();
                    return (new Vector3(x, y, zs) + n * 0.012f, n);
                }
                for (int i = 0; i < 4; i++)
                {
                    var (gp, gn) = OnSurface(0.66f + i * 0.035f, 0f, true);
                    parent.AddChild(new MeshInstance3D { Mesh = _box, MaterialOverride = _grille, Transform = new Transform3D(Facing(gn) * Basis.FromScale(new Vector3(0.36f, 0.014f, 0.02f)), gp) });
                }
                var (rl, rn) = OnSurface(0.83f, 0f, true);
                BadgeAt(parent, "folksvagem", rl, rn, 0.12f);
                var (nl, nn) = OnSurface(0.6f, 0.22f, true);
                LabelAt(parent, "FUSKA", nl, nn);
                var (fl, fn) = OnSurface(0.66f, 0f, false);
                BadgeAt(parent, "folksvagem", fl, fn, 0.14f);
                // Retrovisor redondo na porta do motorista (haste + espelho).
                foreach (float side in new[] { -1f, 1f })
                {
                    Box(parent, new Vector3(side * 0.74f, 0.98f, -0.62f), new Vector3(0.1f, 0.02f, 0.02f), _chrome);
                    parent.AddChild(new MeshInstance3D { Mesh = _wheel, Position = new Vector3(side * 0.8f, 1.03f, -0.62f), Scale = new Vector3(0.11f, 0.03f, 0.11f), Rotation = new Vector3(Mathf.Pi * 0.5f, 0f, 0f), MaterialOverride = _chrome });
                }
                foreach (float x in new[] { -0.66f, 0.66f })
                    foreach (float z in new[] { -1.2f, 1.25f })
                        Wheel(parent, new Vector3(x, 0.3f, z), 0.3f, 0.18f);
                return new Vector3(w, 1.5f, l);
            }
            case CarType.Van80: // "Viajante" (Fórdi): furgão anos 80, saia-e-blusa com faixas
            {
                const float w = 1.95f, l = 4.9f;
                float hz = l * 0.5f;
                StandardMaterial3D cream = PaintFor(new Color(0.9f, 0.86f, 0.74f));
                StandardMaterial3D stripeA = PaintFor(new Color(0.95f, 0.72f, 0.12f));
                StandardMaterial3D stripeB = PaintFor(new Color(0.88f, 0.38f, 0.1f));
                StandardMaterial3D stripeC = PaintFor(new Color(0.42f, 0.22f, 0.12f));
                // Bico baixo com capô curto + baú alto do meio para trás; a cabine é a estufa.
                Box(parent, new Vector3(0f, 0.72f, -1.375f), new Vector3(w, 0.74f, 2.15f), paint);
                Box(parent, new Vector3(0f, 1.2f, 1.075f), new Vector3(w, 1.7f, 2.75f), cream);
                Box(parent, new Vector3(0f, 0.72f, 1.075f), new Vector3(w + 0.004f, 0.74f, 2.75f), paint); // saia da cor da van
                Greenhouse(parent, w - 0.02f, 1.09f, 2.05f, -1.85f, -1.3f, -0.28f, -0.28f, cream, bPillar: false);
                // Três faixas retas de ponta a ponta (amarelo, laranja, marrom).
                int k = 0;
                foreach (StandardMaterial3D stripe in new[] { stripeA, stripeB, stripeC })
                {
                    float y = 1.0f + k * 0.075f;
                    foreach (float side in new[] { -1f, 1f })
                        Box(parent, new Vector3(side * (w * 0.5f + 0.008f), y, 0.05f), new Vector3(0.012f, 0.06f, l - 0.3f), stripe);
                    Box(parent, new Vector3(0f, y, hz + 0.008f), new Vector3(w - 0.1f, 0.06f, 0.012f), stripe);
                    k++;
                }
                // Janelinha redonda ("bolha") dos dois lados do baú, com aro cromado.
                foreach (float side in new[] { -1f, 1f })
                {
                    parent.AddChild(new MeshInstance3D { Mesh = _wheel, Position = new Vector3(side * (w * 0.5f + 0.01f), 1.62f, 1.35f), Scale = new Vector3(0.7f, 0.02f, 0.7f), Rotation = new Vector3(0f, 0f, Mathf.Pi * 0.5f), MaterialOverride = _chrome });
                    parent.AddChild(new MeshInstance3D { Mesh = _wheel, Position = new Vector3(side * (w * 0.5f + 0.02f), 1.62f, 1.35f), Scale = new Vector3(0.58f, 0.02f, 0.58f), Rotation = new Vector3(0f, 0f, Mathf.Pi * 0.5f), MaterialOverride = _glassDouble });
                }
                // Porta corrediça do lado do passageiro (vinco e trilho).
                Box(parent, new Vector3(-(w * 0.5f + 0.006f), 1.2f, 0.25f), new Vector3(0.012f, 1.5f, 0.02f), _rubber);
                Box(parent, new Vector3(-(w * 0.5f + 0.006f), 1.2f, 1.25f), new Vector3(0.012f, 1.5f, 0.02f), _rubber);
                Box(parent, new Vector3(-(w * 0.5f + 0.012f), 1.92f, 0.9f), new Vector3(0.02f, 0.03f, 1.6f), _chrome);
                // Traseira: duas portas com vidro e o estepe pendurado.
                Box(parent, new Vector3(0f, 1.2f, hz + 0.006f), new Vector3(0.02f, 1.5f, 0.012f), _rubber);
                foreach (float x in new[] { -0.46f, 0.46f })
                    Box(parent, new Vector3(x, 1.6f, hz + 0.01f), new Vector3(0.66f, 0.42f, 0.02f), _glassDouble);
                parent.AddChild(new MeshInstance3D { Mesh = _wheel, Position = new Vector3(0f, 1.0f, hz + 0.14f), Scale = new Vector3(0.68f, 0.22f, 0.68f), Rotation = new Vector3(Mathf.Pi * 0.5f, 0f, 0f), MaterialOverride = _rubber });
                parent.AddChild(new MeshInstance3D { Mesh = _wheel, Position = new Vector3(0f, 1.0f, hz + 0.26f), Scale = new Vector3(0.36f, 0.02f, 0.36f), Rotation = new Vector3(Mathf.Pi * 0.5f, 0f, 0f), MaterialOverride = _chrome });
                // Bagageiro cromado no teto (é a van de viagem).
                foreach (float x in new[] { -0.75f, 0.75f })
                    Box(parent, new Vector3(x, 2.17f, 0.9f), new Vector3(0.04f, 0.04f, 2.9f), _chrome);
                for (float z = -0.4f; z <= 2.21f; z += 0.65f)
                    Box(parent, new Vector3(0f, 2.17f, z), new Vector3(1.54f, 0.03f, 0.04f), _chrome);
                foreach (float x in new[] { -0.75f, 0.75f })
                    foreach (float z in new[] { -0.4f, 2.2f })
                        Box(parent, new Vector3(x, 2.12f, z), new Vector3(0.05f, 0.1f, 0.05f), _chrome);
                Details(parent, w, l, 0.78f, 0.42f);
                Badges(parent, "fordi", "VIAJANTE", l, 0.78f, 0.82f, 0.2f);
                foreach (float x in new[] { -0.86f, 0.86f })
                    foreach (float z in new[] { -1.5f, 1.6f })
                        Wheel(parent, new Vector3(x, 0.36f, z), 0.36f, 0.24f);
                return new Vector3(w, 2.2f, l);
            }
            default: // Sedan: "Opalinha" (Chevrolé), três volumes
            {
                const float w = 1.8f, l = 4.6f;
                Box(parent, new Vector3(0f, 0.57f, 0f), new Vector3(w, 0.5f, l), paint);
                Box(parent, new Vector3(0f, 0.84f, -1.5f), new Vector3(w - 0.04f, 0.04f, 1.55f), paint); // capô longo
                Box(parent, new Vector3(0f, 0.84f, 1.75f), new Vector3(w - 0.04f, 0.04f, 1.05f), paint); // porta-malas
                Greenhouse(parent, w - 0.04f, 0.83f, 1.34f, -0.72f, -0.25f, 0.85f, 1.22f, paint);
                Box(parent, new Vector3(0f, 0.6f, 0f), new Vector3(w + 0.012f, 0.03f, l - 0.4f), _chrome); // friso cromado
                Details(parent, w, l, 0.66f, 0.4f);
                Badges(parent, "chevrole", "OPALINHA", l, 0.66f, 0.79f, 0.2f);
                foreach (float x in new[] { -0.84f, 0.84f })
                    foreach (float z in new[] { -1.45f, 1.45f })
                        Wheel(parent, new Vector3(x, 0.32f, z), 0.32f, 0.22f);
                return new Vector3(w, 1.4f, l);
            }
        }
    }
}
