// Scripts/World/VirtualTV.cs
// TV de tubo do apartamento. E troca de canal: Jornal da Frequência, Clipe 96.6 (toca a rádio),
// Canal do Tempo, Fora do Ar e desligada. O texto dos canais é montado com o estado do jogo.
// v0.9.9: a imagem vem chuviscada até alguém enrolar palha de aço na antena (item da Lã de Aço 1001).
using Godot;

namespace DeadFrequency;

public partial class VirtualTV : StaticBody3D, IInteractable
{
    private static readonly string[] ChannelNames = { "desligada", "TV Frequência · Jornal", "Clipe 96.6", "Canal do Tempo", "Fora do ar" };
    private static readonly string[] Headlines =
    {
        "Entregadores relatam chiado na 96.6 depois da meia-noite: culpa do minério, diz a rádio",
        "Atacadinho anuncia promoção de café: fila dobra o quarteirão",
        "Ônibus 966 atropela de novo; empresa diz que buzinou duas vezes",
        "Prefeitura promete consertar postes da Rua Estática \"até 1997\"",
        "Caixa d'água municipal ganha refletores novos",
        "Taxistas reclamam da concorrência das vans de entrega",
        "Rancho Fundo bate recorde de abóboras; ninguém sabe por quê",
        "Lã de Aço 1001 Utilidades esgota palha de aço: \"todo mundo quer a TV sem chuvisco\"",
    };

    private int _channel;
    private MeshInstance3D _screen;
    private ShaderMaterial _mat;
    private Label3D _text;
    private RadioPlayer _radio;
    private AudioStreamPlayer3D _beep;
    private double _tick;
    private int _headline;

    private static readonly System.Collections.Generic.List<VirtualTV> All = new();
    private static bool AntennaFixed => PlayerInventory.Instance?.TvAntennaFixed == true;

    public override void _EnterTree() => All.Add(this);
    public override void _ExitTree() => All.Remove(this);

    /// <summary>Usar a palha de aço: precisa estar perto de uma TV. Retorna a mensagem (vazia = não usou o item).</summary>
    public static string FixNearest(PlayerController player)
    {
        if (player == null || AntennaFixed)
            return "";
        foreach (VirtualTV tv in All)
        {
            if (!IsInstanceValid(tv) || tv.GlobalPosition.DistanceTo(player.GlobalPosition) > 3f)
                continue;
            PlayerInventory.Instance?.FixTvAntenna();
            foreach (VirtualTV other in All)
                if (IsInstanceValid(other))
                    other.ApplySnow();
            return "Palha de aço na antena: a imagem da TV ficou limpinha!";
        }
        return "";
    }

    private void ApplySnow() => _mat?.SetShaderParameter("snow", AntennaFixed ? 0.0f : 0.38f);

