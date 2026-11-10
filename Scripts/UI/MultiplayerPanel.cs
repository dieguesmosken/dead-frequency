// Scripts/UI/MultiplayerPanel.cs
// Tela de multiplayer do menu principal. Três jeitos de jogar:
//   1. Servidor oficial (padrão) — o servidor do projeto na Cloudflare; o endereço nem aparece.
//   2. Meu servidor — você informa o endereço do seu próprio Worker (ou de um amigo que hospeda).
//   3. Hospedar no meu PC — o jogo vira o servidor (LocalServer) e gera um link público com o
//      Cloudflare Tunnel (TunnelService), tipo ngrok, para os amigos entrarem de qualquer lugar.
using System;
using System.Text.RegularExpressions;
using Godot;

namespace DeadFrequency;

public partial class MultiplayerPanel : VBoxContainer
{
    [Signal] public delegate void BackRequestedEventHandler();
    /// <summary>Entrou numa sala: o menu troca para a fase.</summary>
    [Signal] public delegate void JoinedRoomEventHandler();

    private static readonly string[] ModeNames = { "Servidor oficial", "Meu servidor", "Hospedar no meu PC" };
    private static readonly Regex UrlInText = new(@"(https?|wss?)://[^\s]+", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private int _mode;
    private string _level = "city";
    private LineEdit _name;
    private OptionButton _prof;
    private CheckBox _autoProf;
    private CheckBox _public;
    private Button _gear;
    private Button _listButton;
    private PanelContainer _settingsBox;
    private Control _who, _modeRow, _createRow;
    private PanelContainer _roomList;
    private VBoxContainer _roomRows;
    private LineEdit _server;
    private LineEdit _code;
    private LineEdit _roomPassword;
    private Label _status;
    private Label _modeHint;
    private Control _serverRow;
    private Control _joinBlock;
    private Button _createButton;
    private Button _joinButton;
    private readonly Button[] _modeButtons = new Button[3];

    // Hospedar no PC
    private VBoxContainer _hostBox;
    private Label _hostInfo;
    private Button _hostStart;
    private Button _hostCopy;
    private Button _hostEnter;

    public override void _Ready()
    {
        AddThemeConstantOverride("separation", 8);
        CustomMinimumSize = new Vector2(700, 0);

        AddChild(UiStyle.MakeLabel("MULTIPLAYER", 28));

        GameSettings s = GameSettings.Instance;
        _mode = Mathf.Clamp(s?.ServerMode ?? 0, 0, 2);

        // Apelido e profissão na mesma linha.
        var who = new HBoxContainer();
        _who = who;
        who.AddThemeConstantOverride("separation", 10);
        who.AddChild(UiStyle.MakeLabel("Apelido", 17, HorizontalAlignment.Left));
        _name = new LineEdit { Text = s?.PlayerName ?? "Entregador", PlaceholderText = "Entregador", MaxLength = 16, CustomMinimumSize = new Vector2(200, 40) };
        who.AddChild(_name);
        who.AddChild(UiStyle.MakeLabel("Profissão", 17, HorizontalAlignment.Left));
        _prof = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(0, 40) };
        for (int i = 0; i < Professions.All.Length; i++)
        {
            Professions.Def def = Professions.All[i];
            _prof.AddItem(def.Levels.Length > 1 ? def.Name : def.Name + " (cidade)", i);
            if (def.Id == (s?.Profession ?? Professions.Entregador))
                _prof.Selected = i;
        }
        who.AddChild(_prof);
        AddChild(who);

        // Onde jogar.
        var modeRow = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        _modeRow = modeRow;
        modeRow.AddThemeConstantOverride("separation", 8);
        var modeGroup = new ButtonGroup();
        for (int i = 0; i < 3; i++)
        {
            int mode = i;
            var b = new Button { Text = ModeNames[i], ToggleMode = true, ButtonGroup = modeGroup, ButtonPressed = i == _mode, CustomMinimumSize = new Vector2(210, 38) };
            b.AddThemeFontSizeOverride("font_size", 16);
            b.MouseEntered += () => AudioManager.Instance?.PlayUi(UiSound.Hover);
            b.Pressed += () => SetMode(mode);
            _modeButtons[i] = b;
            modeRow.AddChild(b);
        }
        AddChild(modeRow);
        _modeHint = UiStyle.MakeLabel("", 13, color: UiStyle.PhosphorDim);
        _modeHint.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _modeHint.CustomMinimumSize = new Vector2(680, 0);
        AddChild(_modeHint);

        _server = AddField("Endereço", s?.ServerUrl ?? "", "https://meu-servidor.workers.dev");
        _serverRow = _server.GetParent<Control>();

        // Criar sala + engrenagem com as configurações da sala.
        var createRow = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        _createRow = createRow;
        createRow.AddThemeConstantOverride("separation", 10);
        _createButton = UiStyle.MakeButton("Criar sala", OnCreatePressed, 300, UiSound.Start);
        createRow.AddChild(_createButton);
        _hostStart = UiStyle.MakeButton("Hospedar sala", OnHostPressed, 300, UiSound.Start);
        createRow.AddChild(_hostStart);
        _gear = new Button { Icon = GearIcon(), Text = " Sala", TooltipText = "Configurar a sala (fase, profissões, pública)", CustomMinimumSize = new Vector2(120, 44), ToggleMode = true };
        _gear.AddThemeFontSizeOverride("font_size", 16);
        _gear.MouseEntered += () => AudioManager.Instance?.PlayUi(UiSound.Hover);
        _gear.Toggled += on => ShowSettings(on);
        createRow.AddChild(_gear);
        AddChild(createRow);

        BuildSettingsBox(s);
        BuildHostBox();

        // Entrar na sala de alguém.
        var join = new VBoxContainer();
        join.AddThemeConstantOverride("separation", 8);
        join.AddChild(UiStyle.MakeLabel("— ou entre na sala de um amigo —", 14, color: UiStyle.PhosphorDim));
        var joinRow = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        joinRow.AddThemeConstantOverride("separation", 10);
        _code = new LineEdit
        {
            PlaceholderText = "CÓDIGO",
            MaxLength = 5,
            CustomMinimumSize = new Vector2(140, 44),
            Alignment = HorizontalAlignment.Center,
        };
        _code.AddThemeFontSizeOverride("font_size", 24);
        _code.TextChanged += text =>
        {
            int caret = _code.CaretColumn;
            _code.Text = text.ToUpperInvariant();
            _code.CaretColumn = caret;
        };
        _code.TextSubmitted += _ => OnJoinPressed();
        joinRow.AddChild(_code);
        _joinButton = UiStyle.MakeButton("Entrar", OnJoinPressed, 130, UiSound.Start);
        joinRow.AddChild(_joinButton);
        joinRow.AddChild(UiStyle.MakeButton("Colar convite", PasteInvite, 170));
        _listButton = UiStyle.MakeButton("Salas online", ShowRoomList, 170);
        joinRow.AddChild(_listButton);
        join.AddChild(joinRow);
        var passwordRow = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        passwordRow.AddThemeConstantOverride("separation", 10);
        passwordRow.AddChild(UiStyle.MakeLabel("Senha da sala", 15, color: UiStyle.PhosphorDim));
        _roomPassword = new LineEdit
        {
            PlaceholderText = "Deixe vazio para salas sem senha",
            Secret = true,
            MaxLength = 64,
            CustomMinimumSize = new Vector2(280, 40),
        };
        passwordRow.AddChild(_roomPassword);
        join.AddChild(passwordRow);
        _joinBlock = join;
        AddChild(join);

        BuildRoomList();

        _status = UiStyle.MakeLabel("", 15, color: UiStyle.Warning);
        _status.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _status.CustomMinimumSize = new Vector2(680, 0);
        AddChild(_status);

        Button back = UiStyle.MakeButton("Voltar", OnBack, sound: UiSound.Back);
        back.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
        AddChild(back);

        if (NetClient.Instance != null)
        {
            NetClient.Instance.StatusMessage += OnStatus;
            NetClient.Instance.Joined += OnJoined;
            NetClient.Instance.Disconnected += OnFailed;
        }
        SetMode(_mode, false);
    }

