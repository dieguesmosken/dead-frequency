// Scripts/Core/GameSettings.cs
// Autoload (registrado no project.godot como "GameSettings").
// Guarda as opções do jogador em user://settings.cfg e as aplica (áudio, janela, vsync).
// Quem depende delas (Player, menus) escuta o sinal SettingsChanged.
using Godot;

namespace DeadFrequency;

public partial class GameSettings : Node
{
    [Signal] public delegate void SettingsChangedEventHandler();

    public static GameSettings Instance { get; private set; }

    private const string FilePath = "user://settings.cfg";

    /// <summary>Multiplicador sobre a sensibilidade base do PlayerController.</summary>
    public float MouseSensitivity { get; set; } = 1.0f;
    public bool InvertY { get; set; }
    public float Fov { get; set; } = 75f;
    /// <summary>0..1 linear.</summary>
    public float MasterVolume { get; set; } = 0.8f;
    public float MusicVolume { get; set; } = 0.7f;
    public float SfxVolume { get; set; } = 0.9f;
    public bool Fullscreen { get; set; }
    public bool VSync { get; set; } = true;
    /// <summary>0 = ciclo dia/noite com o tempo passando, 1 = sempre noite, 2 = sempre dia.</summary>
    public int TimeMode { get; set; }
    /// <summary>Minutos reais para um dia inteiro no jogo (24 h).</summary>
    public float DayLengthMinutes { get; set; } = 24f;
    /// <summary>Última versão cujas novidades o jogador já viu (para mostrar o changelog só uma vez).</summary>
    public string LastSeenVersion { get; set; } = "";
    /// <summary>Menu inicial (MenuStyle): 0 clássico, 1 órbita, 2 cinema, 3 terminal. Muda por código nas Opções.</summary>
    public int MenuStyle { get; set; }
    /// <summary>VISUAL966: mostra a escolha do menu inicial nas Opções.</summary>
    public bool MenuPicker { get; set; }
    /// <summary>Multiplayer: 0 = servidor oficial do projeto, 1 = meu servidor (ServerUrl), 2 = hospedar no meu PC.</summary>
    public int ServerMode { get; set; }
    /// <summary>Endereço do "meu servidor" (só usado no modo 1).</summary>
    public string ServerUrl { get; set; } = "";
    public string PlayerName { get; set; } = "Entregador";
    // Personagem (v0.9.6): homem/mulher e índices das paletas de CharacterLook.
    public bool CharWoman { get; set; }
    public int CharSkin { get; set; } = 2;
    public int CharHair { get; set; } = 1;
    public int CharShirt { get; set; }
    public int CharPants { get; set; }
    /// <summary>Câmera estabilizada no carro: o horizonte não balança com a carroceria (contra enjoo).</summary>
    public bool StableVehicleCamera { get; set; } = true;

    // ---------------- v0.9.2: opções completas (abas) ----------------
    /// <summary>0 = janela, 1 = tela cheia (sem borda), 2 = tela cheia exclusiva, 3 = janela maximizada.</summary>
    public int WindowMode { get; set; }
    /// <summary>Resolução interna do 3D (0,5 a 1,0). Menor = mais leve; a interface continua nítida.</summary>
    public float RenderScale { get; set; } = 1f;
    /// <summary>0 = sem limite.</summary>
    public int MaxFps { get; set; }
    /// <summary>0 = baixa (sem sombras nem brilho), 1 = média, 2 = alta.</summary>
    public int Quality { get; set; } = 2;
    /// <summary>0 = desligado, 1 = FXAA, 2 = MSAA 2x, 3 = MSAA 4x.</summary>
    public int AntiAliasing { get; set; } = 1;
    public float Brightness { get; set; } = 1f;
    public float Contrast { get; set; } = 1f;
    public float UiVolume { get; set; } = 0.8f;
    /// <summary>Volume das rádios 96.6 (multiplica a música).</summary>
    public float RadioVolume { get; set; } = 1f;
    public bool MuteOnFocusLoss { get; set; } = true;
    public bool PauseOnFocusLoss { get; set; } = true;
    /// <summary>Perguntar ao GitHub se há versão nova ao abrir o menu.</summary>
    public bool CheckUpdates { get; set; } = true;
    public float VehicleSensitivity { get; set; } = 1f;
    public bool HeadBob { get; set; } = false;
    public bool ShowFps { get; set; }
    public float UiScale { get; set; } = 1f;
    public bool Scanlines { get; set; } = true;
    public bool ShowCrosshair { get; set; } = true;
    public bool ShowKeyHints { get; set; } = true;
    public bool ShowCompass { get; set; } = true;
    /// <summary>Multiplica quanto tempo os avisos ficam na tela.</summary>
    public float ToastDuration { get; set; } = 1f;
    /// <summary>0 = km/h, 1 = mph.</summary>
    public int SpeedUnit { get; set; }
    public bool Clock24h { get; set; } = true;