    public override void _Ready()
    {
        CollisionLayer = 4 | 1; // interativo e sólido
        CollisionMask = 0;
        var cabinet = new StandardMaterial3D { AlbedoColor = new Color(0.18f, 0.16f, 0.15f), Roughness = 0.6f };
        var dark = new StandardMaterial3D { AlbedoColor = new Color(0.05f, 0.05f, 0.05f) };
        AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(0.9f, 0.7f, 0.6f) }, Position = new Vector3(0f, 0.35f, 0f) });
        AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(0.9f, 0.7f, 0.6f) }, MaterialOverride = cabinet, Position = new Vector3(0f, 0.35f, 0f) });
        AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(0.74f, 0.54f, 0.02f) }, MaterialOverride = dark, Position = new Vector3(-0.04f, 0.37f, 0.3f) });
        AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(0.06f, 0.06f, 0.02f) }, MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.9f, 0.1f, 0.1f), EmissionEnabled = true, Emission = new Color(1f, 0.1f, 0.1f) }, Position = new Vector3(0.38f, 0.1f, 0.305f) });
        // Antena "orelha de coelho".
        foreach (float a in new[] { -0.5f, 0.5f })
        {
            var rod = new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 0.006f, BottomRadius = 0.006f, Height = 0.6f }, MaterialOverride = dark, Position = new Vector3(a * 0.3f, 0.95f, -0.05f), Rotation = new Vector3(0f, 0f, a) };
            AddChild(rod);
        }

        _mat = new ShaderMaterial { Shader = GD.Load<Shader>("res://Shaders/tv_screen.gdshader") };
        _mat.SetShaderParameter("seed", GD.Randf() * 100f);
        _screen = new MeshInstance3D { Mesh = new QuadMesh { Size = new Vector2(0.68f, 0.5f) }, MaterialOverride = _mat, Position = new Vector3(-0.04f, 0.37f, 0.312f) };
        AddChild(_screen);
        ApplySnow();
        _text = new Label3D
        {
            FontSize = 22,
            PixelSize = 0.0016f,
            Modulate = Colors.White,
            OutlineSize = 4,
            Width = 400,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            HorizontalAlignment = HorizontalAlignment.Center,
            Position = new Vector3(-0.04f, 0.37f, 0.318f),
        };
        AddChild(_text);
        _radio = new RadioPlayer { On = false, VolumeDb = -6f, UnitSize = 3f, MaxDistance = 15f, Position = new Vector3(0f, 0.4f, 0.2f) };
        AddChild(_radio);
        _beep = new AudioStreamPlayer3D { Bus = AudioManager.SfxBus, UnitSize = 3f, VolumeDb = -14f };
        if (ResourceLoader.Exists("res://Audio/SFX/pc_beep.wav"))
            _beep.Stream = GD.Load<AudioStream>("res://Audio/SFX/pc_beep.wav");
        AddChild(_beep);
        SetChannel(0);
    }

    public string GetInteractionPrompt(PlayerController player)
        => !AntennaFixed && _channel != 0 && System.Linq.Enumerable.Contains(PlayerInventory.Instance?.Items ?? System.Array.Empty<string>(), ItemCatalog.SteelWool)
            ? $"[E] Mudar de canal ({ChannelNames[_channel]})  ·  use a palha de aço para tirar o chuvisco"
            : _channel == ChannelNames.Length - 1 ? "[E] Desligar a TV" : _channel == 0 ? "[E] Ligar a TV" : $"[E] Mudar de canal ({ChannelNames[_channel]})";

    public void Interact(PlayerController player) => SetChannel((_channel + 1) % ChannelNames.Length);

    private void SetChannel(int ch)
    {
        _channel = ch;
        // Chuvisco rapidinho na troca, como TV de tubo.
        _mat.SetShaderParameter("mode", ch == 0 ? 0 : 5);
        _text.Text = "";
        _radio.SetOn(ch == 2);
        if (ch != 0)
            _beep.Play();
        GetTree().CreateTimer(0.25).Timeout += () =>
        {
            if (IsInstanceValid(this) && _channel == ch)
            {
                _mat.SetShaderParameter("mode", ch);
                _tick = 0;
            }
        };
    }

    public override void _Process(double delta)
    {
        if (_channel == 0)
            return;
        _tick -= delta;
        if (_tick > 0)
            return;
        _tick = 1.0;
        switch (_channel)
        {
            case 1:
                if ((int)(Time.GetTicksMsec() / 6000) != _headline)
                    _headline = (int)(Time.GetTicksMsec() / 6000);
                string h = Headlines[_headline % Headlines.Length];
                var pm = ProfessionManager.Instance;
                string extra = pm != null && pm.SessionTasks > 0 ? $"\n\nEXCLUSIVO: {Professions.Get(pm.Current).Name.ToLowerInvariant()} faz {pm.SessionTasks} serviços e fatura $ {pm.SessionEarned} hoje" : "";
                _text.Text = $"JORNAL DA FREQUÊNCIA · {DayNightController.ClockText()}\n\n{h}{extra}";
                break;
            case 2:
                _text.Text = $"\n\n\n\n♪ {RadioStation.NowPlayingText()}";
                break;
            case 3:
                float hr = DayNightController.Hour;
                string sky = hr is >= 6f and < 18f ? "sol entre nuvens" : "neblina densa, visibilidade baixa";
                _text.Text = $"CANAL DO TEMPO\n\nZona Morta: {sky}\nMín 14°  Máx 23°\nAmanhã: chiado na 96.6 de madrugada";
                break;
            case 4:
                _text.Text = "FORA DO AR\nvoltamos já";
                break;
        }
    }
}
