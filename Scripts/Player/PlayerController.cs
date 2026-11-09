// Scripts/Player/PlayerController.cs
// Movimento em 1ª pessoa, mira de interação, segurar/arremessar caixas e entrar/sair da van.
//
// Cena esperada (Scenes/Player/player.tscn):
// Player (CharacterBody3D + este script)
//  ├── CollisionShape3D (CapsuleShape3D, altura 1.8, raio 0.35, Y = 0.9)
//  └── Head (Node3D, Y = 1.6)
//       ├── Camera3D
//       └── CarryMarker (Marker3D, 0,-0.25,-1.3)
//
// A mira usa uma consulta direta ao espaço físico (IntersectRay) em vez de um nó RayCast3D:
// assim a própria caixa segurada e a van onde o player está sentado ficam fora do raio.
using System;
using Godot;

namespace DeadFrequency;

public enum PlayerState
{
    Walking,
    Driving,
    /// <summary>Carona no banco do passageiro (v0.9.6): não controla o veículo.</summary>
    Riding,
}

public partial class PlayerController : CharacterBody3D
{
    [ExportGroup("Movimento")]
    [Export] public float WalkSpeed { get; set; } = 3.5f;
    [Export] public float SprintSpeed { get; set; } = 6.0f;
    [Export] public float GroundAcceleration { get; set; } = 12f;
    [Export] public float AirAcceleration { get; set; } = 2.5f;
    [Export] public float JumpVelocity { get; set; } = 4.2f;
    /// <summary>Força ao esbarrar em caixas soltas no chão.</summary>
    [Export] public float PushForce { get; set; } = 25f;

    [ExportGroup("Câmera")]
    /// <summary>Sensibilidade base; o multiplicador vem das Opções (GameSettings).</summary>
    [Export] public float BaseMouseSensitivity { get; set; } = 0.0025f;
    [Export(PropertyHint.Range, "45,89,1")] public float MaxPitchDegrees { get; set; } = 85f;
    /// <summary>Quanto dá para virar a cabeça sentado no banco.</summary>
    [Export(PropertyHint.Range, "30,170,1")] public float DrivingMaxYawDegrees { get; set; } = 110f;

    [ExportGroup("Interação")]
    [Export] public float InteractReach { get; set; } = 2.6f;
    /// <summary>World (1) + Interactables (3) + Cargo (4). World incluso para não pegar coisas através de paredes.</summary>
    [Export(PropertyHint.Layers3DPhysics)] public uint InteractionMask { get; set; } = 1 | 4 | 8;

    [ExportGroup("Som e lanterna")]
    /// <summary>Variações de passo (sorteadas, nunca a mesma duas vezes seguidas).</summary>
    [Export] public AudioStream[] FootstepSounds { get; set; } = System.Array.Empty<AudioStream>();
    /// <summary>Metros andados entre cada passo (andando / correndo).</summary>
    [Export] public float StepDistance { get; set; } = 1.6f;
    [Export] public float SprintStepDistance { get; set; } = 2.3f;
    [Export] public float FlashlightEnergy { get; set; } = 3f;

    [ExportGroup("Carregar objetos")]
    /// <summary>Peso máximo sem luvas. Luvas reforçadas somam GlovesCarryBonusKg.</summary>
    [Export] public float BaseCarryWeightKg { get; set; } = 40f;
    [Export] public float GlovesCarryBonusKg { get; set; } = 20f;
    [Export] public float CarryStiffness { get; set; } = 14f;
    [Export] public float MaxCarrySpeed { get; set; } = 10f;
    /// <summary>Se a caixa ficar presa e se afastar mais que isso do ponto de carga, ela cai da mão.</summary>
    [Export] public float CarryBreakDistance { get; set; } = 2.5f;
    /// <summary>Impulso em N·s. Caixa de 10 kg sai a ~6 m/s; de 40 kg, a ~1.5 m/s.</summary>
    [Export] public float ThrowImpulse { get; set; } = 60f;
    [Export(PropertyHint.Range, "0,0.9,0.05")] public float MaxCarrySlowdown { get; set; } = 0.45f;

