// Scripts/World/BusLine.cs
// Linha de ônibus circular em volta do centro: monta os pontos (abrigo + placa) e o ônibus,
// que roda sozinho parando em cada ponto.
//
// O horário é calculado pelo relógio do sistema, então online todos os jogadores veem
// o ônibus no mesmo lugar sem precisar mandar nada pela rede.
using System.Collections.Generic;
using Godot;

namespace DeadFrequency;

public partial class BusLine : Node3D
{
    [Export] public CityGenerator City { get; set; }
    /// <summary>Distância (em quarteirões) do anel do centro. 1 = rua em volta dos 8 quarteirões vizinhos do depósito.</summary>
    [Export(PropertyHint.Range, "1,3,1")] public int Ring { get; set; } = 1;
    [Export] public float Speed { get; set; } = 8.5f;
    [Export] public float StopSeconds { get; set; } = 6f;
    /// <summary>Distância do centro da rua até o meio da faixa do ônibus (pista da direita).</summary>
    [Export] public float LaneOffset { get; set; } = 2.2f;
    [Export] public string LineName { get; set; } = "966 · CIRCULAR · CENTRO";

    private readonly List<Vector3> _path = new();
    private readonly List<float> _segmentLength = new();
    private readonly List<float> _stopAt = new(); // distância no segmento onde fica o ponto
    private float _loopSeconds;
    private AnimatableBody3D _bus;
    private AudioStreamPlayer3D _engine;
    private AudioStreamPlayer3D _horn;
    private AudioStreamPlayer3D _impact;
    private bool _stopped;
    private PlayerController _player;
    private double _hornCooldown;
    private double _hitCooldown;

    public override void _Ready()
    {
        if (City == null)
        {
            GD.PushWarning("BusLine: defina City no Inspector.");
            return;
        }
        BuildRoute();
        foreach (int i in new[] { 0, 1, 2, 3 })
            BuildStop(i);
        BuildBus();
    }

    // ---------------- Rota ----------------

    private static Vector3 RightOf(Vector3 dir) => new(-dir.Z, 0f, dir.X);

    private void BuildRoute()
    {
        float a = City.Period * (Ring + 0.5f);
        Vector3[] corners = { new(-a, 0f, -a), new(a, 0f, -a), new(a, 0f, a), new(-a, 0f, a) };
        for (int i = 0; i < 4; i++)
        {
            Vector3 prevDir = (corners[i] - corners[(i + 3) % 4]).Normalized();
            Vector3 nextDir = (corners[(i + 1) % 4] - corners[i]).Normalized();
            _path.Add(corners[i] + RightOf(prevDir) * LaneOffset + RightOf(nextDir) * LaneOffset);
        }
        _loopSeconds = 0f;
        for (int i = 0; i < 4; i++)
        {
            float length = _path[i].DistanceTo(_path[(i + 1) % 4]);
            _segmentLength.Add(length);
            _stopAt.Add(length * 0.5f + 7f); // um pouco depois do meio (longe dos pontos de entrega)
            _loopSeconds += length / Speed + StopSeconds;
        }
    }

    /// <summary>Posição e direção do ônibus num instante da volta.</summary>
    private (Vector3 position, Vector3 direction, bool stopped) Sample(double time)
    {
        double t = time % _loopSeconds;
        if (t < 0)
            t += _loopSeconds;
        for (int i = 0; i < 4; i++)
        {
            Vector3 from = _path[i];
            Vector3 dir = (_path[(i + 1) % 4] - from).Normalized();
            float length = _segmentLength[i];
            float toStop = _stopAt[i] / Speed;
            float segmentTime = length / Speed + StopSeconds;
            if (t < segmentTime)
            {
                float distance;
                bool stopped = false;
                if (t < toStop)
                    distance = (float)t * Speed;
                else if (t < toStop + StopSeconds)
                {
                    distance = _stopAt[i];
                    stopped = true;
                }
                else
                    distance = (float)(t - StopSeconds) * Speed;
                return (from + dir * distance, dir, stopped);
            }
            t -= segmentTime;
        }
        return (_path[0], Vector3.Right, false);
    }

    // ---------------- Pontos ----------------

