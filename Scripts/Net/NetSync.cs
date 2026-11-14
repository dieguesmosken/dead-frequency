// Scripts/Net/NetSync.cs
// Ponte entre a fase e o servidor. Só faz algo quando o NetClient está numa sala.
//  - Envia ~10x/s a posição do jogador e o estado dos objetos que ele controla (van/caixas).
//  - Cria e move os outros jogadores (RemotePlayer).
//  - Cuida da posse: quem pega uma caixa ou senta na van vira dono e passa a simular a física;
//    nos outros clientes o objeto fica congelado seguindo o estado recebido.
//  - Repassa pedidos/entregas/turnos do servidor para o DeliveryManager.
using System;
using System.Collections.Generic;
using System.Text.Json;
using Godot;

namespace DeadFrequency;

public partial class NetSync : Node
{
    [Export] public PlayerController Player { get; set; }
    [Export] public VanController Van { get; set; }
    [Export] public DeliveryManager Delivery { get; set; }
    [Export(PropertyHint.Range, "5,30,1")] public float TickRate { get; set; } = 10f;
    [Export(PropertyHint.File, "*.tscn")] public string MainMenuScene { get; set; } = "res://Scenes/UI/main_menu.tscn";

    public const string VanId = "van";

    public static NetSync Instance { get; private set; }

    private readonly Dictionary<int, Action<bool>> _purchaseCallbacks = new();
    private int _nextPurchaseId = 1;
    private readonly Dictionary<int, Action<bool, int>> _saleCallbacks = new();
    private int _nextSaleId = 1;

    private readonly Dictionary<int, RemotePlayer> _remotes = new();
    private readonly Dictionary<int, Vector3> _remoteChunkPins = new();
    private readonly Dictionary<string, int> _owners = new();
    private readonly Dictionary<string, WorldItem> _worldItems = new();
    private readonly Dictionary<int, bool> _remoteDriving = new();
    private readonly Queue<Action> _pendingWorldDataActions = new();
    private bool _worldDataReady;
    private Node3D _remoteRoot;
    /// <summary>Todos os veículos da fase por NetId (van, moto, caminhões).</summary>
    private readonly Dictionary<string, VanController> _vehicles = new();

    private VanController VehicleOf(string id) =>
        id != null && _vehicles.TryGetValue(id, out VanController v) && IsInstanceValid(v) ? v : null;
    private double _tickTimer;
    private bool _hasSentTick;
    private Vector3 _lastSentPosition;
    private float _lastSentYaw;
    private float _lastSentPitch;
    private int _lastSentState;
    private string _lastSentLook = "";
    private string _lastSentHeld = "";
    private string _lastSentObjects = "";
    private ulong _lastSentAtMsec;
    private const float TickPositionEpsilonSquared = 0.0025f; // 5 cm
    private const float TickRotationEpsilon = 0.015f; // ~0,86°
    private const ulong IdleHeartbeatMsec = 1000;
    private bool _active;
    private bool _onlineInventorySession;

    private static NetClient Net => NetClient.Instance;
    private int MyId => Net?.MyId ?? 0;

    public override void _Ready()
    {
        if (Net == null || !Net.IsOnline || Player == null || Van == null || Delivery == null)
        {
            SetPhysicsProcess(false);
            return;
        }
        _active = true;
        Instance = this;
        PlayerInventory.Instance?.BeginOnlineSession();
        _onlineInventorySession = PlayerInventory.Instance != null;
        // A cena ainda está montando os filhos; criar caixas agora daria erro de add_child.
        Callable.From(Initialize).CallDeferred();
    }