    // ---------------- Itens ----------------

    private static bool Has(string itemId) => PlayerInventory.Instance?.IsEquipped(itemId) == true;
    public float MaxCarryWeightKg => BaseCarryWeightKg + (Has(ItemCatalog.Gloves) ? GlovesCarryBonusKg : 0f);
    /// <summary>Multiplicador do dano que a caixa sofre batendo enquanto está na mão.</summary>
    public float HeldDamageMultiplier => Has(ItemCatalog.Gloves) ? 0.2f : 0.5f;
    public bool NightVisionOn { get; private set; }
    public float SpeedBoostLeft { get; private set; }
    /// <summary>Liga/desliga o filtro verde (NightVisionOverlay escuta).</summary>
    public event Action<bool> NightVisionToggled;

    public bool ApplySpeedBoost(float seconds)
    {
        SpeedBoostLeft = Mathf.Max(SpeedBoostLeft, seconds);
        return true;
    }

    private void SetNightVision(bool on)
    {
        if (NightVisionOn == on)
            return;
        NightVisionOn = on;
        NightVisionToggled?.Invoke(on);
    }

    // ---------------- Eventos (usados pelo multiplayer) ----------------

    public event Action<CargoItem> CargoGrabbed;
    public event Action<VanController> VehicleEntered;
    public event Action<VanController> VehicleExited;

    // ---------------- Estado público (lido pelo HUD) ----------------

    public PlayerState State { get; private set; } = PlayerState.Walking;
    /// <summary>Desligado com o menu aberto no multiplayer (o jogo não pausa online).</summary>
    public bool InputEnabled { get; set; } = true;
    public bool IsHolding => _heldItem != null && IsInstanceValid(_heldItem);
    public CargoItem HeldItem => IsHolding ? _heldItem : null;
    public VanController CurrentVehicle { get; private set; }
    public Camera3D Camera => _camera;
    public string CurrentPrompt { get; private set; } = "";
    public bool HasTarget => _currentTarget != null;
    /// <summary>Nome do que o raio de interação está tocando (overlay F3).</summary>
    public string DebugLookingAt { get; private set; } = "-";

    private Node3D _head;
    private Camera3D _camera;
    private Marker3D _carryMarker;
    private CollisionShape3D _collisionShape;

    private float _gravity;
    private float _sensitivityMultiplier = 1f;
    private float _vehicleSensitivity = 1f;
    private bool _headBob = false;
    private float _bobTime;
    private Vector3 _cameraBase;
    private bool _invertY;

    private IInteractable _currentTarget;
    private CargoItem _heldItem;

    private AudioStreamPlayer3D _footsteps;
    private SpotLight3D _flashlight;
    private float _stepAccumulator;
    private int _lastStep = -1;

    private Node _returnParent;
    private Node3D _exitMarker;
    private bool _inTransition;

    public override void _Ready()
    {
        _head = GetNode<Node3D>("Head");
        _camera = GetNode<Camera3D>("Head/Camera3D");
        _cameraBase = _camera.Position;
        InitializeComfortCamera();
        _carryMarker = GetNode<Marker3D>("Head/CarryMarker");
        _collisionShape = GetNode<CollisionShape3D>("CollisionShape3D");

        _gravity = ProjectSettings.GetSetting("physics/3d/default_gravity").AsSingle();

        if (GameSettings.Instance != null)
        {
            GameSettings.Instance.SettingsChanged += ApplySettings;
            ApplySettings();
        }

        if (FootstepSounds.Length == 0)
        {
            var steps = new System.Collections.Generic.List<AudioStream>();
            for (int i = 1; i <= 4; i++)
            {
                string path = $"res://Audio/SFX/footstep_{i}.wav";
                if (ResourceLoader.Exists(path))
                    steps.Add(GD.Load<AudioStream>(path));
            }
            FootstepSounds = steps.ToArray();
        }
        // Som "2D" (não posicional): passos do próprio jogador não devem mudar de lado quando a câmera gira.
        _footsteps = new AudioStreamPlayer3D
        {
            Bus = AudioManager.SfxBus,
            Position = new Vector3(0f, 1.6f, 0f),
            AttenuationModel = AudioStreamPlayer3D.AttenuationModelEnum.Disabled,
            PanningStrength = 0f,
            MaxPolyphony = 2,
        };
        AddChild(_footsteps);

        _flashlight = new SpotLight3D
        {
            Visible = false,
            LightEnergy = FlashlightEnergy,
            LightColor = new Color(1f, 0.96f, 0.85f),
            SpotRange = 22f,
            SpotAngle = 24f,
            SpotAttenuation = 0.9f,
            ShadowEnabled = true,
            Position = new Vector3(0.15f, -0.1f, 0f),
        };
        _camera.AddChild(_flashlight);

        Input.MouseMode = Input.MouseModeEnum.Captured;
    }

