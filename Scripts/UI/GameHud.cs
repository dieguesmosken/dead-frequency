// Scripts/UI/GameHud.cs
// HUD mínimo de protótipo: mira, texto de interação, caixa na mão, velocímetro e avisos.
// (No jogo final boa parte disso migra para o painel físico da van — UI diegética.)
// F3 liga/desliga o overlay de debug.
using Godot;

namespace DeadFrequency;

public partial class GameHud : CanvasLayer
{
    [Export] public PlayerController Player { get; set; }
    [Export] public DeliveryManager Delivery { get; set; }

    private ColorRect _crosshair;
    private ColorRect _staminaBack, _staminaFill, _drowsy;
    private ShaderMaterial _drowsyMat;
    private Label _prompt;
    private PanelContainer _cargoPanel;
    private Label _cargoName;
    private ProgressBar _cargoBar;
    private Label _cargoDetails;
    private Label _speed;
    private Label _toast;
    private PanelContainer _toastPanel;
    private PanelContainer _chatPanel;
    private LineEdit _chatInput;
    private Label _clock;
    private Label _debug;
    private PanelContainer _debugAuthPanel;
    private LineEdit _debugPassword;
    private Label _debugAuthMessage;
    private Button _debugAuthButton;
    private PanelContainer _debugToolsPanel;
    private LineEdit _debugMoneyAmount;

    private bool _developerUiBlocking;
    private bool _previousInputEnabled;
    private Input.MouseModeEnum _previousMouseMode;
    private bool _chatPreviousInputEnabled;
    private Input.MouseModeEnum _chatPreviousMouseMode;
    private Viewport.DebugDrawEnum _originalDebugDraw;
    private Viewport.DebugDrawEnum _activeDebugDraw;
    private bool _debugDrawChanged;
    private PanelContainer _jobPanel;
    private Label _jobTitle;
    private Label _jobBody;
    private Label _money;
    private MinimapView _minimap;
    private CompassArrow _compass;
    private Label _status;
    private Label _fps;
    private readonly Button[] _hotbarSlots = new Button[4];

    [Export] public CityGenerator City { get; set; }
    private double _toastTimeLeft;