    private void Initialize()
    {
        _remoteRoot = new Node3D { Name = "RemotePlayers" };
        AddChild(_remoteRoot);
        _vehicles[VanId] = Van;
        foreach (Node n in GetTree().GetNodesInGroup(VanController.GroupName))
            if (n is VanController v && !string.IsNullOrEmpty(v.NetId))
                _vehicles[v.NetId] = v;

        // As caixas colocadas na cena são só do modo solo.
        foreach (Node node in GetTree().GetNodesInGroup(CargoItem.GroupName))
        {
            if (node is CargoItem cargo && string.IsNullOrEmpty(cargo.NetId))
            {
                cargo.RemoveFromGroup(CargoItem.GroupName);
                cargo.QueueFree();
            }
        }

        Delivery.CanReportDelivery = cargo => IsMine(cargo.NetId);
        Delivery.DeliveryReached += OnDeliveryReached;
        Delivery.JobCargoDestroyed += OnJobCargoDestroyed;
        Player.CargoGrabbed += OnCargoGrabbed;
        Player.VehicleEntered += OnVehicleEntered;
        Player.VehicleExited += OnVehicleExited;
        if (PlayerInventory.Instance != null)
            PlayerInventory.Instance.Changed += SyncInventory;
        Net.MessageReceived += OnMessage;
        Net.Disconnected += OnDisconnected;

        CityGenerator city = Delivery.City;
        _worldDataReady = city == null || city.WorldData != null;
        if (!_worldDataReady)
            city.WorldDataReady += OnWorldDataReady;

        using JsonDocument welcome = JsonDocument.Parse(Net.WelcomeJson);
        ApplyWelcome(welcome.RootElement);
    }

    public override void _ExitTree()
    {
        ReleaseAllRemoteChunkPins();
        if (Instance == this)
            Instance = null;
        if (Delivery?.City != null)
            Delivery.City.WorldDataReady -= OnWorldDataReady;
        _pendingWorldDataActions.Clear();
        if (_active && Net != null)
        {
            if (PlayerInventory.Instance != null)
                PlayerInventory.Instance.Changed -= SyncInventory;
            Net.MessageReceived -= OnMessage;
            Net.Disconnected -= OnDisconnected;
        }
        if (_onlineInventorySession)
        {
            PlayerInventory.Instance?.EndOnlineSession();
            _onlineInventorySession = false;
        }
    }

    // ================================================================ recebendo

    /// <summary>Veículo criado depois do início (carro comprado na concessionária, v0.9.7).</summary>
    public void RegisterVehicle(VanController v)
    {
        if (v != null && !string.IsNullOrEmpty(v.NetId))
            _vehicles[v.NetId] = v;
    }

    private void ApplyWelcome(JsonElement m)
    {
        ApplyInventoryState(m);
        // Carros da sala (precisam existir antes de aplicar donos e posições).
        var cars = new System.Collections.Generic.List<string>();
        if (m.TryGetProperty("cars", out JsonElement carsEl) && carsEl.ValueKind == JsonValueKind.Array)
            foreach (JsonElement c in carsEl.EnumerateArray())
                if (c.ValueKind == JsonValueKind.String)
                    cars.Add(c.GetString());
        CarGarage.SetOnline(cars);

        foreach (JsonElement p in m.GetProperty("players").EnumerateArray())
        {
            RemotePlayer remote = SpawnRemote(p.GetProperty("id").GetInt32(), p.GetProperty("name").GetString(), NetClient.ParseColor(p.GetProperty("color").GetString()));
            if (remote != null && p.TryGetProperty("state", out JsonElement state) && state.ValueKind == JsonValueKind.Object)
                ApplyPlayerState(remote, state);
        }

        if (_worldDataReady)
            ApplyShift(m);
        else
        {
            JsonElement pendingWelcome = m.Clone();
            QueueWorldDataAction(() => ApplyShift(pendingWelcome));
        }

        foreach (JsonProperty owner in m.GetProperty("owners").EnumerateObject())
            SetOwner(owner.Name, owner.Value.ValueKind == JsonValueKind.Number ? owner.Value.GetInt32() : 0);

        foreach (JsonElement obj in m.GetProperty("objects").EnumerateArray())
            ApplyObject(obj);
        if (m.TryGetProperty("worldItems", out JsonElement worldItems) && worldItems.ValueKind == JsonValueKind.Array)
            foreach (JsonElement item in worldItems.EnumerateArray())
                AddWorldItem(item);
    }

