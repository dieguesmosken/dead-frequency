// Scripts/Jobs/TaxiJob.cs
// Taxista: um passageiro espera na calçada (feixe amarelo). Pare a van perto dele, ele embarca e diz
// o destino; pare perto do destino para receber a corrida (R$ 30 + pela distância, até R$ 400).
// v0.9.5: passageiros exigentes. Executivo (roupa social) quer a 96.6 ligada, sem passar de 60 km/h e sem
// batida; apressado paga bônus se chegar rápido; tranquilo só não gosta de batida.
using Godot;

namespace DeadFrequency;

public partial class TaxiJob : JobBase
{
    private static readonly Color Yellow = new(1f, 0.85f, 0.2f);
    private static readonly string[] Lines =
    {
        "Me leva na {0}, por favor. E abaixa esse rádio... ou não, pode deixar.",
        "{0}. Rapidinho que eu tô atrasado pro plantão.",
        "Toca pra {0}! Essa van é segura mesmo?",
        "Boa noite. {0}. Não passa pela Rua Estática que lá tem coisa estranha.",
        "Vai pra {0}? Eu pago em dinheiro... trocado, tá?",
    };

    private int _pickup = -1;
    private int _dest = -1;
    private Vector3 _pickupPinPosition;
    private Vector3 _destPinPosition;
    private bool _hasPickupPin;
    private bool _hasDestPin;
    private readonly System.Collections.Generic.List<Vector3> _pendingPassengerPins = new();
    private bool _riding;
    private CharacterModel _passenger;
    private Node3D _marker;
    private double _wait;
    private float _anim;

    private enum Mood { Executivo, Apressado, Tranquilo }
    private Mood _mood;
    private float _rideTime, _radioTime, _maxSpeed;
    private int _crashes;
    private bool _warnedSpeed;
    private VanController _rideVan;
    private static readonly string[] MoodName = { "Executivo", "Apressado", "Tranquilo" };

    public override void _Ready() => NewPassenger();

    public override void _ExitTree()
    {
        if (_rideVan != null && IsInstanceValid(_rideVan))
            _rideVan.Crashed -= OnCrash;
        JobMarkers.Free(ref _marker);
        ReleasePickupPin();
        ReleaseDestinationPin();
        foreach (Vector3 position in _pendingPassengerPins)
            City?.UnpinChunkForPosition(position);
        _pendingPassengerPins.Clear();
        if (_passenger != null && IsInstanceValid(_passenger))
            _passenger.QueueFree();
    }

    private void ReleasePickupPin()
    {
        if (!_hasPickupPin)
            return;
        City?.UnpinChunkForPosition(_pickupPinPosition);
        _hasPickupPin = false;
    }

    private void ReleaseDestinationPin()
    {
        if (!_hasDestPin)
            return;
        City?.UnpinChunkForPosition(_destPinPosition);
        _hasDestPin = false;
    }

    private void NewPassenger()
    {
        ReleasePickupPin();
        ReleaseDestinationPin();
        _riding = false;
        _pickup = RandomPoint(PlayerPos, 60f, 320f);
        if (_pickup < 0)
            return;
        DeliveryPoint p = City.DeliveryPoints[_pickup];
        _pickupPinPosition = p.Position;
        _hasPickupPin = City.PinChunkForPosition(_pickupPinPosition);
        var rng = new RandomNumberGenerator();
        rng.Randomize();
        _mood = (Mood)rng.RandiRange(0, 2);
        // O executivo vem de roupa social: camisa branca e calça escura.
        Color shirt = _mood == Mood.Executivo ? new Color(0.92f, 0.92f, 0.9f) : Color.FromHsv(rng.Randf(), 0.5f, 0.7f);
        Color pants = _mood == Mood.Executivo ? new Color(0.06f, 0.06f, 0.08f) : Color.FromHsv(rng.Randf(), 0.3f, 0.3f);
        _passenger = CharacterModel.Create(shirt, pants,
            new Color(0.55f + rng.Randf() * 0.3f, 0.4f + rng.Randf() * 0.2f, 0.3f + rng.Randf() * 0.15f));
        AddChild(_passenger);
        _passenger.GlobalPosition = p.Position;
        JobMarkers.Free(ref _marker);
        _marker = JobMarkers.Create(this, p.Position, Yellow, "PASSAGEIRO");
    }

