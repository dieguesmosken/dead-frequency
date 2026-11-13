// Scripts/Story/ChapterTransitionZone.cs
// "Loading disfarçado" na estrada: a van do jogador entra na área, segue no piloto automático com a 96.6
// tocando e um banner de capítulo, enquanto a próxima cena carrega em segundo plano.
// Ver docs/tech/01_ARQUITETURA_MODO_HISTORIA.md, seção 3.
using Godot;

namespace DeadFrequency;

public partial class ChapterTransitionZone : Area3D
{
    [Export] public int RequiredChapter = 1;
    [Export] public int NextChapter = 2;
    [Export] public string ChapterNumberText = "CAPÍTULO II";
    [Export] public string ChapterSubtitle = "";
    [Export(PropertyHint.File, "*.tscn")] public string TargetScenePath = "";
    /// <summary>Id da fase de destino (GameLevels), para as profissões e o save saberem onde estão.</summary>
    [Export] public string TargetLevel = "farm";
    [Export] public float AutoDriveSpeed = 10f;        // m/s (~36 km/h)
    [Export] public float MinimumBannerSeconds = 3.5f;

    private VanController _van;
    private ChapterBanner _banner;
    private bool _loading;
    private float _elapsed;

    public override void _Ready()
    {
        Monitoring = true;
        Monitorable = false;
        CollisionLayer = 0;
        CollisionMask = 1u << 4; // camada 5 = Vehicle
        BodyEntered += OnBodyEntered;
    }

    private void OnBodyEntered(Node3D body)
    {
        if (_loading || body is not VanController van)
            return;
        // Só o jogador local ao volante (no online a história não existe).
        if (van.Driver == null || van.HasRemoteDriver)
            return;
        if (StoryManager.Instance == null || !StoryManager.Instance.CanTriggerTransition(RequiredChapter))
            return;
        if (string.IsNullOrEmpty(TargetScenePath) || !ResourceLoader.Exists(TargetScenePath))
        {
            GD.PushWarning($"ChapterTransitionZone: cena '{TargetScenePath}' não existe.");
            return;
        }
        if (ResourceLoader.LoadThreadedRequest(TargetScenePath) != Error.Ok)
        {
            GD.PushWarning($"ChapterTransitionZone: não deu para começar a carregar '{TargetScenePath}'.");
            return;
        }

        _van = van;
        _van.SetAutoDrive(true, AutoDriveSpeed);
        _banner = ChapterBanner.Show(this, ChapterNumberText, ChapterSubtitle, 0f);
        _loading = true;
        _elapsed = 0f;
    }

    public override void _Process(double delta)
    {
        if (!_loading)
            return;
        _elapsed += (float)delta;
        ResourceLoader.ThreadLoadStatus status = ResourceLoader.LoadThreadedGetStatus(TargetScenePath);

        if (status == ResourceLoader.ThreadLoadStatus.Failed || status == ResourceLoader.ThreadLoadStatus.InvalidResource)
        {
            GD.PushError($"ChapterTransitionZone: falhou ao carregar '{TargetScenePath}'.");
            Cancel();
            return;
        }
        if (status != ResourceLoader.ThreadLoadStatus.Loaded || _elapsed < MinimumBannerSeconds)
            return;

        _loading = false;
        var scene = (PackedScene)ResourceLoader.LoadThreadedGet(TargetScenePath);
        if (_van != null && IsInstanceValid(_van))
            _van.SetAutoDrive(false, 0f);
        Professions.CurrentLevel = TargetLevel;
        StoryManager.Instance?.AdvanceToChapter(NextChapter);
        GetTree().ChangeSceneToPacked(scene);
    }

    private void Cancel()
    {
        _loading = false;
        if (_van != null && IsInstanceValid(_van))
            _van.SetAutoDrive(false, 0f);
        _banner?.FadeOutAndFree();
        _banner = null;
    }
}