    private void QueueWorldDataAction(Action action)
    {
        if (_worldDataReady)
        {
            action();
            return;
        }

        _pendingWorldDataActions.Enqueue(action);
    }

    private void OnWorldDataReady()
    {
        if (Delivery?.City != null)
            Delivery.City.WorldDataReady -= OnWorldDataReady;
        _worldDataReady = true;

        while (_pendingWorldDataActions.Count > 0)
            _pendingWorldDataActions.Dequeue()?.Invoke();

        // Donos e posições recebidos durante o loading são reaplicados depois que as cargas existem.
        foreach (KeyValuePair<string, int> owner in new List<KeyValuePair<string, int>>(_owners))
            SetOwner(owner.Key, owner.Value);

        foreach (KeyValuePair<int, RemotePlayer> remote in _remotes)
        {
            if (remote.Value != null && GodotObject.IsInstanceValid(remote.Value))
                UpdateRemoteChunkPin(remote.Key, remote.Value.GlobalPosition);
        }
    }

    /// <summary>
    /// A fase foi descarregada mas o evento ainda chegou (ex.: troca de cena no meio de um frame):
    /// solta os eventos e ignora. Antes isso enchia o console de ObjectDisposedException.
    /// </summary>
    private bool Stale()
    {
        if (IsInstanceValid(this) && IsInstanceValid(Van) && IsInstanceValid(Player) && IsInstanceValid(Delivery))
            return false;
        if (Net != null)
        {
            Net.MessageReceived -= OnMessage;
            Net.Disconnected -= OnDisconnected;
        }
        return true;
    }

