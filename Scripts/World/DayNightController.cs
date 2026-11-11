// Scripts/World/DayNightController.cs
// Ciclo de dia e noite com o tempo passando. Move o sol (nasce no leste, se põe no oeste) e,
// à noite, a lua; mistura céu, luz ambiente e neblina entre dia, entardecer e noite;
// e avisa a cidade para acender janelas e postes aos poucos.
//
// O horário vem do relógio do sistema quando online (todos os jogadores veem a mesma hora,
// sem tráfego de rede) e começa no fim da tarde no modo solo.
// Opções → Horário: ciclo, sempre noite ou sempre dia. Opções → Duração do dia.
using Godot;

namespace DeadFrequency;

public partial class DayNightController : Node
{
    [Export] public WorldEnvironment World { get; set; }
    /// <summary>Luz direcional única: faz o papel do sol de dia e da lua à noite.</summary>
    [Export] public DirectionalLight3D Sun { get; set; }
    [Export] public CityGenerator City { get; set; }
    /// <summary>Hora em que o turno solo começa.</summary>
    [Export] public float SoloStartHour { get; set; } = 17.5f;

    /// <summary>Hora atual do mundo (0..24). Lida pelo HUD e pelo PC do escritório.</summary>
    public static float Hour { get; private set; } = 20f;
    /// <summary>0 = dia, 1 = noite.</summary>
    public static float Night { get; private set; } = 1f;

    private double _soloHours;
    private float _lastNight = -1f;
    private int _lastMode = -1;
    private float _lastAppliedHour = -1f;

    // Paletas: dia, pôr do sol e noite.
    private static readonly Color DayTop = new(0.2f, 0.4f, 0.72f), DayHorizon = new(0.68f, 0.74f, 0.8f);
    private static readonly Color DuskTop = new(0.16f, 0.14f, 0.3f), DuskHorizon = new(0.9f, 0.45f, 0.22f);
    private static readonly Color NightTop = new(0.012f, 0.016f, 0.035f), NightHorizon = new(0.07f, 0.07f, 0.09f);

    public static DayNightController Instance { get; private set; }

    public override void _EnterTree() => Instance = this;

    public override void _ExitTree()
    {
        if (Instance == this)
            Instance = null;
    }

    /// <summary>Dormir: adianta o relógio (só no solo com o ciclo ligado). Retorna false se não deu.</summary>
    public bool SkipHours(double hours)
    {
        if (NetClient.Instance?.IsOnline == true || (GameSettings.Instance?.TimeMode ?? 0) != 0)
            return false;
        _soloHours += hours;
        return true;
    }

    /// <summary>Hora (0..24) para acordar: o jogo começa de novo às 18h, no início do turno.</summary>
    public void SetSoloHour(float hour)
    {
        double day = System.Math.Floor(_soloHours / 24.0) * 24.0;
        double target = day + hour;
        if (target <= _soloHours)
            target += 24.0;
        _soloHours = target;
    }

    private bool _glowDefault;
    private bool _captured;

    /// <summary>Qualidade, brilho e contraste das Opções > Vídeo.</summary>
    private void ApplyVideo()
    {
        GameSettings s = GameSettings.Instance;
        if (s == null)
            return;
        Environment env = World?.Environment;
        if (env != null)
        {
            if (!_captured)
            {
                _glowDefault = env.GlowEnabled;
                _captured = true;
            }
            env.GlowEnabled = _glowDefault && s.Quality > 0;
            env.AdjustmentEnabled = true;
            env.AdjustmentBrightness = s.Brightness;
            env.AdjustmentContrast = s.Contrast;
            env.SsaoEnabled = env.SsaoEnabled && s.Quality >= 2;
        }
        if (Sun != null)
        {
            Sun.ShadowEnabled = s.Quality > 0;
            Sun.DirectionalShadowMaxDistance = s.Quality >= 2 ? 120f : 60f;
        }
        // Atlas menor no preset Baixo reduz o uso de memória da GPU.
        int shadowAtlasSize = s.Quality >= 2 ? 4096 : s.Quality == 1 ? 2048 : 1024;
        RenderingServer.DirectionalShadowAtlasSetSize(shadowAtlasSize, true);
    }

    public override void _Ready()
    {
        _soloHours = SoloStartHour;
        if (GameSettings.Instance != null)
        {
            GameSettings.Instance.SettingsChanged += ApplyVideo;
            TreeExiting += () => GameSettings.Instance.SettingsChanged -= ApplyVideo;
        }
        Callable.From(ApplyVideo).CallDeferred();
        // A cidade termina de montar no _Ready dela; o primeiro ajuste vai para o fim do frame.
        Callable.From(() => Update(0.0)).CallDeferred();
    }

    public override void _Process(double delta) => Update(delta);

    private void Update(double delta)
    {
        GameSettings s = GameSettings.Instance;
        int mode = s?.TimeMode ?? 0;
        float dayLengthSec = Mathf.Max(60f, (s?.DayLengthMinutes ?? 24f) * 60f);
        double hoursPerSecond = 24.0 / dayLengthSec;

        double totalHours;
        if (mode == 1)
            totalHours = 23.0;
        else if (mode == 2)
            totalHours = 13.0;
        else if (NetClient.Instance?.IsOnline == true)
            totalHours = Time.GetUnixTimeFromSystem() * hoursPerSecond + 18.0; // mesmo horário para todos
        else
        {
            _soloHours += delta * hoursPerSecond; // pausa junto com o jogo
            totalHours = _soloHours;
        }

        Hour = (float)Mathf.PosMod(totalHours, 24.0);

        // Em horário fixo, céu, iluminação e materiais não precisam ser recalculados a cada frame.
        if (mode != 0 && mode == _lastMode && Mathf.IsEqualApprox(Hour, _lastAppliedHour))
            return;

        _lastMode = mode;
        _lastAppliedHour = Hour;
        Apply(Hour);
    }

