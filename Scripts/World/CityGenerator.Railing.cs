// Scripts/World/CityGenerator.Railing.cs
// Grade de metal em cima dos muros (v0.9.9): central de entregas e Rádio 96.6.
// As barras são uma textura com transparência (uma malha por trecho, um material só para todas),
// com corrimão em cima, mourões a cada ~2,5 m e colisão própria (ninguém pula o muro).
using Godot;

namespace DeadFrequency;

public partial class CityGenerator
{
    private const float RailBarSpacing = 0.14f;
    private StandardMaterial3D _railBars, _railMetal;

    private void EnsureRailingResources()
    {
        if (_railBars != null)
            return;
        // Textura 8x32: uma barra de 2 px por repetição, com ponta de lança no alto.
        Image img = Image.CreateEmpty(8, 32, false, Image.Format.Rgba8);
        var metal = new Color(0.1f, 0.1f, 0.11f);
        for (int y = 0; y < 32; y++)
        {
            for (int x = 0; x < 8; x++)
            {
                bool bar = x is 3 or 4;
                bool tip = y < 4 && (y < 2 ? x == 3 || x == 4 : x is >= 2 and <= 5); // lança alarga um pouco
                bool opaque = y < 4 ? tip : bar;
                img.SetPixel(x, y, opaque ? metal : new Color(0f, 0f, 0f, 0f));
            }
        }
        img.GenerateMipmaps();
        _railBars = new StandardMaterial3D
        {
            AlbedoTexture = ImageTexture.CreateFromImage(img),
            Transparency = BaseMaterial3D.TransparencyEnum.AlphaScissor,
            AlphaScissorThreshold = 0.5f,
            TextureFilter = BaseMaterial3D.TextureFilterEnum.NearestWithMipmaps,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            Roughness = 0.6f,
            Metallic = 0.4f,
        };
        _railMetal = Flat(metal, 0.5f);
    }

    /// <summary>
    /// Grade reta de a até b (no chão, coordenadas de parent), começando na altura baseY (topo do muro).
    /// </summary>
    private void BuildRailing(Node3D parent, Vector3 a, Vector3 b, float baseY, float height)
    {
        if (!BuildRailingVisual(parent, a, b, baseY, height))
            return;
        Vector3 along = b - a;
        along.Y = 0f;
        float len = along.Length();
        float yaw = Mathf.Atan2(along.X, along.Z);
        // Colisão da grade inteira (o muro já tem a dele).
        var body = new StaticBody3D { CollisionLayer = 1, CollisionMask = 0, Position = (a + b) * 0.5f + new Vector3(0f, baseY + height * 0.5f, 0f), Rotation = new Vector3(0f, yaw, 0f) };
        body.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(0.08f, height, len) } });
        parent.AddChild(body);
    }

    /// <summary>Só a parte visual da grade (barras, corrimão e mourões). Falso se o trecho é curto demais.</summary>
    private bool BuildRailingVisual(Node3D parent, Vector3 a, Vector3 b, float baseY, float height)
    {
        EnsureRailingResources();
        Vector3 along = b - a;
        along.Y = 0f;
        float len = along.Length();
        if (len < 0.2f)
            return false;
        Vector3 dir = along / len;
        Vector3 mid = (a + b) * 0.5f;

        // Barras: um quad com UV repetindo a textura a cada RailBarSpacing.
        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);
        Vector3 p0 = a - mid + new Vector3(0f, baseY, 0f), p1 = b - mid + new Vector3(0f, baseY, 0f);
        Vector3 up = new(0f, height, 0f);
        float u = len / RailBarSpacing;
        Vector3 n = dir.Cross(Vector3.Up).Normalized();
        void V(Vector3 p, float uu, float vv)
        {
            st.SetNormal(n);
            st.SetUV(new Vector2(uu, vv));
            st.AddVertex(p);
        }
        V(p0, 0f, 1f); V(p1, u, 1f); V(p1 + up, u, 0f);
        V(p0, 0f, 1f); V(p1 + up, u, 0f); V(p0 + up, 0f, 0f);
        parent.AddChild(new MeshInstance3D { Mesh = st.Commit(), MaterialOverride = _railBars, Position = mid });

        float yaw = Mathf.Atan2(dir.X, dir.Z);
        // Corrimão logo abaixo das lanças e travessa de baixo.
        AddBox(parent, mid + new Vector3(0f, baseY + height - 0.16f, 0f), new Vector3(0.05f, 0.04f, len), _railMetal, false, yaw);
        AddBox(parent, mid + new Vector3(0f, baseY + 0.06f, 0f), new Vector3(0.05f, 0.04f, len), _railMetal, false, yaw);
        // Mourões.
        int posts = Mathf.Max(1, Mathf.RoundToInt(len / 2.5f));
        for (int i = 0; i <= posts; i++)
        {
            Vector3 p = a + dir * (len * i / posts);
            AddBox(parent, new Vector3(p.X, baseY + height * 0.5f, p.Z), new Vector3(0.07f, height, 0.07f), _railMetal, false, yaw);
        }
        return true;
    }

    /// <summary>Muro reto (caixa com colisão) com grade em cima. center/size como no AddBox; a grade segue o lado maior.</summary>
    private void WallWithRailing(Node3D parent, Vector3 center, Vector3 size, Material wallMat, float railHeight)
    {
        AddBox(parent, center, size, wallMat);
        Vector3 half = size.X >= size.Z ? new Vector3(size.X * 0.5f, 0f, 0f) : new Vector3(0f, 0f, size.Z * 0.5f);
        Vector3 ground = new(center.X, 0f, center.Z);
        BuildRailing(parent, ground - half, ground + half, center.Y + size.Y * 0.5f, railHeight);
    }
}
