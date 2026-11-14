// Scripts/Vehicle/WheelSpin.cs
// Faz as rodas girarem de verdade conforme o carro anda (v0.10.1). Funciona com qualquer veículo:
// mede quanto o pai andou para a frente a cada quadro e gira cada "pivô de roda" (Node3D com o meta
// wheel_radius, criado por CarModels.AddWheel) pelo ângulo certo (distância / raio).
// Serve para carros dirigíveis (física), carros pelo relógio (carro de som, carro do ovo) e o que mais andar.
using System.Collections.Generic;
using Godot;

namespace DeadFrequency;

public partial class WheelSpin : Node
{
    public const string RadiusMeta = "wheel_radius";

    private Node3D _car;
    private readonly List<(Node3D Pivot, float Radius)> _wheels = new();
    private Vector3 _last;
    private bool _started;

    /// <summary>Põe um WheelSpin no carro (se ainda não tiver) e devolve ele.</summary>
    public static WheelSpin Attach(Node3D car)
    {
        foreach (Node c in car.GetChildren())
            if (c is WheelSpin existing)
                return existing;
        var spin = new WheelSpin { Name = "WheelSpin" };
        car.AddChild(spin);
        return spin;
    }

    public override void _Ready()
    {
        _car = GetParent() as Node3D;
        Refresh();
    }

    /// <summary>Procura de novo os pivôs (chame se as rodas forem criadas depois).</summary>
    public void Refresh()
    {
        _wheels.Clear();
        if (_car == null)
            return;
        foreach (Node n in _car.FindChildren("*", "Node3D", true, false))
            if (n is Node3D p && p.HasMeta(RadiusMeta))
                _wheels.Add((p, Mathf.Max(0.05f, (float)p.GetMeta(RadiusMeta))));
        _started = false;
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_car == null || _wheels.Count == 0 || !_car.IsInsideTree())
            return;
        Vector3 pos = _car.GlobalPosition;
        if (!_started)
        {
            _last = pos;
            _started = true;
            return;
        }
        Vector3 moved = pos - _last;
        _last = pos;
        if (moved.LengthSquared() > 25f)
            return; // teleporte (respawn, troca de fase): não gira a roda à toa
        // Frente dos carros = -Z (convenção dos CarModels).
        float forward = moved.Dot(-_car.GlobalTransform.Basis.Z);
        if (Mathf.Abs(forward) < 0.0001f)
            return;
        foreach (var (pivot, radius) in _wheels)
        {
            Vector3 r = pivot.Rotation;
            r.X = Mathf.Wrap(r.X - forward / radius, -Mathf.Pi, Mathf.Pi);
            pivot.Rotation = r;
        }
    }
}