    private void OnMessage(JsonElement m)
    {
        if (Stale())
            return;
        switch (m.GetProperty("t").GetString())
        {
            case "join":
            {
                JsonElement p = m.GetProperty("player");
                SpawnRemote(p.GetProperty("id").GetInt32(), p.GetProperty("name").GetString(), NetClient.ParseColor(p.GetProperty("color").GetString()));
                break;
            }
            case "leave":
            {
                int id = m.GetProperty("id").GetInt32();
                if (_remotes.Remove(id, out RemotePlayer remote))
                    remote.QueueFree();
                ReleaseRemoteChunkPin(id);
                _remoteDriving.Remove(id);
                RefreshRemoteDriver();
                break;
            }
            case "tick":
            {
                int id = m.GetProperty("id").GetInt32();
                // Pode ter entrado enquanto a fase carregava: cria a partir da lista do NetClient.
                if (!_remotes.TryGetValue(id, out RemotePlayer remote) && Net.Players.TryGetValue(id, out NetPlayerInfo info))
                    remote = SpawnRemote(info.Id, info.Name, info.Color);
                if (remote != null)
                    ApplyPlayerState(remote, m);
                _remoteDriving[id] = m.GetProperty("st").GetInt32() == 1;
                foreach (JsonElement obj in m.GetProperty("objs").EnumerateArray())
                    ApplyObject(obj);
                RefreshRemoteDriver();
                break;
            }
            case "owner":
            {
                JsonElement owner = m.GetProperty("owner");
                SetOwner(m.GetProperty("id").GetString(), owner.ValueKind == JsonValueKind.Number ? owner.GetInt32() : 0);
                break;
            }
            case "claimFail":
                OnClaimFailed(m.GetProperty("id").GetString(), m.TryGetProperty("owner", out JsonElement o) && o.ValueKind == JsonValueKind.Number ? o.GetInt32() : 0);
                break;
            case "shift":
            {
                JsonElement pendingShift = m.Clone();
                QueueWorldDataAction(() => ApplyShift(pendingShift));
                break;
            }
            case "delivered":
            {
                JsonElement pendingDelivery = m.Clone();
                QueueWorldDataAction(() =>
                {
                    string cargoId = pendingDelivery.GetProperty("cargoId").GetString();
                    _owners.Remove(cargoId);
                    Delivery.ApplyNetDelivered(
                        cargoId,
                        pendingDelivery.GetProperty("payment").GetInt32(),
                        pendingDelivery.GetProperty("money").GetInt32(),
                        pendingDelivery.GetProperty("name").GetString());
                });
                break;
            }
            case "lost":
            {
                JsonElement pendingLost = m.Clone();
                QueueWorldDataAction(() =>
                    Delivery.ApplyNetLost(pendingLost.GetProperty("cargoId").GetString()));
                break;
            }
            case "shiftDone":
            {
                JsonElement pendingShiftDone = m.Clone();
                QueueWorldDataAction(() =>
                    Delivery.ApplyNetShiftDone(pendingShiftDone.GetProperty("shift").GetInt32()));
                break;
            }
            case "gameOver":
            {
                JsonElement pendingGameOver = m.Clone();
                QueueWorldDataAction(() =>
                {
                    Delivery.ApplyNetMoney(pendingGameOver.GetProperty("money").GetInt32());
                    Delivery.ApplyNetGameOver(pendingGameOver.GetProperty("shift").GetInt32());
                });
                break;
            }
            case "money":
            {
                Delivery.ApplyNetMoney(m.GetProperty("money").GetInt32());
                // Carro comprado (por qualquer um da sala) aparece no pátio da concessionária.
                if (m.TryGetProperty("item", out JsonElement boughtItem) && boughtItem.ValueKind == JsonValueKind.String && ItemCatalog.Get(boughtItem.GetString())?.Car == true)
                    CarGarage.MarkBought(boughtItem.GetString());
                // Resposta de uma compra minha?
                if (m.TryGetProperty("by", out JsonElement by) && by.GetInt32() == MyId
                    && m.TryGetProperty("req", out JsonElement req) && _purchaseCallbacks.Remove(req.GetInt32(), out Action<bool> done))
                    done(true);
                break;
            }
            case "buyFail":
                if (m.TryGetProperty("req", out JsonElement failReq) && _purchaseCallbacks.Remove(failReq.GetInt32(), out Action<bool> failed))
                    failed(false);
                break;
            case "sold":
                Delivery.ApplyNetMoney(m.GetProperty("money").GetInt32());
                if (m.GetProperty("by").GetInt32() == MyId
                    && m.TryGetProperty("req", out JsonElement soldReq)
                    && _saleCallbacks.Remove(soldReq.GetInt32(), out Action<bool, int> sold))
                    sold(true, m.GetProperty("price").GetInt32());
                break;
            case "sellFail":
                if (m.TryGetProperty("req", out JsonElement failedSaleReq)
                    && _saleCallbacks.Remove(failedSaleReq.GetInt32(), out Action<bool, int> failedSale))
                    failedSale(false, 0);
                break;
            case "itemSpawn":
                AddWorldItem(m);
                break;
            case "itemRemove":
                RemoveWorldItem(m.GetProperty("id").GetString());
                break;
            case "itemsState":
                ApplyInventoryState(m);
                break;
        }
    }

    private void ApplyShift(JsonElement m)
    {
        var jobs = new List<DeliveryManager.NetJob>();
        foreach (JsonElement j in m.GetProperty("jobs").EnumerateArray())
        {
            jobs.Add(new DeliveryManager.NetJob(
                j.GetProperty("id").GetString(),
                j.GetProperty("type").GetInt32(),
                j.GetProperty("pick").GetInt32(),
                j.GetProperty("slot").GetInt32(),
                j.GetProperty("status").GetString()));
        }
        foreach (DeliveryJob old in Delivery.Jobs)
            _owners.Remove(old.Id);
        Delivery.ApplyNetShift(m.GetProperty("shift").GetInt32(), jobs, m.GetProperty("money").GetInt32());
    }

