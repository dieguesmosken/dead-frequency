// Scripts/Vehicle/DashboardButton.cs
// Botão físico do painel da caminhonete: mire e aperte E, sentado no banco (motorista ou carona).
// O botão não sabe o que liga: recebe funções do VanDashboard (texto, ação, aceso ou não).
using System;
using Godot;

namespace DeadFrequency;

public partial class DashboardButton : Area3D, IInteractable
{
    public VanController Van { get; set; }
    public string Caption { get; set; } = "";
    public Func<string> Prompt { get; set; }
    public Action<PlayerController> Pressed { get; set; }
    /// <summary>Luzinha do botão acesa?</summary>
    public Func<bool> Lit { get; set; }
    public Color LampColor { get; set; } = new(0.4f, 1f, 0.5f);
    public Vector3 Size { get; set; } = new(0.05f, 0.04f, 0.03f);

    private MeshInstance3D _cap;
    private StandardMaterial3D _lamp;
    private double _refresh;

    public override void _Ready()
    {
        CollisionLayer = 4; // Interactables
        CollisionMask = 0;
        Monitoring = false;
        // Área maior que o botão: fácil de mirar (principalmente no celular).
        AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(Size.X + 0.012f, Size.Y + 0.03f, Size.Z + 0.03f) } });
        _cap = new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = Size },
            MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.13f, 0.13f, 0.14f), Roughness = 0.6f },
        };
        AddChild(_cap);
        _lamp = new StandardMaterial3D { AlbedoColor = new Color(0.08f, 0.1f, 0.08f), EmissionEnabled = true, Emission = LampColor, EmissionEnergyMultiplier = 0f };
        AddChild(new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = new Vector3(Size.X * 0.55f, 0.008f, 0.004f) },
            MaterialOverride = _lamp,
            Position = new Vector3(0f, Size.Y * 0.5f - 0.008f, Size.Z * 0.5f + 0.001f),
        });
        AddChild(new Label3D
        {
            Text = Caption,
            FontSize = 20,
            PixelSize = 0.0007f,
            Modulate = new Color(0.85f, 0.87f, 0.8f),
            OutlineSize = 0,
            Position = new Vector3(0f, -0.004f, Size.Z * 0.5f + 0.002f),
        });
    }

    public override void _Process(double delta)
    {
        _refresh -= delta;
        if (_refresh > 0)
            return;
        _refresh = 0.15;
        bool lit = Lit?.Invoke() ?? false;
        _lamp.EmissionEnergyMultiplier = lit ? 2.2f : 0f;
        _lamp.AlbedoColor = lit ? LampColor : new Color(0.08f, 0.1f, 0.08f);
    }

    private bool Seated(PlayerController player) =>
        player != null && Van != null && player.CurrentVehicle == Van && !player.IsThirdPerson && player.State is PlayerState.Driving or PlayerState.Riding;

    public string GetInteractionPrompt(PlayerController player) => Seated(player) ? Prompt?.Invoke() ?? "" : "";

    public bool CanInteract(PlayerController player) => Seated(player);

    public void Interact(PlayerController player)
    {
        Pressed?.Invoke(player);
        // Clique físico: o botão afunda e volta.
        Tween tween = CreateTween();
        tween.TweenProperty(_cap, "position:z", -0.008f, 0.05);
        tween.TweenProperty(_cap, "position:z", 0f, 0.09);
        AudioManager.Instance?.PlayUi(UiSound.Click);
        _refresh = 0;
    }
}
