// Scripts/UI/MainMenu.cs
// Tela de título. Montada em código (ver UiStyle); a cena main_menu.tscn só tem este nó.
using Godot;

namespace DeadFrequency;

public partial class MainMenu : Control
{
    [Export(PropertyHint.File, "*.tscn")]
    public string GameScene { get; set; } = "res://Scenes/Levels/test_level.tscn";

    [Export(PropertyHint.File, "*.wav,*.ogg")]
    public string MenuMusic { get; set; } = "res://Audio/Music/music_menu.wav";

    private Label _title;
    private VBoxContainer _menu;
    private OptionsPanel _options;
    private PanelContainer _optionsPanel;
    private PanelContainer _characterPanel;
    private CharacterPanel _character;
    private PanelContainer _multiplayerPanel;
    private MultiplayerPanel _multiplayer;
    private Button _startButton;
    private PanelContainer _infoPanel;
    private VBoxContainer _levelPanel;
    private VBoxContainer _profPanel;
    private VBoxContainer _header;
    private HBoxContainer _updateBox;
    private InfoPanel _info;
    private double _glitchTimer;

    public override void _Ready()
    {
        GetTree().Paused = false;
        Input.MouseMode = Input.MouseModeEnum.Visible;
        if (NetClient.Instance?.Status != NetClient.ConnectionStatus.Offline)
            NetClient.Instance?.Leave();
        // Voltou ao menu: a sala hospedada no PC (se havia) fecha junto.
        LocalServer.Instance?.Stop();
        TunnelService.Instance?.Stop();
        AudioManager.Instance?.SetMuffled(false);
        AudioManager.Instance?.StopAmbience(1f);
        AudioManager.Instance?.PlayMusic(MenuMusic, 1.5f);

        Theme = UiStyle.Theme;
        SetAnchorsPreset(LayoutPreset.FullRect);

        var background = new ColorRect { Color = new Color(0.015f, 0.025f, 0.02f), MouseFilter = MouseFilterEnum.Ignore };
        background.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(background);

        var center = new CenterContainer { MouseFilter = MouseFilterEnum.Ignore };
        center.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(center);

        var stack = new VBoxContainer();
        stack.AddThemeConstantOverride("separation", 14);
        center.AddChild(stack);

        _title = UiStyle.MakeLabel("DEAD FREQUENCY", 72);
        stack.AddChild(_title);
        _header = new VBoxContainer();
        _header.AddThemeConstantOverride("separation", 14);
        stack.AddChild(_header);
        _header.AddChild(UiStyle.MakeLabel("cidade simulada na zona morta · 1996", 20, color: UiStyle.PhosphorDim));
        _header.AddChild(UiStyle.MakeLabel("escolha sua profissão, sintonize a 96.6 e boa sorte", 16, color: UiStyle.PhosphorDim));
        _header.AddChild(new Control { CustomMinimumSize = new Vector2(0, 30) });

        _menu = new VBoxContainer();
        _menu.AddThemeConstantOverride("separation", 12);
        stack.AddChild(_menu);

        _startButton = UiStyle.MakeButton("Iniciar turno", ShowLevels, sound: UiSound.Start);
        _startButton.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
        _menu.AddChild(_startButton);

        Button multiplayer = UiStyle.MakeButton("Multiplayer", ShowMultiplayer);
        multiplayer.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
        _menu.AddChild(multiplayer);

        Button character = UiStyle.MakeButton("Personagem", ShowCharacter);
        character.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
        _menu.AddChild(character);

        Button options = UiStyle.MakeButton("Opções", ShowOptions);
        options.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
        _menu.AddChild(options);

        Button news = UiStyle.MakeButton("Novidades", () => ShowInfo(false));
        news.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
        _menu.AddChild(news);

        Button credits = UiStyle.MakeButton("Créditos", ShowCredits);
        credits.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
        _menu.AddChild(credits);

        Button quit = UiStyle.MakeButton("Sair", () => GetTree().Quit());
        quit.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
        _menu.AddChild(quit);

        _optionsPanel = new PanelContainer { Visible = false };
        _options = new OptionsPanel();
        _optionsPanel.AddChild(_options);
        _options.BackRequested += ShowMenu;
        stack.AddChild(_optionsPanel);

        _characterPanel = new PanelContainer { Visible = false };
        _character = new CharacterPanel();
        _characterPanel.AddChild(_character);
        _character.BackRequested += ShowMenu;
        stack.AddChild(_characterPanel);

        _multiplayerPanel = new PanelContainer { Visible = false };
        _multiplayer = new MultiplayerPanel();
        _multiplayerPanel.AddChild(_multiplayer);
        _multiplayer.BackRequested += ShowMenu;
        _multiplayer.JoinedRoom += () => GetTree().ChangeSceneToFile(GameLevels.Get(NetClient.Instance?.Level ?? "city").Scene);
        stack.AddChild(_multiplayerPanel);

        // Escolha de fase (solo).
        _levelPanel = new VBoxContainer { Visible = false };
        _levelPanel.AddThemeConstantOverride("separation", 12);
        _levelPanel.AddChild(UiStyle.MakeLabel("ESCOLHA A FASE", 30));
        foreach (GameLevels.Level level in GameLevels.All)
        {
            GameLevels.Level lv = level;
            Button b = UiStyle.MakeButton(level.Name, () => ShowProfessions(lv), 420, UiSound.Start);
            b.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
            _levelPanel.AddChild(b);
            _levelPanel.AddChild(UiStyle.MakeLabel(level.Subtitle, 15, color: UiStyle.PhosphorDim));
        }
        Button levelBack = UiStyle.MakeButton("Voltar", ShowMenu, sound: UiSound.Back);
        levelBack.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
        _levelPanel.AddChild(levelBack);
        stack.AddChild(_levelPanel);

        // Escolha de profissão (solo), depois da fase.
        _profPanel = new VBoxContainer { Visible = false };
        _profPanel.AddThemeConstantOverride("separation", 8);
        stack.AddChild(_profPanel);

        _infoPanel = new PanelContainer { Visible = false };
        _info = new InfoPanel();
        _infoPanel.AddChild(_info);
        _info.BackRequested += ShowMenu;
        stack.AddChild(_infoPanel);

        string save = SaveManager.Summary();
        var footerRow = new HBoxContainer();
        footerRow.AddThemeConstantOverride("separation", 8);
        footerRow.AddChild(UiStyle.MakeLabel($"v{GameVersion.Display}", 14, HorizontalAlignment.Left, UiStyle.PhosphorDim));
        footerRow.AddChild(UiStyle.AlphaBadge());
        if (save.Length > 0)
            footerRow.AddChild(UiStyle.MakeLabel("·   " + save, 14, HorizontalAlignment.Left, UiStyle.PhosphorDim));
        AddChild(footerRow);
        footerRow.SetAnchorsAndOffsetsPreset(LayoutPreset.BottomLeft, LayoutPresetMode.Minsize, 16);

        // Aviso de versão nova no GitHub (canto inferior direito).
        _updateBox = new HBoxContainer { Visible = false };
        _updateBox.AddThemeConstantOverride("separation", 10);
        AddChild(_updateBox);
        _updateBox.SetAnchorsAndOffsetsPreset(LayoutPreset.BottomRight, LayoutPresetMode.Minsize, 16);
        _updateBox.GrowHorizontal = GrowDirection.Begin;
        if (GameSettings.Instance?.CheckUpdates != false)
        {
            UpdateChecker.Check(this, (tag, url) =>
            {
                if (!IsInstanceValid(this))
                    return;
                _updateBox.AddChild(UiStyle.MakeLabel($"Nova versão {tag} disponível!", 16, color: UiStyle.Warning));
                _updateBox.AddChild(UiStyle.MakeButton("Baixar", () => OS.ShellOpen(url), 140));
                _updateBox.Visible = true;
            });
        }

        AddChild(UiStyle.MakeScanlines());

        _startButton.GrabFocus();

        // Primeira vez nesta versão: mostra o que mudou (uma vez só).
        GameSettings settings = GameSettings.Instance;
        if (settings != null && settings.LastSeenVersion != GameVersion.Current)
        {
            string previous = settings.LastSeenVersion;
            settings.LastSeenVersion = GameVersion.Current;
            settings.Save();
            Callable.From(() => ShowInfo(true, previous)).CallDeferred();
        }
    }