    /// <summary>"18:45 · ENTARDECER" para o HUD.</summary>
    public static string ClockText()
    {
        int h = (int)Hour;
        int m = (int)((Hour - h) * 60f);
        string phase = Hour switch
        {
            >= 5f and < 7f => "AMANHECER",
            >= 7f and < 17f => "DIA",
            >= 17f and < 19.5f => "ENTARDECER",
            _ => "NOITE",
        };
        if (GameSettings.Instance?.Clock24h == false)
        {
            int h12 = h % 12 == 0 ? 12 : h % 12;
            return $"{h12}:{m:00} {(h < 12 ? "AM" : "PM")} · {phase}";
        }
        return $"{h:00}:{m:00} · {phase}";
    }

    private void Apply(float hour)
    {
        // Sol: ângulo 0 às 6h (leste, +X), 90° ao meio-dia, 180° às 18h (oeste, -X).
        float angle = (hour - 6f) / 12f * Mathf.Pi;
        float sunHeight = Mathf.Sin(angle);
        // Pequena inclinação para o sul (+Z) para as sombras não ficarem retas demais.
        Vector3 toSun = new Vector3(Mathf.Cos(angle), sunHeight, 0.35f).Normalized();
        // Lua oposta ao sol.
        Vector3 toMoon = new Vector3(-Mathf.Cos(angle), -sunHeight, 0.45f).Normalized();

        // night: 0 com o sol bem acima do horizonte, 1 com ele bem abaixo.
        float night = Mathf.SmoothStep(0.12f, -0.18f, sunHeight);
        // dusk: pico com o sol perto do horizonte.
        float dusk = Mathf.Clamp(1f - Mathf.Abs(sunHeight) / 0.3f, 0f, 1f);
        Night = night;

        if (Sun != null)
        {
            bool sunUp = sunHeight > -0.03f;
            Vector3 toLight = sunUp ? toSun : toMoon;
            // Nunca deixa a luz rasante abaixo de ~6° (iluminaria a cidade "por baixo").
            if (toLight.Y < 0.1f)
                toLight = new Vector3(toLight.X, 0.1f, toLight.Z).Normalized();
            // A DirectionalLight ilumina na direção do seu -Z: aponta do astro para o chão.
            Sun.GlobalBasis = Basis.LookingAt(-toLight, Vector3.Up);

            if (sunUp)
            {
                float strength = Mathf.Clamp(sunHeight / 0.35f, 0f, 1f);
                Sun.LightColor = new Color(1f, 0.95f, 0.86f).Lerp(new Color(1f, 0.55f, 0.3f), dusk);
                Sun.LightEnergy = Mathf.Lerp(0.05f, 1.5f, strength);
            }
            else
            {
                float moonStrength = Mathf.Clamp(-sunHeight / 0.25f, 0f, 1f);
                Sun.LightColor = new Color(0.55f, 0.65f, 1f);
                Sun.LightEnergy = Mathf.Lerp(0.05f, 0.35f, moonStrength);
            }
            int quality = GameSettings.Instance?.Quality ?? 2;
            Sun.DirectionalShadowMaxDistance = quality <= 0 ? 0f : quality == 1 ? 60f : night > 0.5f ? 80f : 120f;
        }

        Environment env = World?.Environment;
        if (env != null)
        {
            Color top = DayTop.Lerp(NightTop, night).Lerp(DuskTop, dusk * 0.6f);
            Color horizon = DayHorizon.Lerp(NightHorizon, night).Lerp(DuskHorizon, dusk * 0.75f);
            if (env.Sky?.SkyMaterial is ProceduralSkyMaterial sky)
            {
                sky.SkyTopColor = top;
                sky.SkyHorizonColor = horizon;
                sky.GroundHorizonColor = horizon;
                sky.GroundBottomColor = new Color(0.22f, 0.22f, 0.22f).Lerp(new Color(0.02f, 0.02f, 0.02f), night);
                sky.SkyEnergyMultiplier = Mathf.Lerp(1f, 0.6f, night);
            }
            else if (env.Sky?.SkyMaterial is ShaderMaterial skyShader)
            {
                skyShader.SetShaderParameter("day_night_mix", 1f - night * 2f);
            }
            env.AmbientLightColor = new Color(0.62f, 0.66f, 0.74f).Lerp(new Color(0.17f, 0.18f, 0.24f), night).Lerp(new Color(0.6f, 0.45f, 0.4f), dusk * 0.4f);
            env.AmbientLightEnergy = Mathf.Lerp(0.75f, 0.9f, night);
            env.FogLightColor = horizon.Lerp(new Color(0.05f, 0.06f, 0.08f), night * 0.6f);
            env.FogDensity = Mathf.Lerp(0.004f, 0.016f, night);
            env.GlowIntensity = Mathf.Lerp(0.25f, 0.7f, night);
        }

        if (City != null && Mathf.Abs(night - _lastNight) > 0.004f)
        {
            _lastNight = night;
            City.SetNightAmount(night);
        }
    }
}
