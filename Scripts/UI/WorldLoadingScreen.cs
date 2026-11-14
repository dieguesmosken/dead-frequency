// Scripts/UI/WorldLoadingScreen.cs
// Tela de carregamento no tema CRT verde (v0.10.1): fundo preto com linhas de varredura, barra de blocos,
// cursor piscando e uma dica de rua trocando a cada poucos segundos. Serve ao mundo (CityGenerator),
// ao menu (SceneLoader) e ao Modo História (título do capítulo no cabeçalho).
using Godot;

namespace DeadFrequency;

public partial class WorldLoadingScreen : CanvasLayer
{
    private const int BarCells = 36;
    private const float TipSeconds = 5f;

    private static readonly string[] Tips =
    {
        "Rádio 96.6 FM: a única coisa que pega aqui atrás da serra.",
        "Caixa que cai do caminhão perde valor. Amarra bem a carga.",
        "O orelhão ainda funciona. Leva moeda.",
        "Tá cansado? Senta num banco e respira um pouco.",
        "O caramelo da esquina segue quem tem comida no bolso.",
        "De noite o ônibus passa menos. Confere o horário no ponto.",
        "Chinelo aguenta calçada quente, mas não aguenta pressa.",
        "Cuidado com a faixa: pedestre tem preferência.",
        "No mercado, o troco sempre sai em moeda de 50.",
        "Sem sinal de celular por aqui. Só rádio e boa conversa.",
    };

    private Label _header;
    private Label _title;
    private Label _stage;
    private Label _bar;
    private Label _detail;
    private Label _tip;
    private Label _cursor;
    private float _lastOverallProgress;
    private float _time;
    private int _tipIndex;
    private bool _failed;

    public override void _Ready()
    {
        Layer = 100;
        ProcessMode = ProcessModeEnum.Always;

        var root = new Control { Name = "Root", MouseFilter = Control.MouseFilterEnum.Stop, Theme = UiStyle.Theme };
        root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        AddChild(root);

        var backdrop = new ColorRect
        {
            Name = "Backdrop",
            Color = new Color(0.005f, 0.02f, 0.01f),
            MouseFilter = Control.MouseFilterEnum.Stop,
        };
        backdrop.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        root.AddChild(backdrop);

        var center = new CenterContainer { Name = "Center" };
        center.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        root.AddChild(center);

        var panel = new PanelContainer { Name = "Panel", CustomMinimumSize = new Vector2(640f, 260f) };
        center.AddChild(panel);

        var layout = new VBoxContainer { Name = "Layout", Alignment = BoxContainer.AlignmentMode.Center };
        layout.AddThemeConstantOverride("separation", 12);
        panel.AddChild(layout);

        _header = UiStyle.MakeLabel("", 15, color: UiStyle.PhosphorDim);
        _header.Visible = false;
        layout.AddChild(_header);

        _title = UiStyle.MakeLabel("PREPARANDO O MUNDO", 28);
        layout.AddChild(_title);

        _stage = UiStyle.MakeLabel("Inicializando...", 16, color: UiStyle.PhosphorDim);
        layout.AddChild(_stage);

        _bar = UiStyle.MakeLabel("", 22);
        layout.AddChild(_bar);

        _detail = UiStyle.MakeLabel("0 / 0", 13, color: UiStyle.PhosphorDim);
        layout.AddChild(_detail);

        layout.AddChild(new Control { CustomMinimumSize = new Vector2(0f, 8f) });

        _tip = UiStyle.MakeLabel("> " + Tips[0], 14, color: UiStyle.Phosphor);
        _tip.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _tip.CustomMinimumSize = new Vector2(560f, 0f);
        layout.AddChild(_tip);

        _cursor = UiStyle.MakeLabel("", 14);
        _cursor.HorizontalAlignment = HorizontalAlignment.Left;
        layout.AddChild(_cursor);

        root.AddChild(UiStyle.MakeScanlines(0.3f));

        _tipIndex = (int)(GD.Randi() % (uint)Tips.Length);
        _tip.Text = "> " + Tips[_tipIndex];
        DrawBar(0f);
    }

    public override void _Process(double delta)
    {
        _time += (float)delta;
        if (_cursor != null)
            _cursor.Text = (int)(_time * 2f) % 2 == 0 ? "█" : " ";
        if (!_failed && _tip != null && _time >= TipSeconds * (_tipsShown + 1))
        {
            _tipsShown++;
            _tipIndex = (_tipIndex + 1) % Tips.Length;
            _tip.Text = "> " + Tips[_tipIndex];
        }
    }

    private int _tipsShown;

    /// <summary>Linha pequena acima do título (ex.: "CAPÍTULO II · Cidade") e título grande (ex.: nome do capítulo).</summary>
    public void SetHeading(string header, string title)
    {
        if (!string.IsNullOrEmpty(header))
        {
            _header.Text = header;
            _header.Visible = true;
        }
        if (!string.IsNullOrEmpty(title))
            _title.Text = title;
    }

    private void DrawBar(float ratio)
    {
        int filled = Mathf.Clamp(Mathf.RoundToInt(ratio * BarCells), 0, BarCells);
        _bar.Text = "[" + new string('█', filled) + new string('░', BarCells - filled) + $"] {Mathf.RoundToInt(ratio * 100f),3}%";
    }

    public void SetProgress(string stage, int completed, int total, float? overallRatio = null)
    {
        if (!IsInsideTree() || _failed)
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
        DrawBar(_lastOverallProgress);
    }

    public void SetFailure(string message)
    {
        if (!IsInsideTree())
            return;

        _failed = true;
        _title.Text = "NÃO FOI POSSÍVEL CARREGAR O MUNDO";
        _title.AddThemeColorOverride("font_color", UiStyle.Danger);
        _stage.Text = message;
        _detail.Text = "Consulte o log do jogo para mais detalhes.";
        _tip.Text = "";
    }
}
