// Scripts/Vehicle/VanController.Rider.cs
// Pose de quem dirige (v0.10.1), vista em terceira pessoa e pelos outros jogadores no online:
//  - carros: mãos no volante (marcadores filhos do volante, então giram junto quando ele vira);
//  - moto: mãos nas manoplas e pés nas pedaleiras, corpo no banco.
// O boneco usa o IK do CharacterModel (TwoBoneIK3D); o banco ajusta a altura do corpo (SeatOffset).
using System.Collections.Generic;
using Godot;

namespace DeadFrequency;

public partial class VanController
{
    /// <summary>Todos os veículos da cena (o jogador remoto acha o veículo em que está sentado).</summary>
    public static readonly HashSet<VanController> All = new();

    private Node3D _riderHandL, _riderHandR, _riderFootL, _riderFootR;
    private bool _riderReady;

    public override void _EnterTree() => All.Add(this);
    public override void _ExitTree() => All.Remove(this);

    /// <summary>Deslocamento do corpo sentado em relação ao SeatMarker (cada veículo tem o banco numa altura).</summary>
    public Vector3 RiderSeatOffset => Kind == "moto" ? MotoSeat : Kind == "van" ? VanSeat : CarSeat;

    // Ajustados olhando o boneco em render (frente do banco = -Z do SeatMarker).
    public static Vector3 MotoSeat = new(0f, -0.16f, 0.02f);
    public static Vector3 CarSeat = new(0f, -0.44f, -0.1f);
    public static Vector3 VanSeat = new(0f, -0.2f, 0.1f);
    /// <summary>Pedais em relação ao banco (local do carro, frente +Z).</summary>
    public static float PedalDrop = -0.3f, PedalReach = 0.85f;

    private void EnsureRiderTargets()
    {
        if (_riderReady)
            return;
        _riderReady = true;
        if (Kind == "moto")
        {
            // Manoplas e pedaleiras (local da moto, frente +Z).
            _riderHandL = Marker(this, "ManoplaE", new Vector3(0.33f, 1.02f, 0.5f));
            _riderHandR = Marker(this, "ManoplaD", new Vector3(-0.33f, 1.02f, 0.5f));
            _riderFootL = Marker(this, "PedaleiraE", new Vector3(0.2f, 0.42f, 0.02f));
            _riderFootR = Marker(this, "PedaleiraD", new Vector3(-0.2f, 0.42f, 0.02f));
            return;
        }
        Node3D wheel = GetNodeOrNull<Node3D>("Cabin/SteeringWheel") ?? GetNodeOrNull<Node3D>("CabinSimple/SteeringWheel");
        if (wheel != null)
        {
            // Mãos nas "10 e 10" do volante; giram com ele.
            _riderHandL = Marker(wheel, "MaoE", new Vector3(0.15f, 0f, 0.05f));
            _riderHandR = Marker(wheel, "MaoD", new Vector3(-0.15f, 0f, 0.05f));
        }
        // Pés esticados para os pedais (no carro o banco é baixo: com a pose de cadeira as pernas atravessavam o assoalho).
        if (GetNodeOrNull<Node3D>("SeatMarker") is Node3D seat)
        {
            Vector3 s = seat.Position;
            _riderFootL = Marker(this, "PedalE", s + new Vector3(0.14f, PedalDrop, PedalReach));
            _riderFootR = Marker(this, "PedalD", s + new Vector3(-0.14f, PedalDrop, PedalReach));
        }
    }

    private static Node3D Marker(Node3D parent, string name, Vector3 pos)
    {
        var m = new Node3D { Name = name, Position = pos };
        parent.AddChild(m);
        return m;
    }

    /// <summary>Põe o boneco sentado na pose deste veículo (motorista).</summary>
    public void ApplyRiderPose(CharacterModel model)
    {
        if (model == null)
            return;
        EnsureRiderTargets();
        model.Straddle = Kind == "moto";
        model.SeatOffset = RiderSeatOffset;
        model.SetHandTargets(_riderHandL, _riderHandR);
        model.SetFootTargets(_riderFootL, _riderFootR);
    }

    /// <summary>Tira a pose de veículo (desceu ou virou carona).</summary>
    public static void ClearRiderPose(CharacterModel model)
    {
        if (model == null)
            return;
        model.SeatOffset = Vector3.Zero;
        model.Straddle = false;
        model.SetHandTargets(null, null);
        model.SetFootTargets(null, null);
    }

    /// <summary>Veículo mais perto de um ponto (até maxDist), para o jogador remoto que está dirigindo.</summary>
    public static VanController Nearest(Vector3 point, float maxDist = 2.5f)
    {
        VanController best = null;
        float bestD = maxDist * maxDist;
        foreach (VanController v in All)
        {
            if (!IsInstanceValid(v) || !v.IsInsideTree())
                continue;
            Node3D seat = v.GetNodeOrNull<Node3D>("SeatMarker");
            float d = (seat ?? v).GlobalPosition.DistanceSquaredTo(point);
            if (d < bestD)
            {
                bestD = d;
                best = v;
            }
        }
        return best;
    }
}