    public override void _ExitTree()
    {
        if (GameSettings.Instance != null)
            GameSettings.Instance.SettingsChanged -= ApplySettings;
        DropHeld();
    }

    private void ApplySettings()
    {
        GameSettings s = GameSettings.Instance;
        _sensitivityMultiplier = s.MouseSensitivity;
        _vehicleSensitivity = s.VehicleSensitivity;
        _headBob = s.HeadBob;
        _invertY = s.InvertY;
        if (_camera != null)
            _camera.Fov = s.Fov;
    }

    // ---------------- Input ----------------

    public override void _UnhandledInput(InputEvent @event)
    {
        HandleComfortCameraInput(@event);
        if (!InputEnabled)
            return;

        if (@event is InputEventMouseMotion motion && Input.MouseMode == Input.MouseModeEnum.Captured)
        {
            RotateLook(motion.Relative);
            return;
        }

        // Clicar na janela com o mouse solto recaptura (o menu de pausa solta o mouse).
        if (Input.MouseMode != Input.MouseModeEnum.Captured && @event is InputEventMouseButton { Pressed: true })
        {
            Input.MouseMode = Input.MouseModeEnum.Captured;
            GetViewport().SetInputAsHandled();
        }
    }

    private void RotateLook(Vector2 relative)
    {
        bool seatedLook = State == PlayerState.Driving || State == PlayerState.Riding;
        float sens = BaseMouseSensitivity * _sensitivityMultiplier * (seatedLook ? _vehicleSensitivity : 1f);
        float pitchDelta = relative.Y * sens * (_invertY ? -1f : 1f);
        float maxPitch = Mathf.DegToRad(MaxPitchDegrees);

        // Vector3 é struct: copiar, alterar, reatribuir.
        Vector3 headRotation = _head.Rotation;
        headRotation.X = Mathf.Clamp(headRotation.X - pitchDelta, -maxPitch, maxPitch);

        if (seatedLook)
        {
            // Sentado: o corpo fica preso ao banco, só a cabeça gira.
            float maxYaw = Mathf.DegToRad(DrivingMaxYawDegrees);
            headRotation.Y = Mathf.Clamp(headRotation.Y - relative.X * sens, -maxYaw, maxYaw);
        }
        else
        {
            RotateY(-relative.X * sens);
        }

        _head.Rotation = headRotation;
    }

