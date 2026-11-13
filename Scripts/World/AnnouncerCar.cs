// Scripts/World/AnnouncerCar.cs
// Carros que rodam devagar pela cidade anunciando (v0.10):
//  - Carro de som: sempre na rua, dando a volta no anel 2, com os comerciais das lojas paródia da cidade
//    (os mesmos roteiros da Rádio 96.6, docs/radio/ANUNCIOS_966.md).
//  - Carro do ovo: evento de vez em quando (alguns minutos a cada tanto), no anel 1, oferecendo ovo.
// Como o ônibus, a posição vem do relógio do sistema: online todo mundo vê o carro no mesmo lugar, sem rede.
//
// Som: ainda sem áudio. Quando os arquivos existirem, basta colocá-los em Audio/Street/<id>.ogg (o id de cada
// anúncio está em Ads/EggLines abaixo); enquanto não existem, a fala aparece num balão em cima do carro.
using System.Collections.Generic;
using Godot;

namespace DeadFrequency;

public partial class AnnouncerCar : Node3D
{
    public enum CarKind { Som, Ovo }

    [Export] public CityGenerator City { get; set; }
    [Export] public CarKind Kind { get; set; } = CarKind.Som;
    /// <summary>Anel de ruas em volta do centro (1 = em volta dos vizinhos do depósito, como o ônibus).</summary>
    [Export(PropertyHint.Range, "1,3,1")] public int Ring { get; set; } = 2;
    /// <summary>Bem devagar (m/s): 4 = ~15 km/h.</summary>
    [Export] public float Speed { get; set; } = 4f;
    /// <summary>Dá a volta no sentido contrário (usa a outra faixa da rua).</summary>
    [Export] public bool Reverse { get; set; }
    [Export] public float LaneOffset { get; set; } = 2.2f;
    /// <summary>Carro do ovo: de quanto em quanto tempo passa (s) e por quanto tempo fica na rua (s).</summary>
    [Export] public float EventEvery { get; set; } = 900f;
    [Export] public float EventDuration { get; set; } = 240f;
    /// <summary>Segundos entre uma fala e outra (balão ou áudio).</summary>
    [Export] public float LineInterval { get; set; } = 9f;

    public const string AudioFolder = "res://Audio/Street/";

    /// <summary>Comerciais do carro de som: id (= nome do áudio e do roteiro da 96.6) e o texto curto do balão.</summary>
    public static readonly (string Id, string Text)[] Ads =
    {
        ("ad_agro_tudao", "Agro-Tudão! Ração, semente e o Repelente Espanta-Muriçoca. Do pintinho ao trator, tem!"),
        ("ad_conserta_tudo", "Quebrou? Rachou? Leva no Conserta-Tudo do Primo! Ele ainda compra sua sucata!"),
        ("ad_atacadinho", "Atenção, dona de casa! Supermercado Atacadinho: arroz, feijão e guaraná trincando!"),
        ("ad_pao_de_sal", "Pão francês saindo agora na Padaria Pão de Sal! Quentinho, quentinho!"),
        ("ad_rabibs", "Esfiha aberta a cinquenta centavos, só no Rabib's!"),
        ("ad_shopping_marconi", "Shopping Marconi: dois andares, oito lojas e a escada mais charmosa da cidade!"),
        ("ad_lambari_feliz", "Pesqueiro Lambari Feliz, no rancho! Traga a vara, a minhoca e a paciência!"),
        ("ad_taxi_sintonia", "Precisa de carona? Rádio Táxi Sintonia, o amarelinho da cooperativa!"),
        ("ev_feira_praca", "Domingo tem feira na praça! Pastel, caldo de cana e o churrasquinho do Duvidoso!"),
        ("ev_show_966", "Sexta, oito da noite: Show da Frequência, ao vivo na Rádio 96.6!"),
    };

    /// <summary>Falas do carro do ovo (texto nosso; o áudio vai em Audio/Street/ovo_*.ogg).</summary>
    public static readonly (string Id, string Text)[] EggLines =
    {
        ("ovo_01", "Olha o carro do ovo passando na sua rua! Ovo fresquinho, direto da granja!"),
        ("ovo_02", "É ovo branco, é ovo vermelho! A bandeja com trinta, preço de amigo!"),
        ("ovo_03", "Dona de casa, pode chegar! O carro do ovo está parado ali na esquina!"),
        ("ovo_04", "Ovo caipira do rancho, gema amarelinha! Vai um ovinho aí, freguês?"),
    };

