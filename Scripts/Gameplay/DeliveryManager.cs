// Scripts/Gameplay/DeliveryManager.cs
// Loop principal do turno: cada caixa do depósito vira um pedido com endereço na cidade,
// marcado por um feixe de luz verde. Leve a caixa (na mão ou na caçamba) até o feixe.
//
// Modo solo: os pedidos são sorteados aqui.
// Multiplayer: o servidor manda os pedidos (NetSync chama os métodos ApplyNet*) e decide o pagamento;
// aqui só se detecta a chegada e se avisa pelo evento DeliveryReached.
using System;
using System.Collections.Generic;
using Godot;

namespace DeadFrequency;

public class DeliveryJob
{
    public string Id = "";
    public CargoItem Cargo;
    public int PointIndex = -1;
    public DeliveryPoint Point;
    public string CargoName = "";
    public Node3D Marker;
    public Label3D Label;
    /// <summary>Multiplayer: já avisamos o servidor, esperando confirmação.</summary>
    public bool Reported;
    public bool ChunkPinned;
}

public partial class DeliveryManager : Node3D
{
    [Signal] public delegate void DeliveryCompletedEventHandler(string cargoName, int payment);
    [Signal] public delegate void CargoLostEventHandler(string cargoName);
    [Signal] public delegate void ShiftStartedEventHandler(int shift);
    [Signal] public delegate void ShiftCompletedEventHandler(int shift, int earnings);
    /// <summary>Todas as caixas do turno foram destruídas.</summary>
    [Signal] public delegate void GameOverEventHandler(int shift, int totalDelivered);

    [Export] public CityGenerator City { get; set; }
    [Export] public PlayerController Player { get; set; }
    /// <summary>Onde as caixas novas aparecem a cada turno.</summary>
    [Export] public Node3D CargoSpawn { get; set; }
    [Export(PropertyHint.File, "*.tscn")] public string CargoScenePath { get; set; } = "res://Scenes/Cargo/cargo_box.tscn";
    /// <summary>A ORDEM importa: é a mesma lista do servidor (CARGO_CATALOG em server/src/index.ts).</summary>
    [Export] public string[] CargoCatalog { get; set; } =
    {
        "res://Data/Cargo/fitas_magneticas.tres",
        "res://Data/Cargo/monitor_crt.tres",
        "res://Data/Cargo/suprimentos.tres",
        "res://Data/Cargo/pecas_radio.tres",
        "res://Data/Cargo/valvulas.tres",
        "res://Data/Cargo/documentos.tres",
        "res://Data/Cargo/bateria.tres",
        // v0.8 (mesma ordem no servidor: CARGO_CATALOG e LocalServer.CargoPayments)
        "res://Data/Cargo/botijao.tres",
        "res://Data/Cargo/bolo.tres",
        "res://Data/Cargo/aquario.tres",
        "res://Data/Cargo/parabolica.tres",
        "res://Data/Cargo/tomates.tres",
        "res://Data/Cargo/feijoada.tres",
    };
    [Export] public int CargoPerShift { get; set; } = 3;
    /// <summary>Distância mínima do depósito para sortear um destino.</summary>
    [Export] public float MinDistance { get; set; } = 70f;
    [Export] public Vector3 DeliveryZoneSize { get; set; } = new(7f, 4f, 7f);

    /// <summary>Multiplayer: avisar o servidor que uma caixa chegou ao destino.</summary>
    public event Action<DeliveryJob> DeliveryReached;
    /// <summary>Multiplayer: a caixa de um pedido quebrou neste cliente.</summary>
    public event Action<DeliveryJob> JobCargoDestroyed;
    /// <summary>Multiplayer: só o dono da caixa pode reportar a entrega.</summary>
    public Func<CargoItem, bool> CanReportDelivery { get; set; } = _ => true;