    /// <summary>Ações que dá para trocar a tecla nas Opções (id do InputMap, nome na tela).</summary>
    public static readonly (string Action, string Label)[] RebindableActions =
    {
        ("move_forward", "Frente / acelerar"), ("move_back", "Trás / ré"), ("move_left", "Esquerda"), ("move_right", "Direita"),
        ("sprint", "Correr"), ("jump", "Pular / freio de mão"), ("interact", "Usar (E)"), ("throw", "Arremessar"),
        ("flashlight", "Lanterna"), ("inventory", "Inventário"), ("night_vision", "Visão noturna"), ("radio", "Rádio da van"),
        ("pause", "Pausa"),
    };
    /// <summary>Última profissão escolhida (Professions.*).</summary>
    public string Profession { get; set; } = "entregador";
    /// <summary>Ao criar sala: o servidor escolhe a profissão de cada um (equilibra as vagas).</summary>
    public bool AutoProfession { get; set; }
    /// <summary>Ao criar sala: aparece na lista de salas online.</summary>
    public bool PublicRoom { get; set; } = true;

    public override void _EnterTree()
    {
        Instance = this;
        ProcessMode = ProcessModeEnum.Always;
    }

    public override void _ExitTree()
    {
        if (Instance == this)
            Instance = null;
    }

    public override void _Ready()
    {
        Load();
        Apply();
    }

    public override void _Notification(int what)
    {
        if (what == NotificationApplicationFocusOut && MuteOnFocusLoss)
            AudioServer.SetBusMute(0, true);
        else if (what == NotificationApplicationFocusIn)
            AudioServer.SetBusMute(0, MasterVolume <= 0.001f);
    }

    // ---------------- teclas ----------------

    /// <summary>Texto da tecla principal de uma ação ("E", "Shift", "Mouse esq.").</summary>
    public static string KeyName(string action)
    {
        if (!InputMap.HasAction(action))
            return "-";
        foreach (InputEvent e in InputMap.ActionGetEvents(action))
            return EventName(e);
        return "-";
    }

    public static string EventName(InputEvent e) => e switch
    {
        InputEventKey k => OS.GetKeycodeString(k.PhysicalKeycode != Key.None ? DisplayServer.KeyboardGetKeycodeFromPhysical(k.PhysicalKeycode) : k.Keycode),
        InputEventMouseButton m => m.ButtonIndex switch
        {
            MouseButton.Left => "Mouse esq.",
            MouseButton.Right => "Mouse dir.",
            MouseButton.Middle => "Mouse meio",
            _ => $"Mouse {(int)m.ButtonIndex}",
        },
        _ => e.AsText(),
    };

    /// <summary>Troca a tecla principal da ação (as outras teclas dela continuam).</summary>
    public void Rebind(string action, InputEvent e)
    {
        if (!InputMap.HasAction(action))
            return;
        var events = InputMap.ActionGetEvents(action);
        InputMap.ActionEraseEvents(action);
        InputMap.ActionAddEvent(action, e);
        for (int i = 1; i < events.Count; i++)
            InputMap.ActionAddEvent(action, events[i]);
        _customKeys[action] = Serialize(e);
    }

    public void ResetKeys()
    {
        InputMap.LoadFromProjectSettings();
        _customKeys.Clear();
    }

    private readonly System.Collections.Generic.Dictionary<string, string> _customKeys = new();

    private static string Serialize(InputEvent e) => e switch
    {
        InputEventKey k => "k:" + (long)(k.PhysicalKeycode != Key.None ? k.PhysicalKeycode : k.Keycode),
        InputEventMouseButton m => "m:" + (long)m.ButtonIndex,
        _ => "",
    };

    private static InputEvent Deserialize(string s)
    {
        if (s.Length < 3 || !long.TryParse(s[2..], out long code))
            return null;
        return s[0] switch
        {
            'k' => new InputEventKey { PhysicalKeycode = (Key)code },
            'm' => new InputEventMouseButton { ButtonIndex = (MouseButton)code },
            _ => null,
        };
    }

