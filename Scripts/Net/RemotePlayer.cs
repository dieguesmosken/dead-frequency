// Scripts/Net/RemotePlayer.cs
// Outro entregador na sala: boneco com colete na cor do jogador, nome em cima e os equipamentos
// que ele está usando. A posição chega ~10x por segundo e é suavizada aqui.
using Godot;

namespace DeadFrequency;

public partial class RemotePlayer : Node3D, IInteractable
{
    public int PlayerId { get; private set; }
    public string PlayerName { get; private set; } = "";

    private CharacterModel _model;
    private Label3D _nameLabel;
    private Vector3 _targetPosition;
    private Vector3 _lastPosition;
    private float _targetYaw;
    private float _targetPitch;
    private bool _hasState;
    private bool _driving;
    private int _seat; // 0 a pé, 1 motorista, 2 carona
    private string _look = "";
    private Color _color;
    private Area3D _interactionArea;
    private MeshInstance3D _heldVisual;
    private string _heldValue = "";

    public void Setup(int id, string name, Color color)
    {
        PlayerId = id;
        PlayerName = name;
        Name = $"Remote_{id}";
        AddToGroup(MinimapView.MarkerGroup);
        SetMeta("map_color", color);
        SetMeta("map_kind", "player");

        _color = color;
        _model = CharacterModel.Create(color.Darkened(0.55f), new Color(0.12f, 0.13f, 0.18f), new Color(0.76f, 0.58f, 0.44f), color);
        AddChild(_model);

        _nameLabel = new Label3D
        {
            Text = name,
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            FontSize = 48,
            PixelSize = 0.006f,
            Modulate = color,
            OutlineSize = 8,
            Position = new Vector3(0f, 2.25f, 0f),
            NoDepthTest = true,
        };
        AddChild(_nameLabel);

        _interactionArea = new Area3D { CollisionLayer = 4, CollisionMask = 0, Monitoring = false, Monitorable = true };
        _interactionArea.AddChild(new CollisionShape3D
        {
            Shape = new CapsuleShape3D { Radius = 0.35f, Height = 1.7f },
            Position = new Vector3(0f, 0.9f, 0f),
        });
        AddChild(_interactionArea);
    }

    public void ApplyState(Vector3 position, float yaw, float pitch, int seat, string equipment, string look = "", string held = "")
    {
        _targetPosition = position;
        _targetYaw = yaw;
        _targetPitch = pitch;
        _seat = seat;
        _driving = seat == 1;
        if (_heldValue != held)
        {
            _heldValue = held ?? "";
            _heldVisual?.QueueFree();
            _heldVisual = null;
            if (!string.IsNullOrEmpty(_heldValue))
            {
                string visualValue = _heldValue.StartsWith("item:") ? _heldValue : "cargo";
                _heldVisual = WorldItem.CreateVisual(visualValue);
                _heldVisual.Position = new Vector3(0.34f, 1.12f, -0.22f);
                AddChild(_heldVisual);
            }
        }
        if (!string.IsNullOrEmpty(look) && look != _look && CharacterLook.TryDecode(look, out CharacterLook l))
        {
            // O jogador escolheu a aparência (v0.9.6): troca o boneco.
            _look = look;
            string eq = _model?.EquipmentKey ?? "";
            _model?.QueueFree();
            _model = l.Build();
            AddChild(_model);
            _model.SetEquipment(eq);
        }

        _model.SetEquipment(equipment);
        if (!_hasState)
        {
            GlobalPosition = position;
            _lastPosition = position;
            Rotation = new Vector3(0f, yaw, 0f);
            _hasState = true;
        }
    }

    public string GetInteractionPrompt(PlayerController player)
    {
        string itemId = PlayerInventory.Instance?.SelectedHotbarItemId;
        if (string.IsNullOrEmpty(itemId))
            return "";
        return $"[E] Entregar {ItemCatalog.Get(itemId)?.Name ?? "item"} para {PlayerName}";
    }

    public bool CanInteract(PlayerController player) =>
        player != null && player.HasSelectedItem && player.State == PlayerState.Walking;

    public void Interact(PlayerController player) => player.TransferSelectedItem(PlayerId);

    public override void _Process(double delta)
    {
        if (!_hasState)
            return;
        float dt = (float)delta;
        float t = 1f - Mathf.Exp(-12f * dt);
        GlobalPosition = GlobalPosition.Lerp(_targetPosition, t);
        Rotation = new Vector3(0f, Mathf.LerpAngle(Rotation.Y, _targetYaw, t), 0f);
        _model.SetHeadPitch(-_targetPitch);

        Vector3 moved = GlobalPosition - _lastPosition;
        moved.Y = 0f;
        _lastPosition = GlobalPosition;
        float speed = dt > 0f ? moved.Length() / dt : 0f;
        // Sentado (motorista ou carona): aparece no banco, sem andar (v0.9.6 — antes sumia).
        _model.SetSeated(_seat != 0);
        _model.Animate(dt, _seat != 0 ? 0f : speed);
    }
}
