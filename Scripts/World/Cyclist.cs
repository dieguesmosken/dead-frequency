using System.Collections.Generic;
using Godot;

namespace DeadFrequency;

public partial class Cyclist : Node3D
{
    [Export] public CityGenerator City { get; set; }
    [Export] public float Speed { get; set; } = 5f;
    [Export] public float LaneOffset { get; set; } = 0f;

    private readonly List<Vector3> _path = new();
    private readonly List<float> _segmentLengths = new();
    private float _loopSeconds;
    private Node3D[] _wheels;
    private CharacterModel _rider;
    private AudioStreamPlayer3D _bell;
    private PlayerController _player;
    private double _bellCooldown;

    private static AudioStreamWav _bellStream;

    public override void _Ready()
    {
        AddToGroup(WorldChunkManager.TrackedObjectGroup);
        if (City == null)
        {
            GD.PushWarning("Cyclist: defina City no Inspector.");
            return;
        }

        if (LaneOffset <= 0f)
            LaneOffset = City.RoadWidth * 0.5f - 0.625f;
        BuildRoute();
        BuildBicycle();
        Place(Time.GetUnixTimeFromSystem(), true);
    }

    private static Vector3 RightOf(Vector3 direction) => new(-direction.Z, 0f, direction.X);

    private void BuildRoute()
    {
        int ring = Mathf.Clamp(City.Radius - 1, 1, 3);
        float distance = City.Period * (ring + 0.5f);
        Vector3[] corners =
        {
            new(-distance, 0f, -distance),
            new(distance, 0f, -distance),
            new(distance, 0f, distance),
            new(-distance, 0f, distance),
        };

        for (int i = 0; i < corners.Length; i++)
        {
            Vector3 previousDirection = (corners[i] - corners[(i + 3) % corners.Length]).Normalized();
            Vector3 nextDirection = (corners[(i + 1) % corners.Length] - corners[i]).Normalized();
            _path.Add(corners[i] + RightOf(previousDirection) * LaneOffset + RightOf(nextDirection) * LaneOffset);
        }

        _loopSeconds = 0f;
        for (int i = 0; i < _path.Count; i++)
        {
            float length = _path[i].DistanceTo(_path[(i + 1) % _path.Count]);
            _segmentLengths.Add(length);
            _loopSeconds += length / Speed;
        }
    }

    private (Vector3 position, Vector3 direction) Sample(double time)
    {
        double remaining = time % _loopSeconds;
        if (remaining < 0)
            remaining += _loopSeconds;

        for (int i = 0; i < _path.Count; i++)
        {
            Vector3 from = _path[i];
            Vector3 direction = (_path[(i + 1) % _path.Count] - from).Normalized();
            float segmentTime = _segmentLengths[i] / Speed;
            if (remaining < segmentTime)
                return (from + direction * (float)remaining * Speed, direction);
            remaining -= segmentTime;
        }

        return (_path[0], Vector3.Right);
    }

    private void BuildBicycle()
    {
        var bike = new Node3D { Name = "Bicicleta" };
        AddChild(bike);

        var tire = new StandardMaterial3D { AlbedoColor = new Color(0.055f, 0.06f, 0.06f), Roughness = 0.9f };
        var frame = new StandardMaterial3D { AlbedoColor = new Color(0.12f, 0.32f, 0.19f), Roughness = 0.75f };
        var metal = new StandardMaterial3D { AlbedoColor = new Color(0.45f, 0.46f, 0.42f), Metallic = 0.45f, Roughness = 0.6f };
        var wheelMesh = new CylinderMesh { TopRadius = 0.5f, BottomRadius = 0.5f, Height = 0.08f, RadialSegments = 10, Rings = 1 };
        var hubMesh = new CylinderMesh { TopRadius = 0.5f, BottomRadius = 0.5f, Height = 1f, RadialSegments = 8, Rings = 1 };
        var tubeMesh = new CylinderMesh { TopRadius = 0.5f, BottomRadius = 0.5f, Height = 1f, RadialSegments = 6, Rings = 1 };

        _wheels = new[] { AddWheel(bike, -0.72f, wheelMesh, hubMesh, tire, metal), AddWheel(bike, 0.72f, wheelMesh, hubMesh, tire, metal) };

        Vector3 rearAxle = new(0f, 0.45f, 0.72f);
        Vector3 frontAxle = new(0f, 0.45f, -0.72f);
        Vector3 crank = new(0f, 0.43f, 0f);
        Vector3 seat = new(0f, 0.95f, 0.28f);
        Vector3 handlebar = new(0f, 1.02f, -0.56f);
        AddFrameBeam(bike, rearAxle, crank, tubeMesh, frame);
        AddFrameBeam(bike, crank, seat, tubeMesh, frame);
        AddFrameBeam(bike, seat, rearAxle, tubeMesh, frame);
        AddFrameBeam(bike, seat, handlebar, tubeMesh, frame);
        AddFrameBeam(bike, crank, frontAxle, tubeMesh, frame);
        AddFrameBeam(bike, handlebar, frontAxle, tubeMesh, metal);
        AddFrameBeam(bike, new Vector3(-0.3f, 1.02f, -0.56f), new Vector3(0.3f, 1.02f, -0.56f), tubeMesh, metal);
        AddFrameBeam(bike, new Vector3(0f, 0.92f, 0.28f), new Vector3(0f, 1f, 0.42f), tubeMesh, metal);

        _rider = CharacterModel.Create(
            new Color(0.78f, 0.16f, 0.12f),
            new Color(0.12f, 0.14f, 0.17f),
            new Color(0.68f, 0.47f, 0.33f));
        _rider.Name = "Ciclista";
        _rider.Position = new Vector3(0f, 0.42f, 0.05f);
        _rider.Scale = Vector3.One * 0.72f;
        bike.AddChild(_rider);

        _bell = new AudioStreamPlayer3D
        {
            Stream = _bellStream ??= CreateBell(),
            Bus = AudioManager.SfxBus,
            UnitSize = 6f,
            MaxDistance = 25f,
            VolumeDb = -2f,
            Position = new Vector3(0f, 1.05f, -0.58f),
        };
        AddChild(_bell);
    }