    // ---------------- Configurações da sala (engrenagem) ----------------

    private void BuildSettingsBox(GameSettings s)
    {
        _settingsBox = new PanelContainer { Visible = false };
        _settingsBox.AddThemeStyleboxOverride("panel", UiStyle.Box(new Color(0f, 0.06f, 0.03f, 0.9f), UiStyle.PhosphorDim, 2, 12));
        var v = new VBoxContainer();
        v.AddThemeConstantOverride("separation", 8);
        _settingsBox.AddChild(v);
        v.AddChild(UiStyle.MakeLabel("CONFIGURAÇÃO DA SALA NOVA", 18));
        var levelRow = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        levelRow.AddThemeConstantOverride("separation", 10);
        levelRow.AddChild(UiStyle.MakeLabel("Fase:", 16));
        var levelGroup = new ButtonGroup();
        foreach (GameLevels.Level level in GameLevels.All)
        {
            string id = level.Id;
            var b = new Button { Text = level.Name, ToggleMode = true, ButtonGroup = levelGroup, ButtonPressed = id == _level, CustomMinimumSize = new Vector2(220, 38) };
            b.MouseEntered += () => AudioManager.Instance?.PlayUi(UiSound.Hover);
            b.Pressed += () =>
            {
                _level = id;
                AudioManager.Instance?.PlayUi(UiSound.Click);
            };
            levelRow.AddChild(b);
        }
        v.AddChild(levelRow);
        _autoProf = new CheckBox { Text = "O servidor escolhe a profissão de cada jogador", ButtonPressed = s?.AutoProfession ?? false };
        _autoProf.AddThemeFontSizeOverride("font_size", 15);
        v.AddChild(_autoProf);
        _public = new CheckBox { Text = "Sala pública (aparece em \"Salas online\")", ButtonPressed = s?.PublicRoom ?? true };
        _public.AddThemeFontSizeOverride("font_size", 15);
        v.AddChild(_public);
        Button ok = UiStyle.MakeButton("OK", () => _gear.ButtonPressed = false, 140);
        ok.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
        v.AddChild(ok);
        AddChild(_settingsBox);
    }

