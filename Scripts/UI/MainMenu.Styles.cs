// Scripts/UI/MainMenu.Styles.cs
// Visuais do menu inicial liberados por código (MenuStyles, v0.9.9). Os protótipos aprovados:
//   Órbita   → cidade à noite vista do alto, título e menu alinhados à esquerda com sombra lateral;
//   Cinema   → faixas pretas de filme, título no meio da cena e o menu numa linha na faixa de baixo;
//   Terminal → tela de despacho do depósito: moldura de fósforo verde, itens com ">" e linha de status.
// O fundo é uma captura da própria cidade com um "passeio de câmera" lento (zoom e deslize suaves),
// leve para PC fraco: nada de cidade 3D carregada atrás do menu.
using Godot;

namespace DeadFrequency;

public partial class MainMenu
{
    private Label _radioLine;
    private Label _statusLine;
    private double _radioTimer;

    private int TitleSize => _style switch
    {
        MenuStyle.Orbita => 64,
        MenuStyle.Cinema => 80,
        MenuStyle.Terminal => 54,
        _ when GameSettings.IsMobile => MobileTitleSize,
        _ => 72,
    };

    private void ApplyStyle()
    {
        if (_style == MenuStyle.Classico)
            return;
        AddBackground();
        switch (_style)
        {
            case MenuStyle.Orbita: StyleOrbita(); break;
            case MenuStyle.Cinema: StyleCinema(); break;
            case MenuStyle.Terminal: StyleTerminal(); break;
        }
        _title.AddThemeFontSizeOverride("font_size", TitleSize);
        UpdateRadioLine();
    }

    /// <summary>Captura da cidade cobrindo a tela, com zoom e deslize lentos indo e voltando.</summary>
    private void AddBackground()
    {
        string path = MenuStyles.Background(_style);
        if (path == null || !ResourceLoader.Exists(path))
            return;
        var tex = new TextureRect
        {
            Texture = GD.Load<Texture2D>(path),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
            MouseFilter = MouseFilterEnum.Ignore,
            TextureFilter = TextureFilterEnum.Nearest,
        };
        tex.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(tex);
        MoveChild(tex, _background.GetIndex() + 1);
        // Começa no tamanho da tela; o pivô vai para o centro quando o tamanho for conhecido.
        tex.Resized += () => tex.PivotOffset = tex.Size * 0.5f;
        Tween tw = CreateTween().SetLoops().SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
        tw.TweenProperty(tex, "scale", new Vector2(1.08f, 1.08f), 38.0);
        tw.Parallel().TweenProperty(tex, "position", new Vector2(-40f, -12f), 38.0);
        tw.TweenProperty(tex, "scale", Vector2.One, 38.0);
        tw.Parallel().TweenProperty(tex, "position", Vector2.Zero, 38.0);
    }

    private Label AddRadioLine(LayoutPreset corner, int margin)
    {
        _radioLine = UiStyle.MakeLabel("", 16, HorizontalAlignment.Left, UiStyle.Warning);
        AddChild(_radioLine);
        _radioLine.SetAnchorsAndOffsetsPreset(corner, LayoutPresetMode.Minsize, margin);
        return _radioLine;
    }

    // ---------------------------------------------------------------- Órbita

