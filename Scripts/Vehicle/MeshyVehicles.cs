// Scripts/Vehicle/MeshyVehicles.cs
// Carros feitos no Meshy (v0.10): Fuska e a caminhonete de entregas. Os GLBs vêm com o comprimento no eixo X
// e a frente para -X; aqui viram para a frente pedida (-Z nos CarModels, +Z no VanController), na escala
// certa e com as rodas no chão. Textura de 512 px sem filtro (PS1). O Fuska é pintado por máscara
// (shader meshy_recolor, canal R = lataria), então cada um continua com a sua cor.
// As rodas fazem parte do modelo (fixas, como nos outros carros); vidros pintados somem vistos de dentro.
using Godot;

namespace DeadFrequency;

public static class MeshyVehicles
{
    public const string FuskaPath = "res://Assets/Meshy/Vehicles/fuska.glb";
    public const string TruckPath = "res://Assets/Meshy/Vehicles/caminhonete.glb";
    private const float FuskaPaintRef = 0.1752f;

    private static Mesh _fuska, _truck;
    private static Texture2D _fuskaTex, _fuskaMask, _truckTex;
    private static Material _truckMat;
    private static Shader _lit;

    /// <summary>Material sem recolor: textura sem filtro + tremor de vértices do PS1 (Shaders/ps1_lit.gdshader).</summary>
    private static Material Lit(Texture2D tex)
    {
        _lit ??= GD.Load<Shader>("res://Shaders/ps1_lit.gdshader");
        var m = new ShaderMaterial { Shader = _lit };
        m.SetShaderParameter("base_tex", tex);
        return m;
    }
    private static Shader _recolor;

    public static bool HasFuska => ResourceLoader.Exists(FuskaPath);

    /// <summary>Objetos do Meshy em Assets/Meshy/Props (barraca, churrasco, bombeiros, polícia).</summary>
    public static bool HasProp(string name) => ResourceLoader.Exists($"res://Assets/Meshy/Props/{name}.glb");

    private static readonly System.Collections.Generic.Dictionary<string, Mesh> PropMeshes = new();

    /// <summary>
    /// Monta um objeto do Meshy com o tamanho exato (largura X, altura Y, comprimento Z) e a frente para frontZ.
    /// tint: cor que substitui a região da máscara (ex.: toldo da barraca); null = textura original.
    /// </summary>
    public static Vector3 BuildProp(Node3D parent, string name, Vector3 size, int frontZ, Color? tint = null, float tintRef = 0.12f)
    {
        if (!PropMeshes.TryGetValue(name, out Mesh mesh))
        {
            mesh = LoadMesh($"res://Assets/Meshy/Props/{name}.glb");
            PropMeshes[name] = mesh;
        }
        if (mesh == null)
            return Vector3.Zero;
        var tex = GD.Load<Texture2D>($"res://Assets/Meshy/Props/{name}_cor.png");
        string maskPath = $"res://Assets/Meshy/Props/{name}_mascara.png";
        Material mat;
        if (tint != null && ResourceLoader.Exists(maskPath))
        {
            _recolor ??= GD.Load<Shader>("res://Shaders/Characters/meshy_recolor.gdshader");
            var sm = new ShaderMaterial { Shader = _recolor };
            sm.SetShaderParameter("base_tex", tex);
            sm.SetShaderParameter("mask_tex", GD.Load<Texture2D>(maskPath));
            sm.SetShaderParameter("shirt_color", tint.Value);
            sm.SetShaderParameter("ref_lum", new Vector4(tintRef, 1f, 1f, 1f));
            mat = sm;
        }
        else
        {
            mat = Lit(tex);
        }
        Aabb box = mesh.GetAabb();
        // Comprimento (frente-trás) = eixo X do modelo; largura = Z; altura = Y.
        var scale = new Vector3(size.Z / box.Size.X, size.Y / box.Size.Y, size.X / box.Size.Z);
        float yaw = frontZ < 0 ? -Mathf.Pi * 0.5f : Mathf.Pi * 0.5f;
        Vector3 center = new Basis(Vector3.Up, yaw) * (box.GetCenter() * scale);
        parent.AddChild(new MeshInstance3D
        {
            Name = "Meshy_" + name,
            Mesh = mesh,
            MaterialOverride = mat,
            Rotation = new Vector3(0f, yaw, 0f),
            Scale = scale,
            Position = new Vector3(-center.X, -box.Position.Y * scale.Y, -center.Z),
        });
        return size;
    }
    /// <summary>
    /// Carroceria do Meshy feita SEM rodas (v0.10.1) mais rodas de verdade que giram (CarModels.AddWheel + WheelSpin).
    /// size = (largura, altura da carroceria, comprimento); lift = altura do chão até o fundo da carroceria;
    /// axles = posição dos eixos em fração do comprimento (frente = -Z, ex.: -0.33 e 0.33).
    /// Devolve o tamanho total (largura, altura com as rodas, comprimento).
    /// </summary>
    public static Vector3 BuildWheeledProp(Node3D parent, string name, Vector3 size, int frontZ, float lift,
        float wheelRadius, float wheelWidth, float[] axles, float trackInset = 0.1f, Color? paint = null, bool spin = false)
    {
        var body = new Node3D { Name = "Body", Position = new Vector3(0f, lift, 0f) };
        parent.AddChild(body);
        if (BuildProp(body, name, size, frontZ) == Vector3.Zero)
        {
            body.QueueFree();
            return Vector3.Zero;
        }
        if (paint != null && body.GetChildCount() > 0 && body.GetChild(0) is MeshInstance3D mi && mi.MaterialOverride is ShaderMaterial sm)
        {
            var tinted = (ShaderMaterial)sm.Duplicate();
            Color c = paint.Value.Lerp(Colors.White, 0.3f) * 1.3f;
            tinted.SetShaderParameter("tint", new Vector3(c.R, c.G, c.B));
            mi.MaterialOverride = tinted;
        }
        float x = size.X * 0.5f - trackInset - wheelWidth * 0.5f;
        foreach (float a in axles)
            foreach (float side in new[] { -1f, 1f })
                CarModels.AddWheel(parent, new Vector3(side * x, wheelRadius, a * size.Z), wheelRadius, wheelWidth);
        if (spin)
            WheelSpin.Attach(parent);
        return new Vector3(size.X, size.Y + lift, size.Z);
    }