    public bool NetworkMode { get; private set; }
    /// <summary>Solo: carteira do jogador (salva no disco). Online: dinheiro do grupo, vindo do servidor.</summary>
    public int Money => NetworkMode ? _netMoney : (PlayerInventory.Instance?.Wallet ?? _localMoney);
    public int TotalDelivered { get; private set; }
    public bool IsGameOver { get; private set; }
    private int _netMoney;
    private int _localMoney;
    private int _shiftSuccesses;
    public int Shift { get; private set; } = 1;
    public int ShiftTotal { get; private set; }
    public int ShiftDelivered { get; private set; }
    public int ShiftEarnings { get; private set; }
    public IReadOnlyList<DeliveryJob> Jobs => _jobs;

    private readonly List<DeliveryJob> _jobs = new();
    private AudioStreamPlayer3D _deliveredSound;
    private ShaderMaterial _beamMaterial;
    private readonly RandomNumberGenerator _rng = new();
    private int _localCounter;

    public override void _Ready()
    {
        _rng.Randomize();
        AddToGroup("delivery_manager");
        _beamMaterial = new ShaderMaterial { Shader = GD.Load<Shader>("res://Shaders/beacon_beam.gdshader") };
        _deliveredSound = new AudioStreamPlayer3D { Bus = AudioManager.SfxBus, UnitSize = 10f };
        if (ResourceLoader.Exists("res://Audio/SFX/delivery_ok.wav"))
            _deliveredSound.Stream = GD.Load<AudioStream>("res://Audio/SFX/delivery_ok.wav");
        AddChild(_deliveredSound);

        NetworkMode = NetClient.Instance?.IsOnline == true;
        if (!NetworkMode)
        {
            // Continua do último save (turno e total de entregas).
            SaveData save = SaveManager.Load();
            if (save != null)
            {
                Shift = Mathf.Max(1, save.Shift);
                TotalDelivered = save.TotalDelivered;
            }
        }
        // As profissões aguardam o preparo dos destinos e pontos de serviço da fase.
        if (City != null && City.WorldData == null)
            City.WorldDataReady += OnWorldDataReady;
        else
            QueueProfessionManager();
    }

    private bool _professionManagerQueued;

    private void OnWorldDataReady()
    {
        if (City != null)
            City.WorldDataReady -= OnWorldDataReady;
        QueueProfessionManager();
    }

    private void QueueProfessionManager()
    {
        if (_professionManagerQueued || !IsInsideTree())
            return;

        _professionManagerQueued = true;
        Callable.From(() =>
        {
            _professionManagerQueued = false;
            if (IsInsideTree() && ProfessionManager.Instance == null)
                GetParent().AddChild(new ProfessionManager { Name = "Professions", City = City, Player = Player, Delivery = this });
        }).CallDeferred();
    }

    public override void _ExitTree()
    {
        if (City != null)
            City.WorldDataReady -= OnWorldDataReady;
    }

    /// <summary>
    /// Só o entregador tem turnos de caixas. Nas outras profissões (solo) não há pedidos nem game over;
    /// no online os pedidos continuam (o servidor manda), mas os feixes ficam escondidos.
    /// </summary>
    public bool EntregadorMode { get; private set; } = true;
    private bool _localShiftStarted;

    public void SetProfessionMode(bool entregador)
    {
        EntregadorMode = entregador;
        foreach (DeliveryJob job in _jobs)
            if (job.Marker != null && IsInstanceValid(job.Marker))
                job.Marker.Visible = entregador;
        if (!NetworkMode && entregador && !_localShiftStarted)
        {
            _localShiftStarted = true;
            StartLocalShift(useExistingCargo: true);
        }
        else if (!NetworkMode && !entregador && _localShiftStarted)
        {
            // Saiu do turno de entregas: as caixas ficam no lugar, sem pedido (nem game over).
            _localShiftStarted = false;
            ClearJobs(freeCargo: false);
        }
    }

    public override void _Process(double delta)
    {
        foreach (DeliveryJob job in _jobs)
        {
            if (job.Label != null)
                job.Label.Text = $"ENTREGA\n{job.CargoName}\n{DistanceTo(job):0} m";
        }
    }

    public float DistanceTo(DeliveryJob job)
    {
        Vector3 from = Player != null && IsInstanceValid(Player) ? Player.GlobalPosition : Vector3.Zero;
        return from.DistanceTo(job.Point.Position);
    }

