// Scripts/Jobs/LocutorJob.cs
// Locutor da 96.6: fique na mesa de som da rádio. Quando a luz NO AR piscar, aperte E para ler
// o comercial (tem uns segundos). R$ 20 por comercial; se perder, vai silêncio no ar.
// Os comerciais com áudio gravado (AnnouncerCar.Ads, os mesmos da 96.6) tocam depois da vinheta;
// os outros são só texto.
using Godot;

namespace DeadFrequency;

public partial class LocutorJob : JobBase
{
    private static readonly Color Purple = new(0.85f, 0.5f, 1f);
    private const float Window = 7f;

    private static readonly string[] Ads =
    {
        "Disque Pizza 0800-96-96-96: chega fria, mas chega!",
        "Energético Frequência: te dá asas... de morcego.",
        "Supermercado Atacadinho: preço de atacado, fila de varejo.",
        "Posto Petrobraz: o combustível que move a zona morta.",
        "Correrios: sua encomenda chega. Um dia.",
        "Guaraná Antartida: o sabor que gela até a alma.",
        "Casas Baixa: dedicação total a você e às suas prestações.",
        "Edifício Sintonia: more pertinho do trabalho. Muito pertinho.",
    };

    private enum Phase { Waiting, OnAir }

    private Phase _phase;
    private double _timer = 8.0;
    private string _ad = "";
    private string _adAudio;
    private AudioStreamPlayer3D _voice;
    private Node3D _console;
    private OmniLight3D _light;
    private Label3D _sign;
    private AudioStreamPlayer3D _jingle;
    private Node3D _marker;
    private bool _hasRadioPin;

    public override void _Ready()
    {
        if (City.RadioConsolePoint == Vector3.Zero)
        {
            GameHud.Toast(this, "Esta fase não tem estação de rádio.", UiStyle.Warning);
            return;
        }
        _hasRadioPin = City.PinChunkForPosition(City.RadioConsolePoint);
        _console = new Node3D { Name = "MesaLocutor" };
        AddChild(_console);
        _console.GlobalPosition = City.RadioConsolePoint;
        _light = new OmniLight3D { LightColor = new Color(1f, 0.1f, 0.05f), LightEnergy = 0f, OmniRange = 6f, Position = new Vector3(0f, 1.4f, 0f) };
        _console.AddChild(_light);
        _sign = new Label3D
        {
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            FontSize = 44,
            PixelSize = 0.006f,
            Modulate = Purple,
            OutlineSize = 8,
            Position = new Vector3(0f, 1.9f, 0f),
            Text = "MESA DE SOM",
        };
        _console.AddChild(_sign);
        _jingle = new AudioStreamPlayer3D { Bus = AudioManager.SfxBus, UnitSize = 6f };
        if (ResourceLoader.Exists(RadioStation.JinglePath))
            _jingle.Stream = GD.Load<AudioStream>(RadioStation.JinglePath);
        _console.AddChild(_jingle);
        _voice = new AudioStreamPlayer3D { Bus = AudioManager.SfxBus, UnitSize = 6f };
        _console.AddChild(_voice);
        _jingle.Finished += PlayAdVoice;
        ActionArea.Create(_console, new Vector3(0f, 0.9f, 0f), new Vector3(2.4f, 0.6f, 1.2f),
            _ => _phase == Phase.OnAir ? $"[E] LER NO AR: \"{_ad}\"" : $"Próximo intervalo comercial em {_timer:0} s",
            _ => Read(),
            _ => _phase == Phase.OnAir);
        _marker = JobMarkers.Create(this, City.RadioConsolePoint + new Vector3(0f, 0f, 2.5f), Purple, "", 0f, 0.5f);
        _marker.Visible = true;
    }

    public override void _ExitTree()
    {
        if (_hasRadioPin)
            City?.UnpinChunkForPosition(City.RadioConsolePoint);
        _hasRadioPin = false;
        JobMarkers.Free(ref _marker);
    }