    private void ApplyPlayerState(RemotePlayer remote, JsonElement s)
    {
        string equipment = s.TryGetProperty("eq", out JsonElement eq) && eq.ValueKind == JsonValueKind.String ? eq.GetString() : "";
        string look = s.TryGetProperty("lk", out JsonElement lk) && lk.ValueKind == JsonValueKind.String ? lk.GetString() : "";
        string held = s.TryGetProperty("held", out JsonElement heldElement) && heldElement.ValueKind == JsonValueKind.String
            ? heldElement.GetString()
            : "";
        Vector3 position = ReadVector(s.GetProperty("p"));
        remote.ApplyState(position, s.GetProperty("ry").GetSingle(), s.GetProperty("hp").GetSingle(), s.GetProperty("st").GetInt32(), equipment, look, held);
        UpdateRemoteChunkPin(remote.PlayerId, position);
    }

    private CityGenerator StreamingCity
    {
        get
        {
            CityGenerator city = Delivery?.City;
            return city != null && IsInstanceValid(city) && city.UsesChunkStreaming ? city : null;
        }
    }

    private void UpdateRemoteChunkPin(int playerId, Vector3 position)
    {
        CityGenerator city = StreamingCity;
        if (city == null || city.WorldData == null)
            return;

        WorldChunkCoordinate next = city.WorldData.GetChunkCoordinate(position);
        if (_remoteChunkPins.TryGetValue(playerId, out Vector3 previousPosition))
        {
            WorldChunkCoordinate previous = city.WorldData.GetChunkCoordinate(previousPosition);
            if (previous == next)
                return;

            city.UnpinChunkForPosition(previousPosition);
            _remoteChunkPins.Remove(playerId);
        }

        if (city.PinChunkForPosition(position))
            _remoteChunkPins[playerId] = position;
    }

    private void ReleaseRemoteChunkPin(int playerId)
    {
        if (!_remoteChunkPins.Remove(playerId, out Vector3 position))
            return;

        CityGenerator city = Delivery?.City;
        if (city != null && IsInstanceValid(city))
            city.UnpinChunkForPosition(position);
    }

    private void ReleaseAllRemoteChunkPins()
    {
        foreach (Vector3 position in _remoteChunkPins.Values)
        {
            CityGenerator city = Delivery?.City;
            if (city != null && IsInstanceValid(city))
                city.UnpinChunkForPosition(position);
        }
        _remoteChunkPins.Clear();
    }

    /// <summary>Compra online: o servidor confere o preço e desconta do dinheiro do grupo.</summary>
    public void RequestPurchase(string itemId, Action<bool> done)
    {
        int req = _nextPurchaseId++;
        _purchaseCallbacks[req] = done;
        Net.Send(new { t = "buy", item = itemId, req });
    }

    public void RequestSale(string itemId, Action<bool, int> done)
    {
        int req = _nextSaleId++;
        _saleCallbacks[req] = done;
        Net.Send(new { t = "sell", item = itemId, req });
    }

    public void RequestWorldItemDrop(string itemId, Vector3 position, Quaternion rotation) =>
        Net.Send(new { t = "itemDrop", item = itemId, p = Vec(position), q = new[] { Round(rotation.X), Round(rotation.Y), Round(rotation.Z), Round(rotation.W) } });

    public void RequestWorldItemPickup(WorldItem item)
    {
        if (item != null && !string.IsNullOrEmpty(item.WorldId))
            Net.Send(new { t = "itemPickup", id = item.WorldId });
    }

    public void RequestItemTransfer(int playerId, string itemId) =>
        Net.Send(new { t = "itemTransfer", to = playerId, item = itemId });

