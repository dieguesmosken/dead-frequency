// Scripts/Vehicle/VanDashboard.cs
// Painel diegético da caminhonete (Fase 3): no lugar do HUD, o motorista lê o painel.
//  - Instrumentos atrás do volante: velocímetro, conta-giros, combustível, temperatura e luzes-espia.
//  - Console no meio: monitor CRT verde (DashMonitor) e os botões CHAVE, FAROL, LUZ, RÁDIO e TELA.
//  - Caixa de fusíveis do lado do carona: mire no fusível queimado e aperte E para trocar.
// Montado em código por cima do painel da cena. Só lê Engine/Electrical do VanController; não decide nada.
// Frente da caminhonete = +Z; o motorista (x = +0,45) olha para +Z, então a "direita" dele é o -X.
using Godot;

namespace DeadFrequency;

public partial class VanDashboard : Node3D
{
    private VanController _van;
    private EngineSystem _engine;
    private ElectricalSystem _electrical;

    private Node3D _speedNeedle, _rpmNeedle, _fuelNeedle, _tempNeedle;
    private StandardMaterial3D _lampBattery, _lampTemp, _lampReserve, _lampLights, _lampFuse;
    private readonly StandardMaterial3D[] _fuseMats = new StandardMaterial3D[4];
    private readonly System.Collections.Generic.List<Label3D> _backlit = new();
    private Label3D _spares;
    private ShaderMaterial _screen;
    private DashMonitor _monitor;
    private OmniLight3D _cabinLight, _glow;
    private double _monitorTimer;
    private float _speedShown, _fuelShown, _tempShown;
    private bool _wasPowered = true;

    private static readonly Color Ink = new(0.86f, 0.9f, 0.82f);
    private static readonly Color InkOff = new(0.3f, 0.32f, 0.3f);
    private static readonly Color Red = new(1f, 0.2f, 0.15f);
    private static readonly Color Amber = new(1f, 0.65f, 0.12f);
    private static readonly Color Green = new(0.3f, 1f, 0.45f);

    public static VanDashboard Build(VanController van)
    {
        var dash = new VanDashboard { Name = "DashPanel", _van = van, _engine = van.Engine, _electrical = van.Electrical };
        van.AddChild(dash);
        return dash;
    }

