using Godot;

namespace DeadFrequency;

/// <summary>Tela simples de carregamento com progresso baseado no trabalho concluído.</summary>
public partial class WorldLoadingScreen : CanvasLayer
{
    private Label _title;
    private Label _stage;
    private Label _detail;
    private ProgressBar _progress;
    private float _lastOverallProgress;

    public override void _Ready()
    {
        Layer = 100;
        ProcessMode = ProcessModeEnum.Always;

        var root = new Control { Name = "Root", MouseFilter = Control.MouseFilterEnum.Stop };
        root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        AddChild(root);

        var backdrop = new ColorRect
        {
            Name = "Backdrop",
            Color = new Color(0.025f, 0.035f, 0.05f, 0.97f),
            MouseFilter = Control.MouseFilterEnum.Stop,
        };
        backdrop.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        root.AddChild(backdrop);

        var center = new CenterContainer { Name = "Center" };
        center.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        root.AddChild(center);

        var panel = new PanelContainer
        {
            Name = "Panel",
            CustomMinimumSize = new Vector2(560f, 190f),
        };
        center.AddChild(panel);

        var layout = new VBoxContainer
        {
            Name = "Layout",
            Alignment = BoxContainer.AlignmentMode.Center,
        };
        layout.AddThemeConstantOverride("separation", 14);
        panel.AddChild(layout);

        _title = new Label
        {
            Text = "PREPARANDO O MUNDO",
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        _title.AddThemeFontSizeOverride("font_size", 25);
        _title.AddThemeColorOverride("font_color", new Color(0.88f, 0.92f, 1f));
        layout.AddChild(_title);

        _stage = new Label
        {
            Text = "Inicializando...",
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        _stage.AddThemeFontSizeOverride("font_size", 16);
        _stage.AddThemeColorOverride("font_color", new Color(0.72f, 0.78f, 0.86f));
        layout.AddChild(_stage);

        _progress = new ProgressBar
        {
            MinValue = 0,
            MaxValue = 100,
            Value = 0,
            ShowPercentage = true,
            CustomMinimumSize = new Vector2(480f, 22f),
        };
        layout.AddChild(_progress);

        _detail = new Label
        {
            Text = "0 / 0",
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        _detail.AddThemeFontSizeOverride("font_size", 13);
        _detail.AddThemeColorOverride("font_color", new Color(0.58f, 0.65f, 0.75f));
        layout.AddChild(_detail);
    }

    public void SetProgress(string stage, int completed, int total, float? overallRatio = null)
    {
        if (!IsInsideTree())
            return;

        int safeTotal = Mathf.Max(1, total);
        int safeCompleted = Mathf.Clamp(completed, 0, safeTotal);
        float localRatio = (float)safeCompleted / safeTotal;
        float requestedRatio = overallRatio.HasValue
            ? Mathf.Clamp(overallRatio.Value, 0f, 1f)
            : localRatio;

        _lastOverallProgress = Mathf.Max(_lastOverallProgress, requestedRatio);
        _stage.Text = stage;
        _detail.Text = $"{safeCompleted:N0} / {safeTotal:N0}";
        _progress.Value = _lastOverallProgress * 100.0;
    }

    public void SetFailure(string message)
    {
        if (!IsInsideTree())
            return;

        _title.Text = "NÃO FOI POSSÍVEL CARREGAR O MUNDO";
        _stage.Text = message;
        _detail.Text = "Consulte o log do jogo para mais detalhes.";
    }
}