    public CargoItem FindCargo(string id)
    {
        foreach (DeliveryJob job in _jobs)
        {
            if (job.Id == id && job.Cargo != null && IsInstanceValid(job.Cargo))
                return job.Cargo;
        }
        return null;
    }

    // ================================================================ modo solo

    private void StartLocalShift(bool useExistingCargo)
    {
        if (!EntregadorMode)
        {
            _localShiftStarted = false;
            return;
        }
        ClearJobs(freeCargo: false);
        ShiftDelivered = 0;
        ShiftEarnings = 0;
        _shiftSuccesses = 0;
        IsGameOver = false;

        var cargoList = new List<CargoItem>();
        if (useExistingCargo)
        {
            foreach (Node node in GetTree().GetNodesInGroup(CargoItem.GroupName))
            {
                if (node is CargoItem cargo && IsDeliverable(cargo))
                    cargoList.Add(cargo);
            }
        }
        if (cargoList.Count == 0)
        {
            for (int i = 0; i < CargoPerShift; i++)
            {
                CargoItem cargo = SpawnCargo(_rng.RandiRange(0, CargoCatalog.Length - 1), i);
                if (cargo != null)
                    cargoList.Add(cargo);
            }
        }

        foreach (CargoItem cargo in cargoList)
            AddJob($"L{++_localCounter}", cargo, _rng.RandiRange(0, 99999));

        ShiftTotal = _jobs.Count;
        EmitSignal(SignalName.ShiftStarted, Shift);
    }

    private bool IsDeliverable(CargoItem cargo) =>
        IsInstanceValid(cargo) && cargo.Integrity > 0f && (Player == null || cargo.WeightKg <= Player.MaxCarryWeightKg);

    private void CompleteLocal(DeliveryJob job)
    {
        if (!_jobs.Contains(job) || job.Cargo == null || !IsInstanceValid(job.Cargo))
            return;
        int payment = job.Cargo.GetCurrentPayment();
        AddLocalMoney(payment);
        ShiftEarnings += payment;
        _shiftSuccesses++;
        TotalDelivered++;
        ShiftDelivered++;
        RemoveJob(job, freeCargo: true, playSound: true);
        EmitSignal(SignalName.DeliveryCompleted, job.CargoName, payment);
        CheckLocalShiftDone();
    }

    private void CheckLocalShiftDone()
    {
        if (_jobs.Count > 0)
            return;
        if (_shiftSuccesses == 0)
        {
            // Nenhuma entrega no turno: perdeu tudo.
            IsGameOver = true;
            if (PlayerInventory.Instance != null)
                PlayerInventory.Instance.SetWallet(0);
            else
                _localMoney = 0;
            EmitSignal(SignalName.GameOver, Shift, TotalDelivered);
            return;
        }
        EmitSignal(SignalName.ShiftCompleted, Shift, ShiftEarnings);
        Shift++;
        GetTree().CreateTimer(4.0).Timeout += () =>
        {
            if (IsInsideTree() && !NetworkMode)
                StartLocalShift(useExistingCargo: false);
        };
    }

    // ================================================================ multiplayer

    public readonly struct NetJob
    {
        public readonly string Id;
        public readonly int Type;
        public readonly int Pick;
        public readonly int Slot;
        public readonly string Status;

        public NetJob(string id, int type, int pick, int slot, string status)
        {
            Id = id;
            Type = type;
            Pick = pick;
            Slot = slot;
            Status = status;
        }
    }

    public void ApplyNetShift(int shift, IReadOnlyList<NetJob> jobs, int money)
    {
        NetworkMode = true;
        ClearJobs(freeCargo: true);
        IsGameOver = false;
        Shift = shift;
        _netMoney = money;
        ShiftEarnings = 0;
        ShiftTotal = jobs.Count;
        ShiftDelivered = 0;

        foreach (NetJob netJob in jobs)
        {
            if (netJob.Status != "pending")
            {
                ShiftDelivered++;
                continue;
            }
            CargoItem cargo = SpawnCargo(netJob.Type, netJob.Slot);
            if (cargo == null)
                continue;
            cargo.NetId = netJob.Id;
            // Sem dono: cada cliente simula a caixa parada no depósito até alguém pegar.
            AddJob(netJob.Id, cargo, netJob.Pick);
        }

        EmitSignal(SignalName.ShiftStarted, Shift);
    }