    private void AddWorldItem(JsonElement data)
    {
        string id = data.GetProperty("id").GetString();
        if (string.IsNullOrEmpty(id) || _worldItems.ContainsKey(id))
            return;
        string itemId = data.GetProperty("item").GetString();
        Vector3 position = ReadVector(data.GetProperty("p"));
        Quaternion rotation = data.TryGetProperty("q", out JsonElement q) ? ReadQuaternion(q) : Quaternion.Identity;
        WorldItem item = WorldItem.Create(id, itemId, position, rotation);
        if (item == null)
            return;
        _worldItems[id] = item;
        Node parent = GetTree().CurrentScene ?? GetTree().Root;
        parent.AddChild(item);
        item.GlobalPosition = position;
        item.GlobalBasis = new Basis(rotation);
    }

    private void RemoveWorldItem(string id)
    {
        if (_worldItems.Remove(id, out WorldItem item) && IsInstanceValid(item))
            item.QueueFree();
    }

    private static void ApplyInventoryState(JsonElement message)
    {
        if (!message.TryGetProperty("items", out JsonElement items) || items.ValueKind != JsonValueKind.Array)
            return;
        var result = new List<string>();
        foreach (JsonElement item in items.EnumerateArray())
            if (item.ValueKind == JsonValueKind.String)
                result.Add(item.GetString());
        string equipment = message.TryGetProperty("equipment", out JsonElement eq) && eq.ValueKind == JsonValueKind.String
            ? eq.GetString()
            : "";
        PlayerInventory.Instance?.ApplyNetworkState(result, equipment);
    }

    private void SyncInventory()
    {
        if (Net?.IsOnline == true && PlayerInventory.Instance != null)
            Net.Send(new { t = "inventory", items = PlayerInventory.Instance.Items, eq = PlayerInventory.Instance.EquipmentCsv() });
    }

    private void ApplyObject(JsonElement o)
    {
        if (!_worldDataReady)
        {
            JsonElement pendingObject = o.Clone();
            QueueWorldDataAction(() => ApplyObject(pendingObject));
            return;
        }

        string id = o.GetProperty("id").GetString();
        if (string.IsNullOrEmpty(id) || IsMine(id))
            return;

        Vector3 position = ReadVector(o.GetProperty("p"));
        Quaternion rotation = ReadQuaternion(o.GetProperty("q"));
        Vector3 velocity = o.TryGetProperty("v", out JsonElement v) ? ReadVector(v) : Vector3.Zero;

        if (VehicleOf(id) is VanController vehicle)
        {
            vehicle.ApplyRemoteState(position, rotation, velocity);
            return;
        }

        float integrity = o.TryGetProperty("integ", out JsonElement integ) ? integ.GetSingle() : -1f;
        Delivery.FindCargo(id)?.ApplyRemoteState(position, rotation, velocity, integrity);
    }

    private void SetOwner(string id, int owner)
    {
        if (string.IsNullOrEmpty(id))
            return;
        _owners[id] = owner;
        bool mine = owner != 0 && owner == MyId;
        // Sem dono: cada cliente simula sozinho (objeto parado). Só segue o estado remoto se OUTRO jogador for dono.
        bool remote = owner != 0 && owner != MyId;

        if (VehicleOf(id) is VanController vehicle)
        {
            // Se eu estou dirigindo, o veículo continua comigo até eu sair.
            if (!mine && Player.CurrentVehicle == vehicle && Player.State == PlayerState.Driving)
                return;
            vehicle.SetRemote(remote);
            RefreshRemoteDriver();
            return;
        }

        CargoItem cargo = Delivery.FindCargo(id);
        if (cargo == null)
            return;
        if (remote && cargo.IsHeld)
            Player.DropHeld();
        cargo.SetRemote(remote);
    }

    private void OnClaimFailed(string id, int owner)
    {
        if (VehicleOf(id) is VanController vehicle)
        {
            if (Player.CurrentVehicle == vehicle)
                Player.ExitVehicle();
            SetOwner(id, owner);
            return;
        }
        if (Player.HeldItem != null && Player.HeldItem.NetId == id)
            Player.DropHeld();
        SetOwner(id, owner);
    }

