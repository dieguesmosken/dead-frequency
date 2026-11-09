// Scripts/UI/OptionsPanel.cs
// Opções com abas (Vídeo, Áudio, Controles, Interface, Jogo), usadas no menu principal e na pausa.
// Cada alteração é aplicada na hora; "Voltar" salva em user://settings.cfg e emite BackRequested.
// Na aba Controles dá para trocar as teclas: clique no botão da ação e aperte a tecla nova (Esc cancela).
using System;
using System.Collections.Generic;
using Godot;

namespace DeadFrequency;

public partial class OptionsPanel : VBoxContainer
{
    [Signal] public delegate void BackRequestedEventHandler();

    private static readonly string[] TabNames = { "Vídeo", "Áudio", "Controles", "Interface", "Jogo" };

    private Button _backButton;
    private readonly List<Button> _tabButtons = new();
    private readonly List<Control> _pages = new();
    private VBoxContainer _current;
    private ScrollContainer _scroll;
    private static int _lastTab;

    // Troca de tecla em andamento.
    private string _waitingAction;
    private Button _waitingButton;
    private readonly Dictionary<string, Button> _keyButtons = new();

    public override void _Ready()
    {
        AddThemeConstantOverride("separation", 10);
        CustomMinimumSize = new Vector2(780, 0);

        AddChild(UiStyle.MakeLabel("OPÇÕES", 30, HorizontalAlignment.Left, UiStyle.Warning));

        GameSettings s = GameSettings.Instance;
        if (s == null)
        {
            AddChild(UiStyle.MakeLabel("GameSettings não carregado (autoload ausente).", 16, color: UiStyle.Danger));
            AddBackRow();
            return;
        }

        var tabs = new HBoxContainer();
        tabs.AddThemeConstantOverride("separation", 6);
        AddChild(tabs);
        var group = new ButtonGroup();
        for (int i = 0; i < TabNames.Length; i++)
        {
            int index = i;
            var b = new Button { Text = TabNames[i], ToggleMode = true, ButtonGroup = group, CustomMinimumSize = new Vector2(0, 42), SizeFlagsHorizontal = SizeFlags.ExpandFill };
            b.AddThemeFontSizeOverride("font_size", 17);
            b.MouseEntered += () => AudioManager.Instance?.PlayUi(UiSound.Hover);
            b.Pressed += () => ShowTab(index, true);
            _tabButtons.Add(b);
            tabs.AddChild(b);
        }

        _scroll = new ScrollContainer { CustomMinimumSize = new Vector2(780, 430), HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        AddChild(_scroll);
        var holder = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _scroll.AddChild(holder);
        for (int i = 0; i < TabNames.Length; i++)
        {
            var page = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, Visible = false };
            page.AddThemeConstantOverride("separation", 6);
            holder.AddChild(page);
            _pages.Add(page);
        }

        BuildVideo(s, (VBoxContainer)_pages[0]);
        BuildAudio(s, (VBoxContainer)_pages[1]);
        BuildControls(s, (VBoxContainer)_pages[2]);
        BuildInterface(s, (VBoxContainer)_pages[3]);
        BuildGame(s, (VBoxContainer)_pages[4]);

        AddBackRow();
        ShowTab(Mathf.Clamp(_lastTab, 0, TabNames.Length - 1), false);
    }

    private void AddBackRow()
    {
        var row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        row.AddThemeConstantOverride("separation", 12);
        if (GameSettings.Instance != null)
            row.AddChild(UiStyle.MakeButton("Restaurar padrão", OnReset, 240));
        _backButton = UiStyle.MakeButton("Voltar", OnBack, 240, UiSound.Back);
        row.AddChild(_backButton);
        AddChild(row);
    }

    private void ShowTab(int index, bool sound)
    {
        if (sound)
            AudioManager.Instance?.PlayUi(UiSound.Click);
        _lastTab = index;
        for (int i = 0; i < _pages.Count; i++)
            _pages[i].Visible = i == index;
        _tabButtons[index].ButtonPressed = true;
        if (_scroll != null)
            _scroll.ScrollVertical = 0;
        CancelRebind();
    }

    public void FocusFirst()
    {
        if (_tabButtons.Count > 0)
            _tabButtons[_lastTab].GrabFocus();
        else
            _backButton?.GrabFocus();
    }

    private void OnBack()
    {
        CancelRebind();
        GameSettings.Instance?.Save();
        EmitSignal(SignalName.BackRequested);
    }

    private void OnReset()
    {
        GameSettings s = GameSettings.Instance;
        s.ResetToDefaults();
        s.Apply();
        s.Save();
        // Remonta as abas com os valores padrão.
        int tab = _lastTab;
        foreach (Node c in GetChildren())
            c.QueueFree();
        _tabButtons.Clear();
        _pages.Clear();
        _keyButtons.Clear();
        _lastTab = tab;
        Callable.From(_Ready).CallDeferred();
    }