    /// <summary>Aplica tudo e avisa os ouvintes. Chamado pelos menus a cada alteração.</summary>
    public void Apply()
    {
        SetBusVolume("Master", MasterVolume);
        SetBusVolume("Music", MusicVolume);
        SetBusVolume("SFX", SfxVolume);
        SetBusVolume("UI", UiVolume);

        // Só mexe na janela quando muda: rodando incorporado no editor, trocar o modo gera aviso.
        Fullscreen = WindowMode is 1 or 2;
        DisplayServer.WindowMode wantedMode = WindowMode switch
        {
            1 => DisplayServer.WindowMode.Fullscreen,
            2 => DisplayServer.WindowMode.ExclusiveFullscreen,
            3 => DisplayServer.WindowMode.Maximized,
            _ => DisplayServer.WindowMode.Windowed,
        };
        if (DisplayServer.WindowGetMode() != wantedMode && !Engine.IsEmbeddedInEditor())
            DisplayServer.WindowSetMode(wantedMode);

        Engine.MaxFps = MaxFps;
        Window root = GetTree()?.Root;
        if (root != null)
        {
            root.Scaling3DScale = Mathf.Clamp(RenderScale, 0.5f, 1f);
            root.ScreenSpaceAA = AntiAliasing == 1 ? Viewport.ScreenSpaceAAEnum.Fxaa : Viewport.ScreenSpaceAAEnum.Disabled;
            root.Msaa3D = AntiAliasing switch { 2 => Viewport.Msaa.Msaa2X, 3 => Viewport.Msaa.Msaa4X, _ => Viewport.Msaa.Disabled };
            root.ContentScaleFactor = Mathf.Clamp(UiScale, 0.75f, 1.5f);
        }
        if (GetTree() != null)
            foreach (Node n in GetTree().GetNodesInGroup(UiStyle.ScanlineGroup))
                if (n is CanvasItem c)
                    c.Visible = Scanlines;

        DisplayServer.VSyncMode wantedVsync = VSync ? DisplayServer.VSyncMode.Enabled : DisplayServer.VSyncMode.Disabled;
        if (DisplayServer.WindowGetVsyncMode() != wantedVsync)
            DisplayServer.WindowSetVsyncMode(wantedVsync);

        EmitSignal(SignalName.SettingsChanged);
    }

    private static void SetBusVolume(string busName, float linear)
    {
        int bus = AudioServer.GetBusIndex(busName);
        if (bus < 0)
            return;
        AudioServer.SetBusMute(bus, linear <= 0.001f);
        AudioServer.SetBusVolumeDb(bus, Mathf.LinearToDb(Mathf.Max(linear, 0.0001f)));
    }

    public void Save()
    {
        var cfg = new ConfigFile();
        cfg.SetValue("controls", "mouse_sensitivity", MouseSensitivity);
        cfg.SetValue("controls", "invert_y", InvertY);
        cfg.SetValue("video", "fov", Fov);
        cfg.SetValue("video", "fullscreen", Fullscreen);
        cfg.SetValue("video", "vsync", VSync);
        cfg.SetValue("video", "time_mode", TimeMode);
        cfg.SetValue("video", "day_length_minutes", DayLengthMinutes);
        cfg.SetValue("meta", "last_seen_version", LastSeenVersion);
        cfg.SetValue("meta", "menu_style", MenuStyle);
        cfg.SetValue("meta", "menu_picker", MenuPicker);
        cfg.SetValue("audio", "master_volume", MasterVolume);
        cfg.SetValue("audio", "music_volume", MusicVolume);
        cfg.SetValue("audio", "sfx_volume", SfxVolume);
        cfg.SetValue("network", "server_mode", ServerMode);
        cfg.SetValue("network", "server_url", ServerUrl);
        cfg.SetValue("network", "player_name", PlayerName);
        cfg.SetValue("character", "woman", CharWoman);
        cfg.SetValue("character", "skin", CharSkin);
        cfg.SetValue("character", "hair", CharHair);
        cfg.SetValue("character", "shirt", CharShirt);
        cfg.SetValue("character", "pants", CharPants);
        cfg.SetValue("game", "stable_vehicle_camera", StableVehicleCamera);
        cfg.SetValue("video", "window_mode", WindowMode);
        cfg.SetValue("video", "render_scale", RenderScale);
        cfg.SetValue("video", "max_fps", MaxFps);
        cfg.SetValue("video", "quality", Quality);
        cfg.SetValue("video", "anti_aliasing", AntiAliasing);
        cfg.SetValue("video", "brightness", Brightness);
        cfg.SetValue("video", "contrast", Contrast);
        cfg.SetValue("audio", "ui_volume", UiVolume);
        cfg.SetValue("audio", "radio_volume", RadioVolume);
        cfg.SetValue("audio", "mute_on_focus_loss", MuteOnFocusLoss);
        cfg.SetValue("game", "pause_on_focus_loss", PauseOnFocusLoss);
        cfg.SetValue("game", "check_updates", CheckUpdates);
        cfg.SetValue("controls", "vehicle_sensitivity", VehicleSensitivity);
        cfg.SetValue("controls", "head_bob", HeadBob);
        cfg.SetValue("interface", "show_fps", ShowFps);
        cfg.SetValue("interface", "ui_scale", UiScale);
        cfg.SetValue("interface", "scanlines", Scanlines);
        cfg.SetValue("interface", "crosshair", ShowCrosshair);
        cfg.SetValue("interface", "key_hints", ShowKeyHints);
        cfg.SetValue("interface", "compass", ShowCompass);
        cfg.SetValue("interface", "toast_duration", ToastDuration);
        cfg.SetValue("interface", "speed_unit", SpeedUnit);
        cfg.SetValue("interface", "clock_24h", Clock24h);
        if (cfg.HasSection("keys"))
            cfg.EraseSection("keys");
        foreach ((string action, string code) in _customKeys)
            cfg.SetValue("keys", action, code);
        cfg.SetValue("game", "profession", Profession);
        cfg.SetValue("network", "auto_profession", AutoProfession);
        cfg.SetValue("network", "public_room", PublicRoom);

        Error err = cfg.Save(FilePath);
        if (err != Error.Ok)
            GD.PushWarning($"GameSettings: não foi possível salvar ({err}).");
    }