    private void BuildStop(int segment)
    {
        Vector3 dir = (_path[(segment + 1) % 4] - _path[segment]).Normalized();
        Vector3 right = RightOf(dir);
        float sidewalkOffset = City.RoadWidth * 0.5f - LaneOffset + 1.6f;
        Vector3 pos = _path[segment] + dir * _stopAt[segment] + right * sidewalkOffset + new Vector3(0f, City.CurbHeight, 0f);

        var stop = new Node3D { Name = $"BusStop{segment}", Position = pos };
        // Só o ônibus da linha para aqui: nada de carro estacionado na frente do ponto.
        City.ForbidParking(pos, dir, 11f, null, -9f); // placa no começo do trecho, não na frente do abrigo
        City.RegisterObstacle(pos + right * 0.6f + dir * 1.6f);
        City.RegisterObstacle(pos + right * 0.6f - dir * 1.6f);
        City.RegisterObstacle(pos + right * 0.6f);
        // Frente do abrigo (-Z local) virada para a rua.
        stop.Basis = Basis.LookingAt(-right, Vector3.Up);
        AddChild(stop);

        var metal = new StandardMaterial3D { AlbedoColor = new Color(0.25f, 0.27f, 0.3f), Metallic = 0.5f, Roughness = 0.4f };
        var glass = new StandardMaterial3D { AlbedoColor = new Color(0.5f, 0.65f, 0.7f, 0.35f), Transparency = BaseMaterial3D.TransparencyEnum.Alpha, Roughness = 0.1f };
        var roof = new StandardMaterial3D { AlbedoColor = new Color(0.15f, 0.35f, 0.6f) };
        var wood = new StandardMaterial3D { AlbedoColor = new Color(0.45f, 0.3f, 0.18f) };

        var body = new StaticBody3D { CollisionLayer = 1, CollisionMask = 0 };
        body.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(3.2f, 2.5f, 0.1f) }, Position = new Vector3(0f, 1.25f, 0.6f) });
        stop.AddChild(body);

        CarModels.Box(stop, new Vector3(0f, 2.55f, 0f), new Vector3(3.6f, 0.12f, 1.6f), roof);
        CarModels.Box(stop, new Vector3(0f, 1.3f, 0.6f), new Vector3(3.2f, 2.2f, 0.05f), glass);
        foreach (float x in new[] { -1.65f, 1.65f })
        {
            CarModels.Box(stop, new Vector3(x, 1.27f, 0.6f), new Vector3(0.08f, 2.55f, 0.08f), metal);
            CarModels.Box(stop, new Vector3(x, 1.27f, -0.6f), new Vector3(0.08f, 2.55f, 0.08f), metal);
        }
        CarModels.Box(stop, new Vector3(0f, 0.48f, 0.35f), new Vector3(2.6f, 0.08f, 0.4f), wood);
        CarModels.Box(stop, new Vector3(0f, 0.24f, 0.35f), new Vector3(2.4f, 0.48f, 0.06f), metal);

        // Cartaz do recadin.com.br no vidro do abrigo.
        CityGenerator.Poster(stop, new Vector3(1.0f, 1.35f, 0.565f), Mathf.Pi);
        // Placa do ponto (v0.9.8): a placa azul de "ÔNIBUS" de verdade, de frente para o ônibus que chega.
        Vector3 localFacing = stop.Basis.Transposed() * -dir;
        CityGenerator.SignOnPole(stop, new Vector3(2.5f, 0f, -1.15f), localFacing, 2.3f, new Vector2(0.4f, 0.6f), "onibus.png", null, metal);
        stop.AddToGroup(WorldChunkManager.TrackedObjectGroup);
        stop.AddChild(new Label3D
        {
            Text = LineName,
            FontSize = 32,
            PixelSize = 0.004f,
            Modulate = new Color(1f, 0.9f, 0.4f),
            OutlineSize = 6,
            Position = new Vector3(0f, 2.75f, -0.82f),
            RotationDegrees = new Vector3(0f, 180f, 0f),
        });
    }

    // ---------------- Ônibus ----------------

    private void BuildBus()
    {
        _bus = new AnimatableBody3D { Name = "Bus", SyncToPhysics = true, CollisionLayer = 1, CollisionMask = 0 };
        _bus.AddToGroup(PedestrianSignal.TrafficGroup); // semáforos de pedestre ficam vermelhos quando ele passa
        _bus.AddToGroup(WorldChunkManager.TrackedObjectGroup);
        _bus.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(2.5f, 2.9f, 11f) }, Position = new Vector3(0f, 1.75f, 0f) });
        AddChild(_bus);
        _bus.AddToGroup(MinimapView.MarkerGroup);
        _bus.SetMeta("map_color", new Color(0.35f, 0.65f, 1f));
        _bus.SetMeta("map_kind", "bus");

        var paint = CarModels.PaintFor(new Color(0.85f, 0.82f, 0.7f));
        var stripe = CarModels.PaintFor(new Color(0.15f, 0.4f, 0.75f));
        var glass = new StandardMaterial3D { AlbedoColor = new Color(0.07f, 0.1f, 0.12f), Roughness = 0.1f };
        var dark = new StandardMaterial3D { AlbedoColor = new Color(0.05f, 0.05f, 0.05f), Roughness = 1f };
        var headlight = new StandardMaterial3D { AlbedoColor = Colors.White, EmissionEnabled = true, Emission = new Color(1f, 0.95f, 0.8f), EmissionEnergyMultiplier = 2f };
        var interior = new StandardMaterial3D { AlbedoColor = new Color(1f, 0.95f, 0.75f), EmissionEnabled = true, Emission = new Color(1f, 0.9f, 0.6f), EmissionEnergyMultiplier = 0.6f };

        CarModels.Box(_bus, new Vector3(0f, 1.8f, 0f), new Vector3(2.5f, 2.7f, 11f), paint);
        CarModels.Box(_bus, new Vector3(0f, 1.0f, 0f), new Vector3(2.52f, 0.35f, 11.02f), stripe);
        CarModels.Box(_bus, new Vector3(0f, 2.25f, 0.3f), new Vector3(2.54f, 0.85f, 9.4f), interior); // janelas com luz interna
        CarModels.Box(_bus, new Vector3(0f, 2.25f, 0.3f), new Vector3(2.56f, 0.8f, 9.3f), glass);
        CarModels.Box(_bus, new Vector3(0f, 2.0f, -5.51f), new Vector3(2.3f, 1.5f, 0.04f), glass); // para-brisa
        CarModels.Box(_bus, new Vector3(0f, 3.05f, -5.52f), new Vector3(2.0f, 0.3f, 0.04f), dark);
        CarModels.Box(_bus, new Vector3(1.27f, 1.5f, -4.3f), new Vector3(0.03f, 2.0f, 1.0f), dark); // porta
        foreach (float x in new[] { -0.85f, 0.85f })
            CarModels.Box(_bus, new Vector3(x, 0.85f, -5.52f), new Vector3(0.35f, 0.2f, 0.05f), headlight);
        BuildLivery();

        var wheelMesh = new CylinderMesh { TopRadius = 0.5f, BottomRadius = 0.5f, Height = 0.35f, RadialSegments = 12, Rings = 1 };
        foreach (float x in new[] { -1.15f, 1.15f })
            foreach (float z in new[] { -3.6f, 3.4f })
                _bus.AddChild(new MeshInstance3D { Mesh = wheelMesh, Position = new Vector3(x, 0.5f, z), Rotation = new Vector3(0f, 0f, Mathf.Pi * 0.5f), MaterialOverride = dark });

        foreach (float x in new[] { -0.85f, 0.85f })
        {
            _bus.AddChild(new SpotLight3D
            {
                Position = new Vector3(x, 0.9f, -5.6f),
                RotationDegrees = new Vector3(-6f, 0f, 0f),
                LightEnergy = 3f,
                SpotRange = 22f,
                SpotAngle = 30f,
                LightColor = new Color(1f, 0.95f, 0.8f),
            });
        }

        _horn = new AudioStreamPlayer3D { Bus = AudioManager.SfxBus, UnitSize = 16f, VolumeDb = 2f, Position = new Vector3(0f, 1.5f, -5f) };
        if (ResourceLoader.Exists("res://Audio/SFX/bus_horn.wav"))
            _horn.Stream = GD.Load<AudioStream>("res://Audio/SFX/bus_horn.wav");
        _bus.AddChild(_horn);
        _impact = new AudioStreamPlayer3D { Bus = AudioManager.SfxBus, UnitSize = 12f, Position = new Vector3(0f, 1f, -5f) };
        if (ResourceLoader.Exists("res://Audio/SFX/crash_heavy.wav"))
            _impact.Stream = GD.Load<AudioStream>("res://Audio/SFX/crash_heavy.wav");
        _bus.AddChild(_impact);

        _engine = new AudioStreamPlayer3D { Bus = AudioManager.SfxBus, UnitSize = 9f, VolumeDb = -6f, Position = new Vector3(0f, 1f, 4f) };
        if (ResourceLoader.Exists("res://Audio/SFX/engine_loop.wav"))
        {
            _engine.Stream = AudioManager.MakeLooping(GD.Load<AudioStream>("res://Audio/SFX/engine_loop.wav"));
            _bus.AddChild(_engine);
            _engine.Play();
        }
        else
        {
            _bus.AddChild(_engine);
        }

        PlaceBus(Time.GetUnixTimeFromSystem(), true);
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_bus == null)
            return;
        PlaceBus(Time.GetUnixTimeFromSystem(), false);
        CheckPlayerAhead(delta);
    }

    // ---------------- Pintura, letreiro e anúncios ----------------

    private void BuildLivery()
    {
        // Faixa da empresa nas laterais, letreiro de LED na frente e anúncios (mudam a cada partida).
        const string stripe = "res://Assets/Textures/Vehicles/bus_stripe.png";
        const string led = "res://Assets/Textures/Vehicles/bus_led.png";
        System.Collections.Generic.List<string> ads = CityGenerator.RunAds();
        string sideAd = CityGenerator.AdTexturePath(ads[8 % ads.Count]);
        string rearAd = CityGenerator.AdTexturePath(ads[9 % ads.Count]);
        foreach (float side in new[] { -1f, 1f })
        {
            float rot = side > 0 ? Mathf.Pi * 0.5f : -Mathf.Pi * 0.5f;
            Quad(stripe, new Vector3(side * 1.258f, 1.45f, -1.8f), new Vector2(7f, 0.44f), rot, 0.2f);
            Quad(sideAd, new Vector3(side * 1.258f, 1.5f, 3.6f), new Vector2(2.0f, 0.66f), rot, 0.25f);
        }
        Quad(rearAd, new Vector3(0f, 2.15f, 5.505f), new Vector2(2.2f, 0.73f), 0f, 0.25f);
        Quad(led, new Vector3(0f, 3.05f, -5.545f), new Vector2(1.95f, 0.25f), Mathf.Pi, 2.2f);
    }

    private void Quad(string path, Vector3 position, Vector2 size, float rotY, float glow)
    {
        if (!ResourceLoader.Exists(path))
            return;
        var tex = GD.Load<Texture2D>(path);
        var mat = new StandardMaterial3D { AlbedoTexture = tex, Roughness = 0.4f, TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmapsAnisotropic };
        if (glow > 0f)
        {
            mat.EmissionEnabled = true;
            mat.EmissionTexture = tex;
            mat.EmissionEnergyMultiplier = glow;
        }
        _bus.AddChild(new MeshInstance3D
        {
            Mesh = new QuadMesh { Size = size },
            Position = position,
            Rotation = new Vector3(0f, rotY, 0f),
            MaterialOverride = mat,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        });
    }

    // ---------------- Buzina e atropelamento ----------------

    /// <summary>
    /// O ônibus segue o horário (todos os jogadores o veem no mesmo lugar), então não freia:
    /// se você estiver na faixa à frente, ele buzina duas vezes; se não sair, é atropelado.
    /// </summary>
    private void CheckPlayerAhead(double delta)
    {
        _hornCooldown -= delta;
        _hitCooldown -= delta;
        if (_player == null || !IsInstanceValid(_player))
        {
            _player = GetTree().CurrentScene?.GetNodeOrNull<PlayerController>("Player");
            if (_player == null)
                return;
        }
        if (_player.State != PlayerState.Walking || _stopped)
            return;

        Transform3D bus = _bus.GlobalTransform;
        Vector3 rel = bus.AffineInverse() * _player.GlobalPosition;
        if (rel.Y < -1f || rel.Y > 3.5f)
            return;

        // Frente do ônibus = -Z (z = -5,5).
        bool inLane = Mathf.Abs(rel.X) < 2.0f;
        if (inLane && rel.Z < -5.5f && rel.Z > -26f && _hornCooldown <= 0)
        {
            _hornCooldown = 5.0;
            Honk();
            GetTree().CreateTimer(0.6, false, true).Timeout += Honk;
        }

        bool hit = Mathf.Abs(rel.X) < 1.5f && rel.Z > -5.9f && rel.Z < 5.6f;
        if (hit && _hitCooldown <= 0)
        {
            _hitCooldown = 2.5;
            Vector3 forward = -bus.Basis.Z.Normalized();
            Vector3 right = bus.Basis.X.Normalized();
            Vector3 impulse = forward * 12f + right * (rel.X >= 0 ? 4f : -4f) + Vector3.Up * 6f;
            _player.Knockback(impulse);
            _impact?.Play();
            GameHud.Toast(this, "ATROPELADO pelo 966! A Viação Frequência buzinou duas vezes...", UiStyle.Danger, 4.0);
        }
    }

    private void Honk()
    {
        if (_horn?.Stream == null || !IsInstanceValid(_horn))
            return;
        _horn.PitchScale = 1f;
        _horn.Play();
    }

    private void PlaceBus(double time, bool snap)
    {
        (Vector3 position, _, bool stopped) = Sample(time);
        // Olha para onde vai estar daqui a pouco: faz a curva nas esquinas.
        (Vector3 ahead, _, _) = Sample(time + 0.9);
        Vector3 look = ahead - position;
        look.Y = 0f;
        Transform3D current = _bus.GlobalTransform;
        Basis target = look.LengthSquared() > 0.01f ? Basis.LookingAt(look.Normalized(), Vector3.Up) : current.Basis;
        Basis basis = snap ? target : current.Basis.Orthonormalized().Slerp(target, 0.15f);
        _bus.GlobalTransform = new Transform3D(basis, position);

        if (stopped != _stopped)
            _stopped = stopped;
        if (_engine?.Stream != null)
            _engine.PitchScale = Mathf.Lerp(_engine.PitchScale, stopped ? 0.42f : 0.75f, 0.05f);
    }
}