    private readonly List<Vector3> _path = new();
    private readonly List<float> _segmentLength = new();
    private float _loopLength;
    private AnimatableBody3D _car;
    private CollisionShape3D _shape;
    private Label3D _balloon;
    private AudioStreamPlayer3D _voice;
    private AudioStreamPlayer3D _engine;
    private int _lineIndex;
    private double _nextLine;
    private double _balloonHide;
    private bool _active = true;
    private bool _announced;

    public override void _Ready()
    {
        if (City == null)
        {
            GD.PushWarning("AnnouncerCar: defina City no Inspector.");
            return;
        }
        BuildRoute();
        BuildCar();
        // Cada carro começa a falar numa fala diferente (o horário define, igual para todos online).
        _lineIndex = (int)(Time.GetUnixTimeFromSystem() / LineInterval) % Lines.Length;
        _nextLine = 2.0;
        Place(Time.GetUnixTimeFromSystem(), true);
    }

    private (string Id, string Text)[] Lines => Kind == CarKind.Ovo ? EggLines : Ads;

    // ---------------- Rota (mesmo desenho do ônibus) ----------------

    private static Vector3 RightOf(Vector3 dir) => new(-dir.Z, 0f, dir.X);

    private void BuildRoute()
    {
        float a = City.Period * (Ring + 0.5f);
        var corners = new List<Vector3> { new(-a, 0f, -a), new(a, 0f, -a), new(a, 0f, a), new(-a, 0f, a) };
        if (Reverse)
            corners.Reverse();
        for (int i = 0; i < 4; i++)
        {
            Vector3 prevDir = (corners[i] - corners[(i + 3) % 4]).Normalized();
            Vector3 nextDir = (corners[(i + 1) % 4] - corners[i]).Normalized();
            _path.Add(corners[i] + RightOf(prevDir) * LaneOffset + RightOf(nextDir) * LaneOffset);
        }
        for (int i = 0; i < 4; i++)
        {
            float length = _path[i].DistanceTo(_path[(i + 1) % 4]);
            _segmentLength.Add(length);
            _loopLength += length;
        }
    }

    private Vector3 Sample(double time)
    {
        double d = time * Speed % _loopLength;
        if (d < 0)
            d += _loopLength;
        for (int i = 0; i < 4; i++)
        {
            if (d < _segmentLength[i])
                return _path[i] + (_path[(i + 1) % 4] - _path[i]).Normalized() * (float)d;
            d -= _segmentLength[i];
        }
        return _path[0];
    }

    // ---------------- Carro ----------------