    public static bool HasTruck => ResourceLoader.Exists(TruckPath);

    private static Mesh LoadMesh(string path)
    {
        if (!ResourceLoader.Exists(path) || GD.Load<PackedScene>(path)?.Instantiate() is not Node3D scene)
            return null;
        Mesh mesh = null;
        foreach (Node n in scene.FindChildren("*", "MeshInstance3D", true, false))
        {
            mesh = ((MeshInstance3D)n).Mesh;
            break;
        }
        scene.Free();
        return mesh;
    }

    /// <summary>Fuska pintado na cor pedida, frente para -Z (convenção dos CarModels). Devolve (largura, altura, comprimento).</summary>
    public static Vector3 BuildFuska(Node3D parent, Color paint, float length)
    {
        _fuska ??= LoadMesh(FuskaPath);
        if (_fuska == null)
            return Vector3.Zero;
        _recolor ??= GD.Load<Shader>("res://Shaders/Characters/meshy_recolor.gdshader");
        _fuskaTex ??= GD.Load<Texture2D>("res://Assets/Meshy/Vehicles/fuska_cor.png");
        _fuskaMask ??= GD.Load<Texture2D>("res://Assets/Meshy/Vehicles/fuska_mascara.png");
        var mat = new ShaderMaterial { Shader = _recolor };
        mat.SetShaderParameter("base_tex", _fuskaTex);
        mat.SetShaderParameter("mask_tex", _fuskaMask);
        mat.SetShaderParameter("shirt_color", paint);
        mat.SetShaderParameter("ref_lum", new Vector4(FuskaPaintRef, 1f, 1f, 1f));
        return Place(parent, _fuska, mat, length, -1);
    }

    /// <summary>Caminhonete de entregas, frente para +Z (VanController). Devolve (largura, altura, comprimento).</summary>
    public static Vector3 BuildTruck(Node3D parent, float length, float width)
    {
        _truck ??= LoadMesh(TruckPath);
        if (_truck == null)
            return Vector3.Zero;
        _truckTex ??= GD.Load<Texture2D>("res://Assets/Meshy/Vehicles/caminhonete_cor.png");
        _truckMat ??= Lit(_truckTex);
        return Place(parent, _truck, _truckMat, length, 1, width);
    }

    /// <summary>Gira o comprimento (X) para o eixo Z com a frente em frontZ (-1 ou +1), escala e apoia no chão.</summary>
    private static Vector3 Place(Node3D parent, Mesh mesh, Material mat, float length, int frontZ, float width = 0f)
    {
        Aabb box = mesh.GetAabb();
        float s = length / box.Size.X;
        // Largura opcional: estica/encolhe só de lado (eixo Z do modelo) para casar com as colisões.
        float sw = width > 0f ? width / box.Size.Z : s;
        var scale = new Vector3(s, s, sw);
        float yaw = frontZ < 0 ? -Mathf.Pi * 0.5f : Mathf.Pi * 0.5f;
        Vector3 center = new Basis(Vector3.Up, yaw) * (box.GetCenter() * scale);
        parent.AddChild(new MeshInstance3D
        {
            Name = "MeshyBody",
            Mesh = mesh,
            MaterialOverride = mat,
            Rotation = new Vector3(0f, yaw, 0f),
            Scale = scale,
            Position = new Vector3(-center.X, -box.Position.Y * s, -center.Z),
        });
        return new Vector3(box.Size.Z * sw, box.Size.Y * s, box.Size.X * s);
    }
}
