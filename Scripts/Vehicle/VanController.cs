// Scripts/Vehicle/VanController.cs
// Física de direção da van. Só lê os controles enquanto há um motorista sentado.
// A frente da van é o +Z local (convenção do VehicleBody3D).
//
// Cena: Scenes/Vehicle/van.tscn
// Van (VehicleBody3D + este script)
//  ├── *Shape (CollisionShape3D: chassi, cabine, laterais e tampa da caçamba)
//  ├── Body (malhas)          ├── Cabin/SteeringWheel (Node3D que gira com o volante)
//  ├── Wheel* (4x VehicleWheel3D)
//  ├── SeatMarker / ExitMarker (Marker3D)
//  ├── DriverDoor (Area3D + DriverSeatInteractable)
//  └── Headlights (2x SpotLight3D)
using Godot;

namespace DeadFrequency;

public partial class VanController : VehicleBody3D
{
    [ExportGroup("Motor e freio")]
    [Export] public float MaxEngineForce { get; set; } = 2600f;
    [Export] public float MaxReverseForce { get; set; } = 1300f;
    [Export] public float BrakeForce { get; set; } = 45f;
    [Export] public float HandbrakeForce { get; set; } = 90f;
    /// <summary>Freio leve quando ninguém acelera (freio-motor).</summary>
    [Export] public float CoastBrake { get; set; } = 3f;
    /// <summary>Freio quando não há motorista (van estacionada).</summary>
    [Export] public float ParkedBrake { get; set; } = 30f;
    [Export] public float MaxSpeedKmh { get; set; } = 95f;

    [ExportGroup("Direção")]
    [Export(PropertyHint.Range, "10,45,1")] public float MaxSteerDegrees { get; set; } = 32f;
    /// <summary>Velocidade de giro do volante (rad/s).</summary>
    [Export] public float SteerSpeed { get; set; } = 2.5f;
    /// <summary>Acima dessa velocidade (m/s) o esterço máximo cai pela metade.</summary>
    [Export] public float SteerFalloffSpeed { get; set; } = 25f;
    /// <summary>Quanto do esterço sobra na velocidade de SteerFalloffSpeed (0,35 = 35%). Menos esterço rápido = menos capotagem.</summary>
    [Export] public float SteerAtSpeed { get; set; } = 0.35f;
    /// <summary>Estabilizador (barra estabilizadora "de jogo"): torque que puxa a carroceria de volta para o nível nas curvas.</summary>
    [Export] public float AntiRoll { get; set; } = 2.2f;
    /// <summary>Velocidade máxima de rolagem/arfagem (rad/s): segura a carroceria quando passa de lombada ou bate de lado.</summary>
    [Export] public float MaxTiltRate { get; set; } = 1.6f;
    /// <summary>Quantas vezes o volante gira mais que as rodas (visual).</summary>
    [Export] public float SteeringWheelRatio { get; set; } = 5f;

    [ExportGroup("Som")]
    [Export] public AudioStream EngineSound { get; set; }
    [Export] public AudioStream DoorSound { get; set; }

    [ExportGroup("Saída")]
    /// <summary>Velocidade máxima (m/s) em que o motorista pode descer.</summary>
    [Export] public float ExitMaxSpeed { get; set; } = 2f;

    [ExportGroup("Identidade")]
    /// <summary>Id no online ("van", "moto", "truck_yellow", "truck_red"). Precisa existir no servidor (VEHICLES).</summary>
    [Export] public string NetId { get; set; } = "van";
    /// <summary>"van" monta o acabamento da van (VanDetails); "moto"/"truck" usam o modelo próprio.</summary>
    [Export] public string Kind { get; set; } = "van";
    /// <summary>Ajuste da altura dos olhos em primeira pessoa, sentado (a cabeça do jogador fica 1,6 m acima do banco;
    /// nos carros baixos isso passava do teto). Definido pela VehicleFactory.</summary>
    public float EyeOffsetY { get; set; }

    /// <summary>Piloto automático (Modo História, ChapterTransitionZone): segue reto mantendo a velocidade.</summary>
    public bool AutoDrive { get; private set; }
    private float _autoDriveSpeed;