    // ================================================================ abas

    private void BuildVideo(GameSettings s, VBoxContainer p)
    {
        Section(p, "TELA");
        Choice(p, "Modo de janela", new[] { "Janela", "Tela cheia (sem borda)", "Tela cheia exclusiva", "Janela maximizada" }, s.WindowMode, i => s.WindowMode = i,
            "Tela cheia exclusiva costuma dar menos atraso; sem borda troca de janela mais rápido (Alt+Tab).");
        Toggle(p, "V-Sync", s.VSync, b => s.VSync = b, "Trava no ritmo do monitor e evita cortes na imagem; desligue para menos atraso.");
        int[] fpsValues = { 0, 30, 60, 90, 120, 144, 165, 240 };
        Choice(p, "Limite de FPS", new[] { "Sem limite", "30", "60", "90", "120", "144", "165", "240" }, Math.Max(0, Array.IndexOf(fpsValues, s.MaxFps)), i => s.MaxFps = fpsValues[i],
            "Limitar economiza bateria e deixa o PC mais silencioso.");
        Slider(p, "Resolução 3D", 0.5, 1.0, 0.05, s.RenderScale, v => s.RenderScale = (float)v, v => $"{v * 100:0}%",
            "Desenha o mundo numa resolução menor (a interface continua nítida). Ótimo para PCs fracos — e combina com o visual PS1.");

        Section(p, "GRÁFICOS");
        Choice(p, "Qualidade", new[] { "Baixa", "Média", "Alta" }, s.Quality, i => s.Quality = i, "Baixa tira as sombras e o brilho; Média encurta as sombras.");
        Choice(p, "Antisserrilhado", new[] { "Desligado", "FXAA", "MSAA 2x", "MSAA 4x" }, s.AntiAliasing, i => s.AntiAliasing = i,
            "Suaviza os serrilhados das bordas. MSAA é mais bonito e mais pesado.");
        Slider(p, "Campo de visão", 60, 110, 1, s.Fov, v => s.Fov = (float)v, v => $"{v:0}°", "Quanto do mundo aparece na tela.");
        Slider(p, "Brilho", 0.6, 1.6, 0.05, s.Brightness, v => s.Brightness = (float)v, v => $"{v * 100:0}%", "Clareie se as noites estiverem escuras demais no seu monitor.");
        Slider(p, "Contraste", 0.7, 1.4, 0.05, s.Contrast, v => s.Contrast = (float)v, v => $"{v * 100:0}%");
    }

    private void BuildAudio(GameSettings s, VBoxContainer p)
    {
        Section(p, "VOLUME");
        Func<double, string> pct = v => $"{v * 100:0}%";
        Slider(p, "Geral", 0, 1, 0.01, s.MasterVolume, v => s.MasterVolume = (float)v, pct);
        Slider(p, "Música", 0, 1, 0.01, s.MusicVolume, v => s.MusicVolume = (float)v, pct, "Trilha do jogo e das rádios.");
        Slider(p, "Rádio 96.6", 0, 1.5, 0.01, s.RadioVolume, v => s.RadioVolume = (float)v, pct, "Rádios da van, do escritório, do mercado e dos apartamentos.");
        Slider(p, "Efeitos", 0, 1, 0.01, s.SfxVolume, v => s.SfxVolume = (float)v, pct, "Motor, batidas, passos, ônibus, orelhões...");
        Slider(p, "Interface", 0, 1, 0.01, s.UiVolume, v => s.UiVolume = (float)v, pct, "Cliques e sons dos menus.");
        Section(p, "OUTROS");
        Toggle(p, "Silenciar em segundo plano", s.MuteOnFocusLoss, b => s.MuteOnFocusLoss = b, "Tira o som quando você troca de janela (Alt+Tab).");
    }

