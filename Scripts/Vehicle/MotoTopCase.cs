// Scripts/Vehicle/MotoTopCase.cs
// Baú da moto de entrega (v0.9.3). Com uma caixa na mão (até 20 kg), mire no baú e aperte E para guardar;
// a caixa vai presa na moto até o destino (o feixe verde reconhece a caixa dentro do baú) ou até você tirar.
using Godot;

namespace DeadFrequency;

public partial class MotoTopCase : Node3D
{
    public const float MaxWeightKg = 20f;

    public VanController Moto { get; private set; }
    public CargoItem Stored { get; private set; }

    public static void Attach(VanController moto)
    {
        var tc = new MotoTopCase { Name = "TopCase", Moto = moto, Position = new Vector3(0f, MeshyVehicles.HasProp("moto") ? 0.82f : 0.98f, -0.78f) };
        moto.AddChild(tc);
    }

    public override void _Ready()
    {
        var red = new StandardMaterial3D { AlbedoColor = new Color(0.75f, 0.1f, 0.08f), Roughness = 0.5f };
        var dark = new StandardMaterial3D { AlbedoColor = new Color(0.08f, 0.08f, 0.08f) };
        // v0.10: baú térmico do DF Entregas (aberto em cima: a caixa vai dentro, presa com elástico), com o logo
        // do aplicativo nos três lados (Assets/Textures/Depot/bau_lado.png).
        const float w = 0.56f, d = 0.56f, h = 0.4f, t = 0.03f;
        void Box(Vector3 pos, Vector3 size, Material m) => AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = size }, Position = pos, MaterialOverride = m });
        Box(new Vector3(0f, 0f, 0f), new Vector3(w, 0.04f, d), dark);                                  // fundo/bagageiro
        Box(new Vector3(0f, h * 0.5f, -d * 0.5f + t * 0.5f), new Vector3(w, h, t), red);              // trás
        Box(new Vector3(0f, h * 0.5f, d * 0.5f - t * 0.5f), new Vector3(w, h, t), red);               // frente (lado do piloto)
        Box(new Vector3(w * 0.5f - t * 0.5f, h * 0.5f, 0f), new Vector3(t, h, d), red);
        Box(new Vector3(-w * 0.5f + t * 0.5f, h * 0.5f, 0f), new Vector3(t, h, d), red);
        foreach (float x in new[] { -w * 0.5f, w * 0.5f })                                             // cantoneiras
            foreach (float z in new[] { -d * 0.5f, d * 0.5f })
                Box(new Vector3(x, h * 0.5f, z), new Vector3(0.035f, h + 0.01f, 0.035f), dark);
        const string logo = "res://Assets/Textures/Depot/bau_lado.png";
        if (ResourceLoader.Exists(logo))
        {
            var face = new StandardMaterial3D { AlbedoTexture = GD.Load<Texture2D>(logo), Roughness = 0.6f, TextureFilter = BaseMaterial3D.TextureFilterEnum.NearestWithMipmaps };
            void Face(Vector3 pos, float rotY) => AddChild(new MeshInstance3D { Mesh = new QuadMesh { Size = new Vector2(0.5f, 0.34f) }, Position = pos, Rotation = new Vector3(0f, rotY, 0f), MaterialOverride = face });
            Face(new Vector3(0f, h * 0.5f, -d * 0.5f - 0.002f), Mathf.Pi);
            Face(new Vector3(w * 0.5f + 0.002f, h * 0.5f, 0f), Mathf.Pi * 0.5f);
            Face(new Vector3(-w * 0.5f - 0.002f, h * 0.5f, 0f), -Mathf.Pi * 0.5f);
        }
        else
        {
            AddChild(new Label3D { Text = "DF ENTREGAS", FontSize = 24, PixelSize = 0.003f, Modulate = Colors.White, OutlineSize = 0, Position = new Vector3(0f, 0.2f, -d * 0.5f - 0.005f), Rotation = new Vector3(0f, Mathf.Pi, 0f) });
        }
        ActionArea.Create(this, new Vector3(0f, 0.25f, 0f), new Vector3(0.7f, 0.6f, 0.75f), Prompt, Use, Can);
    }

    private string Prompt(PlayerController p)
    {
        if (Stored != null && IsInstanceValid(Stored))
            return p.IsHolding ? "Baú cheio" : $"[E] Tirar {Stored.Data?.CargoName ?? "a caixa"} do baú";
        if (!p.IsHolding)
            return "Baú da moto (traga uma caixa de até 20 kg)";
        return p.HeldItem.WeightKg > MaxWeightKg ? $"Pesada demais para a moto ({p.HeldItem.WeightKg:0} kg)" : "[E] Guardar a caixa no baú";
    }

    private bool Can(PlayerController p)
    {
        if (p.State != PlayerState.Walking)
            return false;
        if (Stored != null && IsInstanceValid(Stored))
            return !p.IsHolding;
        return p.IsHolding && p.HeldItem.WeightKg <= MaxWeightKg;
    }

    private void Use(PlayerController p)
    {
        if (Stored != null && IsInstanceValid(Stored))
        {
            CargoItem c = Stored;
            Release();
            p.TryGrab(c);
            return;
        }
        CargoItem cargo = p.HeldItem;
        if (cargo == null)
            return;
        p.DropHeld();
        Stored = cargo;
        Moto.AddCollisionExceptionWith(cargo);
        cargo.FreezeMode = RigidBody3D.FreezeModeEnum.Kinematic;
        cargo.Freeze = true;
        cargo.LinearVelocity = Vector3.Zero;
        cargo.AngularVelocity = Vector3.Zero;
        cargo.Reparent(this, false);
        cargo.Position = new Vector3(0f, 0.25f, 0f);
        cargo.Rotation = Vector3.Zero;
        GameHud.Toast(this, "Caixa no baú. Leve até o feixe verde de moto!", UiStyle.Phosphor, 3.0);
    }

    private void Release()
    {
        CargoItem c = Stored;
        Stored = null;
        if (c == null || !IsInstanceValid(c))
            return;
        Transform3D t = c.GlobalTransform;
        Node root = GetTree().CurrentScene;
        c.Reparent(root, false);
        c.GlobalTransform = t;
        c.Freeze = c.IsRemote;
        if (IsInstanceValid(Moto))
            Moto.RemoveCollisionExceptionWith(c);
    }

    public override void _Process(double delta)
    {
        // Entregue (a caixa some) ou pega direto com a mão: solta do baú.
        if (Stored != null && (!IsInstanceValid(Stored) || Stored.IsHeld))
        {
            if (IsInstanceValid(Stored))
                Release();
            else
                Stored = null;
        }
    }
}