    /// <summary>Liga/desliga o piloto automático. speedMs = velocidade alvo em m/s; o volante fica reto.</summary>
    public void SetAutoDrive(bool on, float speedMs)
    {
        AutoDrive = on;
        _autoDriveSpeed = Mathf.Max(0f, speedMs);
        if (on)
            Sleeping = false;
        // A viagem não pode morrer no meio: se o motor estiver desligado, dá a partida.
        if (on && Engine != null && !Engine.Running && !Engine.Cranking)
            Engine.Start(false);
    }
    [Export] public string DisplayName { get; set; } = "van";

    public const string GroupName = "vehicles";

    public PlayerController Driver { get; private set; }
    /// <summary>Outro jogador está dirigindo: aqui a van só segue o estado recebido.</summary>
    public bool IsRemote { get; private set; }
    /// <summary>Há um motorista remoto (para o som do motor e faróis).</summary>
    public bool HasRemoteDriver { get; set; }
    private Vector3 _remotePosition;
    private Quaternion _remoteRotation = Quaternion.Identity;
    private Vector3 _remoteVelocity;
    private bool _hasRemoteState;
    private Transform3D _lastGoodTransform;
    private bool _hasGoodTransform;
    public bool HasDriver => Driver != null;
    public float SpeedKmh => LinearVelocity.Length() * 3.6f;
    public bool CanExit => LinearVelocity.Length() <= ExitMaxSpeed;

    private Node3D _steeringWheel;
    private Node3D _headlights;
    private AudioStreamPlayer3D _engine;
    private AudioStreamPlayer3D _door;
    private float _engineLoad;
    private AudioStreamPlayer3D _crash;
    private AudioStream _crashHeavy;
    private AudioStream _crashLight;
    private Vector3 _previousVelocity;
    private double _crashCooldown;
    private AudioStreamPlayer3D _skid;
    private AudioStream _impactKenney;
    /// <summary>Rádio do painel: R liga/desliga (sintonizado na 96.6).</summary>
    public RadioPlayer Radio { get; private set; }
    /// <summary>Batida (variação brusca de velocidade em m/s). O taxista usa para o passageiro reclamar.</summary>
    public event System.Action<float> Crashed;
    /// <summary>Painel diegético (Fase 3): motor, elétrica e painel físico. Só a caminhonete de entregas tem; nulo nos outros.</summary>
    public EngineSystem Engine { get; private set; }
    public ElectricalSystem Electrical { get; private set; }
    public VanDashboard Dashboard { get; private set; }
    /// <summary>Centro da caçamba (para medir a interferência da carga).</summary>
    private static readonly Vector3 BedCenter = new(0f, 1.3f, -0.7f);

    /// <summary>Banco do carona (v0.9.6): ao lado do motorista (garupa, na moto) e onde ele desce.</summary>
    public Marker3D PassengerSeat { get; private set; }
    public Marker3D PassengerExit { get; private set; }