    public override void _Process(double delta)
    {
        if (_console == null)
            return;
        _timer -= delta;
        if (_phase == Phase.Waiting)
        {
            _light.LightEnergy = 0f;
            _sign.Text = $"COMERCIAL EM {Mathf.Max(0, _timer):0}s";
            if (_timer <= 0)
            {
                _phase = Phase.OnAir;
                _timer = Window;
                PickAd();
                if (PlayerPos.DistanceTo(_console.GlobalPosition) < 25f)
                    GameHud.Toast(this, "NO AR! Aperte E na mesa de som para ler o comercial.", Purple, 3.0);
            }
        }
        else
        {
            bool blink = Mathf.PosMod((float)Time.GetTicksMsec() / 250f, 2f) < 1f;
            _light.LightEnergy = blink ? 3f : 0.4f;
            _sign.Text = blink ? "● NO AR" : $"LEIA! {_timer:0}s";
            if (_timer <= 0)
            {
                _phase = Phase.Waiting;
                _timer = Rng.RandfRange(15f, 25f);
                if (PlayerPos.DistanceTo(_console.GlobalPosition) < 25f)
                    GameHud.Toast(this, "Silêncio no ar... o comercial passou.", UiStyle.Warning, 2.5);
            }
        }
    }

    private void Read()
    {
        if (_phase != Phase.OnAir)
            return;
        _phase = Phase.Waiting;
        _timer = Rng.RandfRange(15f, 25f);
        if (_jingle?.Stream != null)
            _jingle.Play();
        else
            PlayAdVoice();
        M.Earn(1, "Comercial lido no ar");
    }

    /// <summary>Prefere um comercial gravado; se nenhum áudio existir, usa os textos antigos.</summary>
    private void PickAd()
    {
        _adAudio = null;
        var recorded = new System.Collections.Generic.List<(string Id, string Text)>();
        foreach (var a in AnnouncerCar.Ads)
            if (ResourceLoader.Exists(AnnouncerCar.RadioAdsFolder + a.Id + ".ogg"))
                recorded.Add(a);
        if (recorded.Count > 0)
        {
            var pick = recorded[Rng.RandiRange(0, recorded.Count - 1)];
            _ad = pick.Text;
            _adAudio = AnnouncerCar.RadioAdsFolder + pick.Id + ".ogg";
            return;
        }
        _ad = Ads[Rng.RandiRange(0, Ads.Length - 1)];
    }

    private void PlayAdVoice()
    {
        if (_voice == null || _adAudio == null || !ResourceLoader.Exists(_adAudio))
            return;
        if (GD.Load<AudioStream>(_adAudio) is AudioStream stream)
        {
            if (stream is AudioStreamOggVorbis ogg)
                ogg.Loop = false;
            _voice.Stream = stream;
            _voice.Play();
        }
        _adAudio = null;
    }

    public override bool TryGetTarget(out Vector3 position, out string label)
    {
        position = City.RadioConsolePoint;
        label = PlayerPos.DistanceTo(position) > 12f ? "RÁDIO 96.6" : "MESA DE SOM";
        return position != Vector3.Zero;
    }

    public override bool TryGetSpawn(out Vector3 position, out float yaw)
    {
        // Dentro do estúdio, atrás da mesa de som.
        position = City.RadioConsolePoint + new Vector3(0f, 0.1f, 2.6f);
        yaw = 0f;
        return City.RadioConsolePoint != Vector3.Zero;
    }

    public override string PanelText()
    {
        if (_console == null)
            return "Sem rádio nesta fase.";
        float d = PlayerPos.DistanceTo(_console.GlobalPosition);
        string where = d > 12f ? $"\n· Vá até a Rádio 96.6 ({d:0} m)" : "";
        return _phase == Phase.OnAir
            ? $"► NO AR! Leia o comercial ({_timer:0} s){where}"
            : $"· Próximo comercial em {Mathf.Max(0, _timer):0} s{where}\n· No ar agora: {RadioStation.NowPlayingText()}";
    }
}
