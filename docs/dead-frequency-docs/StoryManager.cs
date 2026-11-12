using Godot;

public partial class RadioBeacon : Node3D
{
    [Export] public NodePath PlayerPath;
    [Export] public AudioStreamPlayer MusicPlayer;
    [Export] public AudioStreamPlayer StaticPlayer;
    [Export] public float MaxSignalDistance = 120.0f;
    [Export] public float ClearSignalDistance = 15.0f;

    private Node3D _player;

    public override void _Ready()
    {
        if (PlayerPath != null && !PlayerPath.IsEmpty)
            _player = GetNodeOrNull<Node3D>(PlayerPath);
    }

    public override void _Process(double delta)
    {
        if (_player == null || MusicPlayer == null || StaticPlayer == null) return;

        float dist = GlobalPosition.DistanceTo(_player.GlobalPosition);
        float t = Mathf.Clamp((dist - ClearSignalDistance) / (MaxSignalDistance - ClearSignalDistance), 0.0f, 1.0f);

        MusicPlayer.VolumeDb = Mathf.Lerp(0.0f, -45.0f, t);
        StaticPlayer.VolumeDb = Mathf.Lerp(-35.0f, -4.0f, t);
    }
}