    private void RefreshRemoteDriver()
    {
        foreach ((string id, VanController v) in _vehicles)
        {
            if (!IsInstanceValid(v))
                continue;
            _owners.TryGetValue(id, out int owner);
            bool remoteDriving = owner != 0 && owner != MyId && _remoteDriving.TryGetValue(owner, out bool d) && d;
            v.SetRemoteDriver(remoteDriving);
        }
    }

    private void OnDisconnected(string message)
    {
        if (Stale())
            return;
        _active = false;
        SetPhysicsProcess(false);
        GetTree().Paused = false;
        foreach (Action<bool, int> callback in _saleCallbacks.Values)
            callback(false, 0);
        _saleCallbacks.Clear();
        GetTree().CreateTimer(2.5).Timeout += () =>
        {
            if (IsInsideTree())
                GetTree().ChangeSceneToFile(MainMenuScene);
        };
    }

    // ================================================================ ações locais

    public bool IsMine(string id) => !string.IsNullOrEmpty(id) && _owners.TryGetValue(id, out int owner) && owner == MyId;

    private void Claim(string id)
    {
        if (IsMine(id))
            return;
        _owners[id] = MyId; // otimista: o servidor confirma com "owner" ou desfaz com "claimFail"
        Net.Send(new { t = "claim", id });
    }

    private void OnCargoGrabbed(CargoItem cargo)
    {
        if (string.IsNullOrEmpty(cargo.NetId))
            return;
        cargo.SetRemote(false);
        Claim(cargo.NetId);
    }

    private void OnVehicleEntered(VanController van)
    {
        van.SetRemote(false);
        Claim(van.NetId);
    }

    private void OnVehicleExited(VanController van)
    {
        if (IsMine(van.NetId))
            Net.Send(new { t = "release", id = van.NetId });
    }

    private void OnDeliveryReached(DeliveryJob job)
    {
        Net.Send(new { t = "deliver", cargoId = job.Id, integ = Round(job.Cargo.Integrity) });
    }

    private void OnJobCargoDestroyed(DeliveryJob job)
    {
        if (IsMine(job.Id))
            Net.Send(new { t = "lost", cargoId = job.Id });
    }

    // ================================================================ enviando

    public override void _PhysicsProcess(double delta)
    {
        if (!_active || _remoteRoot == null || !Net.IsOnline)
            return;

        _tickTimer -= delta;
        if (_tickTimer > 0)
            return;
        _tickTimer = 1.0 / TickRate;

        ClaimCargoInVanBed();
        SendTick();
    }