    /// <summary>Cria o banco do carona espelhando o do motorista; a van ganha também a porta da esquerda.</summary>
    private void SetupPassengerSeat()
    {
        if (GetNodeOrNull<Marker3D>("SeatMarker") is not Marker3D driver)
            return;
        var exit = GetNodeOrNull<Marker3D>("ExitMarker");
        bool moto = Kind == "moto";
        PassengerSeat = new Marker3D
        {
            Name = "PassengerSeat",
            Position = moto ? driver.Position + new Vector3(0f, 0.08f, -0.42f) : new Vector3(-driver.Position.X, driver.Position.Y, driver.Position.Z),
            Rotation = driver.Rotation,
        };
        AddChild(PassengerSeat);
        Vector3 ex = exit?.Position ?? driver.Position + new Vector3(1.4f, 0f, 0f);
        PassengerExit = new Marker3D
        {
            Name = "PassengerExit",
            Position = moto ? ex + new Vector3(0f, 0f, -0.6f) : new Vector3(-ex.X, ex.Y, ex.Z),
            Rotation = new Vector3(0f, (exit?.Rotation.Y ?? 0f) + (moto ? 0f : Mathf.Pi), 0f),
        };
        AddChild(PassengerExit);
        if (Kind == "van" && GetNodeOrNull<Area3D>("DriverDoor") is Area3D door)
        {
            var left = new DriverSeatInteractable
            {
                Name = "PassengerDoor",
                Van = this,
                SeatMarker = PassengerSeat,
                ExitMarker = PassengerExit,
                Passenger = true,
                CollisionLayer = 4,
                CollisionMask = 0,
                Monitoring = false,
                Position = new Vector3(-door.Position.X, door.Position.Y, door.Position.Z),
            };
            left.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(0.5f, 1.4f, 1.2f) } });
            AddChild(left);
        }
    }

    public override void _Ready()
    {
        AddToGroup(PedestrianSignal.TrafficGroup);
        AddToGroup(GroupName);
        _steeringWheel = GetNodeOrNull<Node3D>("Cabin/SteeringWheel");
        Callable.From(SetupPassengerSeat).CallDeferred(); // depois do VehicleFactory montar os marcadores
        _headlights = GetNodeOrNull<Node3D>("Headlights");
        if (_headlights != null)
            _headlights.Visible = false;

        EngineSound ??= ResourceLoader.Exists("res://Audio/SFX/engine_loop.wav") ? GD.Load<AudioStream>("res://Audio/SFX/engine_loop.wav") : null;
        DoorSound ??= ResourceLoader.Exists("res://Audio/SFX/van_door.wav") ? GD.Load<AudioStream>("res://Audio/SFX/van_door.wav") : null;

        _engine = new AudioStreamPlayer3D
        {
            Stream = AudioManager.MakeLooping(EngineSound),
            Bus = AudioManager.SfxBus,
            UnitSize = 8f,
            VolumeDb = -60f,
            Position = new Vector3(0f, 0.8f, 1.8f),
        };
        AddChild(_engine);

        _door = new AudioStreamPlayer3D { Stream = DoorSound, Bus = AudioManager.SfxBus, UnitSize = 5f, Position = new Vector3(0.95f, 1.3f, 1.4f) };
        AddChild(_door);

        _crashHeavy = ResourceLoader.Exists("res://Audio/SFX/crash_heavy.wav") ? GD.Load<AudioStream>("res://Audio/SFX/crash_heavy.wav") : null;
        _crashLight = ResourceLoader.Exists("res://Audio/SFX/crash_light.wav") ? GD.Load<AudioStream>("res://Audio/SFX/crash_light.wav") : null;
        _crash = new AudioStreamPlayer3D { Bus = AudioManager.SfxBus, UnitSize = 12f, MaxPolyphony = 2, Position = new Vector3(0f, 1f, 0f) };
        AddChild(_crash);

        // Sons CC0 da Kenney: pneu cantando e batida leve (ver Créditos).
        const string kenney = "res://Assets/Kenney/Racing/Audio/";
        _impactKenney = ResourceLoader.Exists(kenney + "impact.ogg") ? GD.Load<AudioStream>(kenney + "impact.ogg") : null;
        if (ResourceLoader.Exists(kenney + "skid.ogg"))
        {
            _skid = new AudioStreamPlayer3D
            {
                Stream = AudioManager.MakeLooping(GD.Load<AudioStream>(kenney + "skid.ogg")),
                Bus = AudioManager.SfxBus,
                UnitSize = 10f,
                VolumeDb = -60f,
                Position = new Vector3(0f, 0.3f, 0f),
            };
            AddChild(_skid);
        }

        Radio = new RadioPlayer { Name = "Radio", On = false, VolumeDb = -4f, UnitSize = 3f, MaxDistance = 18f, Position = Kind == "van" ? new Vector3(0f, 1.25f, 1.75f) : new Vector3(0f, 1.2f, 0.5f) };
        AddChild(Radio);

        if (Kind == "van")
        {
            VanDetails.Build(this);
            SetupSystems();
        }
    }

    // ---------------- Painel diegético (Fase 3)

    private void SetupSystems()
    {
        Engine = new EngineSystem { Name = "Engine" };
        Electrical = new ElectricalSystem { Name = "Electrical" };
        AddChild(Engine);
        AddChild(Electrical);
        // Offline, a caminhonete da empresa lembra do tanque e da bateria do último save.
        if (NetId == "van" && !(NetClient.Instance?.IsOnline ?? false) && SaveManager.Load() is SaveData save)
        {
            if (save.VanFuel >= 0f) Engine.Restore(save.VanFuel);
            if (save.VanBattery >= 0f) Electrical.Restore(save.VanBattery);
        }
        Engine.Started += () =>
        {
            if (_engine?.Stream != null && !_engine.Playing)
                _engine.Play();
        };
        Engine.Notice += (text, warning) =>
        {
            if (LocalInside)
                GameHud.Toast(this, text, warning ? UiStyle.Warning : UiStyle.Phosphor, 4.5);
        };
        Crashed += change => Electrical.OnCrash(change);
        Dashboard = VanDashboard.Build(this);
    }

    private bool LocalInside => PlayerController.Local is PlayerController p && IsInstanceValid(p) && p.CurrentVehicle == this;

    /// <summary>Botão CHAVE do painel: liga ou desliga o motor.</summary>
    public void ToggleIgnition()
    {
        if (Engine == null)
            return;
        if (Engine.Running || Engine.Cranking)
            Engine.Stop();
        else
            StartEngine();
    }

    private void StartEngine()
    {
        if (Engine == null || Engine.Running || Engine.Cranking)
            return;
        Engine.Start(Electrical.BatteryLow);
        if (_engine?.Stream != null && !_engine.Playing)
            _engine.Play();
    }

    /// <summary>Botão FAROL (ou a tecla da lanterna, dirigindo).</summary>
    public void ToggleLights()
    {
        if (Electrical == null)
            return;
        Electrical.LightsSwitch = !Electrical.LightsSwitch;
        if (Electrical.LightsSwitch && !Electrical.FuseOk(Circuit.Lights))
            GameHud.Toast(this, "O farol não acende: fusível queimado (caixa do lado do carona).", UiStyle.Warning, 4.0);
        else if (Electrical.LightsSwitch && Electrical.BatteryDead)
            GameHud.Toast(this, "Bateria arriada: sem farol até o motor carregar.", UiStyle.Warning, 4.0);
    }

    /// <summary>Botão RÁDIO do painel ou a tecla R.</summary>
    public void ToggleRadio()
    {
        if (Radio == null)
            return;
        if (!Radio.On && Electrical != null && !Electrical.Powered(Circuit.Radio))
        {
            GameHud.Toast(this, Electrical.BatteryDead ? "Bateria arriada: o rádio nem acende." : "Rádio mudo: fusível queimado (caixa do lado do carona).", UiStyle.Warning, 4.0);
            return;
        }
        Radio.Toggle();
        if (HasDriver)
            AudioManager.Instance?.SetMusicDucked(Radio.On);
        if (GetNodeOrNull<Label3D>("RadioDisplay") is Label3D display)
            display.Visible = Radio.On;
        GameHud.Toast(this, Radio.On ? "📻 " + Radio.NowPlaying : "Rádio desligado", UiStyle.Phosphor, 4.0);
    }

    /// <summary>Galão de gasolina (item): abastece a caminhonete mais perto do jogador. Retorna a mensagem do HUD ("" = não usou).</summary>
    public static string RefuelNearest(PlayerController player)
    {
        if (player == null || !player.IsInsideTree())
            return "";
        VanController best = null;
        float bestDistance = 7f;
        foreach (Node n in player.GetTree().GetNodesInGroup(GroupName))
        {
            if (n is not VanController van || van.Engine == null)
                continue;
            float d = van.GlobalPosition.DistanceTo(player.GlobalPosition);
            if (d < bestDistance)
            {
                bestDistance = d;
                best = van;
            }
        }
        if (best == null)
        {
            GameHud.Toast(player, "Chegue perto da caminhonete para abastecer.", UiStyle.Warning, 3.0);
            return "";
        }
        if (!best.Engine.Refuel(EngineSystem.JerryCanAmount))
        {
            GameHud.Toast(player, "O tanque já está cheio.", UiStyle.Phosphor, 3.0);
            return "";
        }
        return $"Glub, glub... tanque com {best.Engine.FuelLiters:0} litros.";
    }

    /// <summary>Motor, bateria, faróis e rádio a cada passo de física (com ou sem motorista).</summary>
    private void UpdateSystems(float dt, float throttle)
    {
        // Pisou com o motor desligado: dá a partida sozinho (ninguém fica preso sem achar a chave).
        if (HasDriver && Mathf.Abs(throttle) > 0.05f)
            StartEngine();
        Engine.Tick(dt, HasDriver ? throttle : 0f, SpeedKmh);
        bool radioOn = Radio != null && Radio.On;
        Electrical.Tick(dt, Engine.Running, Engine.Cranking, radioOn, GlobalTransform * BedCenter);
        if (radioOn && !Electrical.Powered(Circuit.Radio))
        {
            Radio.SetOn(false);
            if (HasDriver)
                AudioManager.Instance?.SetMusicDucked(false);
            if (GetNodeOrNull<Label3D>("RadioDisplay") is Label3D display)
                display.Visible = false;
        }
        if (Radio != null)
            Radio.Interference = Electrical.Interference;
        if (_headlights != null && !HasRemoteDriver)
        {
            float level = Electrical.LightLevel;
            _headlights.Visible = level > 0f;
            if (!Mathf.IsEqualApprox(level, _lightLevel))
            {
                _lightLevel = level;
                foreach (Node n in _headlights.GetChildren())
                    if (n is SpotLight3D spot)
                        spot.LightEnergy = 6f * level;
            }
        }
    }

    private float _lightLevel = 1f;

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!HasDriver || !Driver.InputEnabled || IsRemote)
            return;
        if (@event.IsActionPressed("radio"))
        {
            ToggleRadio();
            GetViewport().SetInputAsHandled();
        }
        else if (@event.IsActionPressed("radio_next") && Radio.On)
        {
            GameHud.Toast(this, "📻 " + Radio.NowPlaying + "  (só tem a 96.6 por aqui)", UiStyle.Phosphor, 4.0);
            GetViewport().SetInputAsHandled();
        }
    }

    /// <summary>Pneu cantando quando a van escorrega de lado (curva rápida ou freio de mão).</summary>
    private void UpdateSkid(float dt, float brake)
    {
        if (_skid == null)
            return;
        Vector3 v = LinearVelocity;
        float lateral = Mathf.Abs(v.Dot(GlobalBasis.X));
        float forward = Mathf.Abs(v.Dot(GlobalBasis.Z));
        bool grounded = GetNodeOrNull<VehicleWheel3D>("WheelRL")?.IsInContact() ?? true;
        float amount = grounded ? Mathf.Clamp((lateral - 3f) / 5f, 0f, 1f) : 0f;
        if (brake > 0.5f && forward > 6f && grounded)
            amount = Mathf.Max(amount, Mathf.Clamp((forward - 6f) / 10f, 0f, 0.8f));
        float target = amount > 0.01f ? Mathf.Lerp(-18f, -2f, amount) : -60f;
        _skid.VolumeDb = Mathf.MoveToward(_skid.VolumeDb, target, 90f * dt);
        if (amount > 0.01f && !_skid.Playing)
            _skid.Play();
        else if (_skid.Playing && _skid.VolumeDb <= -59f)
            _skid.Stop();
    }

    /// <summary>
    /// Remoto = outro jogador dirige. NÃO usamos Freeze: VehicleBody3D congelado divide por massa zero
    /// no cálculo das rodas e enche a van de NaN (era o travamento online). Em vez disso a física continua
    /// ligada, sem gravidade, e a cada passo a van é posicionada no estado recebido.
    /// </summary>
    public void SetRemote(bool remote)
    {
        if (IsRemote == remote)
            return;
        IsRemote = remote;
        Freeze = false;
        GravityScale = remote ? 0f : 1f;
        EngineForce = 0f;
        Brake = remote ? 0f : ParkedBrake;
        if (remote)
        {
            LinearVelocity = Vector3.Zero;
            AngularVelocity = Vector3.Zero;
        }
        else if (_hasRemoteState)
        {
            LinearVelocity = _remoteVelocity;
            Sleeping = false;
        }
    }

    public void SetRemoteDriver(bool driving)
    {
        HasRemoteDriver = driving;
        if (!IsInstanceValid(this))
            return;
        if (_headlights != null && IsInstanceValid(_headlights))
            _headlights.Visible = driving || HasDriver;
        if (driving && _engine != null && IsInstanceValid(_engine) && _engine.Stream != null && !_engine.Playing)
            _engine.Play();
    }

    public void ApplyRemoteState(Vector3 position, Quaternion rotation, Vector3 velocity)
    {
        if (!position.IsFinite() || !velocity.IsFinite() || !rotation.IsFinite())
            return;
        _remotePosition = position;
        _remoteRotation = rotation.Normalized();
        _remoteVelocity = velocity;
        if (!_hasRemoteState)
        {
            GlobalPosition = position;
            Quaternion = _remoteRotation;
        }
        _hasRemoteState = true;
    }

    public void PlayDoorSound()
    {
        if (_door?.Stream == null)
            return;
        _door.PitchScale = (float)GD.RandRange(0.95, 1.05);
        _door.Play();
    }

    public void SetDriver(PlayerController driver)
    {
        Driver = driver;
        if (Radio != null)
            AudioManager.Instance?.SetMusicDucked(driver != null && Radio.On);
        if (Engine != null)
        {
            // Sentou: gira a chave. Desceu: desliga o motor (o farol fica como o botão estiver!).
            if (driver != null)
                StartEngine();
            else
                Engine.Stop();
            return;
        }
        if (driver != null && _engine?.Stream != null && !_engine.Playing)
            _engine.Play();
        if (_headlights != null)
            _headlights.Visible = driver != null;
    }

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;

        if (!GuardAgainstNaN())
            return;

        if (IsRemote)
        {
            if (_hasRemoteState)
            {
                float t = 1f - Mathf.Exp(-12f * dt);
                Vector3 position = GlobalPosition.Lerp(_remotePosition + _remoteVelocity * 0.05f, t);
                Quaternion rotation = GlobalBasis.GetRotationQuaternion().Slerp(_remoteRotation, t);
                GlobalTransform = new Transform3D(new Basis(rotation), position);
                LinearVelocity = _remoteVelocity;
                AngularVelocity = Vector3.Zero;
            }
            UpdateEngineSound(dt, 0f, _remoteVelocity.Dot(GlobalBasis.Z));
            return;
        }

        float throttle = 0f;
        float steerInput = 0f;
        bool handbrake = false;

        // Velocidade no eixo da van: positiva indo para frente.
        float forwardSpeed = LinearVelocity.Dot(GlobalBasis.Z);

        if (AutoDrive && HasDriver)
        {
            // Acelera até a velocidade alvo e mantém; acima dela, solta (o freio-motor segura).
            throttle = Mathf.Clamp((_autoDriveSpeed - forwardSpeed) * 0.4f, -0.6f, 0.8f);
        }
        else if (HasDriver && Driver.InputEnabled)
        {
            throttle = Input.GetAxis("move_back", "move_forward");
            steerInput = Input.GetAxis("move_right", "move_left"); // esterço positivo = esquerda
            handbrake = Input.IsActionPressed("jump");
        }

        float engine = 0f;
        float brake;

        if (!HasDriver)
        {
            brake = ParkedBrake;
        }
        else if (throttle > 0.05f)
        {
            // Acelerar enquanto anda de ré = frear primeiro.
            if (forwardSpeed < -0.5f) { brake = BrakeForce * throttle; }
            else { engine = MaxEngineForce * throttle; brake = 0f; }
        }
        else if (throttle < -0.05f)
        {
            if (forwardSpeed > 0.5f) { brake = BrakeForce * -throttle; }
            else { engine = MaxReverseForce * throttle; brake = 0f; }
        }
        else
        {
            brake = CoastBrake;
        }

        if (Engine != null)
        {
            UpdateSystems(dt, throttle);
            engine *= Engine.PowerFactor;
            if (Engine.OutOfFuel && SpeedKmh > 25f && engine != 0f)
                engine = 0f; // no cheiro da gasolina: só devagarinho
        }
        if (engine > 0f && SpeedKmh > MaxSpeedKmh)
            engine = 0f;
        if (handbrake)
            brake = HandbrakeForce;

        EngineForce = engine;
        Brake = brake;

        // Menos esterço em alta velocidade evita capotar.
        float falloff = Mathf.Lerp(1f, SteerAtSpeed, Mathf.Clamp(Mathf.Abs(forwardSpeed) / SteerFalloffSpeed, 0f, 1f));
        float targetSteer = steerInput * Mathf.DegToRad(MaxSteerDegrees) * falloff;
        Steering = Mathf.MoveToward(Steering, targetSteer, SteerSpeed * dt);

        UpdateEngineSound(dt, throttle, forwardSpeed);
        UpdateSkid(dt, handbrake && HasDriver ? 1f : 0f);
        DetectCrash(dt);
        Stabilize(dt);

        if (_steeringWheel != null)
        {
            Vector3 rotation = _steeringWheel.Rotation;
            rotation.Z = -Steering * SteeringWheelRatio;
            _steeringWheel.Rotation = rotation;
        }
    }

    /// <summary>
    /// Batida = queda brusca de velocidade entre dois passos de física (sem precisar de contact monitor).
    /// Quanto maior a variação, mais forte o som.
    /// </summary>
    private void DetectCrash(float dt)
    {
        Vector3 velocity = LinearVelocity;
        Vector3 delta = velocity - _previousVelocity;
        delta.Y *= 0.3f; // pulos da suspensão não contam como batida
        float change = delta.Length();
        _previousVelocity = velocity;

        if (_crashCooldown > 0)
        {
            _crashCooldown -= dt;
            return;
        }
        if (change < 3.5f)
            return;
        Crashed?.Invoke(change);
        if (_crash == null)
            return;

        bool heavy = change > 8f;
        _crash.Stream = heavy ? _crashHeavy : (_impactKenney != null && GD.Randf() < 0.5f ? _impactKenney : _crashLight);
        if (_crash.Stream == null)
            return;
        _crash.VolumeDb = Mathf.Lerp(-10f, 2f, Mathf.Clamp((change - 3.5f) / 12f, 0f, 1f));
        _crash.PitchScale = (float)GD.RandRange(0.9, 1.1);
        _crash.Play();
        _crashCooldown = heavy ? 0.6 : 0.3;
    }

    private float _flippedTime;

    /// <summary>
    /// Ajuda de jogo contra capotagem (v0.10):
    ///  - com as rodas no chão, um torque puxa a carroceria de volta para o nível (como barra estabilizadora);
    ///  - limita a velocidade de rolagem/arfagem, para lombada e batida de lado não virarem cambalhota;
    ///  - se mesmo assim virar e ficar parado, desvira sozinho depois de uns segundos.
    /// </summary>
    private void Stabilize(float dt)
    {
        Basis basis = GlobalBasis.Orthonormalized();
        Vector3 up = basis.Y;
        float upright = up.Dot(Vector3.Up);
        bool grounded = false;
        foreach (Node n in GetChildren())
            if (n is VehicleWheel3D w && w.IsInContact())
            {
                grounded = true;
                break;
            }

        if (grounded && upright > 0.2f && AntiRoll > 0f)
        {
            // Eixo e tamanho da inclinação: cross(up do carro, up do mundo).
            Vector3 axis = up.Cross(Vector3.Up);
            // Só rolagem (eixo da frente) e um pouco de arfagem: não briga com a direção.
            Vector3 forward = basis.Z;
            float roll = axis.Dot(forward);
            float pitch = axis.Dot(basis.X);
            Vector3 torque = (forward * roll + basis.X * pitch * 0.4f) * AntiRoll * Mass * 9.8f;
            ApplyTorque(torque);
        }

        // Limite de rolagem/arfagem (no referencial do carro: X = arfagem, Z = rolagem).
        Vector3 local = basis.Inverse() * AngularVelocity;
        if (Mathf.Abs(local.X) > MaxTiltRate || Mathf.Abs(local.Z) > MaxTiltRate)
        {
            local.X = Mathf.Clamp(local.X, -MaxTiltRate, MaxTiltRate);
            local.Z = Mathf.Clamp(local.Z, -MaxTiltRate, MaxTiltRate);
            AngularVelocity = basis * local;
        }

        // Virou (de lado ou de rodas para cima) e parou: desvira depois de 2,5 s.
        if (upright < 0.35f && LinearVelocity.Length() < 3f)
        {
            _flippedTime += dt;
            if (_flippedTime > 2.5f)
                RightUp();
        }
        else
        {
            _flippedTime = 0f;
        }
    }

    /// <summary>Põe o veículo de pé de novo no mesmo lugar (um pouco acima do chão), mantendo a direção.</summary>
    public void RightUp()
    {
        _flippedTime = 0f;
        Vector3 forward = GlobalBasis.Z;
        forward.Y = 0f;
        if (forward.LengthSquared() < 0.01f)
            forward = Vector3.Forward;
        Basis basis = Basis.LookingAt(-forward.Normalized(), Vector3.Up);
        LinearVelocity = Vector3.Zero;
        AngularVelocity = Vector3.Zero;
        GlobalTransform = new Transform3D(basis, GlobalPosition + Vector3.Up * 1.2f);
        if (HasDriver)
            GameHud.Toast(this, "O carro virou... e foi desvirado.", UiStyle.Warning, 3.0);
    }

    /// <summary>Se a física explodir (NaN/infinito), volta para a última posição boa em vez de travar o jogo.</summary>
    private bool GuardAgainstNaN()
    {
        Transform3D t = GlobalTransform;
        bool finite = t.Origin.IsFinite() && t.Basis.X.IsFinite() && t.Basis.Y.IsFinite() && t.Basis.Z.IsFinite()
                      && LinearVelocity.IsFinite() && AngularVelocity.IsFinite();
        if (finite)
        {
            _lastGoodTransform = t;
            _hasGoodTransform = true;
            return true;
        }

        GD.PushWarning("VanController: física inválida (NaN), restaurando a última posição boa.");
        LinearVelocity = Vector3.Zero;
        AngularVelocity = Vector3.Zero;
        EngineForce = 0f;
        Steering = 0f;
        if (_hasGoodTransform)
            GlobalTransform = _lastGoodTransform with { Origin = _lastGoodTransform.Origin + Vector3.Up * 0.5f };
        return false;
    }

    private void UpdateEngineSound(float dt, float throttle, float forwardSpeed)
    {
        if (_engine?.Stream == null)
            return;

        bool engineOn = HasRemoteDriver || (Engine != null && !IsRemote ? Engine.Running || Engine.Cranking : HasDriver);
        if (!engineOn)
        {
            // Motor desligado: some aos poucos.
            _engine.VolumeDb = Mathf.MoveToward(_engine.VolumeDb, -60f, 40f * dt);
            if (_engine.Playing && _engine.VolumeDb <= -59f)
                _engine.Stop();
            return;
        }

        if (Engine != null && Engine.Cranking)
        {
            // Motor de arranque: grave e tremido até pegar.
            _engine.PitchScale = 0.42f + 0.05f * Mathf.Sin((float)Time.GetTicksMsec() * 0.04f);
            _engine.VolumeDb = Mathf.MoveToward(_engine.VolumeDb, -13f, 80f * dt);
            return;
        }
        _engineLoad = Mathf.MoveToward(_engineLoad, Mathf.Abs(throttle), 3f * dt);
        float speedFactor = Mathf.Clamp(Mathf.Abs(forwardSpeed) / 26f, 0f, 1f);
        float targetPitch = 0.55f + speedFactor * 1.1f + _engineLoad * 0.25f;
        _engine.PitchScale = Mathf.Lerp(_engine.PitchScale, targetPitch, 1f - Mathf.Exp(-6f * dt));
        float targetVolume = -10f + _engineLoad * 5f + speedFactor * 3f;
        _engine.VolumeDb = Mathf.MoveToward(_engine.VolumeDb, targetVolume, 60f * dt);
    }
}