    private static Node3D AddWheel(Node3D bike, float z, CylinderMesh wheelMesh, CylinderMesh hubMesh, Material tire, Material metal)
    {
        var pivot = new Node3D { Position = new Vector3(0f, 0.45f, z) };
        bike.AddChild(pivot);
        pivot.AddChild(new MeshInstance3D
        {
            Mesh = wheelMesh,
            Rotation = new Vector3(0f, 0f, Mathf.Pi * 0.5f),
            MaterialOverride = tire,
        });
        foreach (float side in new[] { -1f, 1f })
        {
            pivot.AddChild(new MeshInstance3D
            {
                Mesh = hubMesh,
                Scale = new Vector3(0.04f, 0.08f, 0.08f),
                Position = new Vector3(side * 0.05f, 0f, 0f),
                Rotation = new Vector3(0f, 0f, Mathf.Pi * 0.5f),
                MaterialOverride = metal,
            });
        }
        return pivot;
    }

    private static void AddFrameBeam(Node3D bike, Vector3 from, Vector3 to, CylinderMesh mesh, Material material)
    {
        Vector3 direction = to - from;
        Vector3 axis = Vector3.Up.Cross(direction.Normalized());
        Basis rotation = axis.LengthSquared() < 0.001f
            ? Basis.Identity
            : new Basis(axis.Normalized(), Vector3.Up.AngleTo(direction.Normalized()));
        Basis scale = Basis.FromScale(new Vector3(0.055f, direction.Length(), 0.055f));
        bike.AddChild(new MeshInstance3D
        {
            Mesh = mesh,
            Transform = new Transform3D(rotation * scale, (from + to) * 0.5f),
            MaterialOverride = material,
        });
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_path.Count == 0)
            return;

        double time = Time.GetUnixTimeFromSystem();
        Place(time, false);
        float dt = (float)delta;
        float spin = Mathf.PosMod((float)(time * Speed / 0.42 % Mathf.Tau), Mathf.Tau);
        foreach (Node3D wheel in _wheels)
            wheel.Rotation = new Vector3(spin, 0f, 0f);
        _rider.Animate(dt, 1.8f);
        CheckPlayerAhead(delta);
    }

    private void Place(double time, bool snap)
    {
        (Vector3 position, _) = Sample(time);
        (Vector3 ahead, _) = Sample(time + 0.6);
        Vector3 look = ahead - position;
        look.Y = 0f;
        Transform3D current = GlobalTransform;
        Basis target = look.LengthSquared() > 0.01f ? Basis.LookingAt(look.Normalized(), Vector3.Up) : current.Basis;
        Basis basis = snap ? target : current.Basis.Orthonormalized().Slerp(target, 0.15f);
        GlobalTransform = new Transform3D(basis, position);
    }

    private void CheckPlayerAhead(double delta)
    {
        _bellCooldown -= delta;
        if (_bellCooldown > 0)
            return;
        if (_player == null || !IsInstanceValid(_player))
            _player = GetTree().CurrentScene?.GetNodeOrNull<PlayerController>("Player");
        if (_player == null || _player.State != PlayerState.Walking)
            return;

        Vector3 relative = GlobalTransform.AffineInverse() * _player.GlobalPosition;
        if (relative.Y < -1f || relative.Y > 3.5f || Mathf.Abs(relative.X) > 3f || relative.Z >= -1.5f || relative.Z < -12f)
            return;

        _bellCooldown = 4.5;
        _bell.Play();
    }

    private static AudioStreamWav CreateBell()
    {
        const int rate = 22050;
        int sampleCount = rate * 2 / 3;
        var data = new byte[sampleCount * 2];
        for (int i = 0; i < sampleCount; i++)
        {
            float time = (float)i / rate;
            float attack = Mathf.Min(1f, time * 90f);
            float decay = Mathf.Exp(-time * 6f) * attack;
            float sample = (Mathf.Sin(time * Mathf.Tau * 1900f) * 0.58f
                + Mathf.Sin(time * Mathf.Tau * 2670f) * 0.32f) * decay * 0.72f;
            short value = (short)(Mathf.Clamp(sample, -1f, 1f) * 30000f);
            data[i * 2] = (byte)(value & 0xff);
            data[i * 2 + 1] = (byte)((value >> 8) & 0xff);
        }

        return new AudioStreamWav
        {
            Format = AudioStreamWav.FormatEnum.Format16Bits,
            MixRate = rate,
            Data = data,
        };
    }
}
