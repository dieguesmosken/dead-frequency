// Scripts/Jobs/GariJob.cs
// Gari: sacos de lixo aparecem nas calçadas (marcador laranja). Pegue até 3 (E) e jogue numa caçamba.
// R$ 25 por saco. Há caçambas pelas calçadas da cidade e uma na esquina do depósito.
// v0.9.9: às vezes aparece SUCATA no lugar do saco (rádio velho, cano, roda). Ela não vai na caçamba:
// leve ao Conserta-Tudo do Primo (barraca em frente ao depósito), que paga o mesmo e vende melhorias.
using System.Collections.Generic;
using Godot;

namespace DeadFrequency;

public partial class GariJob : JobBase
{
    private const int MaxCarry = 3;
    private const int BagsOnStreet = 6;
    private static readonly Color Orange = new(1f, 0.55f, 0.15f);

    private readonly List<Node3D> _bags = new();
    private readonly List<Node3D> _dumpsters = new();
    private readonly Dictionary<Node3D, Vector3> _bagPinPositions = new();
    private int _carrying;      // total na mão (sacos + sucata)
    private int _scrap;         // quantos desses são sucata
    private Node3D _primo;      // balança do Conserta-Tudo do Primo (se a fase tiver)
    private StandardMaterial3D _rustMat;
    private static readonly Color Gray = new(0.75f, 0.75f, 0.8f);
    private double _respawn;
    private StandardMaterial3D _bagMat;
    private StandardMaterial3D _tieMat;

    public override void _Ready()
    {
        _bagMat = new StandardMaterial3D { AlbedoColor = new Color(0.04f, 0.04f, 0.05f), Roughness = 0.3f, Metallic = 0.1f };
        _tieMat = new StandardMaterial3D { AlbedoColor = new Color(0.9f, 0.75f, 0.1f) };
        foreach (Vector3 spot in City.DumpsterSpots)
            AddDumpster(spot, false);
        // Caçamba da empresa, na esquina do depósito (vale para a cidade e o rancho).
        AddDumpster(new Vector3(City.BlockSize * 0.5f - 2f, City.IsFarm ? 0f : City.CurbHeight, City.BlockSize * 0.5f - 2f), true);
        _rustMat = new StandardMaterial3D { AlbedoColor = new Color(0.45f, 0.24f, 0.12f), Roughness = 0.8f };
        AddPrimo();
        for (int i = 0; i < BagsOnStreet; i++)
            SpawnBag();
    }

    /// <summary>Ponto de venda de sucata ao lado da barraca do Conserta-Tudo do Primo.</summary>
    private void AddPrimo()
    {
        foreach (StreetVendor v in StreetVendor.All)
        {
            if (!IsInstanceValid(v) || v.Stock != ShopStock.ConsertaTudo)
                continue;
            _primo = new Node3D { Name = "BalancaPrimo" };
            AddChild(_primo);
            _primo.GlobalPosition = v.GlobalTransform * new Vector3(1.9f, 0f, 0.9f);
            ActionArea.Create(_primo, new Vector3(0f, 0.5f, 0f), new Vector3(1.2f, 1.2f, 1.2f),
                _ => _scrap > 0 ? $"[E] Vender {_scrap} sucata(s) ao Primo" : "Primo compra sucata (traga da rua)",
                _ => SellScrap(),
                _ => _scrap > 0);
            _primo.AddToGroup(MinimapView.MarkerGroup);
            _primo.SetMeta("map_color", Gray);
            _primo.SetMeta("map_kind", "job");
            return;
        }
    }

    public override void _ExitTree()
    {
        foreach (Vector3 position in _bagPinPositions.Values)
            City?.UnpinChunkForPosition(position);
        _bagPinPositions.Clear();
        foreach (Node3D n in _bags)
            if (IsInstanceValid(n))
                n.QueueFree();
        foreach (Node3D n in _dumpsters)
            if (IsInstanceValid(n))
                n.QueueFree();
    }