    public override void _Ready()
    {
        Layer = 1;

        var root = new Control { Theme = UiStyle.Theme, MouseFilter = Control.MouseFilterEnum.Ignore };
        root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(root);

        // Leseira da madrugada: bordas da tela escurecendo (v0.9.5).
        _drowsyMat = new ShaderMaterial { Shader = new Shader { Code = "shader_type canvas_item;\nuniform float amount = 0.0;\nvoid fragment() {\n  float d = distance(UV, vec2(0.5));\n  COLOR = vec4(0.0, 0.0, 0.02, smoothstep(0.2, 0.75, d) * amount);\n}" } };
        _drowsy = new ColorRect { Material = _drowsyMat, MouseFilter = Control.MouseFilterEnum.Ignore, Visible = false };
        _drowsy.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        root.AddChild(_drowsy);

        // Fôlego (aparece quando não está cheio).
        _staminaBack = new ColorRect { Color = new Color(0f, 0f, 0f, 0.55f), MouseFilter = Control.MouseFilterEnum.Ignore, Visible = false };
        Place(_staminaBack, 0.5f, 1f, 0.5f, 1f, -122, -58, 122, -50);
        root.AddChild(_staminaBack);
        _staminaFill = new ColorRect { Color = UiStyle.Phosphor, MouseFilter = Control.MouseFilterEnum.Ignore };
        _staminaBack.AddChild(_staminaFill);

        // Mira
        _crosshair = new ColorRect { Color = new Color(1f, 1f, 1f, 0.7f), MouseFilter = Control.MouseFilterEnum.Ignore };
        Place(_crosshair, 0.5f, 0.5f, 0.5f, 0.5f, -2, -2, 2, 2);
        root.AddChild(_crosshair);

        // Texto de interação
        _prompt = UiStyle.MakeLabel("", 20);
        Place(_prompt, 0.5f, 0.5f, 0.5f, 0.5f, -400, 22, 400, 52);
        root.AddChild(_prompt);

        // Caixa na mão (canto inferior esquerdo)
        _cargoPanel = new PanelContainer { Visible = false, MouseFilter = Control.MouseFilterEnum.Ignore };
        _cargoPanel.AddThemeStyleboxOverride("panel", UiStyle.Box(UiStyle.PanelBackground, UiStyle.PhosphorDim, 2, 12));
        Place(_cargoPanel, 0f, 1f, 0f, 1f, 20, -130, 360, -20);
        root.AddChild(_cargoPanel);

        var cargoStack = new VBoxContainer();
        _cargoPanel.AddChild(cargoStack);
        _cargoName = UiStyle.MakeLabel("", 20, HorizontalAlignment.Left);
        _cargoBar = new ProgressBar { MinValue = 0, MaxValue = 100, Value = 100, CustomMinimumSize = new Vector2(0, 22), MouseFilter = Control.MouseFilterEnum.Ignore };
        _cargoDetails = UiStyle.MakeLabel("", 15, HorizontalAlignment.Left, UiStyle.PhosphorDim);
        cargoStack.AddChild(_cargoName);
        cargoStack.AddChild(_cargoBar);
        cargoStack.AddChild(_cargoDetails);

        // Velocímetro (canto inferior direito)
        _speed = UiStyle.MakeLabel("", 40, HorizontalAlignment.Right);
        Place(_speed, 1f, 1f, 1f, 1f, -320, -80, -24, -24);
        root.AddChild(_speed);

        // Avisos: abaixo da bússola, em caixa com fundo e quebra de linha,
        // para não ficarem por trás do painel de pedido (canto superior direito).
        _toastPanel = new PanelContainer { Visible = false, MouseFilter = Control.MouseFilterEnum.Ignore };
        _toastPanel.AddThemeStyleboxOverride("panel", UiStyle.Box(new Color(0f, 0f, 0f, 0.72f), UiStyle.PhosphorDim, 2, 10));
        Place(_toastPanel, 0.5f, 0f, 0.5f, 0f, -320, 168, 320, 168);
        _toastPanel.GrowVertical = Control.GrowDirection.End;
        _toast = UiStyle.MakeLabel("", 19);
        _toast.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _toast.CustomMinimumSize = new Vector2(600, 0);
        _toastPanel.AddChild(_toast);
        root.AddChild(_toastPanel);

        _chatPanel = new PanelContainer { Visible = false, MouseFilter = Control.MouseFilterEnum.Stop };
        _chatPanel.AddThemeStyleboxOverride("panel", UiStyle.Box(UiStyle.PanelBackground, UiStyle.PhosphorDim, 2, 10));
        Place(_chatPanel, 0.5f, 1f, 0.5f, 1f, -300, -92, 300, -38);
        var chatRow = new HBoxContainer();
        chatRow.AddThemeConstantOverride("separation", 10);
        chatRow.AddChild(UiStyle.MakeLabel("CHAT", 16, color: UiStyle.PhosphorDim));
        _chatInput = new LineEdit
        {
            PlaceholderText = "Mensagem para a sala...",
            MaxLength = 120,
            CustomMinimumSize = new Vector2(480, 38),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        _chatInput.TextSubmitted += SubmitChat;
        chatRow.AddChild(_chatInput);
        _chatPanel.AddChild(chatRow);
        root.AddChild(_chatPanel);

        // Relógio do mundo (topo, acima da bússola).
        _clock = UiStyle.MakeLabel("", 22);
        Place(_clock, 0.5f, 0f, 0.5f, 0f, -160, 18, 160, 52);
        root.AddChild(_clock);

        // Pedido atual (canto superior direito)
        _jobPanel = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        _jobPanel.AddThemeStyleboxOverride("panel", UiStyle.Box(UiStyle.PanelBackground, UiStyle.PhosphorDim, 2, 12));
        Place(_jobPanel, 1f, 0f, 1f, 0f, -520, 20, -20, 150);
        root.AddChild(_jobPanel);
        var jobStack = new VBoxContainer();
        _jobPanel.AddChild(jobStack);
        _jobTitle = UiStyle.MakeLabel("", 16, HorizontalAlignment.Left, UiStyle.PhosphorDim);
        _jobBody = UiStyle.MakeLabel("", 18, HorizontalAlignment.Left);
        _jobBody.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _money = UiStyle.MakeLabel("", 22, HorizontalAlignment.Right);
        jobStack.AddChild(_jobTitle);
        jobStack.AddChild(_jobBody);
        jobStack.AddChild(_money);

        // Debug (F3)
        _debug = UiStyle.MakeLabel("", 14, HorizontalAlignment.Left);
        _debug.Visible = false;
        Place(_debug, 0f, 0f, 0f, 0f, 12, 12, 760, 330);
        root.AddChild(_debug);

        // Autenticação das ferramentas extras (F10). Não interfere no overlay público F3.
        _debugAuthPanel = new PanelContainer { Visible = false, MouseFilter = Control.MouseFilterEnum.Stop };
        _debugAuthPanel.AddThemeStyleboxOverride("panel", UiStyle.Box(UiStyle.PanelBackground, UiStyle.PhosphorDim, 2, 12));
        Place(_debugAuthPanel, 0.5f, 0.5f, 0.5f, 0.5f, -210, -105, 210, 105);
        var authStack = new VBoxContainer { Theme = UiStyle.Theme };
        _debugAuthPanel.AddChild(authStack);
        authStack.AddChild(UiStyle.MakeLabel("DEVELOPER ACCESS", 20, HorizontalAlignment.Center));
        authStack.AddChild(UiStyle.MakeLabel("Autentique para liberar ferramentas extras", 13, HorizontalAlignment.Center, UiStyle.PhosphorDim));
        _debugPassword = new LineEdit
        {
            PlaceholderText = "Senha de desenvolvedor",
            Secret = true,
            CustomMinimumSize = new Vector2(380, 38),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        _debugPassword.TextSubmitted += _ => SubmitDebugPassword();
        authStack.AddChild(_debugPassword);
        _debugAuthMessage = UiStyle.MakeLabel("Acesso protegido · validade de 30 minutos", 13, HorizontalAlignment.Center, UiStyle.PhosphorDim);
        authStack.AddChild(_debugAuthMessage);
        var authButtons = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        _debugAuthButton = new Button { Text = "AUTENTICAR", CustomMinimumSize = new Vector2(150, 38) };
        _debugAuthButton.Pressed += SubmitDebugPassword;
        authButtons.AddChild(_debugAuthButton);
        var authClose = new Button { Text = "FECHAR", CustomMinimumSize = new Vector2(100, 38) };
        authClose.Pressed += CloseDeveloperPanels;
        authButtons.AddChild(authClose);
        authStack.AddChild(authButtons);
        root.AddChild(_debugAuthPanel);

        // Ferramentas extras: somente após autenticação válida.
        _debugToolsPanel = new PanelContainer { Visible = false, MouseFilter = Control.MouseFilterEnum.Stop };
        _debugToolsPanel.AddThemeStyleboxOverride("panel", UiStyle.Box(UiStyle.PanelBackground, UiStyle.PhosphorDim, 2, 12));
        Place(_debugToolsPanel, 0.5f, 0.5f, 0.5f, 0.5f, -235, -190, 235, 190);
        var toolsStack = new VBoxContainer { Theme = UiStyle.Theme };
        _debugToolsPanel.AddChild(toolsStack);
        toolsStack.AddChild(UiStyle.MakeLabel("DEVELOPER TOOLS", 20, HorizontalAlignment.Center));
        toolsStack.AddChild(UiStyle.MakeLabel("Ferramentas locais e experimentais", 13, HorizontalAlignment.Center, UiStyle.PhosphorDim));

        var wireframeButton = new Button { Text = "WIREFRAME", CustomMinimumSize = new Vector2(420, 36) };
        wireframeButton.Pressed += () => ToggleDebugDraw(Viewport.DebugDrawEnum.Wireframe);
        toolsStack.AddChild(wireframeButton);

        var noTexturesButton = new Button { Text = "SEM TEXTURAS", CustomMinimumSize = new Vector2(420, 36) };
        noTexturesButton.Pressed += () => ToggleDebugDraw(Viewport.DebugDrawEnum.Lighting);
        toolsStack.AddChild(noTexturesButton);

        _debugMoneyAmount = new LineEdit { PlaceholderText = "Valor (1 a 1.000.000)", CustomMinimumSize = new Vector2(420, 36) };
        toolsStack.AddChild(_debugMoneyAmount);
        var addMoneyButton = new Button { Text = "ADICIONAR DINHEIRO", CustomMinimumSize = new Vector2(420, 36) };
        addMoneyButton.Pressed += SubmitDebugAddMoney;
        toolsStack.AddChild(addMoneyButton);
        var spawnButton = new Button { Text = "SPAWN DE ITENS · INDISPONÍVEL", Disabled = true, CustomMinimumSize = new Vector2(420, 36) };
        toolsStack.AddChild(spawnButton);
        var flyButton = new Button { Text = "FLY · INDISPONÍVEL", Disabled = true, CustomMinimumSize = new Vector2(420, 36) };
        toolsStack.AddChild(flyButton);
        var spectatorButton = new Button { Text = "SPECTATOR · INDISPONÍVEL", Disabled = true, CustomMinimumSize = new Vector2(420, 36) };
        toolsStack.AddChild(spectatorButton);
        var collisionButton = new Button { Text = "CAIXAS DE COLISÃO · INDISPONÍVEL", Disabled = true, CustomMinimumSize = new Vector2(420, 36) };
        toolsStack.AddChild(collisionButton);

        var toolsClose = new Button { Text = "FECHAR", CustomMinimumSize = new Vector2(420, 36) };
        toolsClose.Pressed += CloseDeveloperPanels;
        toolsStack.AddChild(toolsClose);
        root.AddChild(_debugToolsPanel);

        // Minimapa (só com o radar equipado), canto superior esquerdo.
        _minimap = new MinimapView { Player = Player, City = City, Delivery = Delivery, Visible = false };
        Place(_minimap, 0f, 0f, 0f, 0f, 20, 20, 240, 240);
        root.AddChild(_minimap);

        // Seta para o depósito.
        _compass = new CompassArrow { Player = Player, Target = Vector3.Zero };
        Place(_compass, 0.5f, 0f, 0.5f, 0f, -200, 60, 200, 130);
        root.AddChild(_compass);

        // Efeitos ativos (energético, visão noturna) no rodapé.
        _status = UiStyle.MakeLabel("", 15, HorizontalAlignment.Center, UiStyle.PhosphorDim);
        Place(_status, 0.5f, 1f, 0.5f, 1f, -300, -40, 300, -14);
        root.AddChild(_status);

        // Contador de FPS (Opções > Interface).
        _fps = UiStyle.MakeLabel("", 15, HorizontalAlignment.Right, UiStyle.PhosphorDim);
        Place(_fps, 1f, 1f, 1f, 1f, -200, -110, -24, -86);
        root.AddChild(_fps);

        var hotbar = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        hotbar.AddThemeConstantOverride("separation", 6);
        Place(hotbar, 0.5f, 1f, 0.5f, 1f, -270, -102, 270, -42);
        for (int i = 0; i < _hotbarSlots.Length; i++)
        {
            int index = i;
            var slot = new Button
            {
                ToggleMode = true,
                CustomMinimumSize = new Vector2(128, 52),
                MouseFilter = Control.MouseFilterEnum.Stop,
                FocusMode = Control.FocusModeEnum.None,
            };
            slot.Pressed += () => PlayerInventory.Instance?.SelectHotbar(index);
            _hotbarSlots[i] = slot;
            hotbar.AddChild(slot);
        }
        root.AddChild(hotbar);

        root.AddChild(UiStyle.MakeScanlines(0.06f));

        if (Player == null)
            GD.PushWarning("GameHud: campo Player não atribuído no Inspector.");
        else
            Player.ItemActionMessage += OnItemActionMessage;
        if (PlayerInventory.Instance != null)
            PlayerInventory.Instance.Changed += RefreshHotbar;

        if (Delivery != null)
        {
            Delivery.DeliveryCompleted += (name, payment) => ShowToast($"{name} entregue! +{payment}", UiStyle.Phosphor, 3.5);
            Delivery.CargoLost += name => ShowToast($"{name} perdida — sem pagamento", UiStyle.Danger, 3.5);
            Delivery.ShiftStarted += shift => ShowToast($"TURNO {shift} — pegue as caixas no depósito", UiStyle.Phosphor, 4.0);
            Delivery.ShiftCompleted += (shift, earnings) => ShowToast($"Turno {shift} concluído · ganhos {earnings}", UiStyle.Warning, 4.0);
        }

        if (NetClient.Instance != null)
        {
            NetClient.Instance.MessageReceived += OnNetMessage;
            NetClient.Instance.Disconnected += OnNetDisconnected;
            NetClient.Instance.DebugAuthResult += OnDebugAuthResult;
        }

        foreach (Node node in GetTree().GetNodesInGroup(CargoItem.GroupName))
            WatchCargo(node);
        GetTree().NodeAdded += WatchCargo;
    }

    public override void _ExitTree()
    {
        // O SceneTree sobrevive à troca de cena: precisa desconectar.
        RevokeDeveloperTools();
        GetTree().NodeAdded -= WatchCargo;
        if (Player != null && IsInstanceValid(Player))
            Player.ItemActionMessage -= OnItemActionMessage;
        if (PlayerInventory.Instance != null)
            PlayerInventory.Instance.Changed -= RefreshHotbar;
        if (NetClient.Instance != null)
        {
            NetClient.Instance.MessageReceived -= OnNetMessage;
            NetClient.Instance.Disconnected -= OnNetDisconnected;
            NetClient.Instance.DebugAuthResult -= OnDebugAuthResult;
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is not InputEventKey { Pressed: true, Echo: false } key)
            return;

        if (_chatPanel.Visible && key.Keycode == Key.Escape)
        {
            CloseChat();
            GetViewport().SetInputAsHandled();
            return;
        }

        if (key.Keycode == Key.T && NetClient.Instance?.IsOnline == true
            && !_chatPanel.Visible && !ModalScreen.AnyOpen
            && !_debugAuthPanel.Visible && !_debugToolsPanel.Visible)
        {
            OpenChat();
            GetViewport().SetInputAsHandled();
            return;
        }

        // F3 controla exclusivamente o overlay público.
        if (key.Keycode == Key.F3)
        {
            _debug.Visible = !_debug.Visible;
            if (!_debug.Visible)
                CloseDeveloperPanels();
            GetViewport().SetInputAsHandled();
            return;
        }

        // F10 abre login ou ferramentas, sem alterar o overlay público F3.
        if (key.Keycode == Key.F10 && _debug.Visible)
        {
            if (_debugAuthPanel.Visible || _debugToolsPanel.Visible)
                CloseDeveloperPanels();
            else if (NetClient.Instance?.IsDebugAuthorized == true)
                OpenDeveloperTools();
            else
                OpenDeveloperAuth();
            GetViewport().SetInputAsHandled();
        }
    }

    private void BeginDeveloperUi()
    {
        if (_developerUiBlocking)
            return;
        _developerUiBlocking = true;
        _previousInputEnabled = Player?.InputEnabled ?? true;
        _previousMouseMode = Input.MouseMode;
        if (Player != null)
            Player.InputEnabled = false;
        Input.MouseMode = Input.MouseModeEnum.Visible;
    }

    private void EndDeveloperUi()
    {
        if (!_developerUiBlocking)
            return;
        _developerUiBlocking = false;
        if (Player != null && IsInstanceValid(Player))
            Player.InputEnabled = _previousInputEnabled;
        Input.MouseMode = _previousMouseMode;
    }

    private void OpenDeveloperAuth()
    {
        _debugToolsPanel.Visible = false;
        _debugAuthPanel.Visible = true;
        _debugPassword.Clear();
        _debugAuthButton.Disabled = false;
        _debugAuthMessage.Text = "Acesso protegido · validade de 30 minutos";
        _debugAuthMessage.RemoveThemeColorOverride("font_color");
        BeginDeveloperUi();
        _debugPassword.CallDeferred(Control.MethodName.GrabFocus);
    }

    private void OpenDeveloperTools()
    {
        if (NetClient.Instance?.IsDebugAuthorized != true)
        {
            OpenDeveloperAuth();
            return;
        }
        _debugAuthPanel.Visible = false;
        _debugToolsPanel.Visible = true;
        BeginDeveloperUi();
    }

    private void CloseDeveloperPanels()
    {
        if (_debugAuthPanel != null)
            _debugAuthPanel.Visible = false;
        if (_debugToolsPanel != null)
            _debugToolsPanel.Visible = false;
        EndDeveloperUi();
    }

    private void RevokeDeveloperTools()
    {
        CloseDeveloperPanels();
        RestoreDebugDraw();
        _activeDebugDraw = Viewport.DebugDrawEnum.Disabled;
    }

    private void ToggleDebugDraw(Viewport.DebugDrawEnum mode)
    {
        if (!_debugDrawChanged)
        {
            _originalDebugDraw = GetViewport().DebugDraw;
            _debugDrawChanged = true;
        }

        if (_activeDebugDraw == mode && GetViewport().DebugDraw == mode)
        {
            RestoreDebugDraw();
            return;
        }

        _activeDebugDraw = mode;
        GetViewport().DebugDraw = mode;
    }

    private void RestoreDebugDraw()
    {
        if (!_debugDrawChanged)
            return;
        GetViewport().DebugDraw = _originalDebugDraw;
        _debugDrawChanged = false;
    }

    private void SubmitDebugAddMoney()
    {
        if (NetClient.Instance?.IsDebugAuthorized != true) { ShowToast("Autorização expirada.", UiStyle.Warning); return; }
        if (!int.TryParse(_debugMoneyAmount?.Text, out int amount) || amount < 1 || amount > 1_000_000)
        { ShowToast("Informe um valor entre 1 e 1.000.000.", UiStyle.Warning); return; }
        NetClient.Instance.RequestDebugAddMoney(amount);
        _debugMoneyAmount.Clear();
    }

    private void SubmitDebugPassword()
    {
        string password = _debugPassword?.Text ?? "";
        if (string.IsNullOrWhiteSpace(password))
        {
            _debugAuthMessage.Text = "Digite a senha para continuar.";
            return;
        }

        if (NetClient.Instance == null || !NetClient.Instance.IsOnline)
        {
            _debugAuthMessage.Text = "Entre em uma sala online para autenticar.";
            return;
        }

        _debugAuthButton.Disabled = true;
        _debugAuthMessage.Text = "Validando senha...";
        NetClient.Instance.RequestDebugAuth(password);
        _debugPassword.Clear();
    }

    private void OnDebugAuthResult(bool success, string message)
    {
        _debugAuthButton.Disabled = false;
        _debugAuthMessage.Text = message;
        _debugAuthMessage.AddThemeColorOverride("font_color", success ? UiStyle.Phosphor : UiStyle.Warning);
        if (success)
        {
            ShowToast("Ferramentas de desenvolvedor autorizadas por 30 minutos.", UiStyle.Phosphor, 4.0);
            if (_debug.Visible)
                OpenDeveloperTools();
            else
                CloseDeveloperPanels();
        }
    }

    public override void _Process(double delta)
    {
        RefreshHotbar();
        if ((_debugDrawChanged || _debugToolsPanel.Visible) && NetClient.Instance?.IsDebugAuthorized != true)
        {
            RevokeDeveloperTools();
            ShowToast("A autorização das ferramentas expirou.", UiStyle.Warning, 4.0);
        }

        if (_toastTimeLeft > 0)
        {
            _toastTimeLeft -= delta;
            _toastPanel.Modulate = new Color(1f, 1f, 1f, Mathf.Clamp((float)_toastTimeLeft, 0f, 1f));
            if (_toastTimeLeft <= 0)
                _toastPanel.Visible = false;
        }

        _clock.Text = DayNightController.ClockText();

        if (Player == null || !IsInstanceValid(Player))
            return;

        _prompt.Text = Player.CurrentPrompt;

        bool hasTarget = Player.HasTarget;
        _crosshair.Color = hasTarget ? UiStyle.Phosphor : new Color(1f, 1f, 1f, 0.7f);
        float half = hasTarget ? 4f : 2f;
        Place(_crosshair, 0.5f, 0.5f, 0.5f, 0.5f, -half, -half, half, half);
        GameSettings gs = GameSettings.Instance;
        _crosshair.Visible = (gs?.ShowCrosshair ?? true) && (Player.State == PlayerState.Walking || hasTarget);
        _fps.Visible = gs?.ShowFps ?? false;
        if (_fps.Visible)
            _fps.Text = $"{Engine.GetFramesPerSecond()} FPS";
        _status.Visible = gs?.ShowKeyHints ?? true;

        CargoItem held = Player.HeldItem;
        _cargoPanel.Visible = held != null;
        if (held != null)
        {
            _cargoName.Text = held.Data?.CargoName ?? held.Name;
            _cargoBar.Value = held.Integrity * 100f;
            _cargoDetails.Text = $"{held.WeightKg:0.#} kg · pagamento {held.GetCurrentPayment()}";
        }

        VanController van = Player.CurrentVehicle;
        _speed.Visible = van != null;
        if (van != null)
            _speed.Text = GameSettings.Instance?.SpeedUnit == 1 ? $"{van.SpeedKmh * 0.621371f:000} mph" : $"{van.SpeedKmh:000} km/h";

        UpdateJobPanel();
        UpdateItems();
        UpdateCompass();

        if (_debug.Visible)
            UpdateDebug((float)delta);
    }

    // ---------------- Debug (F3) ----------------

    private double _cpuLastTotal, _cpuLastWall;
    private float _cpuPercent, _debugTimer;
    private string _debugStats = "";

    private void UpdateDebug(float dt)
    {
        // Números "pesados" (RAM, CPU) só uma vez por segundo.
        _debugTimer -= dt;
        if (_debugTimer <= 0f)
        {
            _debugTimer = 1f;
            var proc = System.Diagnostics.Process.GetCurrentProcess();
            double total = proc.TotalProcessorTime.TotalSeconds;
            double wall = Time.GetTicksMsec() / 1000.0;
            if (_cpuLastWall > 0 && wall > _cpuLastWall)
                _cpuPercent = (float)((total - _cpuLastTotal) / (wall - _cpuLastWall) / System.Environment.ProcessorCount * 100.0);
            _cpuLastTotal = total;
            _cpuLastWall = wall;
            double ramMb = proc.WorkingSet64 / 1048576.0;
            double godotMb = OS.GetStaticMemoryUsage() / 1048576.0;
            double gcMb = System.GC.GetTotalMemory(false) / 1048576.0;
            double vramMb = Performance.GetMonitor(Performance.Monitor.RenderVideoMemUsed) / 1048576.0;
            _debugStats = $"RAM {ramMb:0} MB (engine {godotMb:0} · C# {gcMb:0}) · VRAM {vramMb:0} MB\n" +
                          $"CPU {_cpuPercent:0}% de {System.Environment.ProcessorCount} núcleos · {OS.GetProcessorName()}\n" +
                          $"GPU {RenderingServer.GetVideoAdapterName()}";
        }

        Vector3 p = Player.GlobalPosition;
        float yaw = Mathf.PosMod(-Mathf.RadToDeg(Player.GlobalRotation.Y), 360f);
        string[] dirs = { "N", "NE", "L", "SE", "S", "SO", "O", "NO" };
        string facing = dirs[Mathf.PosMod(Mathf.RoundToInt(yaw / 45f), 8)];
        string where = "";
        CityGenerator city = ProfessionManager.Instance?.City;
        if (city != null && city.Biome != WorldBiome.Farm)
        {
            float period = city.Period;
            int bx = Mathf.RoundToInt(p.X / period), bz = Mathf.RoundToInt(p.Z / period);
            int sx = Mathf.RoundToInt(p.X / period - 0.5f), sz = Mathf.RoundToInt(p.Z / period - 0.5f);
            where = $"Quarteirão ({bx}, {bz}) · perto de {city.StreetNameX(sx)} × {city.StreetNameZ(sz)}\n";
        }
        else if (city != null)
        {
            where = "Mapa: Sítio\n";
        }

        double frameMs = Performance.GetMonitor(Performance.Monitor.TimeProcess) * 1000.0;
        double physMs = Performance.GetMonitor(Performance.Monitor.TimePhysicsProcess) * 1000.0;
        _debug.Text = $"Dead Frequency {GameVersion.Display}  ·  FPS {Engine.GetFramesPerSecond()}  ({frameMs:0.0} ms lógica · {physMs:0.0} ms física)\n" +
                      $"Posição X {p.X:0.0}  Y {p.Y:0.0}  Z {p.Z:0.0}  ·  olhando {facing} ({yaw:0}°)\n" +
                      where +
                      _debugStats + "\n" +
                      $"Draw calls {Performance.GetMonitor(Performance.Monitor.RenderTotalDrawCallsInFrame):0} · objetos {Performance.GetMonitor(Performance.Monitor.RenderTotalObjectsInFrame):0} · nós {Performance.GetMonitor(Performance.Monitor.ObjectNodeCount):0}\n" +
                      $"Estado: {Player.State}" + (Player.CurrentVehicle != null ? $" ({Player.CurrentVehicle.DisplayName} {Player.CurrentVehicle.SpeedKmh:0} km/h)" : "") + "\n" +
                      $"Mirando: {Player.DebugLookingAt}\n" +
                      $"Prompt: {(string.IsNullOrEmpty(Player.CurrentPrompt) ? "-" : Player.CurrentPrompt)}";
    }

    /// <summary>A seta aponta para o lugar de trabalho da profissão (ou para o depósito).</summary>
    private void UpdateCompass()
    {
        ProfessionManager pm = ProfessionManager.Instance;
        if (pm == null || !pm.TryGetTarget(out Vector3 target, out string label))
            return;
        _compass.Target = target;
        _compass.Label = label;
        bool depot = label == "DEPÓSITO";
        if (label == "CASA")
        {
            _compass.HideDistance = 3f;
            _compass.Color = new Color(1f, 0.85f, 0.5f);
            return;
        }
        _compass.HideDistance = depot ? 30f : 4f;
        _compass.Color = depot ? new Color(1f, 0.6f, 0.15f) : Professions.Get(pm.Current).Color;
    }

    private void UpdateItems()
    {
        PlayerInventory inv = PlayerInventory.Instance;
        bool radar = inv?.IsEquipped(ItemCatalog.Radar) == true;
        _minimap.Visible = radar;
        // Com o minimapa aberto, o debug desce um pouco para não ficar por cima.
        _debug.OffsetTop = radar ? 250 : 12;
        _debug.OffsetBottom = _debug.OffsetTop + 320;

        // Fôlego e leseira.
        float st = Player.Stamina / PlayerController.MaxStamina;
        _staminaBack.Visible = st < 0.995f && Player.State == PlayerState.Walking;
        _staminaFill.Size = new Vector2(240f * st, 4f);
        _staminaFill.Position = new Vector2(2f, 2f);
        _staminaFill.Color = Player.Exhausted ? UiStyle.Warning : st < 0.3f ? new Color(1f, 0.7f, 0.2f) : UiStyle.Phosphor;
        float drowsy = Mathf.Clamp((Player.Drowsiness - 35f) / 65f, 0f, 1f);
        _drowsy.Visible = drowsy > 0.01f;
        _drowsyMat.SetShaderParameter("amount", drowsy * (0.85f + Mathf.Sin(Time.GetTicksMsec() / 900f) * 0.15f));

        var parts = new System.Collections.Generic.List<string>();
        if (Player.Exhausted)
            parts.Add("SEM FÔLEGO");
        if (Player.Drowsiness > 50f)
            parts.Add("LESEIRA (tome um café)");
        if (Player.SlowLeft > 0f)
            parts.Add($"PÓS-ALMOÇO {Player.SlowLeft:0}s");
        if (Player.CoffeeLeft > 0f)
            parts.Add($"CAFÉ {Player.CoffeeLeft / 60f:0}:{Player.CoffeeLeft % 60f:00}");
        if (Player.StaminaBoostLeft > 0f)
            parts.Add($"GUARANÁ {Player.StaminaBoostLeft:0}s");
        if (inv?.IsEquipped(ItemCatalog.SlippersBroken) == true)
            parts.Add("CHINELO ARREBENTADO (use um prego)");
        if (FarmAnimal.Companion != null)
            parts.Add("CARAMELO TE SEGUINDO");
        if (Player.SpeedBoostLeft > 0f)
            parts.Add($"ENERGÉTICO {Player.SpeedBoostLeft:0}s");
        if (inv?.IsEquipped(ItemCatalog.NightVision) == true)
            parts.Add(Player.NightVisionOn ? "VISÃO NOTURNA [N]" : "[N] visão noturna");
        if (inv?.IsEquipped(ItemCatalog.Gloves) == true)
            parts.Add($"LUVAS · até {Player.MaxCarryWeightKg:0} kg");
        parts.Add("[I] inventário");
        _status.Text = string.Join("   ", parts);
    }

    private void UpdateJobPanel()
    {
        _jobPanel.Visible = Delivery != null;
        if (Delivery == null)
            return;

        NetClient net = NetClient.Instance;
        string room = net != null && net.IsOnline ? $"SALA {net.RoomCode} · {net.PlayerCount} online · " : "";
        ProfessionManager pm = ProfessionManager.Instance;
        if (pm != null && pm.Current != Professions.Entregador)
        {
            _jobTitle.Text = room + pm.PanelTitle();
            _jobTitle.AddThemeColorOverride("font_color", Professions.Get(pm.Current).Color);
            _jobBody.Text = pm.PanelText();
            _money.Text = $"$ {Delivery.Money}";
            return;
        }
        _jobTitle.RemoveThemeColorOverride("font_color");
        _jobTitle.AddThemeColorOverride("font_color", UiStyle.PhosphorDim);
        _jobTitle.Text = $"{room}TURNO {Delivery.Shift} · {Delivery.ShiftDelivered}/{Delivery.ShiftTotal}";
        _money.Text = $"$ {Delivery.Money}";

        if (Delivery.Jobs.Count == 0)
        {
            _jobBody.Text = "Aguardando novo turno...";
            return;
        }

        var lines = new System.Text.StringBuilder();
        CargoItem held = Player.HeldItem;
        int shown = 0;
        foreach (DeliveryJob job in Delivery.Jobs)
        {
            if (shown++ >= 5)
            {
                lines.Append($"+{Delivery.Jobs.Count - 5} pedidos...");
                break;
            }
            string marker = job.Cargo == held && held != null ? "► " : "· ";
            lines.Append($"{marker}{job.CargoName} → {job.Point.Address} ({Delivery.DistanceTo(job):0} m)\n");
        }
        _jobBody.Text = lines.ToString().TrimEnd();
    }

    private void OnNetMessage(System.Text.Json.JsonElement m)
    {
        string type = m.GetProperty("t").GetString();
        switch (type)
        {
            case "join":
                ShowToast($"{m.GetProperty("player").GetProperty("name").GetString()} entrou na sala", UiStyle.Phosphor);
                break;
            case "leave":
                // O NetClient já removeu da lista; mostramos só o aviso genérico.
                ShowToast("Um entregador saiu da sala", UiStyle.PhosphorDim);
                break;
            case "chat":
                ShowToast($"{m.GetProperty("name").GetString()}: {m.GetProperty("msg").GetString()}", Colors.White, 5.0);
                break;
            case "chatFail":
                ShowToast(m.TryGetProperty("msg", out var chatFail) ? chatFail.GetString() : "Aguarde antes de enviar outra mensagem.", UiStyle.Warning, 3.0);
                break;
            case "itemTransferFail":
                ShowToast(m.TryGetProperty("msg", out var transferFail) ? transferFail.GetString() : "Nao foi possivel entregar o item.", UiStyle.Warning, 3.0);
                break;
            case "itemDropFail":
            case "itemPickupFail":
                ShowToast(m.TryGetProperty("msg", out var itemFail) ? itemFail.GetString() : "Acao de item recusada.", UiStyle.Warning, 3.0);
                break;
            case "itemTransferred":
                if (m.TryGetProperty("to", out var receiver) && receiver.GetInt32() == NetClient.Instance?.MyId)
                    ShowToast("Um jogador entregou um item para voce.", UiStyle.Phosphor, 3.0);
                break;
            case "claimFail":
                ShowToast("Outro entregador está usando isso", UiStyle.Warning);
                break;
            case "debug_add_money_result":
                if (m.TryGetProperty("ok", out var moneyOk) && moneyOk.ValueKind == System.Text.Json.JsonValueKind.True)
                    ShowToast($"+{m.GetProperty("added").GetInt32():N0} adicionados · saldo: {m.GetProperty("money").GetInt32():N0}", UiStyle.Phosphor, 5.0);
                else
                    ShowToast(m.TryGetProperty("msg", out var moneyMsg) ? moneyMsg.GetString() : "Não foi possível adicionar dinheiro.", UiStyle.Warning, 4.0);
                break;
        }
    }

    private void OnNetDisconnected(string message)
    {
        CloseChat();
        RevokeDeveloperTools();
        ShowToast(message + " Voltando ao menu...", UiStyle.Danger, 5.0);
    }

    private void OpenChat()
    {
        if (Player == null || !IsInstanceValid(Player))
            return;
        _chatPreviousInputEnabled = Player.InputEnabled;
        _chatPreviousMouseMode = Input.MouseMode;
        Player.InputEnabled = false;
        Input.MouseMode = Input.MouseModeEnum.Visible;
        _chatInput.Clear();
        _chatPanel.Visible = true;
        _chatInput.CallDeferred(Control.MethodName.GrabFocus);
    }

    private void CloseChat()
    {
        if (_chatPanel == null || !_chatPanel.Visible)
            return;
        _chatPanel.Visible = false;
        if (Player != null && IsInstanceValid(Player))
            Player.InputEnabled = _chatPreviousInputEnabled;
        Input.MouseMode = _chatPreviousMouseMode;
    }

    private void SubmitChat(string text)
    {
        string message = text.Trim();
        if (message.Length > 0)
            NetClient.Instance?.Send(new { t = "chat", msg = message });
        CloseChat();
    }

    public const string GroupName = "game_hud";

    public override void _EnterTree() => AddToGroup(GroupName);

    /// <summary>Atalho para qualquer script mostrar um aviso no topo da tela.</summary>
    public static void Toast(Node from, string text, Color? color = null, double seconds = 3.0)
    {
        if (from?.GetTree()?.GetFirstNodeInGroup(GroupName) is GameHud hud)
            hud.ShowToast(text, color, seconds);
    }

    /// <summary>Tela escurece e volta (dormir, elevador).</summary>
    public static void FadeBlack(Node from, double seconds = 1.0)
    {
        if (from?.GetTree()?.GetFirstNodeInGroup(GroupName) is not GameHud hud)
            return;
        var rect = new ColorRect { Color = new Color(0f, 0f, 0f, 0f), MouseFilter = Control.MouseFilterEnum.Ignore };
        rect.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        hud.AddChild(rect);
        Tween tw = rect.CreateTween();
        tw.TweenProperty(rect, "color:a", 1.0, seconds * 0.4);
        tw.TweenInterval(seconds * 0.2);
        tw.TweenProperty(rect, "color:a", 0.0, seconds * 0.4);
        tw.TweenCallback(Callable.From(rect.QueueFree));
    }

    public void ShowToast(string text, Color? color = null, double seconds = 3.0)
    {
        _toast.Text = text;
        _toast.AddThemeColorOverride("font_color", color ?? UiStyle.Phosphor);
        _toastTimeLeft = seconds * (GameSettings.Instance?.ToastDuration ?? 1f);
        _toastPanel.Visible = true;
        _toastPanel.Modulate = Colors.White;
        _toastPanel.ResetSize();
    }

    private void OnItemActionMessage(string message) => ShowToast(message, UiStyle.Phosphor, 3.0);

    private void RefreshHotbar()
    {
        PlayerInventory inventory = PlayerInventory.Instance;
        if (inventory == null)
            return;
        for (int i = 0; i < _hotbarSlots.Length; i++)
        {
            Button slot = _hotbarSlots[i];
            if (slot == null)
                continue;
            string itemId = inventory.HotbarItem(i);
            ItemDef def = itemId == null ? null : ItemCatalog.Get(itemId);
            slot.Text = def == null ? $"{i + 1}\n—" : $"{i + 1}  {def.Name}";
            slot.Disabled = def == null;
            slot.ButtonPressed = inventory.SelectedHotbarIndex == i;
            slot.AddThemeColorOverride("font_color", def?.Color ?? UiStyle.PhosphorDim);
        }
    }

    private void WatchCargo(Node node)
    {
        if (node is not CargoItem cargo)
            return;

        cargo.IntegrityChanged += integrity =>
        {
            string name = cargo.Data?.CargoName ?? cargo.Name;
            Color color = integrity > 0.6f ? UiStyle.Phosphor : integrity > 0.3f ? UiStyle.Warning : UiStyle.Danger;
            ShowToast($"{name}: {integrity:P0}", color);
        };
        cargo.CargoDestroyed += () => ShowToast($"{cargo.Data?.CargoName ?? cargo.Name} DESTRUÍDO", UiStyle.Danger, 4.0);
    }

    private static void Place(Control control, float anchorLeft, float anchorTop, float anchorRight, float anchorBottom,
        float left, float top, float right, float bottom)
    {
        control.AnchorLeft = anchorLeft;
        control.AnchorTop = anchorTop;
        control.AnchorRight = anchorRight;
        control.AnchorBottom = anchorBottom;
        control.OffsetLeft = left;
        control.OffsetTop = top;
        control.OffsetRight = right;
        control.OffsetBottom = bottom;
    }
}
