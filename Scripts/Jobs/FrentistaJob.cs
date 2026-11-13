// Scripts/Jobs/FrentistaJob.cs
// Frentista: um carro vem lá do fim da avenida (escondido pela neblina), entra no posto mais perto de você
// pela calçada rebaixada e para na bomba. Mire na tampa do tanque, aperte E e fique por perto até encher (4 s).
// Depois ele faz a volta por trás das bombas, sai pela outra rampa e some na avenida. R$ 45 por tanque.
using Godot;

namespace DeadFrequency;

public partial class FrentistaJob : JobBase
{
    private static readonly Color Green = new(0.3f, 0.9f, 0.4f);
    private const float FillSeconds = 4f;

    private Node3D _car;
    private Node3D _marker;
    private Label3D _label;
    private string _brand = "";
    private Vector3 _spot;
    private bool _hasStationPin;
    private readonly System.Collections.Generic.List<Vector3> _routePins = new();
    private bool _arrived;
    private bool _filling;
    private float _fill;
    private double _next = 2.0;

    public override void _ExitTree()
    {
        ReleaseRoutePins();
        if (_hasStationPin)
            City?.UnpinChunkForPosition(_spot);
        _hasStationPin = false;
        JobMarkers.Free(ref _marker);
        if (_car != null && IsInstanceValid(_car))
            _car.QueueFree();
    }

    private static string BrandName(string id) => id switch
    {
        "petrobraz" => "Petrobraz",
        "ipiranguinha" => "Ipiranguinha",
        "shello" => "Shello",
        _ => id,
    };

