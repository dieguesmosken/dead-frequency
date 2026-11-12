using Godot;

public partial class ChapterTransitionZone : Area3D
{
    [Export] public int RequiredChapter = 1;
    [Export] public int NextChapter = 2;
    [Export] public string ChapterNumberText = "CAPÍTULO II";
    [Export] public string ChapterSubtitle = "Asfalto e Barro";
    [Export(PropertyHint.File, "*.tscn")] public string TargetScenePath;
    [Export] public float AutoDriveSpeed = 12.0f;
    [Export] public float MinimumBannerDuration = 3.5f;

    private bool _triggered = false;
    private bool _loadingStarted = false;
    private float _elapsed = 0f;

    public override void _Ready()
    {
        BodyEntered += OnBodyEntered;
    }

    private void OnBodyEntered(Node3D body)
    {
        if (_triggered) return;
        if (!body.IsInGroup("PlayerVan")) return;

        if (StoryManager.Instance != null && !StoryManager.Instance.CanTriggerTransition(RequiredChapter))
            return;

        _triggered = true;

        if (body.HasMethod("SetAutoPilotMode"))
            body.Call("SetAutoPilotMode", true, AutoDriveSpeed);

        if (!string.IsNullOrEmpty(TargetScenePath))
        {
            ResourceLoader.LoadThreadedRequest(TargetScenePath);
            _loadingStarted = true;
        }

        GD.Print($"[Transição] {ChapterNumberText}: {ChapterSubtitle}");
    }

    public override void _Process(double delta)
    {
        if (!_loadingStarted) return;

        _elapsed += (float)delta;
        var status = ResourceLoader.LoadThreadedGetStatus(TargetScenePath);

        if (_elapsed >= MinimumBannerDuration && status == ResourceLoader.ThreadLoadStatus.Loaded)
        {
            _loadingStarted = false;
            var packedScene = (PackedScene)ResourceLoader.LoadThreadedGet(TargetScenePath);
            StoryManager.Instance?.AdvanceToChapter(NextChapter);
            GetTree().ChangeSceneToPacked(packedScene);
        }
    }
}