    private void ShowInfo(bool justUpdated, string previousVersion = null)
    {
        _menu.Visible = false;
        _infoPanel.Visible = true;
        bool firstRun = string.IsNullOrEmpty(previousVersion);
        string subtitle = !justUpdated ? "histórico de atualizações"
            : firstRun ? "bem-vindo ao Dead Frequency" : $"o jogo foi atualizado (v{previousVersion} → v{GameVersion.Current})";
        string text = GameVersion.ChangelogBbcode(justUpdated && !firstRun ? previousVersion : null);
        if (string.IsNullOrWhiteSpace(text))
            text = GameVersion.ChangelogBbcode();
        _info.Display($"NOVIDADES · v{GameVersion.Display}", subtitle, text);
    }

    private void ShowCredits()
    {
        _menu.Visible = false;
        _infoPanel.Visible = true;
        _info.Display("CRÉDITOS", "quem sintonizou esta frequência", GameVersion.CreditsBbcode());
    }

    public override void _Process(double delta)
    {
        // Falha de sinal ocasional no título.
        _glitchTimer -= delta;
        if (_glitchTimer > 0)
            return;

        bool glitch = GD.Randf() < 0.12f;
        _title.Modulate = glitch ? new Color(1f, 1f, 1f, 0.35f + GD.Randf() * 0.4f) : Colors.White;
        _title.Text = glitch && GD.Randf() < 0.3f ? "DEAD FR3QU_NCY" : "DEAD FREQUENCY";
        _glitchTimer = glitch ? 0.06 : 0.25 + GD.Randf() * 0.6;
    }