    private void StyleOrbita()
    {
        // Sombra da esquerda para o meio: o menu fica legível em cima da cidade.
        var shade = new TextureRect
        {
            Texture = new GradientTexture2D
            {
                Gradient = new Gradient { Colors = new[] { new Color(0.01f, 0.03f, 0.015f, 0.92f), new Color(0.01f, 0.03f, 0.015f, 0f) } },
                Width = 256,
                Height = 4,
            },
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        shade.SetAnchorsPreset(LayoutPreset.LeftWide);
        shade.AnchorRight = 0.6f;
        AddChild(shade);
        MoveChild(shade, _center.GetIndex());

        // O conteúdo vai para a metade esquerda, alinhado à esquerda, com margem.
        _center.AnchorRight = 0.55f;
        _center.OffsetLeft = 72f;
        _stack.AddThemeConstantOverride("separation", 10);
        _menu.AddThemeConstantOverride("separation", 8);
        _title.HorizontalAlignment = HorizontalAlignment.Left;
        foreach (Node n in _header.GetChildren())
            if (n is Label l)
                l.HorizontalAlignment = HorizontalAlignment.Left;
        if (_header.GetChildCount() > 1 && _header.GetChild(0) is Label sub && _header.GetChild(1) is Label sub2)
        {
            sub.Text = "entregas na frequência morta";
            sub2.Visible = false;
        }
        foreach (Node n in _menu.GetChildren())
            if (n is Button b)
            {
                b.SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
                b.Alignment = HorizontalAlignment.Left;
            }
        AddRadioLine(LayoutPreset.BottomLeft, 16).OffsetTop -= 30;
        _radioLine.OffsetBottom -= 30;
    }

    // ---------------------------------------------------------------- Cinema

    private void StyleCinema()
    {
        // Faixas pretas de filme (cima menor, baixo com o menu).
        var top = new ColorRect { Color = Colors.Black, MouseFilter = MouseFilterEnum.Ignore };
        top.SetAnchorsPreset(LayoutPreset.TopWide);
        top.OffsetBottom = 64;
        var bottom = new ColorRect { Color = Colors.Black, MouseFilter = MouseFilterEnum.Ignore };
        bottom.SetAnchorsPreset(LayoutPreset.BottomWide);
        bottom.OffsetTop = -150;
        AddChild(top);
        AddChild(bottom);
        MoveChild(top, _center.GetIndex());
        MoveChild(bottom, _center.GetIndex());

        // Menu numa linha só, dentro da faixa de baixo.
        _stack.RemoveChild(_menu);
        _menu.Vertical = false;
        _menu.AddThemeConstantOverride("separation", 18);
        var bar = new CenterContainer { MouseFilter = MouseFilterEnum.Ignore };
        bar.SetAnchorsPreset(LayoutPreset.BottomWide);
        bar.OffsetTop = -150;
        bar.OffsetBottom = -54;
        AddChild(bar);
        bar.AddChild(_menu);
        foreach (Node n in _menu.GetChildren())
            if (n is Button b)
            {
                b.CustomMinimumSize = new Vector2(0, 44);
                b.AddThemeFontSizeOverride("font_size", 18);
            }

        if (_header.GetChildCount() > 1 && _header.GetChild(0) is Label sub && _header.GetChild(1) is Label sub2)
        {
            sub.Text = "UMA CIDADE  ·  UMA VAN  ·  UM RÁDIO QUE TOCA SOZINHO";
            sub.AddThemeColorOverride("font_color", new Color(0.95f, 0.93f, 0.85f));
            sub2.Visible = false;
        }
        // Contorno escuro: o título fica legível em cima do letreiro da central ao fundo.
        _title.AddThemeConstantOverride("outline_size", 10);
        _title.AddThemeColorOverride("font_outline_color", new Color(0f, 0f, 0f, 0.85f));
        AddRadioLine(LayoutPreset.TopLeft, 20);
    }

    // ---------------------------------------------------------------- Terminal

    private void StyleTerminal()
    {
        // Cidade escurecida atrás da tela de despacho.
        var dim = new ColorRect { Color = new Color(0f, 0.02f, 0.01f, 0.45f), MouseFilter = MouseFilterEnum.Ignore };
        dim.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(dim);
        MoveChild(dim, _center.GetIndex());

        // Moldura de fósforo em volta do conteúdo.
        var frame = new PanelContainer();
        frame.AddThemeStyleboxOverride("panel", UiStyle.Box(new Color(0.01f, 0.04f, 0.02f, 0.9f), UiStyle.Phosphor, 2, 28));
        _center.RemoveChild(_stack);
        frame.AddChild(_stack);
        _center.AddChild(frame);

        if (_header.GetChildCount() > 1 && _header.GetChild(0) is Label sub && _header.GetChild(1) is Label sub2)
        {
            sub.Text = "TERMINAL DE DESPACHO  ·  DEPÓSITO 966";
            sub2.Visible = false;
        }
        // Itens como linhas do terminal: "  ITEM" e "> ITEM" no selecionado.
        foreach (Node n in _menu.GetChildren())
        {
            if (n is not Button b)
                continue;
            string label = b.Text.ToUpperInvariant();
            b.Text = "  " + label;
            b.Alignment = HorizontalAlignment.Left;
            b.SizeFlagsHorizontal = SizeFlags.Fill;
            b.Flat = true;
            b.FocusEntered += () => b.Text = "> " + label + " █";
            b.FocusExited += () => b.Text = "  " + label;
            b.MouseEntered += () => b.GrabFocus();
        }
        _statusLine = UiStyle.MakeLabel("", 14, HorizontalAlignment.Left, UiStyle.PhosphorDim);
        _stack.AddChild(_statusLine);
        _radioLine = UiStyle.MakeLabel("", 14, HorizontalAlignment.Left, UiStyle.Warning);
        _stack.AddChild(_radioLine);
    }

    // ---------------------------------------------------------------- atualização

    private void UpdateStyle(double delta)
    {
        if (_style == MenuStyle.Classico)
            return;
        _radioTimer -= delta;
        if (_radioTimer <= 0)
            UpdateRadioLine();
    }

    private void UpdateRadioLine()
    {
        _radioTimer = 2.0;
        if (_radioLine != null && IsInstanceValid(_radioLine))
            _radioLine.Text = "♪ " + RadioStation.NowPlayingText();
        if (_statusLine != null && IsInstanceValid(_statusLine))
        {
            var t = Time.GetTimeDictFromSystem();
            _statusLine.Text = $"sinal: ▂▃▅ fraco  ·  {(int)t["hour"]:00}:{(int)t["minute"]:00}  ·  neblina densa";
        }
    }
}
