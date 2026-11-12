// Scripts/Jobs/RepositorJob.cs
// Repositor: no Supermercado Atacadinho, pegue uma caixa de produtos no estoque (fundo da loja)
// e reponha uma prateleira marcada como VAZIA. R$ 30 por prateleira.
// v0.9.9: às vezes a caixa é um fardo de palha de aço Brilhaço, da Lã de Aço 1001 Utilidades (carga leve):
// a prateleira fica com os pacotinhos amarelos e vermelhos.
using System.Collections.Generic;
using Godot;

namespace DeadFrequency;

public partial class RepositorJob : JobBase
{
    private static readonly Color Blue = new(0.4f, 0.75f, 1f);
    private const int EmptyAtOnce = 4;

    private bool _carrying;
    private bool _lightLoad;
    private static readonly Color SteelWoolYellow = new(0.96f, 0.78f, 0.12f);
    private readonly List<int> _empty = new();
    private readonly Dictionary<int, Node3D> _emptyNodes = new();
    private readonly Dictionary<int, Node3D> _restocked = new();
    private Node3D _stock;
    private Node3D _boxInHand;
    private double _refill;
    private bool _hasMarketPin;

    public override void _Ready()
    {
        if (City.MarketShelfSlots.Count == 0)
        {
            GameHud.Toast(this, "Esta fase não tem supermercado.", UiStyle.Warning);
            return;
        }
        _hasMarketPin = City.PinChunkForPosition(City.MarketEntrance);
        // Estoque: palete com caixas no fundo da loja.
        _stock = JobMarkers.Create(this, City.MarketStockPoint, Blue, "ESTOQUE", 0f, 0.8f);
        var cardboard = new StandardMaterial3D { AlbedoColor = new Color(0.62f, 0.45f, 0.26f) };
        for (int i = 0; i < 6; i++)
            _stock.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(0.55f, 0.4f, 0.45f) }, MaterialOverride = cardboard, Position = new Vector3((i % 3 - 1) * 0.58f, 0.32f + i / 3 * 0.42f, 0f) });
        ActionArea.Create(_stock, new Vector3(0f, 0.6f, 0f), new Vector3(1.9f, 1.2f, 1f),
            _ => _carrying ? "Você já está com uma caixa" : "[E] Pegar caixa de produtos",
            _ => TakeBox(),
            p => !_carrying && !p.IsHolding);
        EmptySomeShelves();
    }

    public override void _ExitTree()
    {
        if (_hasMarketPin)
            City?.UnpinChunkForPosition(City.MarketEntrance);
        _hasMarketPin = false;
        foreach (Node3D n in _emptyNodes.Values)
            if (IsInstanceValid(n))
                n.QueueFree();
        foreach (Node3D n in _restocked.Values)
            if (IsInstanceValid(n))
                n.QueueFree();
        if (_boxInHand != null && IsInstanceValid(_boxInHand))
            _boxInHand.QueueFree();
    }

    private void TakeBox()
    {
        _carrying = true;
        _lightLoad = Rng.Randf() < 0.35f;
        // Caixa visível na frente da câmera.
        Camera3D cam = Player?.Camera;
        if (cam != null)
        {
            _boxInHand = new MeshInstance3D
            {
                Mesh = new BoxMesh { Size = new Vector3(0.45f, 0.3f, 0.35f) },
                MaterialOverride = new StandardMaterial3D { AlbedoColor = _lightLoad ? SteelWoolYellow : new Color(0.62f, 0.45f, 0.26f) },
                Position = new Vector3(0.25f, -0.35f, -0.6f),
            };
            cam.AddChild(_boxInHand);
        }
        GameHud.Toast(this, _lightLoad ? "Fardo de palha de aço Brilhaço (Lã de Aço 1001): leve como pluma. Leve a uma prateleira VAZIA."
            : "Leve a caixa até uma prateleira VAZIA (azul)", _lightLoad ? SteelWoolYellow : Blue, 3.0);
    }

    private void EmptySomeShelves()
    {
        var slots = City.MarketShelfSlots;
        int guard = 0;
        while (_empty.Count < EmptyAtOnce && guard++ < 50)
        {
            int i = Rng.RandiRange(0, slots.Count - 1);
            if (_empty.Contains(i))
                continue;
            if (_restocked.TryGetValue(i, out Node3D old))
            {
                if (IsInstanceValid(old))
                    old.QueueFree();
                _restocked.Remove(i);
            }
            _empty.Add(i);
            (Vector3 pos, float rot) = slots[i];
            Node3D marker = JobMarkers.Create(this, pos, Blue, "", 0f, 0.35f);
            marker.Rotation = new Vector3(0f, rot, 0f);
            marker.AddChild(new MeshInstance3D
            {
                Mesh = new BoxMesh { Size = new Vector3(1.6f, 0.36f, 0.42f) },
                MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.3f, 0.6f, 1f, 0.35f), Transparency = BaseMaterial3D.TransparencyEnum.Alpha, EmissionEnabled = true, Emission = new Color(0.3f, 0.6f, 1f), EmissionEnergyMultiplier = 0.8f },
                Position = new Vector3(0f, 0.2f, 0f),
            });
            int captured = i;
            ActionArea.Create(marker, new Vector3(0f, 0.2f, 0f), new Vector3(1.6f, 0.5f, 0.6f),
                _ => _carrying ? "[E] Repor a prateleira" : "Prateleira vazia (pegue uma caixa no estoque)",
                _ => Restock(captured),
                _ => _carrying);
            _emptyNodes[i] = marker;
        }
    }

    private void Restock(int slot)
    {
        if (!_carrying || !_empty.Contains(slot))
            return;
        _carrying = false;
        if (_boxInHand != null && IsInstanceValid(_boxInHand))
            _boxInHand.QueueFree();
        _boxInHand = null;
        _empty.Remove(slot);
        if (_emptyNodes.TryGetValue(slot, out Node3D marker) && IsInstanceValid(marker))
            marker.QueueFree();
        _emptyNodes.Remove(slot);

        // Produtos novos na prateleira.
        (Vector3 pos, float rot) = City.MarketShelfSlots[slot];
        var row = new Node3D { Rotation = new Vector3(0f, rot, 0f) };
        AddChild(row);
        row.GlobalPosition = pos;
        bool wool = _lightLoad;
        for (int k = 0; k < 7; k++)
        {
            var mat = new StandardMaterial3D { AlbedoColor = wool ? (k % 2 == 0 ? SteelWoolYellow : new Color(0.85f, 0.15f, 0.1f)) : Color.FromHsv(Rng.Randf(), 0.7f, 0.85f) };
            float h = wool ? 0.12f : Rng.RandfRange(0.18f, 0.32f);
            row.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(0.17f, h, 0.22f) }, MaterialOverride = mat, Position = new Vector3(-0.66f + k * 0.22f, h * 0.5f + 0.02f, 0f) });
        }
        _restocked[slot] = row;
        _lightLoad = false;
        M.Earn(1, wool ? "Prateleira de palha de aço reposta" : "Prateleira reposta");
        if (_empty.Count == 0)
            _refill = 4.0;
        else if (_empty.Count < 2)
            _refill = 8.0;
    }

    public override void _Process(double delta)
    {
        if (_refill > 0)
        {
            _refill -= delta;
            if (_refill <= 0)
                EmptySomeShelves();
        }
    }

    public override bool TryGetTarget(out Vector3 position, out string label)
    {
        position = City.MarketEntrance;
        label = "ATACADINHO";
        if (City.MarketShelfSlots.Count == 0)
            return false;
        if (PlayerPos.DistanceTo(City.MarketStockPoint) > 28f)
            return true;
        if (!_carrying)
        {
            position = City.MarketStockPoint;
            label = "ESTOQUE";
            return true;
        }
        float best = float.MaxValue;
        foreach (int i in _empty)
        {
            float d = PlayerPos.DistanceSquaredTo(City.MarketShelfSlots[i].Position);
            if (d < best)
            {
                best = d;
                position = City.MarketShelfSlots[i].Position;
                label = "PRATELEIRA";
            }
        }
        return true;
    }

    public override bool TryGetSpawn(out Vector3 position, out float yaw)
    {
        // Na calçada, de frente para a porta do mercado.
        position = City.MarketEntrance + new Vector3(0f, 0.1f, 9f);
        yaw = 0f;
        return City.MarketShelfSlots.Count > 0;
    }

    public override string PanelText()
    {
        if (City.MarketShelfSlots.Count == 0)
            return "Sem supermercado nesta fase.";
        float d = PlayerPos.DistanceTo(City.MarketStockPoint);
        string task = _carrying ? "► Leve a caixa até uma prateleira VAZIA (azul)" : "· Pegue uma caixa no ESTOQUE, no fundo da loja";
        return $"{task}\n· Prateleiras vazias: {_empty.Count}\n· Atacadinho a {d:0} m";
    }
}