    private void AddDumpster(Vector3 pos, bool spawnModel)
    {
        var root = new Node3D { Name = "Cacamba" };
        AddChild(root);
        root.GlobalPosition = pos;
        if (spawnModel)
            AssetProps.Spawn(root, AssetProps.City + "dumpster.gltf", Vector3.Zero, Mathf.Pi * 0.5f, 3.2f, new Vector3(1.8f, 1.05f, 1.15f));
        ActionArea.Create(root, new Vector3(0f, 0.7f, 0f), new Vector3(1.6f, 1.6f, 2.2f),
            _ => _carrying - _scrap > 0 ? $"[E] Jogar {_carrying - _scrap} saco(s) na caçamba" : "Caçamba (traga sacos de lixo)",
            _ => Dump(),
            _ => _carrying - _scrap > 0);
        root.AddToGroup(MinimapView.MarkerGroup);
        root.SetMeta("map_color", new Color(0.6f, 0.35f, 0.1f));
        root.SetMeta("map_kind", "job");
        _dumpsters.Add(root);
    }

    private void SpawnBag()
    {
        int idx = RandomPoint(PlayerPos, 25f, 260f);
        if (idx < 0)
            return;
        Vector3 basePos = City.DeliveryPoints[idx].Position;
        Vector3 pos = basePos + new Vector3(Rng.RandfRange(-3f, 3f), 0f, Rng.RandfRange(-3f, 3f));
        if (_primo != null && Rng.Randf() < 0.25f)
        {
            SpawnScrap(pos);
            return;
        }
        var bag = JobMarkers.Create(this, pos, Orange, "LIXO", 10f, 0.5f);
        bag.AddChild(new MeshInstance3D { Mesh = new SphereMesh { Radius = 0.5f, Height = 1f, RadialSegments = 8, Rings = 4 }, MaterialOverride = _bagMat, Scale = new Vector3(0.55f, 0.6f, 0.5f), Position = new Vector3(0f, 0.3f, 0f) });
        bag.AddChild(new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 0.02f, BottomRadius = 0.07f, Height = 0.18f }, MaterialOverride = _bagMat, Position = new Vector3(0f, 0.66f, 0f) });
        bag.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(0.12f, 0.03f, 0.12f) }, MaterialOverride = _tieMat, Position = new Vector3(0f, 0.62f, 0f) });
        Node3D captured = bag;
        ActionArea.Create(bag, new Vector3(0f, 0.4f, 0f), new Vector3(0.9f, 0.9f, 0.9f),
            _ => _carrying < MaxCarry ? $"[E] Pegar saco de lixo ({_carrying}/{MaxCarry})" : "Mãos cheias: jogue os sacos numa caçamba",
            _ => PickBag(captured),
            p => _carrying < MaxCarry && !p.IsHolding && p.CurrentVehicle == null);
        _bags.Add(bag);
        if (City.PinChunkForPosition(pos))
            _bagPinPositions[bag] = pos;
    }

    private void SpawnScrap(Vector3 pos)
    {
        var scrap = JobMarkers.Create(this, pos, Gray, "SUCATA", 10f, 0.5f);
        scrap.SetMeta("scrap", true);
        int kind = Rng.RandiRange(0, 2);
        if (kind == 0) // rádio velho
        {
            scrap.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(0.5f, 0.3f, 0.2f) }, MaterialOverride = _rustMat, Position = new Vector3(0f, 0.15f, 0f) });
            scrap.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(0.2f, 0.18f, 0.02f) }, MaterialOverride = _bagMat, Position = new Vector3(-0.1f, 0.15f, 0.11f) });
        }
        else if (kind == 1) // cano
            scrap.AddChild(new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 0.06f, BottomRadius = 0.06f, Height = 1.1f }, MaterialOverride = _rustMat, Position = new Vector3(0f, 0.07f, 0f), Rotation = new Vector3(0f, 0.6f, Mathf.Pi * 0.5f) });
        else // roda
            scrap.AddChild(new MeshInstance3D { Mesh = new TorusMesh { InnerRadius = 0.18f, OuterRadius = 0.32f, Rings = 10, RingSegments = 6 }, MaterialOverride = _bagMat, Position = new Vector3(0f, 0.07f, 0f) });
        Node3D captured = scrap;
        ActionArea.Create(scrap, new Vector3(0f, 0.3f, 0f), new Vector3(1.1f, 0.8f, 1.1f),
            _ => _carrying < MaxCarry ? $"[E] Pegar sucata ({_carrying}/{MaxCarry})" : "Mãos cheias: venda a sucata ao Primo ou jogue os sacos fora",
            _ => PickBag(captured),
            p => _carrying < MaxCarry && !p.IsHolding && p.CurrentVehicle == null);
        _bags.Add(scrap);
    }

    private void SellScrap()
    {
        if (_scrap <= 0)
            return;
        int n = _scrap;
        _scrap = 0;
        _carrying -= n;
        M.Earn(n, n == 1 ? "Sucata vendida ao Primo" : $"{n} sucatas vendidas ao Primo");
    }

    private void PickBag(Node3D bag)
    {
        if (_carrying >= MaxCarry || !_bags.Contains(bag))
            return;
        bool scrap = bag.HasMeta("scrap");
        _bags.Remove(bag);
        if (_bagPinPositions.Remove(bag, out Vector3 pinnedPosition))
            City.UnpinChunkForPosition(pinnedPosition);
        bag.QueueFree();
        _carrying++;
        if (scrap)
            _scrap++;
        GameHud.Toast(this, scrap ? $"Sucata recolhida ({_carrying}/{MaxCarry}): venda ao Conserta-Tudo do Primo" : $"Saco recolhido ({_carrying}/{MaxCarry})", scrap ? Gray : Orange, 2.0);
        if (_respawn <= 0)
            _respawn = 6.0;
    }

    private void Dump()
    {
        int n = _carrying - _scrap; // sucata não vai na caçamba
        if (n <= 0)
        {
            if (_scrap > 0)
                GameHud.Toast(this, "Sucata não é lixo: o Primo paga por ela (Conserta-Tudo, em frente ao depósito)", Gray, 3.0);
            return;
        }
        _carrying = _scrap;
        M.Earn(n, n == 1 ? "Saco de lixo na caçamba" : $"{n} sacos de lixo na caçamba");
    }

    public override void _Process(double delta)
    {
        if (_respawn > 0)
        {
            _respawn -= delta;
            if (_respawn <= 0 && _bags.Count < BagsOnStreet)
            {
                SpawnBag();
                if (_bags.Count < BagsOnStreet)
                    _respawn = 4.0;
            }
        }
    }

    public override bool TryGetTarget(out Vector3 position, out string label)
    {
        position = Vector3.Zero;
        // Com sucata na mão (e sem saco), a seta aponta para o Primo.
        if (_scrap > 0 && _carrying == _scrap && _primo != null)
        {
            position = _primo.GlobalPosition;
            label = "PRIMO";
            return true;
        }
        label = _carrying > 0 ? "CAÇAMBA" : "LIXO";
        float best = float.MaxValue;
        foreach (Node3D n in _carrying > 0 ? _dumpsters : _bags)
        {
            if (!IsInstanceValid(n))
                continue;
            float d = PlayerPos.DistanceSquaredTo(n.GlobalPosition);
            if (d < best)
            {
                best = d;
                position = n.GlobalPosition;
            }
        }
        return best < float.MaxValue;
    }

    public override bool TryGetSpawn(out Vector3 position, out float yaw)
    {
        // Ao lado da caçamba da empresa, na esquina do depósito.
        position = new Vector3(City.BlockSize * 0.5f - 4f, (City.IsFarm ? 0f : City.CurbHeight) + 0.1f, City.BlockSize * 0.5f - 2f);
        yaw = Mathf.Pi * 0.5f;
        return true;
    }

    public override string PanelText()
    {
        float nearestBag = float.MaxValue, nearestDump = float.MaxValue;
        foreach (Node3D b in _bags)
            if (IsInstanceValid(b))
                nearestBag = Mathf.Min(nearestBag, PlayerPos.DistanceTo(b.GlobalPosition));
        foreach (Node3D d in _dumpsters)
            nearestDump = Mathf.Min(nearestDump, PlayerPos.DistanceTo(d.GlobalPosition));
        string bag = nearestBag < float.MaxValue ? $"{nearestBag:0} m" : "-";
        string primo = _primo != null ? $"\n· Sucata vai para o Conserta-Tudo do Primo ({PlayerPos.DistanceTo(_primo.GlobalPosition):0} m)" : "";
        return $"· Carregando {_carrying}/{MaxCarry} ({_scrap} de sucata)\n· {_bags.Count} na rua (o mais perto a {bag})\n· Caçamba mais perto: {nearestDump:0} m{primo}";
    }
}