    /// <summary>Balanço da câmera ao andar (Opções > Controles).</summary>
    public override void _Process(double delta)
    {
        if (_camera == null)
            return;
        UpdateComfortCamera((float)delta);
        float speed = new Vector2(Velocity.X, Velocity.Z).Length();
        bool bob = _headBob && State == PlayerState.Walking && IsOnFloor() && speed > 0.5f;
        Vector3 target = _cameraBase;
        if (bob)
        {
            _bobTime += (float)delta * speed * 2.1f;
            float amount = Mathf.Clamp(speed / 6f, 0.3f, 1f);
            target += new Vector3(Mathf.Cos(_bobTime * 0.5f) * 0.025f * amount, Mathf.Abs(Mathf.Sin(_bobTime * 0.5f)) * 0.04f * amount, 0f);
        }
        _camera.Position = _camera.Position.Lerp(target, 1f - Mathf.Exp(-12f * (float)delta));

        // Dentro do veículo: horizonte nivelado (sem rolagem/arfagem da carroceria) e o pulo da
        // suspensão amortecido — o balanço dava vertigem (v0.9.6).
        bool seated = State == PlayerState.Driving || State == PlayerState.Riding;
        if (seated && (GameSettings.Instance?.StableVehicleCamera ?? true) && !_inTransition)
        {
            Vector3 fwd = -_head.GlobalBasis.Z;
            float yaw = Mathf.Atan2(-fwd.X, -fwd.Z);
            _camera.GlobalBasis = Basis.FromEuler(new Vector3(_head.Rotation.X, yaw, 0f));
            Vector3 gp = _camera.GlobalPosition;
            _stableY = _stableReset ? gp.Y : Mathf.Lerp(_stableY, gp.Y, 1f - Mathf.Exp(-6f * (float)delta));
            _stableReset = false;
            _camera.GlobalPosition = new Vector3(gp.X, _stableY, gp.Z);
        }
        else
        {
            _camera.Rotation = Vector3.Zero;
            _stableReset = true;
        }
    }

    private float _stableY;
    private bool _stableReset = true;

    // ---------------- Física ----------------

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;

        if (State == PlayerState.Walking && !_inTransition)
            ProcessMovement(dt);
        else
            UpdateNeeds(dt, false);

        if (SpeedBoostLeft > 0f)
            SpeedBoostLeft = Mathf.Max(0f, SpeedBoostLeft - dt);
        // Tirou os óculos com eles ligados: desliga o filtro.
        if (NightVisionOn && !Has(ItemCatalog.NightVision))
            SetNightVision(false);

        UpdateInteractionTarget();
        UpdateHeldItem();
        UpdateLocalCharacter(dt);
        if (!InputEnabled)
            return;

        // Polling em vez de eventos: funciona mesmo se algum Control da UI estiver com foco.
        if (Input.IsActionJustPressed("flashlight"))
            _flashlight.Visible = !_flashlight.Visible;

        if (Input.IsActionJustPressed("night_vision"))
        {
            if (Has(ItemCatalog.NightVision))
                SetNightVision(!NightVisionOn);
        }