    public void Load()
    {
        var cfg = new ConfigFile();
        if (cfg.Load(FilePath) != Error.Ok)
            return; // primeira execução: usa os padrões

        MouseSensitivity = cfg.GetValue("controls", "mouse_sensitivity", MouseSensitivity).AsSingle();
        InvertY = cfg.GetValue("controls", "invert_y", InvertY).AsBool();
        Fov = cfg.GetValue("video", "fov", Fov).AsSingle();
        Fullscreen = cfg.GetValue("video", "fullscreen", Fullscreen).AsBool();
        VSync = cfg.GetValue("video", "vsync", VSync).AsBool();
        // Versões antigas tinham só "Modo dia" (liga/desliga).
        bool oldDay = cfg.GetValue("video", "day_mode", false).AsBool();
        TimeMode = cfg.GetValue("video", "time_mode", oldDay ? 2 : 0).AsInt32();
        DayLengthMinutes = cfg.GetValue("video", "day_length_minutes", DayLengthMinutes).AsSingle();
        LastSeenVersion = cfg.GetValue("meta", "last_seen_version", LastSeenVersion).AsString();
        MenuStyle = cfg.GetValue("meta", "menu_style", MenuStyle).AsInt32();
        MenuPicker = cfg.GetValue("meta", "menu_picker", MenuPicker).AsBool();
        MasterVolume = cfg.GetValue("audio", "master_volume", MasterVolume).AsSingle();
        MusicVolume = cfg.GetValue("audio", "music_volume", MusicVolume).AsSingle();
        SfxVolume = cfg.GetValue("audio", "sfx_volume", SfxVolume).AsSingle();
        ServerUrl = cfg.GetValue("network", "server_url", ServerUrl).AsString();
        Profession = cfg.GetValue("game", "profession", Profession).AsString();
        AutoProfession = cfg.GetValue("network", "auto_profession", AutoProfession).AsBool();
        PublicRoom = cfg.GetValue("network", "public_room", PublicRoom).AsBool();
        // Versões antigas guardavam o endereço oficial no campo: vira o modo "oficial".
        if (NetClient.NormalizeServer(ServerUrl) == NetClient.NormalizeServer(NetClient.OfficialServer))
            ServerUrl = "";
        ServerMode = cfg.GetValue("network", "server_mode", string.IsNullOrWhiteSpace(ServerUrl) ? 0 : 1).AsInt32();
        PlayerName = cfg.GetValue("network", "player_name", PlayerName).AsString();
        CharWoman = cfg.GetValue("character", "woman", CharWoman).AsBool();
        CharSkin = cfg.GetValue("character", "skin", CharSkin).AsInt32();
        CharHair = cfg.GetValue("character", "hair", CharHair).AsInt32();
        CharShirt = cfg.GetValue("character", "shirt", CharShirt).AsInt32();
        CharPants = cfg.GetValue("character", "pants", CharPants).AsInt32();
        StableVehicleCamera = cfg.GetValue("game", "stable_vehicle_camera", StableVehicleCamera).AsBool();
        WindowMode = cfg.GetValue("video", "window_mode", Fullscreen ? 1 : 0).AsInt32();
        RenderScale = cfg.GetValue("video", "render_scale", RenderScale).AsSingle();
        MaxFps = cfg.GetValue("video", "max_fps", MaxFps).AsInt32();
        Quality = cfg.GetValue("video", "quality", Quality).AsInt32();
        AntiAliasing = cfg.GetValue("video", "anti_aliasing", AntiAliasing).AsInt32();
        Brightness = cfg.GetValue("video", "brightness", Brightness).AsSingle();
        Contrast = cfg.GetValue("video", "contrast", Contrast).AsSingle();
        UiVolume = cfg.GetValue("audio", "ui_volume", SfxVolume).AsSingle();
        RadioVolume = cfg.GetValue("audio", "radio_volume", RadioVolume).AsSingle();
        MuteOnFocusLoss = cfg.GetValue("audio", "mute_on_focus_loss", MuteOnFocusLoss).AsBool();
        PauseOnFocusLoss = cfg.GetValue("game", "pause_on_focus_loss", PauseOnFocusLoss).AsBool();
        CheckUpdates = cfg.GetValue("game", "check_updates", CheckUpdates).AsBool();
        VehicleSensitivity = cfg.GetValue("controls", "vehicle_sensitivity", VehicleSensitivity).AsSingle();
        HeadBob = cfg.GetValue("controls", "head_bob", HeadBob).AsBool();
        ShowFps = cfg.GetValue("interface", "show_fps", ShowFps).AsBool();
        UiScale = cfg.GetValue("interface", "ui_scale", UiScale).AsSingle();
        Scanlines = cfg.GetValue("interface", "scanlines", Scanlines).AsBool();
        ShowCrosshair = cfg.GetValue("interface", "crosshair", ShowCrosshair).AsBool();
        ShowKeyHints = cfg.GetValue("interface", "key_hints", ShowKeyHints).AsBool();
        ShowCompass = cfg.GetValue("interface", "compass", ShowCompass).AsBool();
        ToastDuration = cfg.GetValue("interface", "toast_duration", ToastDuration).AsSingle();
        SpeedUnit = cfg.GetValue("interface", "speed_unit", SpeedUnit).AsInt32();
        Clock24h = cfg.GetValue("interface", "clock_24h", Clock24h).AsBool();
        if (cfg.HasSection("keys"))
        {
            foreach (string action in cfg.GetSectionKeys("keys"))
            {
                InputEvent e = Deserialize(cfg.GetValue("keys", action, "").AsString());
                if (e != null && InputMap.HasAction(action))
                    Rebind(action, e);
            }
        }
    }