    private void ShowSettings(bool on)
    {
        AudioManager.Instance?.PlayUi(UiSound.Click);
        _settingsBox.Visible = on;
        if (on)
            _roomList.Visible = false;
        RefreshBlocks();
    }

    // ---------------- Lista de salas online ----------------

    private void BuildRoomList()
    {
        _roomList = new PanelContainer { Visible = false };
        _roomList.AddThemeStyleboxOverride("panel", UiStyle.Box(new Color(0f, 0.06f, 0.03f, 0.9f), UiStyle.PhosphorDim, 2, 12));
        var v = new VBoxContainer();
        v.AddThemeConstantOverride("separation", 6);
        _roomList.AddChild(v);
        v.AddChild(UiStyle.MakeLabel("SALAS ABERTAS", 18));
        var scroll = new ScrollContainer { CustomMinimumSize = new Vector2(660, 180), HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        _roomRows = new VBoxContainer();
        _roomRows.AddThemeConstantOverride("separation", 6);
        _roomRows.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        scroll.AddChild(_roomRows);
        v.AddChild(scroll);
        var row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        row.AddThemeConstantOverride("separation", 10);
        row.AddChild(UiStyle.MakeButton("Atualizar", RefreshRoomList, 160));
        row.AddChild(UiStyle.MakeButton("Fechar", () => { _roomList.Visible = false; RefreshBlocks(); }, 160, UiSound.Back));
        v.AddChild(row);
        AddChild(_roomList);
    }

    private void ShowRoomList()
    {
        _gear.ButtonPressed = false;
        _roomList.Visible = true;
        RefreshBlocks();
        RefreshRoomList();
    }

    private void RefreshRoomList()
    {
        foreach (Node c in _roomRows.GetChildren())
            c.QueueFree();
        _roomRows.AddChild(UiStyle.MakeLabel("Procurando salas...", 15, color: UiStyle.PhosphorDim));
        NetClient.Instance?.ListRooms(CurrentServer(), (rooms, error) =>
        {
            if (!IsInstanceValid(this) || !IsInstanceValid(_roomRows))
                return;
            foreach (Node c in _roomRows.GetChildren())
                c.QueueFree();
            if (error != null)
            {
                _roomRows.AddChild(UiStyle.MakeLabel(error, 15, color: UiStyle.Warning));
                return;
            }
            if (rooms.Count == 0)
            {
                _roomRows.AddChild(UiStyle.MakeLabel("Nenhuma sala aberta agora. Crie a sua!", 15, color: UiStyle.PhosphorDim));
                return;
            }
            foreach (NetClient.RoomListing r in rooms)
            {
                var line = new HBoxContainer();
                line.AddThemeConstantOverride("separation", 10);
                string host = string.IsNullOrEmpty(r.Host) ? "" : $" · {r.Host}";
                string auto = r.AutoProfession ? " · profissões sorteadas" : "";
                string roomName = string.IsNullOrEmpty(r.Name) ? GameLevels.Get(r.Level).Name : r.Name;
                string permanent = r.Permanent ? " · permanente" : "";
                string password = r.HasPassword ? " · senha" : "";
                Label l = UiStyle.MakeLabel($"{r.Code}  {roomName}  {r.Players}/{r.Max}{host}{auto}{permanent}{password}", 15, HorizontalAlignment.Left);
                l.SizeFlagsHorizontal = SizeFlags.ExpandFill;
                l.ClipText = true;
                line.AddChild(l);
                string code = r.Code;
                Button enter = UiStyle.MakeButton(r.Players >= r.Max ? "Cheia" : "Entrar", () =>
                {
                    _code.Text = code;
                    OnJoinPressed();
                }, 120, UiSound.Start);
                enter.Disabled = r.Players >= r.Max;
                line.AddChild(enter);
                _roomRows.AddChild(line);
            }
        });
    }

    /// <summary>Engrenagem desenhada em código (sem depender de fonte com o símbolo).</summary>
    private static Texture2D GearIcon()
    {
        const int size = 24;
        var img = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
        Color c = UiStyle.Phosphor;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - size * 0.5f + 0.5f, dy = y - size * 0.5f + 0.5f;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Atan2(dy, dx);
                float outer = Mathf.Cos(a * 8f) > 0.2f ? 11f : 8.2f;
                bool on = r <= outer && r >= 3.6f;
                img.SetPixel(x, y, on ? c : new Color(0f, 0f, 0f, 0f));
            }
        }
        return ImageTexture.CreateFromImage(img);
    }

    public override void _ExitTree()
    {
        if (TunnelService.Instance != null)
            TunnelService.Instance.Changed -= RefreshHostBox;
        if (NetClient.Instance == null)
            return;
        NetClient.Instance.StatusMessage -= OnStatus;
        NetClient.Instance.Joined -= OnJoined;
        NetClient.Instance.Disconnected -= OnFailed;
    }

    public override void _Process(double delta)
    {
        // Atualiza a porcentagem do download do cloudflared.
        if (_mode == 2 && TunnelService.Instance?.State == TunnelService.TunnelState.Downloading)
            RefreshHostBox();
    }

    public void FocusFirst() => (_mode == 2 ? _hostStart : _createButton)?.GrabFocus();

    // ---------------- Modo ----------------

    private void SetMode(int mode, bool sound = true)
    {
        _mode = mode;
        if (sound)
            AudioManager.Instance?.PlayUi(UiSound.Click);
        _modeButtons[mode].ButtonPressed = true;
        _createButton.Visible = mode != 2;
        if (mode == 2)
            _roomList.Visible = false;
        RefreshBlocks();
        _modeHint.Text = mode switch
        {
            0 => "Servidor do projeto na nuvem. É só criar a sala ou digitar o código do amigo.",
            1 => "Use o endereço do seu servidor (o Worker da pasta server/) ou o link que um amigo gerou ao hospedar.",
            _ => "Seu PC vira o servidor. O jogo gera um link público grátis (Cloudflare Tunnel) para os amigos entrarem de qualquer lugar.",
        };
        _status.Text = "";
        RefreshHostBox();
    }

    /// <summary>Esconde o bloco "entrar" enquanto a engrenagem ou a lista estão abertas (cabe na tela).</summary>
    private void RefreshBlocks()
    {
        if (_joinBlock == null)
            return;
        bool overlay = _settingsBox.Visible || _roomList.Visible;
        // Com a engrenagem ou a lista aberta, o resto some para tudo caber (e o Voltar não cortar).
        _who.Visible = !overlay;
        _modeRow.Visible = !overlay;
        _modeHint.Visible = !overlay;
        _serverRow.Visible = _mode == 1 && !overlay;
        _createRow.Visible = !_roomList.Visible;
        _joinBlock.Visible = _mode != 2 && !overlay;
        _hostBox.Visible = _mode == 2 && !_settingsBox.Visible;
        _listButton.Visible = _mode != 2;
    }

    private string CurrentServer() => _mode == 1 ? _server.Text.Trim() : NetClient.OfficialServer;

    // ---------------- Hospedar no PC ----------------

    private void BuildHostBox()
    {
        _hostBox = new VBoxContainer { Visible = false };
        _hostBox.AddThemeConstantOverride("separation", 8);
        _hostInfo = UiStyle.MakeLabel("", 16);
        _hostInfo.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _hostInfo.CustomMinimumSize = new Vector2(640, 0);

        var row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        row.AddThemeConstantOverride("separation", 10);
        _hostCopy = UiStyle.MakeButton("Copiar convite", CopyHostInvite, 220);
        _hostEnter = UiStyle.MakeButton("Entrar no jogo", EnterHostedRoom, 220, UiSound.Start);
        row.AddChild(_hostCopy);
        row.AddChild(_hostEnter);

        _hostBox.AddChild(_hostInfo);
        _hostBox.AddChild(row);
        AddChild(_hostBox);
        TunnelService.Ensure().Changed += RefreshHostBox;
    }

    private void OnHostPressed()
    {
        if (!SaveFields())
            return;
        LocalServer server = LocalServer.Ensure();
        Error err = server.Start(_level, LocalServer.DefaultPort, _autoProf.ButtonPressed);
        _gear.ButtonPressed = false;
        if (err != Error.Ok)
        {
            _status.Text = $"Não consegui abrir a porta {LocalServer.DefaultPort} ({err}). Feche outro jogo/servidor que esteja usando ela.";
            return;
        }
        TunnelService.Ensure().Start(server.Port);
        RefreshHostBox();
    }

    private void RefreshHostBox()
    {
        if (_hostBox == null || !IsInstanceValid(_hostBox))
            return;
        LocalServer server = LocalServer.Instance;
        TunnelService tunnel = TunnelService.Instance;
        bool running = server != null && server.Running;
        _hostStart.Visible = !running && _mode == 2;
        _hostCopy.Visible = running;
        _hostEnter.Visible = running;

        if (!running)
        {
            _hostInfo.Text = TunnelService.IsInstalled
                ? "Pronto para hospedar. O Windows pode pedir para liberar o jogo no firewall: aceite."
                : "Na primeira vez o jogo baixa o cloudflared (programa oficial e gratuito da Cloudflare) para criar o link público.";
            return;
        }

        string lan = string.Join("  ou  ", LocalServer.LanAddresses().ConvertAll(ip => $"http://{ip}:{server.Port}"));
        string tunnelLine = tunnel?.State switch
        {
            TunnelService.TunnelState.Ready => $"Link público: {tunnel.PublicUrl}",
            TunnelService.TunnelState.Downloading => $"Baixando o cloudflared... {tunnel.DownloadProgress * 100f:0}%",
            TunnelService.TunnelState.Starting => "Gerando o link público (uns 10 segundos)...",
            TunnelService.TunnelState.Failed => "Link público indisponível: " + tunnel.ErrorMessage + "  (amigos na mesma rede ainda podem entrar)",
            _ => "Link público desligado.",
        };
        _hostInfo.Text = $"SALA {server.Code} no seu PC · {GameLevels.Get(_level).Name}\n{tunnelLine}" +
                         (lan.Length > 0 ? $"\nMesma rede (Wi-Fi/cabo): {lan}" : "") +
                         "\nO amigo clica em \"Colar convite\" e entra.";
    }

    private string HostInviteText()
    {
        LocalServer server = LocalServer.Instance;
        TunnelService tunnel = TunnelService.Instance;
        var lan = LocalServer.LanAddresses();
        string address = tunnel?.State == TunnelService.TunnelState.Ready
            ? tunnel.PublicUrl
            : $"http://{(lan.Count > 0 ? lan[0] : "127.0.0.1")}:{server?.Port ?? LocalServer.DefaultPort}";
        return $"Bora entregar comigo no Dead Frequency! Código da sala: {server?.Code}\nServidor: {address}";
    }

    private void CopyHostInvite()
    {
        DisplayServer.ClipboardSet(HostInviteText());
        _status.Text = "Convite copiado! Mande no Discord ou WhatsApp.";
    }

    private void EnterHostedRoom()
    {
        LocalServer server = LocalServer.Instance;
        if (server == null || !server.Running || NetClient.Instance == null)
            return;
        SetBusy(true);
        NetClient.Instance.JoinRoom($"http://127.0.0.1:{server.Port}", server.Code, PlayerName());
        // O convite do menu de pausa usa o link público, não o 127.0.0.1.
        TunnelService tunnel = TunnelService.Instance;
        NetClient.Instance.PublicServerUrl = tunnel?.State == TunnelService.TunnelState.Ready ? tunnel.PublicUrl : "";
    }

    private void OnBack()
    {
        // Voltar sem entrar no jogo desliga a sala hospedada.
        LocalServer.Instance?.Stop();
        TunnelService.Instance?.Stop();
        RefreshHostBox();
        EmitSignal(SignalName.BackRequested);
    }

    // ---------------- Campos ----------------

    private LineEdit AddField(string label, string value, string placeholder)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 12);
        Label name = UiStyle.MakeLabel(label, 18, HorizontalAlignment.Left);
        name.CustomMinimumSize = new Vector2(170, 0);
        var edit = new LineEdit
        {
            Text = value,
            PlaceholderText = placeholder,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 40),
        };
        row.AddChild(name);
        row.AddChild(edit);
        AddChild(row);
        return edit;
    }

    private string PlayerName()
    {
        string n = _name.Text.Trim();
        return n.Length == 0 ? "Entregador" : n;
    }

    private bool SaveFields()
    {
        if (_mode == 1 && string.IsNullOrEmpty(_server.Text.Trim()))
        {
            _status.Text = "Informe o endereço do seu servidor (ou cole o convite do amigo).";
            return false;
        }
        if (GameSettings.Instance != null)
        {
            GameSettings.Instance.ServerMode = _mode;
            GameSettings.Instance.ServerUrl = _server.Text.Trim();
            GameSettings.Instance.PlayerName = PlayerName();
            GameSettings.Instance.Profession = Professions.All[Mathf.Max(0, _prof.Selected)].Id;
            GameSettings.Instance.AutoProfession = _autoProf.ButtonPressed;
            GameSettings.Instance.PublicRoom = _public.ButtonPressed;
            GameSettings.Instance.Save();
        }
        Professions.Current = Professions.All[Mathf.Max(0, _prof.Selected)].Id;
        return true;
    }

    private void OnCreatePressed()
    {
        if (NetClient.Instance == null || !SaveFields())
            return;
        SetBusy(true);
        NetClient.Instance.CreateRoom(CurrentServer(), PlayerName(), _level, _autoProf.ButtonPressed, _public.ButtonPressed);
    }

    private void OnJoinPressed()
    {
        if (NetClient.Instance == null || !SaveFields())
            return;
        string code = ExtractCode(_code.Text);
        if (code.Length != 5)
        {
            _status.Text = "O código da sala tem 5 letras/números.";
            return;
        }
        SetBusy(true);
        NetClient.Instance.JoinRoom(CurrentServer(), code, PlayerName(), _roomPassword.Text);
    }

    /// <summary>Cola o convite: pega o código e, se tiver endereço de servidor, troca para "Meu servidor".</summary>
    private void PasteInvite()
    {
        string text = DisplayServer.ClipboardGet() ?? "";
        _code.Text = ExtractCode(text);
        Match url = UrlInText.Match(text);
        if (url.Success && NetClient.NormalizeServer(url.Value) != NetClient.NormalizeServer(NetClient.OfficialServer))
        {
            _server.Text = url.Value.TrimEnd('.', ',', ')');
            SetMode(1);
            _status.Text = "Convite colado: servidor do amigo + código. É só clicar em Entrar.";
        }
        else if (_code.Text.Length == 5)
        {
            if (_mode == 2)
                SetMode(0);
            _status.Text = "Código colado.";
        }
    }

    private void OnStatus(string message) => _status.Text = message;

    private void OnJoined()
    {
        SetBusy(false);
        EmitSignal(SignalName.JoinedRoom);
    }

    private void OnFailed(string message)
    {
        SetBusy(false);
        _status.Text = message;
    }

    private void SetBusy(bool busy)
    {
        _createButton.Disabled = busy;
        _joinButton.Disabled = busy;
        _hostEnter.Disabled = busy;
    }

    /// <summary>Aceita o código puro ou o texto inteiro do convite colado.</summary>
    private static string ExtractCode(string text)
    {
        text = (text ?? "").ToUpperInvariant();
        int index = text.IndexOf("SALA:", StringComparison.Ordinal);
        if (index >= 0)
            text = text[(index + 5)..];
        string clean = "";
        foreach (char c in text)
        {
            if (char.IsLetterOrDigit(c) && c < 128)
                clean += c;
            else if (clean.Length > 0 && clean.Length < 5)
                clean = ""; // palavra curta demais, continua procurando
            if (clean.Length == 5)
                break;
        }
        return clean.Length == 5 ? clean : (text.Trim().Length <= 5 ? text.Trim() : clean);
    }
}