        if (Input.IsActionJustPressed("interact"))
            HandleInteract();
        else if (Input.IsActionJustPressed("throw") && Input.MouseMode == Input.MouseModeEnum.Captured)
            ThrowHeld();
    }

    private float _knockbackLeft;

    /// <summary>Joga o jogador longe (atropelamento). Larga o que estiver na mão.</summary>
    /// <summary>Elevador/porta do apartamento: move o jogador na hora, virado para yaw (rad).</summary>
    public void Teleport(Vector3 position, float yaw)
    {
        if (State != PlayerState.Walking)
            return;
        GlobalPosition = position;
        Velocity = Vector3.Zero;
        Rotation = new Vector3(0f, yaw, 0f);
        ResetPhysicsInterpolation();
    }

    public void Knockback(Vector3 impulse)
    {
        if (State != PlayerState.Walking)
            return;
        DropHeld();
        Velocity = impulse;
        _knockbackLeft = 0.9f;
    }

    private void ProcessMovement(float dt)
    {
        Vector3 velocity = Velocity;
        if (_knockbackLeft > 0f)
        {
            // Voando depois da pancada: sem controle até cair, com atrito no ar.
            _knockbackLeft -= dt;
            if (!IsOnFloor())
                velocity.Y -= _gravity * dt;
            float drag = IsOnFloor() ? 6f : 0.6f;
            velocity.X = Mathf.MoveToward(velocity.X, 0f, drag * dt * Mathf.Abs(velocity.X));
            velocity.Z = Mathf.MoveToward(velocity.Z, 0f, drag * dt * Mathf.Abs(velocity.Z));
            Velocity = velocity;
            MoveAndSlide();
            return;
        }

        if (!IsOnFloor())
            velocity.Y -= _gravity * dt;
        else if (InputEnabled && Input.IsActionJustPressed("jump"))
            velocity.Y = JumpVelocity;

        Vector2 input = InputEnabled ? Input.GetVector("move_left", "move_right", "move_forward", "move_back") : Vector2.Zero;
        Vector3 direction = GlobalTransform.Basis * new Vector3(input.X, 0f, input.Y);
        direction.Y = 0f;
        direction = direction.Normalized();

        bool moving = direction.LengthSquared() > 0.01f;
        bool sprinting = InputEnabled && Input.IsActionPressed("sprint") && moving && CanSprint && IsOnFloor();
        float speed = sprinting ? SprintSpeed : WalkSpeed;
        if (SpeedBoostLeft > 0f)
            speed *= 1.35f;
        speed *= GetCarrySpeedMultiplier() * NeedsSpeedMultiplier();
        UpdateNeeds(dt, sprinting);
        Vector3 targetVelocity = direction * speed;

        float accel = IsOnFloor() ? GroundAcceleration : AirAcceleration;
        float weight = 1f - Mathf.Exp(-accel * dt);
        velocity.X = Mathf.Lerp(velocity.X, targetVelocity.X, weight);
        velocity.Z = Mathf.Lerp(velocity.Z, targetVelocity.Z, weight);

        Velocity = velocity;
        MoveAndSlide();

        PushRigidBodies(dt);
        UpdateFootsteps(dt);
    }

    private void UpdateFootsteps(float dt)
    {
        if (FootstepSounds.Length == 0 || !IsOnFloor())
            return;
        float horizontalSpeed = new Vector2(Velocity.X, Velocity.Z).Length();
        if (horizontalSpeed < 0.5f)
        {
            _stepAccumulator = StepDistance * 0.7f; // o próximo passo sai logo ao voltar a andar
            return;
        }
        bool sprinting = Input.IsActionPressed("sprint") && InputEnabled && !Exhausted;
        float distance = sprinting ? SprintStepDistance : StepDistance;
        _stepAccumulator += horizontalSpeed * dt;
        if (_stepAccumulator < distance)
            return;
        _stepAccumulator = 0f;

        int index = (int)(GD.Randi() % (uint)FootstepSounds.Length);
        if (index == _lastStep && FootstepSounds.Length > 1)
            index = (index + 1) % FootstepSounds.Length;
        _lastStep = index;
        _footsteps.Stream = SlipperStep(sprinting) ?? FootstepSounds[index];
        _footsteps.PitchScale = (float)GD.RandRange(0.94, 1.06);
        _footsteps.VolumeDb = (sprinting ? -9f : -13f) + (float)GD.RandRange(-1.5, 1.5);
        _footsteps.Play();
    }

    /// <summary>CharacterBody3D não empurra RigidBody3D sozinho; isto faz as caixas no chão reagirem.</summary>
    private void PushRigidBodies(float dt)
    {
        for (int i = 0; i < GetSlideCollisionCount(); i++)
        {
            KinematicCollision3D collision = GetSlideCollision(i);
            if (collision.GetCollider() is RigidBody3D body && body is not VehicleBody3D && body != _heldItem)
            {
                Vector3 pushDirection = -collision.GetNormal();
                pushDirection.Y = 0f;
                body.ApplyImpulse(pushDirection * PushForce * dt, collision.GetPosition() - body.GlobalPosition);
            }
        }
    }

    private float GetCarrySpeedMultiplier()
    {
        if (!IsHolding)
            return 1f;
        float ratio = Mathf.Clamp(_heldItem.WeightKg / MaxCarryWeightKg, 0f, 1f);
        return 1f - ratio * MaxCarrySlowdown;
    }

    // ---------------- Interação ----------------

    private GodotObject CastInteractionRay()
    {
        PhysicsDirectSpaceState3D space = GetWorld3D().DirectSpaceState;
        Camera3D aimCamera = GetAimCamera();
        Vector3 from = aimCamera.GlobalPosition;
        if (!from.IsFinite() || !aimCamera.GlobalTransform.Basis.Z.IsFinite())
            return null;
        Vector3 to = from - aimCamera.GlobalTransform.Basis.Z * InteractReach;

        PhysicsRayQueryParameters3D query = PhysicsRayQueryParameters3D.Create(from, to, InteractionMask);
        query.CollideWithAreas = true;
        query.CollideWithBodies = true;

        var exclude = new Godot.Collections.Array<Rid> { GetRid() };
        if (IsHolding)
            exclude.Add(_heldItem.GetRid());
        if (CurrentVehicle != null)
            exclude.Add(CurrentVehicle.GetRid());
        query.Exclude = exclude;

        Godot.Collections.Dictionary hit = space.IntersectRay(query);
        return hit.Count == 0 ? null : hit["collider"].AsGodotObject();
    }

    private void UpdateInteractionTarget()
    {
        _currentTarget = null;
        GodotObject hit = CastInteractionRay();
        DebugLookingAt = hit is Node node ? $"{node.Name} ({node.GetClass()})" : "-";

        string prompt = "";

        if (IsHolding)
        {
            prompt = "[E] Soltar    [Clique] Arremessar";
        }
        else
        {
            IInteractable target = ResolveInteractable(hit);
            string targetPrompt = target?.GetInteractionPrompt(this) ?? "";
            if (!string.IsNullOrEmpty(targetPrompt))
            {
                _currentTarget = target;
                prompt = targetPrompt;
            }
            else if (State == PlayerState.Driving && CurrentVehicle != null)
            {
                string word = CurrentVehicle.Kind switch { "moto" => "da moto", "truck" => "do caminhão", "car" => "do carro", _ => "da van" };
                prompt = CurrentVehicle.CanExit ? $"[E] Sair {word}" : "Pare para sair";
            }
            else if (State == PlayerState.Riding && CurrentVehicle != null)
            {
                prompt = CurrentVehicle.SpeedKmh < 12f ? "[E] Descer (carona)" : "Carona: espere o veículo parar para descer";
            }
        }

        CurrentPrompt = prompt;
    }

    /// <summary>O raio pode bater no próprio objeto ou num filho dele (ex.: Area3D de um botão).</summary>
    private static IInteractable ResolveInteractable(GodotObject collider)
    {
        if (collider is IInteractable direct)
            return direct;
        if (collider is Node node && node.GetParent() is IInteractable parent)
            return parent;
        return null;
    }

    private void HandleInteract()
    {
        if (_inTransition)
            return;

        if (IsHolding)
        {
            DropHeld();
            return;
        }

        if (_currentTarget != null)
        {
            if (_currentTarget.CanInteract(this))
                _currentTarget.Interact(this);
            return;
        }

        if (State == PlayerState.Driving && CurrentVehicle != null && CurrentVehicle.CanExit)
            ExitVehicle();
        else if (State == PlayerState.Riding && CurrentVehicle != null && CurrentVehicle.SpeedKmh < 12f)
            ExitVehicle();
    }

    // ---------------- Segurar / soltar / arremessar ----------------

    public bool TryGrab(CargoItem item)
    {
        if (State != PlayerState.Walking || IsHolding || item == null || item.IsHeld || item.WeightKg > MaxCarryWeightKg)
            return false;

        _heldItem = item;
        item.OnGrabbed(this);
        CargoGrabbed?.Invoke(item);
        return true;
    }

    public void DropHeld()
    {
        if (!IsHolding)
        {
            _heldItem = null;
            return;
        }

        CargoItem item = _heldItem;
        _heldItem = null;
        item.OnReleased(this);
        item.LinearVelocity = item.LinearVelocity.LimitLength(4f);
    }

    public void ThrowHeld()
    {
        if (!IsHolding)
            return;

        CargoItem item = _heldItem;
        DropHeld();
        Vector3 forward = -GetAimCamera().GlobalTransform.Basis.Z;
        item.ApplyCentralImpulse(forward * ThrowImpulse * (Has(ItemCatalog.Gloves) ? 1.3f : 1f));
    }

    private void UpdateHeldItem()
    {
        if (_heldItem == null)
            return;

        if (!IsInstanceValid(_heldItem))
        {
            _heldItem = null;
            return;
        }

        Vector3 toTarget = _carryMarker.GlobalPosition - _heldItem.GlobalPosition;

        if (toTarget.Length() > CarryBreakDistance)
        {
            DropHeld();
            return;
        }

        // Caixas pesadas respondem mais devagar (sensação de peso).
        float weightFactor = 1f / (1f + _heldItem.WeightKg / MaxCarryWeightKg);
        Vector3 desired = Velocity + toTarget * CarryStiffness * weightFactor;
        _heldItem.LinearVelocity = desired.LimitLength(MaxCarrySpeed);
    }

    // ---------------- Van ----------------

    /// <summary>Chamado pelo DriverSeatInteractable.</summary>
    public void EnterVehicle(VanController van, Node3D seatMarker, Node3D exitMarker, bool passenger = false)
    {
        if (State != PlayerState.Walking || _inTransition || van == null || seatMarker == null)
            return;

        DropHeld();
        _passengerSeat = passenger;
        if (!passenger)
            VehicleEntered?.Invoke(van);
        van.PlayDoorSound();
        _inTransition = true;
        _returnParent = GetParent();
        _exitMarker = exitMarker;
        CurrentVehicle = van;
        State = passenger ? PlayerState.Riding : PlayerState.Driving;
        Velocity = Vector3.Zero;
        _collisionShape.SetDeferred(CollisionShape3D.PropertyName.Disabled, true);

        // Trocar de pai durante o passo de física gera erro; adiamos para o fim do frame.
        Callable.From(() => FinishEnterVehicle(seatMarker)).CallDeferred();
    }

    private void FinishEnterVehicle(Node3D seatMarker)
    {
        Reparent(seatMarker, true);

        // Normaliza o yaw local para o tween não dar uma volta inteira.
        Rotation = new Vector3(0f, Mathf.Wrap(Rotation.Y, -Mathf.Pi, Mathf.Pi), 0f);

        Tween tween = CreateTween().SetParallel().SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(this, "position", Vector3.Zero, 0.45);
        tween.TweenProperty(this, "rotation", Vector3.Zero, 0.45);
        tween.TweenProperty(_head, "rotation", Vector3.Zero, 0.45);
        tween.Chain().TweenCallback(Callable.From(() =>
        {
            _inTransition = false;
            if (!_passengerSeat)
                CurrentVehicle?.SetDriver(this);
        }));
    }

    private bool _passengerSeat;

    public void ExitVehicle()
    {
        if ((State != PlayerState.Driving && State != PlayerState.Riding) || _inTransition)
            return;

        _inTransition = true;
        CurrentVehicle?.PlayDoorSound();
        if (State == PlayerState.Driving)
        {
            CurrentVehicle?.SetDriver(null);
            if (CurrentVehicle != null)
                VehicleExited?.Invoke(CurrentVehicle);
        }
        Callable.From(FinishExitVehicle).CallDeferred();
    }

    private void FinishExitVehicle()
    {
        Node newParent = IsInstanceValid(_returnParent) ? _returnParent : GetTree().CurrentScene;
        Reparent(newParent, true);

        if (_exitMarker != null && IsInstanceValid(_exitMarker))
        {
            GlobalPosition = _exitMarker.GlobalPosition;
            Rotation = new Vector3(0f, _exitMarker.GlobalRotation.Y, 0f);
        }
        else
        {
            Rotation = new Vector3(0f, GlobalRotation.Y, 0f);
        }

        Vector3 headRotation = _head.Rotation;
        headRotation.Y = 0f;
        _head.Rotation = headRotation;

        _collisionShape.SetDeferred(CollisionShape3D.PropertyName.Disabled, false);
        Velocity = Vector3.Zero;
        CurrentVehicle = null;
        State = PlayerState.Walking;
        _inTransition = false;
    }
}