    /// <summary>Volta tudo ao padrão (menos nome, servidor e profissão).</summary>
    public void ResetToDefaults()
    {
        var d = new GameSettings();
        MouseSensitivity = d.MouseSensitivity; InvertY = d.InvertY; Fov = d.Fov;
        MasterVolume = d.MasterVolume; MusicVolume = d.MusicVolume; SfxVolume = d.SfxVolume; UiVolume = d.UiVolume; RadioVolume = d.RadioVolume;
        WindowMode = d.WindowMode; RenderScale = d.RenderScale; MaxFps = d.MaxFps; Quality = d.Quality; AntiAliasing = d.AntiAliasing;
        Brightness = d.Brightness; Contrast = d.Contrast; VSync = d.VSync; MuteOnFocusLoss = d.MuteOnFocusLoss; PauseOnFocusLoss = d.PauseOnFocusLoss; CheckUpdates = d.CheckUpdates; StableVehicleCamera = d.StableVehicleCamera;
        VehicleSensitivity = d.VehicleSensitivity; HeadBob = d.HeadBob; ShowFps = d.ShowFps; UiScale = d.UiScale; Scanlines = d.Scanlines;
        ShowCrosshair = d.ShowCrosshair; ShowKeyHints = d.ShowKeyHints; ShowCompass = d.ShowCompass; ToastDuration = d.ToastDuration;
        SpeedUnit = d.SpeedUnit; Clock24h = d.Clock24h; TimeMode = d.TimeMode; DayLengthMinutes = d.DayLengthMinutes;
        d.Free();
        ResetKeys();
    }
}