    private void ShowLevels()
    {
        _menu.Visible = false;
        _profPanel.Visible = false;
        _levelPanel.Visible = true;
        (_levelPanel.GetChild(1) as Button)?.GrabFocus();
    }

    private void ShowProfessions(GameLevels.Level level)
    {
        _levelPanel.Visible = false;
        _profPanel.Visible = true;
        foreach (Node c in _profPanel.GetChildren())
            c.QueueFree();
        _profPanel.AddChild(UiStyle.MakeLabel("ESCOLHA A PROFISSÃO", 30));
        _profPanel.AddChild(UiStyle.MakeLabel(level.Name, 16, color: UiStyle.PhosphorDim));
        Button first = null;
        string last = GameSettings.Instance?.Profession ?? Professions.Entregador;
        foreach (Professions.Def def in Professions.All)
        {
            if (!Professions.AvailableIn(def.Id, level.Id))
                continue;
            string id = def.Id;
            string scene = level.Scene;
            Button b = UiStyle.MakeButton((id == last ? "► " : "") + def.Name, () =>
            {
                Professions.Current = id;
                Professions.CurrentLevel = level.Id;
                if (GameSettings.Instance != null)
                {
                    GameSettings.Instance.Profession = id;
                    GameSettings.Instance.Save();
                }
                StartGame(scene);
            }, 420, UiSound.Start);
            b.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
            b.AddThemeColorOverride("font_color", def.Color);
            _profPanel.AddChild(b);
            Label desc = UiStyle.MakeLabel(def.Task, 14, color: UiStyle.PhosphorDim);
            desc.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            desc.CustomMinimumSize = new Vector2(620, 0);
            _profPanel.AddChild(desc);
            if (first == null || id == last)
                first = b;
        }
        _profPanel.AddChild(UiStyle.MakeLabel("Dá para trocar depois no menu de pausa.", 14, color: UiStyle.PhosphorDim));
        Button back = UiStyle.MakeButton("Voltar", ShowLevels, sound: UiSound.Back);
        back.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
        _profPanel.AddChild(back);
        first?.GrabFocus();
    }

    private void StartGame(string scene)
    {
        // Pequena pausa para o som de início tocar antes de trocar de cena.
        _levelPanel.Visible = false;
        _profPanel.Visible = false;
        GetTree().CreateTimer(0.35).Timeout += () => GetTree().ChangeSceneToFile(scene);
    }

    private void ShowOptions()
    {
        _menu.Visible = false;
        _header.Visible = false;
        _title.AddThemeFontSizeOverride("font_size", 40);
        _optionsPanel.Visible = true;
        _options.FocusFirst();
    }

    private void ShowCharacter()
    {
        _menu.Visible = false;
        _header.Visible = false;
        _title.AddThemeFontSizeOverride("font_size", 40);
        _characterPanel.Visible = true;
        _character.FocusFirst();
    }

    private void ShowMultiplayer()
    {
        _menu.Visible = false;
        // A tela do multiplayer é alta: some com o subtítulo e o título fica menor.
        _header.Visible = false;
        _title.AddThemeFontSizeOverride("font_size", 40);
        _multiplayerPanel.Visible = true;
        _multiplayer.FocusFirst();
    }

    private void ShowMenu()
    {
        _multiplayerPanel.Visible = false;
        _characterPanel.Visible = false;
        _optionsPanel.Visible = false;
        _infoPanel.Visible = false;
        _levelPanel.Visible = false;
        _profPanel.Visible = false;
        _header.Visible = true;
        _title.AddThemeFontSizeOverride("font_size", 72);
        _menu.Visible = true;
        _startButton.GrabFocus();
    }
}