    public override void _Process(double delta)
    {
        _anim += (float)delta;
        if (_wait > 0)
        {
            _wait -= delta;
            if (_wait <= 0)
                NewPassenger();
            return;
        }
        if (_pickup < 0 || City == null)
            return;

        VanController van = Player?.CurrentVehicle;
        if (!_riding && _passenger != null && IsInstanceValid(_passenger))
        {
            // Passageiro acena e olha para a van.
            _passenger.Animate((float)delta, 0f);
            _passenger.SetHeadPitch(Mathf.Sin(_anim * 2f) * 0.2f);
            if (van != null)
            {
                Vector3 to = van.GlobalPosition - _passenger.GlobalPosition;
                to.Y = 0f;
                if (to.LengthSquared() > 0.01f)
                    _passenger.Rotation = new Vector3(0f, Mathf.Atan2(-to.X, -to.Z), 0f);
            }
            if (van != null && van.GlobalPosition.DistanceTo(City.DeliveryPoints[_pickup].Position) < 9f && van.SpeedKmh < 8f)
                Board();
        }
        else if (_riding && van != null && van.GlobalPosition.DistanceTo(City.DeliveryPoints[_dest].Position) < 9f && van.SpeedKmh < 8f)
        {
            Arrive(van);
        }
        else if (_riding && van != null)
        {
            TrackRide(van, (float)delta);
        }
    }

    /// <summary>Durante a corrida: tempo, rádio ligado, velocidade máxima e reclamações.</summary>
    private void TrackRide(VanController van, float dt)
    {
        if (_rideVan != van)
        {
            if (_rideVan != null && IsInstanceValid(_rideVan))
                _rideVan.Crashed -= OnCrash;
            _rideVan = van;
            van.Crashed += OnCrash;
        }
        _rideTime += dt;
        if (van.Radio != null && van.Radio.On)
            _radioTime += dt;
        _maxSpeed = Mathf.Max(_maxSpeed, van.SpeedKmh);
        if (_mood == Mood.Executivo && van.SpeedKmh > 60f && !_warnedSpeed)
        {
            _warnedSpeed = true;
            GameHud.Toast(this, "Passageiro: \"Ei, devagar! Tenho reunião, não quero chegar no hospital.\"", Colors.White, 4.0);
        }
    }

    private void OnCrash(float change)
    {
        if (!_riding || change < 5f)
            return;
        _crashes++;
        string[] lines = _mood == Mood.Executivo
            ? new[] { "Meu terno! Olha a direção!", "Isso vai ter reclamação, viu?" }
            : _mood == Mood.Apressado ? new[] { "Rápido sim, batendo não!", "Opa! Tá tudo bem aí?" } : new[] { "Calma, motorista...", "Ai! Devagar com o andor." };
        GameHud.Toast(this, $"Passageiro: \"{lines[Rng.RandiRange(0, lines.Length - 1)]}\"", Colors.White, 3.5);
    }

    /// <summary>Quanto o passageiro acha que a corrida vale (multiplicador do preço) e o comentário dele.</summary>
    private (float mult, string comment) Judge()
    {
        float dist = City.DeliveryPoints[_pickup].Position.DistanceTo(City.DeliveryPoints[_dest].Position);
        float expected = dist / (35f / 3.6f) + 10f; // ~35 km/h de média
        float radioShare = _rideTime > 0f ? _radioTime / _rideTime : 0f;
        switch (_mood)
        {
            case Mood.Executivo:
            {
                int faults = (_maxSpeed > 60f ? 1 : 0) + (_crashes > 0 ? 1 : 0) + (radioShare < 0.6f ? 1 : 0);
                if (faults == 0)
                    return (1.4f, "Rádio boa, direção macia. Tá aqui a gorjeta.");
                return (Mathf.Max(0.5f, 1f - 0.25f * faults), faults == 3 ? "Correria, batida e silêncio. Nunca mais." : radioShare < 0.6f ? "Nem uma musiquinha da 96.6... vou descontar." : "Que direção, hein. Vou descontar.");
            }
            case Mood.Apressado:
                if (_rideTime < expected * 0.8f && _crashes == 0)
                    return (1.5f, "Voando baixo! Cheguei a tempo, toma o bônus!");
                if (_rideTime > expected * 1.5f)
                    return (0.7f, "Perdi o plantão... vou pagar menos.");
                return (_crashes > 0 ? 0.85f : 1f, "Chegamos. Valeu.");
            default:
                return (_crashes > 0 ? 0.8f : 1.1f, _crashes > 0 ? "Chegamos inteiros, pelo menos." : "Corrida tranquila, gostei. Um troquinho a mais.");
        }
    }