    private void BuildControls(GameSettings s, VBoxContainer p)
    {
        Section(p, "MOUSE");
        Slider(p, "Sensibilidade", 0.1, 3.0, 0.05, s.MouseSensitivity, v => s.MouseSensitivity = (float)v, v => $"{v:0.00}x");
        Slider(p, "Sensibilidade na van", 0.2, 2.0, 0.05, s.VehicleSensitivity, v => s.VehicleSensitivity = (float)v, v => $"{v:0.00}x", "Multiplica a sensibilidade quando você está dirigindo.");
        Toggle(p, "Inverter eixo Y", s.InvertY, b => s.InvertY = b);
        p.AddChild(Hint("Pressione V durante a partida para alternar entre primeira e terceira pessoa. A terceira pessoa é ativada por padrão."));
        Toggle(p, "Balanço da câmera ao andar", s.HeadBob, b => s.HeadBob = b, "Desativado por padrão para reduzir movimentos visuais. Ative apenas se preferir esse efeito.");

        Section(p, "TECLAS");
        p.AddChild(Hint("Clique no botão e aperte a tecla nova (ou um botão do mouse). Esc cancela."));
        foreach ((string action, string label) in GameSettings.RebindableActions)
        {
            HBoxContainer row = Row(p, label);
            string a = action;
            var b = new Button { Text = GameSettings.KeyName(action), CustomMinimumSize = new Vector2(260, 38), SizeFlagsHorizontal = SizeFlags.ShrinkEnd };
            b.AddThemeFontSizeOverride("font_size", 16);
            b.MouseEntered += () => AudioManager.Instance?.PlayUi(UiSound.Hover);
            b.Pressed += () => StartRebind(a, b);
            _keyButtons[action] = b;
            row.AddChild(b);
        }
        Button reset = UiStyle.MakeButton("Teclas padrão", () =>
        {
            GameSettings.Instance.ResetKeys();
            foreach ((string action, Button btn) in _keyButtons)
                btn.Text = GameSettings.KeyName(action);
        }, 220);
        reset.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
        p.AddChild(reset);
    }

    private void BuildInterface(GameSettings s, VBoxContainer p)
    {
        Section(p, "HUD");
        Slider(p, "Escala da interface", 0.75, 1.5, 0.05, s.UiScale, v => s.UiScale = (float)v, v => $"{v * 100:0}%", "Aumenta ou diminui menus, textos e o painel.");
        Toggle(p, "Mira", s.ShowCrosshair, b => s.ShowCrosshair = b);
        Toggle(p, "Seta de objetivo no topo", s.ShowCompass, b => s.ShowCompass = b, "A seta muda com a profissão: rádio, mercado, posto, depósito...");
        Toggle(p, "Dicas de teclas no rodapé", s.ShowKeyHints, b => s.ShowKeyHints = b);
        Toggle(p, "Mostrar FPS", s.ShowFps, b => s.ShowFps = b);
        Slider(p, "Tempo dos avisos", 0.5, 2.5, 0.1, s.ToastDuration, v => s.ToastDuration = (float)v, v => $"{v:0.0}x", "Quanto tempo as mensagens ficam na tela.");
        Section(p, "VISUAL");
        Toggle(p, "Linhas de CRT", s.Scanlines, b => s.Scanlines = b, "Efeito de tela de tubo por cima dos menus e do HUD.");
        Choice(p, "Velocímetro", new[] { "km/h", "mph" }, s.SpeedUnit, i => s.SpeedUnit = i);
        Choice(p, "Relógio", new[] { "24 horas", "12 horas (AM/PM)" }, s.Clock24h ? 0 : 1, i => s.Clock24h = i == 0);
    }

    private void BuildGame(GameSettings s, VBoxContainer p)
    {
        Section(p, "MUNDO");
        Choice(p, "Horário", new[] { "Ciclo dia/noite", "Sempre noite", "Sempre dia" }, s.TimeMode, i => s.TimeMode = i, "No online o horário é o mesmo para todos (relógio do sistema).");
        Slider(p, "Duração do dia", 6, 60, 1, s.DayLengthMinutes, v => s.DayLengthMinutes = (float)v, v => $"{v:0} min", "Minutos reais para 24 horas no jogo.");
        Section(p, "PARTIDA");
        Toggle(p, "Pausar ao trocar de janela", s.PauseOnFocusLoss, b => s.PauseOnFocusLoss = b, "Só no solo; no online o mundo continua.");
        Toggle(p, "Câmera estável no carro", s.StableVehicleCamera, b => s.StableVehicleCamera = b, "O horizonte não balança com a carroceria (evita enjoo/vertigem).");
        Toggle(p, "Procurar atualizações ao abrir", s.CheckUpdates, b => s.CheckUpdates = b, "Pergunta ao GitHub se saiu versão nova (nenhum dado seu é enviado).");
        SaveData save = SaveManager.Load();
        p.AddChild(Hint(save == null ? "Ainda não há save: durma na cama do seu apartamento (Edifício Sintonia) para salvar."
            : $"Último save: {save.SavedAt} · {Professions.Get(save.Profession).Name} · turno {save.Shift} · {save.TotalDelivered} entregas · {save.PlayMinutes:0} min jogados"));
    }

    // ================================================================ trocar tecla

    private void StartRebind(string action, Button button)
    {
        CancelRebind();
        AudioManager.Instance?.PlayUi(UiSound.Click);
        _waitingAction = action;
        _waitingButton = button;
        button.Text = "aperte uma tecla...";
    }

