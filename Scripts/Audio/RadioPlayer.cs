// Scripts/Audio/RadioPlayer.cs
// Um aparelho de rádio no mundo (van, escritório, apartamento). Toca a 96.6 sincronizada pelo relógio.
// Liga/desliga com Toggle(). Um chiadinho curto ao ligar, como sintonia de rádio de verdade.
using Godot;

namespace DeadFrequency;

public partial class RadioPlayer : Node3D
{
    [Export] public bool On { get; set; } = true;
    [Export] public float VolumeDb { get; set; } = -6f;
    [Export] public float UnitSize { get; set; } = 4f;
    [Export] public float MaxDistance { get; set; } = 30f;

    private AudioStreamPlayer3D _player;
    private AudioStreamPlayer3D _static;
    private int _index = -1;
    private double _checkTimer;

    public string NowPlaying => RadioStation.NowPlayingText();
    /// <summary>Chiado por cima da música, de 0 a 1 (carga com interferência na caçamba da caminhonete).</summary>
    public float Interference { get; set; }
    private AudioStreamPlayer3D _hiss;

    public override void _Ready()
    {
        _player = new AudioStreamPlayer3D
        {
            Bus = AudioManager.MusicBus,
            UnitSize = UnitSize,
            MaxDistance = MaxDistance,
            VolumeDb = VolumeDb,
            AttenuationFilterCutoffHz = 3500f,
            AttenuationFilterDb = -12f,
        };
        AddChild(_player);
        _static = new AudioStreamPlayer3D { Bus = AudioManager.SfxBus, UnitSize = UnitSize, MaxDistance = MaxDistance, VolumeDb = VolumeDb - 8f };
        _static.Stream = MakeStatic();
        AddChild(_static);
        _player.Finished += () => Resync(true);
        _hiss = new AudioStreamPlayer3D { Bus = AudioManager.SfxBus, UnitSize = UnitSize, MaxDistance = MaxDistance, VolumeDb = -60f, Stream = MakeHiss() };
        AddChild(_hiss);
    }

    public void Toggle() => SetOn(!On);

    public void SetOn(bool on)
    {
        On = on;
        if (!IsInsideTree() || _player == null)
            return;
        if (on)
        {
            _static.Play();
            Resync(true);
        }
        else
        {
            _player.Stop();
            _hiss?.Stop();
            _index = -1;
        }
    }

    public override void _Process(double delta)
    {
        if (!On)
            return;
        float radio = GameSettings.Instance?.RadioVolume ?? 1f;
        float hiss = Mathf.Clamp(Interference, 0f, 1f);
        _player.VolumeDb = VolumeDb + Mathf.LinearToDb(Mathf.Max(radio, 0.001f)) - 9f * hiss;
        if (hiss > 0.05f)
        {
            _hiss.VolumeDb = VolumeDb - 26f + 18f * hiss + Mathf.LinearToDb(Mathf.Max(radio, 0.001f));
            if (!_hiss.Playing)
                _hiss.Play();
        }
        else if (_hiss.Playing)
        {
            _hiss.Stop();
        }
        _checkTimer -= delta;
        if (_checkTimer > 0)
            return;
        _checkTimer = 1.0;
        (int i, RadioStation.Segment _, float _) = RadioStation.Now();
        if (!_player.Playing || i != _index)
            Resync(false);
    }

    private void Resync(bool force)
    {
        if (!On || _player == null)
            return;
        (int i, RadioStation.Segment seg, float offset) = RadioStation.Now();
        if (!force && _player.Playing && i == _index)
            return;
        string path = seg.Path;
        _index = i;
        if (!ResourceLoader.Exists(path) || offset >= seg.Length - 0.45f)
            return; // fim do trecho (o respiro depois de um comercial): espera o próximo
        if (GD.Load<AudioStream>(path) is AudioStreamOggVorbis ogg)
        {
            ogg.Loop = false;
            _player.Stream = ogg;
        }
        else
        {
            _player.Stream = GD.Load<AudioStream>(path);
        }
        _player.Play(offset);
    }

    /// <summary>Meio segundo de chiado de sintonia (ruído filtrado), gerado na hora.</summary>
    /// <summary>Um segundo de chiado em loop (interferência).</summary>
    private static AudioStreamWav MakeHiss()
    {
        const int rate = 22050;
        var data = new byte[rate * 2];
        var rng = new RandomNumberGenerator { Seed = 1996 };
        float last = 0f;
        for (int i = 0; i < rate; i++)
        {
            last = Mathf.Lerp(last, rng.RandfRange(-1f, 1f), 0.6f);
            float crackle = rng.Randf() > 0.997f ? rng.RandfRange(-0.8f, 0.8f) : 0f;
            // As pontas se encontram (loop sem estalo).
            float edge = Mathf.Min(1f, Mathf.Min(i, rate - 1 - i) / 400f);
            short v = (short)(Mathf.Clamp((last * 0.45f + crackle) * edge, -1f, 1f) * 30000);
            data[i * 2] = (byte)(v & 0xff);
            data[i * 2 + 1] = (byte)((v >> 8) & 0xff);
        }
        return new AudioStreamWav
        {
            Format = AudioStreamWav.FormatEnum.Format16Bits, MixRate = rate, Data = data,
            LoopMode = AudioStreamWav.LoopModeEnum.Forward, LoopBegin = 0, LoopEnd = rate,
        };
    }

    private static AudioStreamWav MakeStatic()
    {
        const int rate = 22050;
        int n = rate / 2;
        var data = new byte[n * 2];
        var rng = new RandomNumberGenerator { Seed = 966 };
        float last = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / n;
            last = Mathf.Lerp(last, rng.RandfRange(-1f, 1f), 0.35f);
            float whistle = Mathf.Sin(i * Mathf.Tau * Mathf.Lerp(1800f, 600f, t) / rate) * 0.2f;
            float v = (last + whistle) * (1f - t) * 0.5f;
            short s = (short)(Mathf.Clamp(v, -1f, 1f) * 32000);
            data[i * 2] = (byte)(s & 0xff);
            data[i * 2 + 1] = (byte)((s >> 8) & 0xff);
        }
        return new AudioStreamWav { Format = AudioStreamWav.FormatEnum.Format16Bits, MixRate = rate, Data = data };
    }
}