    private void Board()
    {
        _riding = true;
        _rideTime = _radioTime = _maxSpeed = 0f;
        _crashes = 0;
        _warnedSpeed = false;
        _passenger.Visible = false;
        ReleasePickupPin();
        _dest = RandomPoint(City.DeliveryPoints[_pickup].Position, 140f, 600f, _pickup);
        if (_dest < 0)
        {
            // Sem destino válido, encerra a tentativa e deixa o passageiro esperando novamente.
            _riding = false;
            _passenger.Visible = true;
            _pickupPinPosition = City.DeliveryPoints[_pickup].Position;
            _hasPickupPin = City.PinChunkForPosition(_pickupPinPosition);
            _wait = 2.0;
            return;
        }
        DeliveryPoint d = City.DeliveryPoints[_dest];
        _destPinPosition = d.Position;
        _hasDestPin = City.PinChunkForPosition(_destPinPosition);
        JobMarkers.Free(ref _marker);
        _marker = JobMarkers.Create(this, d.Position, Yellow, "DESTINO");
        string line = string.Format(Lines[Rng.RandiRange(0, Lines.Length - 1)], d.Address);
        string wish = _mood switch
        {
            Mood.Executivo => " (Executivo: quer a 96.6 ligada, sem passar de 60 km/h e sem batida.)",
            Mood.Apressado => " (Apressado: bônus se chegar rápido.)",
            _ => "",
        };
        GameHud.Toast(this, "Passageiro: \"" + line + "\"" + wish, Colors.White, 7.0);
    }

    private int Fare() => Mathf.Clamp(30 + Mathf.RoundToInt(City.DeliveryPoints[_pickup].Position.DistanceTo(City.DeliveryPoints[_dest].Position) * 0.5f), 30, 400);

    private void Arrive(VanController van)
    {
        (float mult, string comment) = Judge();
        int fare = Mathf.Clamp(Mathf.RoundToInt(Fare() * mult), 30, 400);
        GameHud.Toast(this, $"Passageiro: \"{comment}\"", Colors.White, 4.0);
        M.Earn(1, mult > 1.01f ? "Corrida com gorjeta" : mult < 0.99f ? "Corrida (com desconto)" : "Corrida concluída", fare);
        if (_rideVan != null && IsInstanceValid(_rideVan))
            _rideVan.Crashed -= OnCrash;
        _rideVan = null;
        JobMarkers.Free(ref _marker);
        if (_passenger != null && IsInstanceValid(_passenger))
        {
            // Desce ao lado da van e vai embora a pé.
            _passenger.Visible = true;
            _passenger.GlobalPosition = City.DeliveryPoints[_dest].Position;
            CharacterModel p = _passenger;
            _passenger = null;
            Vector3 destinationPin = _destPinPosition;
            bool releaseDestinationPin = _hasDestPin;
            _hasDestPin = false;
            Tween tw = p.CreateTween();
            Vector3 away = p.GlobalPosition + new Vector3(Rng.RandfRange(-6f, 6f), 0f, Rng.RandfRange(-6f, 6f));
            tw.TweenProperty(p, "global_position", away, 4.0);
            tw.TweenCallback(Callable.From(() =>
            {
                p.QueueFree();
                if (releaseDestinationPin && _pendingPassengerPins.Remove(destinationPin))
                    City?.UnpinChunkForPosition(destinationPin);
            }));
            if (releaseDestinationPin)
                _pendingPassengerPins.Add(destinationPin);
        }
        else
        {
            ReleaseDestinationPin();
        }
        _riding = false;
        _pickup = -1;
        _wait = 3.0;
    }

    public override bool TryGetTarget(out Vector3 position, out string label)
    {
        position = Vector3.Zero;
        label = "";
        if (City == null)
            return false;
        if (Player?.CurrentVehicle == null && M.Van != null && IsInstanceValid(M.Van))
        {
            position = M.Van.GlobalPosition;
            label = "VAN";
            return true;
        }
        if (_riding && _dest >= 0)
        {
            position = City.DeliveryPoints[_dest].Position;
            label = "DESTINO";
            return true;
        }
        if (_pickup >= 0)
        {
            position = City.DeliveryPoints[_pickup].Position;
            label = "PASSAGEIRO";
            return true;
        }
        return false;
    }

    public override string PanelText()
    {
        if (_pickup < 0 || City == null)
            return "Procurando passageiro...";
        bool inVan = Player?.CurrentVehicle != null;
        string van = inVan ? "" : "\n(Entre na van para fazer as corridas.)";
        if (!_riding)
        {
            DeliveryPoint p = City.DeliveryPoints[_pickup];
            return $"· Passageiro esperando em {p.Address} ({PlayerPos.DistanceTo(p.Position):0} m)\n  Pare a van do lado dele.{van}";
        }
        DeliveryPoint d = City.DeliveryPoints[_dest];
        string mood = _mood switch
        {
            Mood.Executivo => $"\n  Executivo: rádio {(Player?.CurrentVehicle?.Radio?.On == true ? "ligada ✓" : "DESLIGADA (R)")} · máx. {_maxSpeed:0}/60 km/h · batidas {_crashes}",
            Mood.Apressado => $"\n  Apressado: {_rideTime:0} s de corrida",
            _ => "\n  Tranquilo",
        };
        return $"► Levando passageiro para {d.Address} ({PlayerPos.DistanceTo(d.Position):0} m)\n  Corrida: $ {Fare()}{mood}{van}";
    }
}
