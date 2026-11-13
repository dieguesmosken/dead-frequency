// Scripts/Vehicle/Plates.cs
// Placas cinzas dos anos 90 (v0.10): "SINTONIA · SP" em cima e o código ABC-1234, diferente em cada carro.
// O código sai de uma semente (NetId do carro ou a ordem em que a cidade gera os estacionados), então
// é o mesmo em todos os computadores da sala. A caminhonete da empresa continua DFQ-1996.
using Godot;

namespace DeadFrequency;

public static class Plates
{
    private const string Letters = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
    private static int _counter;
    private static StandardMaterial3D _plateMat, _frameMat;

    /// <summary>Código fixo para a semente (ex.: o NetId do carro).</summary>
    public static string For(string seed)
    {
        uint h = (uint)(seed ?? "").Hash();
        h ^= h >> 15; h *= 2246822519u; h ^= h >> 13;
        char a = Letters[(int)(h % 26)], b = Letters[(int)(h / 26 % 26)], c = Letters[(int)(h / 676 % 26)];
        int n = (int)(h / 17576 % 9000) + 1000;
        return $"{a}{b}{c}-{n}";
    }

    /// <summary>Próximo código para os carros gerados pela cidade (mesma ordem de geração = mesma placa).</summary>
    public static string Next(string salt) => For($"{salt}#{_counter++}");

    /// <summary>Placa pronta (0,4 x 0,13 m) virada para +Z local.</summary>
    public static Node3D Make(string code)
    {
        _plateMat ??= new StandardMaterial3D { AlbedoColor = new Color(0.72f, 0.73f, 0.72f), Roughness = 0.6f };
        _frameMat ??= new StandardMaterial3D { AlbedoColor = new Color(0.1f, 0.1f, 0.1f), Roughness = 0.8f };
        var root = new Node3D { Name = "Plate" };
        root.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(0.42f, 0.14f, 0.012f) }, MaterialOverride = _frameMat });
        root.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(0.4f, 0.125f, 0.014f) }, MaterialOverride = _plateMat });
        root.AddChild(new Label3D
        {
            Text = "SINTONIA · SP", FontSize = 20, PixelSize = 0.0012f, Modulate = new Color(0.12f, 0.12f, 0.12f),
            OutlineSize = 0, Position = new Vector3(0f, 0.042f, 0.009f),
        });
        root.AddChild(new Label3D
        {
            Text = code, FontSize = 44, PixelSize = 0.0019f, Modulate = new Color(0.08f, 0.08f, 0.08f),
            OutlineSize = 0, Position = new Vector3(0f, -0.012f, 0.009f),
        });
        return root;
    }

    /// <summary>Põe a placa em pos, de frente para facing (+Z ou -Z).</summary>
    public static void Attach(Node3D parent, string code, Vector3 pos, bool facePlusZ)
    {
        Node3D plate = Make(code);
        plate.Position = pos;
        plate.Rotation = new Vector3(0f, facePlusZ ? 0f : Mathf.Pi, 0f);
        parent.AddChild(plate);
    }
}
