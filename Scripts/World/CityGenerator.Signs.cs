// Scripts/World/CityGenerator.Signs.cs
// Placas chapadas (v0.9.8): o desenho da placa (Assets/Textures/Signs) numa chapa fina, frente e verso
// em planos separados (cada um só aparece do seu lado, então nada de faces brigando pelo mesmo pixel).
// Placa de rua fica de frente para quem vem pela pista (motorista e pedestre na calçada veem).
using System.Collections.Generic;
using Godot;

namespace DeadFrequency;

public partial class CityGenerator
{
    private const string SignDir = "res://Assets/Textures/Signs/";
    private static readonly Dictionary<string, StandardMaterial3D> SignMaterials = new();
    private static StandardMaterial3D _signBack;

    private static StandardMaterial3D SignMaterial(string file)
    {
        if (SignMaterials.TryGetValue(file, out StandardMaterial3D m))
            return m;
        m = new StandardMaterial3D
        {
            AlbedoTexture = GD.Load<Texture2D>(SignDir + file),
            Transparency = BaseMaterial3D.TransparencyEnum.AlphaScissor,
            AlphaScissorThreshold = 0.5f,
            TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmaps,
            Roughness = 0.55f,
        };
        SignMaterials[file] = m;
        return m;
    }

    /// <summary>Base com +Z apontando para onde a placa olha.</summary>
    private static Basis SignFacing(Vector3 normal)
    {
        Vector3 z = new Vector3(normal.X, 0f, normal.Z).Normalized();
        Vector3 x = Vector3.Up.Cross(z).Normalized();
        return new Basis(x, Vector3.Up, z);
    }

    /// <summary>Direção para onde olha a placa na beira da pista: de frente para o trânsito que chega
    /// pela faixa mais próxima (mão direita). side = da pista para a calçada onde fica a placa.</summary>
    public static Vector3 FacingOncomingTraffic(Vector3 side) => new Vector3(-side.Z, 0f, side.X).Normalized();

    /// <summary>Chapa fina com a textura na frente e o verso cinza (back = textura do verso com o mesmo
    /// recorte, ou null para um verso retangular liso). center/normal no espaço de parent.</summary>
    public static Node3D SignPlate(Node3D parent, Vector3 center, Vector3 normal, Vector2 size, string front, string back = null)
    {
        var plate = new Node3D { Name = "Placa", Transform = new Transform3D(SignFacing(normal), center) };
        parent.AddChild(plate);
        var quad = new QuadMesh { Size = size };
        plate.AddChild(new MeshInstance3D { Mesh = quad, MaterialOverride = SignMaterial(front), Position = new Vector3(0f, 0f, 0.004f), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
        _signBack ??= new StandardMaterial3D { AlbedoColor = new Color(0.55f, 0.56f, 0.58f), Metallic = 0.4f, Roughness = 0.5f };
        plate.AddChild(new MeshInstance3D
        {
            Mesh = quad,
            MaterialOverride = back != null ? SignMaterial(back) : _signBack,
            Position = new Vector3(0f, 0f, -0.004f),
            Rotation = new Vector3(0f, Mathf.Pi, 0f),
        });
        return plate;
    }

    /// <summary>Poste com abraçadeiras e a chapa presa na frente dele.</summary>
    public static void SignOnPole(Node3D parent, Vector3 basePos, Vector3 normal, float plateY, Vector2 size, string front, string back, Material poleMat)
    {
        float poleTop = plateY + size.Y * 0.5f + 0.05f;
        var pole = new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = 0.032f, BottomRadius = 0.032f, Height = poleTop, RadialSegments = 8 },
            MaterialOverride = poleMat,
            Position = basePos + new Vector3(0f, poleTop * 0.5f, 0f),
        };
        parent.AddChild(pole);
        Vector3 n = new Vector3(normal.X, 0f, normal.Z).Normalized();
        SignPlate(parent, basePos + new Vector3(0f, plateY, 0f) + n * 0.045f, n, size, front, back);
        foreach (float dy in new[] { -size.Y * 0.3f, size.Y * 0.3f })
        {
            parent.AddChild(new MeshInstance3D
            {
                Mesh = new BoxMesh { Size = new Vector3(0.09f, 0.03f, 0.045f) },
                MaterialOverride = poleMat,
                Transform = new Transform3D(SignFacing(n), basePos + new Vector3(0f, plateY + dy, 0f) + n * 0.02f),
            });
        }
    }
}