    private void SpawnCar()
    {
        if (City.GasSpots.Count == 0)
            return;
        // Posto mais perto de você.
        int best = 0;
        for (int i = 1; i < City.GasSpots.Count; i++)
            if (PlayerPos.DistanceTo(City.GasSpots[i].Position) < PlayerPos.DistanceTo(City.GasSpots[best].Position))
                best = i;
        ReleaseRoutePins();
        if (_hasStationPin)
            City.UnpinChunkForPosition(_spot);
        (_spot, _brand) = City.GasSpots[best];
        _hasStationPin = City.PinChunkForPosition(_spot);
        PinApproachRoadChunks();

        _car = new AnimatableBody3D { Name = "CarroPosto", CollisionLayer = 1, CollisionMask = 0, SyncToPhysics = false };
        AddChild(_car);
        _car.AddToGroup(PedestrianSignal.TrafficGroup);
        var type = (CarType)Rng.RandiRange(0, 4);
        Vector3 size = CarModels.Build(_car, type, Color.FromHsv(Rng.Randf(), 0.55f, 0.6f));
        _car.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = size }, Position = new Vector3(0f, size.Y * 0.5f, 0f) });
        _arrived = false;
        _filling = false;
        _fill = 0f;
        StartPath(ArrivalPath(), arriving: true);

        _label = new Label3D
        {
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            FontSize = 48,
            PixelSize = 0.007f,
            Modulate = Green,
            OutlineSize = 8,
            NoDepthTest = true,
            Position = new Vector3(0f, size.Y + 0.9f, 0f),
            Text = "",
        };
        _car.AddChild(_label);

        // Tampa do tanque (lado direito, atrás).
        ActionArea.Create(_car, new Vector3(size.X * 0.5f + 0.15f, 0.8f, size.Z * 0.25f), new Vector3(0.6f, 0.8f, 1.2f),
            _ => _filling ? $"Abastecendo... {_fill / FillSeconds * 100f:0}%" : "[E] Abastecer o carro",
            _ => StartFill(),
            p => _arrived && !_filling && p.CurrentVehicle == null);
        _car.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(0.02f, 0.14f, 0.14f) }, MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.1f, 0.1f, 0.1f) }, Position = new Vector3(size.X * 0.5f + 0.01f, 0.8f, size.Z * 0.25f) });
    }

    private void OnArrived()
    {
        _arrived = true;
        if (_label != null)
            _label.Text = "ABASTECER (E)";
        JobMarkers.Free(ref _marker);
        _marker = JobMarkers.Create(this, _spot + new Vector3(3f, 0f, 0f), Green, "", 30f, 0.6f);
    }

    private void StartFill()
    {
        _filling = true;
        GameHud.Toast(this, "Abastecendo... fique perto da bomba.", Green, 2.0);
    }

    private void PinApproachRoadChunks()
    {
        // A rota do veículo usa a avenida em z = _spot.Z + 16 e pode cobrir até 170 m
        // em ambos os sentidos. Reserva cada trecho para a animação não cruzar cenário descarregado.
        for (float along = -170f; along <= 170f; along += 36f)
        {
            if (Mathf.Abs(along) < 12f)
                continue;

            Vector3 position = _spot + new Vector3(along, 0f, 16f);
            if (City.PinChunkForPosition(position))
                _routePins.Add(position);
        }
    }

    private void ReleaseRoutePins()
    {
        foreach (Vector3 position in _routePins)
            City?.UnpinChunkForPosition(position);
        _routePins.Clear();
    }

    // ---------------- trajeto do carro ----------------

    private Curve3D _path;
    private float _offset;
    private bool _pathArriving;
    private bool _moving;
    private double _leaveDelay = -1;

    /// <summary>Pontos em volta do posto (x, y, z locais ao centro do posto; y = 0 na rua, 1 na calçada).</summary>
    private Vector3 Local(float x, bool curb, float z)
    {
        Vector3 o = _spot - new Vector3(0f, 0f, 5f); // centro do posto (topo da calçada)
        return new Vector3(o.X + x, curb ? o.Y : 0.02f, o.Z + z);
    }

    // A avenida em frente ao posto fica em z = +24 (meio da rua); a faixa de quem vai para -X é z = +21.
    private Vector3[] ArrivalPath() => new[]
    {
        Local(170f, false, 21f), Local(60f, false, 21f), Local(24f, false, 21f), Local(15f, false, 19.5f),
        Local(11f, true, 15.5f), Local(5f, true, 11f), Local(0.6f, true, 8.5f), Local(0f, true, 6.5f), _spot,
    };

    // Sai para a frente, contorna a ilha da esquerda por trás, desce a outra rampa e vai embora.
    private Vector3[] ExitPath() => new[]
    {
        _spot, Local(0f, true, 1.8f), Local(-2.5f, true, -1.2f), Local(-7f, true, -1.4f), Local(-10f, true, 2.5f),
        Local(-11.5f, true, 9f), Local(-13f, true, 15.5f), Local(-17f, false, 19.8f), Local(-26f, false, 21f),
        Local(-70f, false, 21f), Local(-170f, false, 21f),
    };

    private void StartPath(Vector3[] pts, bool arriving)
    {
        _path = new Curve3D { BakeInterval = 0.25f };
        for (int i = 0; i < pts.Length; i++)
        {
            Vector3 prev = pts[Mathf.Max(0, i - 1)], next = pts[Mathf.Min(pts.Length - 1, i + 1)];
            Vector3 h = (next - prev) / 6f; // Catmull-Rom: curvas suaves passando pelos pontos
            _path.AddPoint(pts[i], -h, h);
        }
        _offset = 0f;
        _pathArriving = arriving;
        _moving = true;
        PlaceOnPath();
    }

    private void PlaceOnPath()
    {
        float len = _path.GetBakedLength();
        Vector3 pos = _path.SampleBaked(Mathf.Min(_offset, len), true);
        Vector3 ahead = _path.SampleBaked(Mathf.Min(_offset + 1.2f, len), true);
        Vector3 dir = ahead - pos;
        dir.Y = 0f;
        if (dir.LengthSquared() < 0.0004f && _offset > 0.5f)
        {
            Vector3 back = _path.SampleBaked(Mathf.Max(0f, _offset - 1.2f), true);
            dir = pos - back;
            dir.Y = 0f;
        }
        float yaw = dir.LengthSquared() > 0.0004f ? Mathf.Atan2(-dir.X, -dir.Z) : _car.Rotation.Y; // frente do carro = -Z
        _car.GlobalPosition = pos;
        _car.Rotation = new Vector3(0f, Mathf.LerpAngle(_car.Rotation.Y, yaw, _offset < 0.01f ? 1f : 0.25f), 0f);
    }

    private void MoveCar(float dt)
    {
        float len = _path.GetBakedLength();
        float speed = _pathArriving
            ? Mathf.Clamp((len - _offset) * 0.55f, 1.2f, 13f)   // freia chegando na bomba
            : Mathf.Clamp(1.5f + _offset * 0.45f, 1.5f, 13f);   // sai devagar e acelera na avenida
        _offset += speed * dt;
        if (_offset >= len)
        {
            _offset = len;
            PlaceOnPath();
            _moving = false;
            if (_pathArriving)
            {
                OnArrived();
            }
            else
            {
                _car.QueueFree();
                _car = null;
                ReleaseRoutePins();
            }
            return;
        }
        PlaceOnPath();
    }

    public override void _Process(double delta)
    {
        if (_car == null || !IsInstanceValid(_car))
        {
            _next -= delta;
            if (_next <= 0)
                SpawnCar();
            return;
        }
        if (_leaveDelay > 0)
        {
            _leaveDelay -= delta;
            if (_leaveDelay <= 0)
                StartPath(ExitPath(), arriving: false);
            return;
        }
        if (_moving)
        {
            MoveCar((float)delta);
            return;
        }
        if (!_filling)
            return;
        if (PlayerPos.DistanceTo(_car.GlobalPosition) > 5f)
        {
            _filling = false;
            if (_label != null)
                _label.Text = "VOLTE PARA A BOMBA (E)";
            return;
        }
        _fill += (float)delta;
        if (_label != null)
            _label.Text = $"ABASTECENDO {_fill / FillSeconds * 100f:0}%";
        if (_fill >= FillSeconds)
            Finish();
    }

    private void Finish()
    {
        _filling = false;
        _arrived = false;
        JobMarkers.Free(ref _marker);
        M.Earn(1, $"Tanque cheio no posto {BrandName(_brand)}");
        if (_label != null)
            _label.Text = "VALEU, CHEFE!";
        _leaveDelay = 0.8;
        _next = Rng.RandfRange(5f, 10f);
    }

    private int NearestStation()
    {
        int best = 0;
        for (int i = 1; i < City.GasSpots.Count; i++)
            if (PlayerPos.DistanceTo(City.GasSpots[i].Position) < PlayerPos.DistanceTo(City.GasSpots[best].Position))
                best = i;
        return best;
    }

    public override bool TryGetTarget(out Vector3 position, out string label)
    {
        position = Vector3.Zero;
        label = "POSTO";
        if (City.GasSpots.Count == 0)
            return false;
        if (_car != null && IsInstanceValid(_car) && (_pathArriving || _arrived || _filling))
        {
            position = _car.GlobalPosition;
            label = "CARRO";
            return true;
        }
        position = City.GasSpots[NearestStation()].Position;
        return true;
    }

    public override bool TryGetSpawn(out Vector3 position, out float yaw)
    {
        position = Vector3.Zero;
        yaw = 0f;
        if (City.GasSpots.Count == 0)
            return false;
        // Na ilha das bombas do primeiro posto (Petrobraz), virado para a avenida.
        position = City.GasSpots[0].Position + new Vector3(2.2f, 0.1f, 1.5f);
        yaw = Mathf.Pi;
        return true;
    }

    public override string PanelText()
    {
        if (City.GasSpots.Count == 0)
            return "Sem postos nesta fase.";
        if (_car == null || !IsInstanceValid(_car) || (!_pathArriving && !_arrived && !_filling))
            return $"· Esperando o próximo carro... ({Mathf.Max(0, _next):0} s)";
        float d = PlayerPos.DistanceTo(_car.GlobalPosition);
        if (!_arrived && !_filling)
            return $"· Carro chegando no posto {BrandName(_brand)} ({d:0} m)";
        return _filling ? $"► Abastecendo: {_fill / FillSeconds * 100f:0}%" : $"► Carro esperando no posto {BrandName(_brand)} ({d:0} m)\n  Mire na tampa do tanque e aperte E";
    }
}