    private void SendTick()
    {
        var objs = new List<Dictionary<string, object>>();
        foreach ((string id, VanController v) in _vehicles)
            if (IsInstanceValid(v) && IsMine(id))
                objs.Add(ObjectState(id, v, null));
        foreach (DeliveryJob job in Delivery.Jobs)
        {
            if (objs.Count >= 16)
                break;
            if (job.Cargo != null && IsInstanceValid(job.Cargo) && IsMine(job.Id))
                objs.Add(ObjectState(job.Id, job.Cargo, job.Cargo.Integrity));
        }

        Vector3 position = Player.GlobalPosition;
        Vector3 look = Player.Camera.GlobalRotation;
        float yaw = (float)Round(look.Y);
        float pitch = (float)Round(look.X);
        int state = Player.State == PlayerState.Driving ? 1 : Player.State == PlayerState.Riding ? 2 : Player.IsClimbing ? 3 : 0;
        string lookCode = CharacterLook.Local.Encode();
        string held = Player.HeldItem?.NetId ?? (PlayerInventory.Instance?.SelectedHotbarItemId is string heldItem ? "item:" + heldItem : "");
        string objectsSignature = JsonSerializer.Serialize(objs);
        ulong now = Time.GetTicksMsec();

        bool changed = !_hasSentTick
            || position.DistanceSquaredTo(_lastSentPosition) > TickPositionEpsilonSquared
            || Mathf.Abs(Mathf.Wrap(yaw - _lastSentYaw, -Mathf.Pi, Mathf.Pi)) > TickRotationEpsilon
            || Mathf.Abs(pitch - _lastSentPitch) > TickRotationEpsilon
            || state != _lastSentState
            || lookCode != _lastSentLook
            || held != _lastSentHeld
            || objectsSignature != _lastSentObjects;

        // Sem mudanças, basta um heartbeat por segundo. Em movimento, mantém o tick normal de 10 Hz.
        if (!changed && now - _lastSentAtMsec < IdleHeartbeatMsec)
            return;

        Net.Send(new
        {
            t = "tick",
            p = Vec(position),
            ry = yaw,
            hp = pitch,
            st = state,
            lk = lookCode,
            held = string.IsNullOrEmpty(held) ? null : held,
            eq = PlayerInventory.Instance?.EquipmentCsv() ?? "",
            objs,
        });

        _hasSentTick = true;
        _lastSentPosition = position;
        _lastSentYaw = yaw;
        _lastSentPitch = pitch;
        _lastSentState = state;
        _lastSentLook = lookCode;
        _lastSentHeld = held;
        _lastSentObjects = objectsSignature;
        _lastSentAtMsec = now;
    }

    /// <summary>Quem dirige passa a simular as caixas que estão na caçamba (senão elas caem da van nos outros clientes).</summary>
    private void ClaimCargoInVanBed()
    {
        if (Player.State != PlayerState.Driving || !IsMine(VanId))
            return;
        foreach (DeliveryJob job in Delivery.Jobs)
        {
            CargoItem cargo = job.Cargo;
            if (cargo == null || !IsInstanceValid(cargo) || IsMine(job.Id))
                continue;
            Vector3 local = Van.ToLocal(cargo.GlobalPosition);
            bool inBed = Mathf.Abs(local.X) < 1f && local.Y > 0.8f && local.Y < 2.4f && local.Z > -2.4f && local.Z < 1f;
            if (inBed)
            {
                cargo.SetRemote(false);
                Claim(job.Id);
            }
        }
    }

    private static Dictionary<string, object> ObjectState(string id, RigidBody3D body, float? integrity)
    {
        Quaternion q = body.GlobalBasis.GetRotationQuaternion();
        var state = new Dictionary<string, object>
        {
            ["id"] = id,
            ["p"] = Vec(body.GlobalPosition),
            ["q"] = new[] { Round(q.X), Round(q.Y), Round(q.Z), Round(q.W) },
            ["v"] = Vec(body.LinearVelocity),
        };
        if (integrity.HasValue)
            state["integ"] = Round(integrity.Value);
        return state;
    }

    // ================================================================ utilitários

    private RemotePlayer SpawnRemote(int id, string name, Color color)
    {
        if (id == MyId)
            return null;
        if (_remotes.TryGetValue(id, out RemotePlayer existing))
            return existing;
        var remote = new RemotePlayer();
        remote.Setup(id, name ?? "?", color);
        _remoteRoot.AddChild(remote);
        _remotes[id] = remote;
        return remote;
    }

    private static double Round(float value) => Math.Round(value, 3);

    private static double[] Vec(Vector3 v) => new[] { Round(v.X), Round(v.Y), Round(v.Z) };

    private static Vector3 ReadVector(JsonElement a) =>
        new(a[0].GetSingle(), a[1].GetSingle(), a[2].GetSingle());

    private static Quaternion ReadQuaternion(JsonElement a)
    {
        var q = new Quaternion(a[0].GetSingle(), a[1].GetSingle(), a[2].GetSingle(), a[3].GetSingle());
        return q.LengthSquared() < 0.0001f ? Quaternion.Identity : q.Normalized();
    }
}