    public void ApplyNetDelivered(string cargoId, int payment, int money, string byName)
    {
        _netMoney = money;
        ShiftEarnings += payment;
        TotalDelivered++;
        ShiftDelivered++;
        DeliveryJob job = _jobs.Find(j => j.Id == cargoId);
        string name = job?.CargoName ?? "Carga";
        if (job != null)
        {
            if (job.Cargo != null && IsInstanceValid(job.Cargo) && job.Cargo.IsHeld)
                Player?.DropHeld();
            RemoveJob(job, freeCargo: true, playSound: true);
        }
        EmitSignal(SignalName.DeliveryCompleted, $"{name} ({byName})", payment);
    }

    public void ApplyNetLost(string cargoId)
    {
        ShiftDelivered++;
        DeliveryJob job = _jobs.Find(j => j.Id == cargoId);
        if (job == null)
            return;
        RemoveJob(job, freeCargo: false, playSound: false);
        EmitSignal(SignalName.CargoLost, job.CargoName);
    }

    public void ApplyNetShiftDone(int shift) => EmitSignal(SignalName.ShiftCompleted, shift, ShiftEarnings);

    public void ApplyNetMoney(int money) => _netMoney = money;

    public void ApplyNetGameOver(int shift)
    {
        IsGameOver = true;
        EmitSignal(SignalName.GameOver, shift, TotalDelivered);
    }

    /// <summary>Solo: gasta da carteira. Retorna false se não tiver dinheiro.</summary>
    public bool TrySpendLocal(int amount)
    {
        if (NetworkMode || Money < amount)
            return false;
        AddLocalMoney(-amount);
        return true;
    }

    public bool TryCreditLocal(int amount)
    {
        if (NetworkMode || amount <= 0)
            return false;
        AddLocalMoney(amount);
        return true;
    }

    private void AddLocalMoney(int amount)
    {
        if (PlayerInventory.Instance != null)
            PlayerInventory.Instance.SetWallet(PlayerInventory.Instance.Wallet + amount);
        else
            _localMoney += amount;
    }

    // ================================================================ comum

    private CargoItem SpawnCargo(int typeIndex, int slot)
    {
        if (CargoSpawn == null || CargoCatalog.Length == 0 || !ResourceLoader.Exists(CargoScenePath))
        {
            GD.PushWarning("DeliveryManager: configure CargoSpawn, CargoCatalog e CargoScenePath.");
            return null;
        }

        var cargo = GD.Load<PackedScene>(CargoScenePath).Instantiate<CargoItem>();
        cargo.Data = GD.Load<CargoData>(CargoCatalog[Mathf.PosMod(typeIndex, CargoCatalog.Length)]);
        GetTree().CurrentScene.AddChild(cargo);
        var offset = new Vector3((slot % 3 - 1) * 1.1f, -0.15f, slot / 3 * 1.1f);
        cargo.GlobalPosition = CargoSpawn.GlobalPosition + offset;
        return cargo;
    }

    private void AddJob(string id, CargoItem cargo, int pick)
    {
        var job = new DeliveryJob
        {
            Id = id,
            Cargo = cargo,
            CargoName = cargo.Data?.CargoName ?? cargo.Name.ToString(),
        };
        if (!ChooseDestination(job, pick))
            return;

        job.ChunkPinned = City.PinChunkForPosition(job.Point.Position);
        BuildMarker(job);
        job.Marker.Visible = EntregadorMode;
        cargo.CargoDestroyed += () => OnJobCargoDestroyed(job);
        _jobs.Add(job);
    }

