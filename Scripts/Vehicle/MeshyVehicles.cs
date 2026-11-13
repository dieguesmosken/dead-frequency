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
    private static StandardMaterial3D _truckMat;
    private static Shader _recolor;

    public static bool HasFuska => ResourceLoader.Exists(FuskaPath);
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
        _truckMat ??= new StandardMaterial3D
        {
            AlbedoTexture = _truckTex,
            TextureFilter = BaseMaterial3D.TextureFilterEnum.Nearest,
            Roughness = 0.75f,
        };
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
