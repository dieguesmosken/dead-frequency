// Scripts/World/Mosquitoes.cs
// Pernilongos do sítio (v0.9.5): à noite, a pé, de vez em quando um zumbido chato perto do ouvido.
// Com um caramelo companheiro por perto, ele late e eles somem.
using Godot;

namespace DeadFrequency;

public partial class Mosquitoes : Node
{
    private AudioStreamPlayer _buzz;
    private float _timer = 20f, _buzzLeft;
    private bool _toldDog;
    private readonly RandomNumberGenerator _rng = new();

    public override void _Ready()
    {
        _rng.Randomize();
        _buzz = new AudioStreamPlayer { Bus = AudioManager.SfxBus, VolumeDb = -80f };
        // O loop vem do .import (edit/loop_mode = Forward).
        if (ResourceLoader.Exists("res://Audio/SFX/pernilongo.wav"))
            _buzz.Stream = GD.Load<AudioStream>("res://Audio/SFX/pernilongo.wav");
        AddChild(_buzz);
    }

    /// <summary>Segundos restantes do repelente (Agro-Tudão). Enquanto durar, nada de pernilongo.</summary>
    public static float RepelLeft { get; private set; }

    /// <summary>Passa o repelente. Retorna a mensagem do HUD.</summary>
    public static string Repel(float seconds)
    {
        RepelLeft = seconds;
        return "Repelente passado: 5 minutos sem muriçoca.";
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        if (RepelLeft > 0f)
            RepelLeft = Mathf.Max(0f, RepelLeft - dt);
        PlayerController player = ProfessionManager.Instance?.Player;
        float hour = DayNightController.Hour;
        bool night = hour >= 19f || hour < 5f;
        bool onFoot = player != null && IsInstanceValid(player) && player.State == PlayerState.Walking && player.GlobalPosition.Y > -5f;
        bool dog = FarmAnimal.Companion != null && player != null && FarmAnimal.Companion.GlobalPosition.DistanceTo(player.GlobalPosition) < 12f;

        if (_buzzLeft > 0f)
        {
            _buzzLeft -= dt;
            if (dog && !_toldDog)
            {
                _toldDog = true;
                GameHud.Toast(this, "Au! Au! O caramelo espantou os pernilongos.", new Color(1f, 0.85f, 0.5f), 3.0);
            }
            bool stop = _buzzLeft <= 0f || dog || !onFoot || RepelLeft > 0f;
            _buzz.VolumeDb = Mathf.MoveToward(_buzz.VolumeDb, stop ? -80f : -14f + Mathf.Sin(Time.GetTicksMsec() / 300f) * 4f, 60f * dt);
            if (stop && _buzz.VolumeDb <= -79f)
            {
                _buzzLeft = 0f;
                _buzz.Stop();
            }
            return;
        }
        _timer -= dt;
        if (_timer > 0f)
            return;
        _timer = _rng.RandfRange(25f, 60f);
        if (!night || !onFoot || dog || RepelLeft > 0f || _buzz.Stream == null)
            return;
        _buzzLeft = _rng.RandfRange(5f, 10f);
        _toldDog = false;
        _buzz.VolumeDb = -60f;
        _buzz.Play();
    }
}