    /// <summary>Determinístico: mesma cidade + mesmo pick + mesma ordem = mesmo endereço em todos os clientes.</summary>
    private bool ChooseDestination(DeliveryJob job, int pick)
    {
        if (City == null || City.DeliveryPoints.Count == 0)
        {
            GD.PushWarning("DeliveryManager: a cidade não tem pontos de entrega.");
            return false;
        }

        var used = new HashSet<int>();
        foreach (DeliveryJob other in _jobs)
            used.Add(other.PointIndex);

        var candidates = new List<int>();
        for (int i = 0; i < City.DeliveryPoints.Count; i++)
        {
            if (!used.Contains(i) && City.DeliveryPoints[i].Position.Length() >= MinDistance)
                candidates.Add(i);
        }
        if (candidates.Count == 0)
            candidates.Add(Mathf.PosMod(pick, City.DeliveryPoints.Count));

        job.PointIndex = candidates[Mathf.PosMod(pick, candidates.Count)];
        job.Point = City.DeliveryPoints[job.PointIndex];
        return true;
    }

    private void BuildMarker(DeliveryJob job)
    {
        var marker = new Node3D { Name = $"Marker_{job.Id}" };
        AddChild(marker);
        marker.GlobalPosition = job.Point.Position;

        var zone = new Area3D { CollisionLayer = 0, CollisionMask = 8, Monitorable = false };
        zone.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = DeliveryZoneSize }, Position = new Vector3(0f, DeliveryZoneSize.Y * 0.5f, 0f) });
        zone.BodyEntered += body => OnZoneBodyEntered(job, body);
        marker.AddChild(zone);

        marker.AddChild(new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = 0.5f, BottomRadius = 0.5f, Height = 1f, RadialSegments = 12, Rings = 1 },
            MaterialOverride = _beamMaterial,
            Scale = new Vector3(2.5f, 80f, 2.5f),
            Position = new Vector3(0f, 40f, 0f),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        });
        marker.AddChild(new MeshInstance3D
        {
            Mesh = new TorusMesh { InnerRadius = 2.9f, OuterRadius = 3.2f, Rings = 24, RingSegments = 4 },
            MaterialOverride = new StandardMaterial3D { ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, AlbedoColor = UiStyle.Phosphor },
            Position = new Vector3(0f, 0.08f, 0f),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        });
        marker.AddChild(new OmniLight3D { Position = new Vector3(0f, 2.5f, 0f), LightColor = UiStyle.Phosphor, LightEnergy = 2f, OmniRange = 9f });

        job.Label = new Label3D
        {
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            FontSize = 64,
            PixelSize = 0.01f,
            Modulate = UiStyle.Phosphor,
            NoDepthTest = true,
            Position = new Vector3(0f, 4.5f, 0f),
        };
        marker.AddChild(job.Label);
        job.Marker = marker;
    }

    private void OnZoneBodyEntered(DeliveryJob job, Node3D body)
    {
        if (body != job.Cargo || !_jobs.Contains(job))
            return;

        if (!NetworkMode)
        {
            Callable.From(() => CompleteLocal(job)).CallDeferred();
            return;
        }

        if (!job.Reported && CanReportDelivery(job.Cargo))
        {
            job.Reported = true;
            DeliveryReached?.Invoke(job);
        }
    }

    private void OnJobCargoDestroyed(DeliveryJob job)
    {
        if (!_jobs.Contains(job))
            return;

        if (NetworkMode)
        {
            JobCargoDestroyed?.Invoke(job);
            return;
        }

        ShiftDelivered++;
        RemoveJob(job, freeCargo: false, playSound: false);
        EmitSignal(SignalName.CargoLost, job.CargoName);
        CheckLocalShiftDone();
    }

    private void RemoveJob(DeliveryJob job, bool freeCargo, bool playSound)
    {
        _jobs.Remove(job);
        if (job.ChunkPinned)
        {
            City?.UnpinChunkForPosition(job.Point.Position);
            job.ChunkPinned = false;
        }
        if (playSound && job.Marker != null && IsInstanceValid(job.Marker))
        {
            _deliveredSound.GlobalPosition = job.Marker.GlobalPosition;
            _deliveredSound.Play();
        }
        job.Marker?.QueueFree();
        job.Marker = null;
        job.Label = null;
        if (freeCargo && job.Cargo != null && IsInstanceValid(job.Cargo))
            job.Cargo.QueueFree();
    }

    private void ClearJobs(bool freeCargo)
    {
        foreach (DeliveryJob job in _jobs.ToArray())
            RemoveJob(job, freeCargo, playSound: false);
        _jobs.Clear();
    }
}