    public override void _Ready()
    {
        var plastic = new StandardMaterial3D { AlbedoColor = new Color(0.07f, 0.07f, 0.08f), Roughness = 0.85f };
        var face = new StandardMaterial3D { AlbedoColor = new Color(0.02f, 0.025f, 0.02f), Roughness = 0.9f };

        // ---------------- instrumentos atrás do volante
        // O motorista senta alto (olho a 2,15 m): os mostradores ficam por cima do volante, na base do para-brisa.
        Box(this, new Vector3(0.45f, 1.78f, 2.09f), new Vector3(0.5f, 0.46f, 0.2f), plastic);
        Box(this, new Vector3(0.45f, 2.025f, 2.1f), new Vector3(0.54f, 0.025f, 0.2f), plastic); // aba contra o sol
        var cluster = new Node3D { Name = "Cluster", Position = new Vector3(0.45f, 1.895f, 1.975f), Rotation = new Vector3(-0.38f, Mathf.Pi, 0f) };
        AddChild(cluster);
        Box(cluster, new Vector3(0f, 0f, -0.004f), new Vector3(0.48f, 0.23f, 0.006f), face);

        _speedNeedle = Gauge(cluster, new Vector3(-0.14f, 0.012f, 0f), 0.088f, 0, 120, 10, 20, "km/h");
        _rpmNeedle = Gauge(cluster, new Vector3(0.14f, 0.012f, 0f), 0.088f, 0, 6, 1, 1, "RPM x1000", redFrom: 5f);
        _fuelNeedle = SmallGauge(cluster, new Vector3(0f, 0.035f, 0f), "E", "F", "GASOLINA");
        _tempNeedle = SmallGauge(cluster, new Vector3(0f, -0.045f, 0f), "C", "H", "TEMP.");

        _lampBattery = Lamp(cluster, new Vector3(-0.2f, -0.1f, 0f), "BAT", Red);
        _lampTemp = Lamp(cluster, new Vector3(-0.1f, -0.1f, 0f), "TEMP", Red);
        _lampReserve = Lamp(cluster, new Vector3(0f, -0.1f, 0f), "RES", Amber);
        _lampFuse = Lamp(cluster, new Vector3(0.1f, -0.1f, 0f), "FUS", Amber);
        _lampLights = Lamp(cluster, new Vector3(0.2f, -0.1f, 0f), "FAROL", Green);

        // Brilho verde do painel no rosto do motorista (bem fraco, sem sombra).
        _glow = new OmniLight3D { Position = new Vector3(0.3f, 1.95f, 1.8f), LightColor = new Color(0.45f, 1f, 0.55f), LightEnergy = 0f, OmniRange = 0.9f, ShadowEnabled = false };
        AddChild(_glow);

        // ---------------- console central: monitor CRT e botões
        // Fica mais perto do motorista que os mostradores (num suporte, como taxímetro), virado para ele.
        Box(this, new Vector3(-0.12f, 1.66f, 1.98f), new Vector3(0.2f, 0.26f, 0.2f), plastic); // suporte
        var console = new Node3D { Name = "Console", Position = new Vector3(-0.1f, 1.83f, 1.86f), Rotation = new Vector3(-0.32f, Mathf.Pi - 0.52f, 0f) };
        AddChild(console);
        Box(console, new Vector3(0f, 0f, -0.03f), new Vector3(0.42f, 0.34f, 0.06f), plastic);

        _monitor = new DashMonitor { Name = "Monitor" };
        AddChild(_monitor);
        _screen = new ShaderMaterial { Shader = GD.Load<Shader>("res://Shaders/Vehicle/crt_monitor.gdshader") };
        _screen.SetShaderParameter("screen", _monitor.Viewport.GetTexture());
        _screen.SetShaderParameter("power", 0f);
        Box(console, new Vector3(0f, 0.04f, 0.0f), new Vector3(0.33f, 0.25f, 0.012f), face); // moldura
        console.AddChild(new MeshInstance3D
        {
            Mesh = new QuadMesh { Size = new Vector2(0.3f, 0.225f) },
            MaterialOverride = _screen,
            Position = new Vector3(0f, 0.04f, 0.008f),
        });

        float bx = -0.156f;
        Button(console, ref bx, "CHAVE", () => _engine.Running || _engine.Cranking, Amber,
            () => _engine.Running ? "[E] Desligar o motor" : _engine.Cranking ? "Dando a partida..." : "[E] Dar a partida",
            _ => _van.ToggleIgnition());
        Button(console, ref bx, "FAROL", () => _electrical.LightLevel > 0f, Green,
            () => !_electrical.FuseOk(Circuit.Lights) ? "Farol sem luz: fusível queimado" : _electrical.LightsSwitch ? "[E] Apagar o farol" : "[E] Acender o farol",
            _ => _van.ToggleLights());
        Button(console, ref bx, "LUZ", () => _electrical.CabinSwitch && _electrical.Powered(Circuit.Cabin), Green,
            () => !_electrical.FuseOk(Circuit.Cabin) ? "Luz interna: fusível queimado" : _electrical.CabinSwitch ? "[E] Apagar a luz da cabine" : "[E] Acender a luz da cabine",
            _ => _electrical.CabinSwitch = !_electrical.CabinSwitch);
        Button(console, ref bx, "RÁDIO", () => _van.Radio != null && _van.Radio.On, Green,
            () => !_electrical.FuseOk(Circuit.Radio) ? "Rádio mudo: fusível queimado" : _van.Radio.On ? "[E] Desligar o rádio" : "[E] Ligar o rádio (96.6)",
            _ => _van.ToggleRadio());
        Button(console, ref bx, "TELA", () => _electrical.Powered(Circuit.Panel), Green,
            () => "[E] Trocar a tela do monitor",
            _ => { _monitor.NextPage(); _monitorTimer = 0; });

        _cabinLight = new OmniLight3D { Position = new Vector3(0f, 2.3f, 1.5f), LightColor = new Color(1f, 0.9f, 0.7f), LightEnergy = 0f, OmniRange = 2.6f, ShadowEnabled = false };
        AddChild(_cabinLight);

        // ---------------- caixa de fusíveis (lado do carona)
        // Na ponta do painel, mais para cá que o monitor (senão ele fica escondido atrás do console).
        Box(this, new Vector3(-0.62f, 1.62f, 1.95f), new Vector3(0.26f, 0.2f, 0.3f), plastic);
        var fuses = new Node3D { Name = "FuseBox", Position = new Vector3(-0.6f, 1.7f, 1.8f), Rotation = new Vector3(-0.6f, Mathf.Pi - 1.1f, 0f) };
        AddChild(fuses);
        Box(fuses, new Vector3(0f, 0f, -0.004f), new Vector3(0.34f, 0.2f, 0.008f), face);
        fuses.AddChild(Text("FUSÍVEIS", new Vector3(0f, 0.08f, 0.002f), 0.001f, Ink));
        Color[] fuseColors = { new(0.95f, 0.75f, 0.1f), new(0.85f, 0.15f, 0.12f), new(0.2f, 0.45f, 0.9f), new(0.9f, 0.9f, 0.85f) };
        for (int i = 0; i < 4; i++)
        {
            var c = (Circuit)i;
            float x = -0.12f + i * 0.08f;
            _fuseMats[i] = new StandardMaterial3D { AlbedoColor = fuseColors[i], Roughness = 0.4f, EmissionEnabled = true, Emission = fuseColors[i], EmissionEnergyMultiplier = 0.25f };
            fuses.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(0.044f, 0.07f, 0.024f) }, MaterialOverride = _fuseMats[i], Position = new Vector3(x, 0.015f, 0.01f) });
            fuses.AddChild(Text(ElectricalSystem.CircuitNames[i], new Vector3(x, -0.04f, 0.002f), 0.00065f, Ink));
            ActionArea area = ActionArea.Create(fuses, new Vector3(x, 0.005f, 0.02f), new Vector3(0.076f, 0.13f, 0.06f),
                p => Seated(p) ? (_electrical.FuseOk(c) ? $"Fusível {ElectricalSystem.CircuitNames[(int)c]}: ok" : $"[E] Trocar o fusível {ElectricalSystem.CircuitNames[(int)c]}") : "",
                p => GameHud.Toast(this, _electrical.Replace(c), UiStyle.Phosphor, 3.5),
                p => Seated(p) && !_electrical.FuseOk(c));
            area.Name = "Fuse" + i;
        }
        _spares = Text("", new Vector3(0f, -0.078f, 0.002f), 0.00065f, Amber);
        fuses.AddChild(_spares);

        _electrical.FuseBlown += OnFuseBlown;
        _electrical.FuseReplaced += _ => UpdateFuses();
        UpdateFuses();
        Apply(true);
        Visible = false;
    }

    private bool Seated(PlayerController p) => p != null && p.CurrentVehicle == _van && !p.IsThirdPerson && p.State is PlayerState.Driving or PlayerState.Riding;

    private void OnFuseBlown(Circuit c)
    {
        UpdateFuses();
        if (LocalInside())
            GameHud.Toast(this, $"Pof! Queimou o fusível: {ElectricalSystem.CircuitNames[(int)c]}. A caixa fica do lado do carona.", UiStyle.Warning, 4.5);
    }

    private void UpdateFuses()
    {
        for (int i = 0; i < 4; i++)
        {
            bool ok = _electrical.FuseOk((Circuit)i);
            bool jumper = _electrical.IsJumper((Circuit)i);
            _fuseMats[i].EmissionEnergyMultiplier = ok ? 0.25f : 0f;
            _fuseMats[i].AlbedoColor = !ok ? new Color(0.05f, 0.04f, 0.04f) : jumper ? new Color(0.6f, 0.6f, 0.62f) : _fuseMats[i].Emission;
        }
        _spares.Text = $"RESERVA: {_electrical.Spares}";
    }

    /// <summary>Tem jogador local sentado aqui (motorista ou carona)? Só aí vale gastar com monitor e ponteiros.</summary>
    private bool LocalInside() =>
        PlayerController.Local is PlayerController p && IsInstanceValid(p) && p.CurrentVehicle == _van;

    public override void _Process(double delta)
    {
        // O painel só é desenhado para quem está sentado em primeira pessoa: de fora (e na câmera de trás)
        // o que aparece é a cabine do modelo, e os mostradores ficariam atravessando o para-brisa.
        bool inside = LocalInside();
        bool show = inside && !PlayerController.Local.IsThirdPerson;
        if (Visible != show)
            Visible = show;
        if (!inside)
        {
            if (_wasPowered)
                Apply(false);
            return;
        }
        Apply(false, (float)delta);
    }

    private void Apply(bool force, float dt = 0f)
    {
        bool inside = LocalInside();
        bool panel = _electrical.Powered(Circuit.Panel);
        bool lit = panel && inside;
        float noise = _electrical.Interference;
        float jitter = noise > 0.05f ? (GD.Randf() - 0.5f) * noise * 0.16f : 0f;

        // Velocímetro é de cabo: funciona mesmo sem o fusível do painel. O resto é elétrico.
        float k = dt <= 0f ? 1f : 1f - Mathf.Exp(-9f * dt);
        _speedShown = Mathf.Lerp(_speedShown, Mathf.Min(_van.SpeedKmh, 125f), k);
        _fuelShown = Mathf.Lerp(_fuelShown, panel ? _engine.Fuel : 0f, dt <= 0f ? 1f : 1f - Mathf.Exp(-1.5f * dt));
        _tempShown = Mathf.Lerp(_tempShown, panel ? Mathf.Clamp((_engine.Temperature - 40f) / 80f, 0f, 1f) : 0f, dt <= 0f ? 1f : 1f - Mathf.Exp(-2f * dt));
        SetNeedle(_speedNeedle, _speedShown / 120f + jitter * 0.3f, 120f);
        SetNeedle(_rpmNeedle, (panel ? _engine.Rpm / 6000f : 0f) + jitter, 120f);
        SetNeedle(_fuelNeedle, _fuelShown + jitter, 48f);
        SetNeedle(_tempNeedle, _tempShown + jitter, 48f);

        bool blink = Time.GetTicksMsec() % 700 < 400;
        SetLamp(_lampBattery, lit && (_electrical.BatteryLow || !_electrical.Charging && _engine.Cranking));
        SetLamp(_lampTemp, lit && (_engine.Overheated ? blink : _engine.TemperatureWarning));
        SetLamp(_lampReserve, lit && (_engine.OutOfFuel ? blink : _engine.FuelReserve));
        SetLamp(_lampFuse, lit && _electrical.AnyFuseBlown);
        SetLamp(_lampLights, lit && _electrical.LightLevel > 0f);

        if (force || lit != _wasPowered)
        {
            _wasPowered = lit;
            foreach (Label3D label in _backlit)
                label.Modulate = lit ? Ink : InkOff;
            _screen.SetShaderParameter("power", lit ? 1f : 0f);
            _glow.LightEnergy = lit ? 0.07f : 0f;
        }
        _cabinLight.LightEnergy = _electrical.CabinSwitch && _electrical.Powered(Circuit.Cabin) ? 0.9f : 0f;

        if (!lit)
            return;
        _screen.SetShaderParameter("interference", noise);
        _monitorTimer -= dt;
        if (_monitorTimer <= 0)
        {
            _monitorTimer = 0.33;
            _monitor.Refresh(_van);
        }
    }

    // ---------------- peças

    private static void SetNeedle(Node3D needle, float t, float halfSweepDeg) =>
        needle.Rotation = new Vector3(0f, 0f, Mathf.DegToRad(halfSweepDeg - 2f * halfSweepDeg * Mathf.Clamp(t, -0.02f, 1.04f)));

    private static void SetLamp(StandardMaterial3D lamp, bool on) => lamp.EmissionEnergyMultiplier = on ? 2.6f : 0f;

    private static void Box(Node3D parent, Vector3 pos, Vector3 size, Material mat) =>
        parent.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = size }, Position = pos, MaterialOverride = mat });

    private static Label3D Text(string text, Vector3 pos, float pixel, Color color) => new()
    {
        Text = text, FontSize = 32, PixelSize = pixel, Modulate = color, OutlineSize = 0, Position = pos,
        RenderPriority = 1,
    };

    private Label3D Backlit(Node3D parent, string text, Vector3 pos, float pixel)
    {
        Label3D label = Text(text, pos, pixel, Ink);
        parent.AddChild(label);
        _backlit.Add(label);
        return label;
    }

    /// <summary>Mostrador redondo de 240° (do canto de baixo à esquerda ao de baixo à direita).</summary>
    private Node3D Gauge(Node3D parent, Vector3 center, float radius, int min, int max, int tickStep, int labelStep, string unit, float redFrom = -1f)
    {
        var root = new Node3D { Position = center };
        parent.AddChild(root);
        root.AddChild(new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = radius, BottomRadius = radius, Height = 0.003f, RadialSegments = 20 },
            MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.035f, 0.04f, 0.035f), Roughness = 0.8f },
            Rotation = new Vector3(Mathf.Pi * 0.5f, 0f, 0f),
        });
        var tickMat = new StandardMaterial3D { AlbedoColor = Ink, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded };
        var redMat = new StandardMaterial3D { AlbedoColor = Red, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded };
        for (int v = min; v <= max; v += tickStep)
        {
            float t = (v - min) / (float)(max - min);
            float a = Mathf.DegToRad(120f - 240f * t);
            var dir = new Vector3(-Mathf.Sin(a), Mathf.Cos(a), 0f);
            bool major = (v - min) % labelStep == 0;
            root.AddChild(new MeshInstance3D
            {
                Mesh = new BoxMesh { Size = new Vector3(major ? 0.004f : 0.0025f, major ? 0.014f : 0.008f, 0.001f) },
                MaterialOverride = redFrom >= 0f && v >= redFrom ? redMat : tickMat,
                Position = dir * (radius - 0.01f) + new Vector3(0f, 0f, 0.003f),
                Rotation = new Vector3(0f, 0f, a),
            });
            if (major)
                Backlit(root, v.ToString(), dir * (radius - 0.03f) + new Vector3(0f, 0f, 0.003f), 0.00055f);
        }
        Backlit(root, unit, new Vector3(0f, -radius * 0.45f, 0.003f), 0.00042f);
        return Needle(root, radius - 0.016f);
    }

    /// <summary>Mostrador pequeno de 96° (combustível e temperatura).</summary>
    private Node3D SmallGauge(Node3D parent, Vector3 center, string left, string right, string title)
    {
        var root = new Node3D { Position = center + new Vector3(0f, -0.022f, 0f) };
        parent.AddChild(root);
        Box(root, new Vector3(0f, 0.022f, 0f), new Vector3(0.078f, 0.06f, 0.003f), new StandardMaterial3D { AlbedoColor = new Color(0.035f, 0.04f, 0.035f), Roughness = 0.8f });
        Backlit(root, left, new Vector3(-0.028f, 0.03f, 0.003f), 0.0005f);
        Backlit(root, right, new Vector3(0.028f, 0.03f, 0.003f), 0.0005f);
        Backlit(root, title, new Vector3(0f, 0.045f, 0.003f), 0.00036f);
        return Needle(root, 0.036f);
    }

    private static Node3D Needle(Node3D root, float length)
    {
        var pivot = new Node3D { Position = new Vector3(0f, 0f, 0.005f) };
        root.AddChild(pivot);
        var mat = new StandardMaterial3D { AlbedoColor = new Color(1f, 0.35f, 0.12f), ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded };
        pivot.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(0.0035f, length, 0.0015f) }, MaterialOverride = mat, Position = new Vector3(0f, length * 0.5f - 0.006f, 0f) });
        pivot.AddChild(new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = 0.007f, BottomRadius = 0.007f, Height = 0.003f, RadialSegments = 10 },
            MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.1f, 0.1f, 0.1f) },
            Rotation = new Vector3(Mathf.Pi * 0.5f, 0f, 0f),
        });
        return pivot;
    }

    private StandardMaterial3D Lamp(Node3D parent, Vector3 pos, string text, Color color)
    {
        var mat = new StandardMaterial3D { AlbedoColor = color * 0.25f, EmissionEnabled = true, Emission = color, EmissionEnergyMultiplier = 0f };
        parent.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(0.05f, 0.014f, 0.003f) }, MaterialOverride = mat, Position = pos });
        parent.AddChild(Text(text, pos + new Vector3(0f, 0f, 0.003f), 0.00036f, new Color(0.02f, 0.02f, 0.02f)));
        return mat;
    }

    private void Button(Node3D parent, ref float x, string caption, System.Func<bool> lit, Color lamp, System.Func<string> prompt, System.Action<PlayerController> pressed)
    {
        parent.AddChild(new DashboardButton
        {
            Name = "Btn" + caption,
            Van = _van,
            Caption = caption,
            Lit = lit,
            LampColor = lamp,
            Prompt = prompt,
            Pressed = pressed,
            Size = new Vector3(0.068f, 0.044f, 0.024f),
            Position = new Vector3(x, -0.128f, 0.012f),
        });
        x += 0.078f;
    }
}