    private void BuildCar()
    {
        _car = new AnimatableBody3D { Name = Kind == CarKind.Ovo ? "CarroDoOvo" : "CarroDeSom", CollisionLayer = 1, CollisionMask = 0, SyncToPhysics = false };
        AddChild(_car);
        bool ovo = Kind == CarKind.Ovo;
        // CarModels monta com a frente em -Z, como o ônibus (Basis.LookingAt olha para -Z).
        Vector3 size = CarModels.Build(_car, ovo ? CarType.Pickup : CarType.Kombi,
            ovo ? new Color(0.92f, 0.9f, 0.84f) : new Color(0.95f, 0.78f, 0.12f), ovo ? "OVO-0030" : "SOM-0966");
        _shape = new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(size.X, size.Y - 0.2f, size.Z) }, Position = new Vector3(0f, size.Y * 0.5f + 0.1f, 0f) };
        _car.AddChild(_shape);
        if (ovo)
            BuildEggLoad(size);
        else
            BuildSpeakers(size);

        _balloon = new Label3D
        {
            Text = "",
            FontSize = 40,
            PixelSize = 0.008f,
            Width = 520f,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            Modulate = ovo ? new Color(1f, 0.95f, 0.7f) : new Color(1f, 0.92f, 0.35f),
            OutlineSize = 10,
            OutlineModulate = new Color(0f, 0f, 0f, 0.9f),
            Position = new Vector3(0f, size.Y + 1.6f, 0f),
            Visible = false,
            NoDepthTest = false,
        };
        _car.AddChild(_balloon);

        _voice = new AudioStreamPlayer3D { Bus = AudioManager.SfxBus, UnitSize = 14f, MaxDistance = 90f, VolumeDb = 2f, Position = new Vector3(0f, size.Y + 0.3f, 0f) };
        _car.AddChild(_voice);
        _engine = new AudioStreamPlayer3D { Bus = AudioManager.SfxBus, UnitSize = 6f, VolumeDb = -10f, PitchScale = 0.55f, Position = new Vector3(0f, 0.8f, -1.5f) };
        if (ResourceLoader.Exists("res://Audio/SFX/engine_loop.wav"))
            _engine.Stream = AudioManager.MakeLooping(GD.Load<AudioStream>("res://Audio/SFX/engine_loop.wav"));
        _car.AddChild(_engine);
        if (_engine.Stream != null)
            _engine.Play();
    }

    private static StandardMaterial3D Mat(Color c, float rough = 0.7f) => new() { AlbedoColor = c, Roughness = rough };

    private MeshInstance3D Box(Vector3 pos, Vector3 size, Material m, Vector3? rot = null)
    {
        var mi = new MeshInstance3D { Mesh = new BoxMesh { Size = size }, Position = pos, MaterialOverride = m, Rotation = rot ?? Vector3.Zero };
        _car.AddChild(mi);
        return mi;
    }

    /// <summary>Cornetas no bagageiro do teto e faixa com o nome nas laterais.</summary>
    private void BuildSpeakers(Vector3 size)
    {
        var grey = Mat(new Color(0.75f, 0.76f, 0.78f), 0.4f);
        var dark = Mat(new Color(0.08f, 0.08f, 0.09f));
        float roof = size.Y + 0.02f;
        Box(new Vector3(0f, roof + 0.03f, 0.1f), new Vector3(size.X - 0.3f, 0.05f, 1.6f), dark); // bagageiro
        foreach (float x in new[] { -0.32f, 0.32f })
            foreach (float dirZ in new[] { -1f, 1f })
            {
                // Corneta: cone apontando para a frente e para trás.
                var horn = new MeshInstance3D
                {
                    Mesh = new CylinderMesh { TopRadius = 0.06f, BottomRadius = 0.22f, Height = 0.5f, RadialSegments = 10 },
                    Position = new Vector3(x, roof + 0.3f, 0.1f + dirZ * 0.35f),
                    Rotation = new Vector3(dirZ * Mathf.Pi * 0.5f, 0f, 0f),
                    MaterialOverride = grey,
                };
                _car.AddChild(horn);
                Box(new Vector3(x, roof + 0.3f, 0.1f + dirZ * 0.05f), new Vector3(0.14f, 0.14f, 0.2f), dark);
            }
        Box(new Vector3(0f, roof + 0.15f, 0.1f), new Vector3(0.06f, 0.25f, 0.06f), dark);
        foreach (float side in new[] { -1f, 1f })
        {
            _car.AddChild(new Label3D
            {
                Text = "CARRO DE SOM · ANUNCIE AQUI · 96.6",
                FontSize = 48,
                PixelSize = 0.004f,
                Modulate = new Color(0.1f, 0.12f, 0.35f),
                OutlineSize = 0,
                Position = new Vector3(side * (size.X * 0.5f + 0.01f), 1.05f, 0f),
                Rotation = new Vector3(0f, side * Mathf.Pi * 0.5f, 0f),
            });
        }
    }

    /// <summary>Bandejas de ovos empilhadas na caçamba, placa "OVO" e uma cornetinha.</summary>
    private void BuildEggLoad(Vector3 size)
    {
        var tray = Mat(new Color(0.78f, 0.7f, 0.56f), 0.95f);
        var egg = Mat(new Color(0.97f, 0.94f, 0.86f), 0.6f);
        var eggRed = Mat(new Color(0.82f, 0.55f, 0.38f), 0.6f);
        var red = Mat(new Color(0.8f, 0.1f, 0.08f), 0.5f);
        var white = Mat(new Color(0.96f, 0.96f, 0.94f));
        var dark = Mat(new Color(0.08f, 0.08f, 0.09f));
        // Caçamba: atrás da cabine (+Z no modelo, que tem a frente em -Z).
        float bedZ = size.Z * 0.25f, bedY = 0.85f;
        for (int stack = 0; stack < 4; stack++)
        {
            float x = (stack % 2 == 0 ? -0.36f : 0.36f), z = bedZ + (stack < 2 ? -0.4f : 0.4f);
            int layers = 5 + stack % 2;
            for (int l = 0; l < layers; l++)
                Box(new Vector3(x, bedY + l * 0.08f, z), new Vector3(0.62f, 0.07f, 0.62f), tray);
            // Ovos aparecendo na bandeja de cima.
            float top = bedY + layers * 0.08f;
            for (int i = 0; i < 3; i++)
                for (int k = 0; k < 3; k++)
                {
                    var e = new MeshInstance3D
                    {
                        Mesh = new SphereMesh { Radius = 0.045f, Height = 0.12f, RadialSegments = 6, Rings = 3 },
                        Position = new Vector3(x - 0.18f + i * 0.18f, top + 0.02f, z - 0.18f + k * 0.18f),
                        MaterialOverride = (i + k + stack) % 3 == 0 ? eggRed : egg,
                    };
                    _car.AddChild(e);
                }
        }
        // Placa vermelha em cima da cabine.
        float roof = size.Y;
        Box(new Vector3(0f, roof + 0.35f, -0.2f), new Vector3(1.3f, 0.5f, 0.06f), red);
        foreach (float side in new[] { -1f, 1f })
        {
            _car.AddChild(new Label3D
            {
                Text = "OVO!\n30 por R$ 5",
                FontSize = 64,
                PixelSize = 0.005f,
                Modulate = Colors.White,
                OutlineSize = 0,
                Position = new Vector3(0f, roof + 0.35f, -0.2f + side * 0.035f),
                Rotation = new Vector3(0f, side > 0 ? 0f : Mathf.Pi, 0f),
            });
        }
        Box(new Vector3(0f, roof + 0.07f, -0.2f), new Vector3(0.06f, 0.14f, 0.06f), dark);
        var horn = new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = 0.05f, BottomRadius = 0.18f, Height = 0.4f, RadialSegments = 10 },
            Position = new Vector3(0.5f, roof + 0.7f, -0.2f),
            Rotation = new Vector3(-Mathf.Pi * 0.5f, 0f, 0f),
            MaterialOverride = white,
        };
        _car.AddChild(horn);
    }

    // ---------------- Andar e falar ----------------

    /// <summary>O carro do ovo só está na rua durante a janela do evento (pelo relógio, igual para todos).</summary>
    private bool IsOnStreet(double time) => Kind != CarKind.Ovo || time % EventEvery < EventDuration;

    public override void _PhysicsProcess(double delta)
    {
        if (_car == null)
            return;
        double now = Time.GetUnixTimeFromSystem();
        bool active = IsOnStreet(now);
        if (active != _active)
            SetActive(active);
        if (!active)
            return;
        Place(now, false);
    }

    private void SetActive(bool active)
    {
        _active = active;
        _car.Visible = active;
        _shape.SetDeferred(CollisionShape3D.PropertyName.Disabled, !active);
        if (_engine?.Stream != null)
        {
            if (active) _engine.Play();
            else _engine.Stop();
        }
        if (!active)
        {
            _voice.Stop();
            _balloon.Visible = false;
            _announced = false;
        }
        else if (Kind == CarKind.Ovo && !_announced)
        {
            _announced = true;
            GameHud.Toast(this, "Olha o carro do ovo passando no bairro! (anel do centro)", new Color(1f, 0.9f, 0.55f), 5.0);
        }
    }

    private void Place(double time, bool snap)
    {
        Vector3 position = Sample(time);
        Vector3 look = Sample(time + 1.5) - position;
        look.Y = 0f;
        Transform3D current = _car.GlobalTransform;
        Basis target = look.LengthSquared() > 0.01f ? Basis.LookingAt(look.Normalized(), Vector3.Up) : current.Basis;
        Basis basis = snap ? target : current.Basis.Orthonormalized().Slerp(target, 0.08f);
        _car.GlobalTransform = new Transform3D(basis, City.GlobalTransform * position);
    }

    public override void _Process(double delta)
    {
        if (_car == null)
        {
            SetProcess(false);
            return;
        }
        if (_active)
            if (!_car.Visible)
                _car.Visible = true;
        if (!_active)
        {
            if (_car.Visible)
                _car.Visible = false;
            return;
        }
        _balloonHide -= delta;
        if (_balloon.Visible && _balloonHide <= 0)
            _balloon.Visible = false;
        _nextLine -= delta;
        if (_nextLine > 0)
            return;
        (string id, string text) = Lines[_lineIndex % Lines.Length];
        _lineIndex++;
        string audio = AudioFolder + id + ".ogg";
        if (ResourceLoader.Exists(audio))
        {
            _voice.Stream = GD.Load<AudioStream>(audio);
            _voice.Play();
            double length = _voice.Stream.GetLength();
            _nextLine = Mathf.Max(LineInterval, length + 3.0);
            ShowBalloon(text, length + 0.5);
        }
        else
        {
            // Sem áudio ainda: só o balão (a fala aparece por escrito em cima do carro).
            _nextLine = LineInterval;
            ShowBalloon(text, LineInterval * 0.7);
        }
    }

    private void ShowBalloon(string text, double seconds)
    {
        _balloon.Text = text;
        _balloon.Visible = true;
        _balloonHide = seconds;
    }
}