    private void CancelRebind()
    {
        if (_waitingButton != null && IsInstanceValid(_waitingButton))
            _waitingButton.Text = GameSettings.KeyName(_waitingAction);
        _waitingAction = null;
        _waitingButton = null;
    }

    public override void _Input(InputEvent @event)
    {
        if (_waitingAction == null || !IsVisibleInTree())
            return;
        if (@event is InputEventKey { Pressed: true, Echo: false } key)
        {
            GetViewport().SetInputAsHandled();
            if (key.Keycode == Key.Escape && _waitingAction != "pause")
            {
                CancelRebind();
                return;
            }
            Apply(new InputEventKey { PhysicalKeycode = key.PhysicalKeycode != Key.None ? key.PhysicalKeycode : key.Keycode });
        }
        else if (@event is InputEventMouseButton { Pressed: true } mouse && mouse.ButtonIndex is not (MouseButton.WheelUp or MouseButton.WheelDown))
        {
            GetViewport().SetInputAsHandled();
            Apply(new InputEventMouseButton { ButtonIndex = mouse.ButtonIndex });
        }
    }

    private void Apply(InputEvent e)
    {
        string action = _waitingAction;
        GameSettings.Instance.Rebind(action, e);
        _waitingButton.Text = GameSettings.KeyName(action);
        _waitingAction = null;
        _waitingButton = null;
        AudioManager.Instance?.PlayUi(UiSound.Click);
    }

    // ================================================================ peças

    private static void Section(VBoxContainer p, string title)
    {
        p.AddChild(new Control { CustomMinimumSize = new Vector2(0, 6) });
        p.AddChild(UiStyle.MakeLabel(title, 15, HorizontalAlignment.Left, new Color(0.4f, 0.95f, 1f)));
    }

    private static Label Hint(string text)
    {
        Label l = UiStyle.MakeLabel(text, 13, HorizontalAlignment.Left, UiStyle.PhosphorDim);
        l.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        l.CustomMinimumSize = new Vector2(740, 0);
        return l;
    }

    private static HBoxContainer Row(VBoxContainer p, string text)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 16);
        Label name = UiStyle.MakeLabel(text, 18, HorizontalAlignment.Left);
        name.CustomMinimumSize = new Vector2(300, 0);
        name.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        row.AddChild(name);
        p.AddChild(row);
        return row;
    }

    private static void Describe(VBoxContainer p, string description)
    {
        if (!string.IsNullOrEmpty(description))
            p.AddChild(Hint(description));
    }

    private static void Slider(VBoxContainer p, string text, double min, double max, double step, double value, Action<double> apply, Func<double, string> format, string description = null)
    {
        HBoxContainer row = Row(p, text);
        var slider = new HSlider
        {
            MinValue = min,
            MaxValue = max,
            Step = step,
            Value = value,
            CustomMinimumSize = new Vector2(300, 24),
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
        };
        Label valueLabel = UiStyle.MakeLabel(format(value), 17, HorizontalAlignment.Right);
        valueLabel.CustomMinimumSize = new Vector2(80, 0);
        slider.ValueChanged += v =>
        {
            apply(v);
            valueLabel.Text = format(v);
            GameSettings.Instance?.Apply();
        };
        row.AddChild(slider);
        row.AddChild(valueLabel);
        Describe(p, description);
    }

    private static void Choice(VBoxContainer p, string text, string[] options, int selected, Action<int> apply, string description = null)
    {
        HBoxContainer row = Row(p, text);
        var option = new OptionButton { CustomMinimumSize = new Vector2(396, 38) };
        option.AddThemeFontSizeOverride("font_size", 16);
        foreach (string o in options)
            option.AddItem(o);
        option.Selected = Mathf.Clamp(selected, 0, options.Length - 1);
        option.ItemSelected += i =>
        {
            AudioManager.Instance?.PlayUi(UiSound.Click);
            apply((int)i);
            GameSettings.Instance?.Apply();
        };
        row.AddChild(option);
        Describe(p, description);
    }

    private static void Toggle(VBoxContainer p, string text, bool value, Action<bool> apply, string description = null)
    {
        HBoxContainer row = Row(p, text);
        var toggle = new CheckButton { ButtonPressed = value, CustomMinimumSize = new Vector2(80, 34) };
        toggle.MouseEntered += () => AudioManager.Instance?.PlayUi(UiSound.Hover);
        toggle.Toggled += pressed =>
        {
            AudioManager.Instance?.PlayUi(UiSound.Click);
            apply(pressed);
            GameSettings.Instance?.Apply();
        };
        row.AddChild(toggle);
        Describe(p, description);
    }
}
