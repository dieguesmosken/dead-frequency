// Scripts/World/Pedestrians.cs
// Pedestres andando pelas calçadas, dando a volta nos quarteirões.
// A posição vem do relógio do sistema + seed da cidade: online todos veem as mesmas pessoas
// nos mesmos lugares sem tráfego de rede. Não têm colisão (são figurantes), mas desviam de postes,
// placas, orelhões, bancos, caçambas, pontos de ônibus e do jogador, andando um pouco de lado.
using System.Collections.Generic;
using Godot;

namespace DeadFrequency;

public partial class Pedestrians : Node3D
{
    [Export] public CityGenerator City { get; set; }
    [Export(PropertyHint.Range, "0,80,1")] public int Count { get; set; } = 30;

    private sealed class Walker
    {
        public CharacterModel Model;
        public Vector3 Center;
        public float Half;
        public float Speed;
        public float Phase;
        public int Direction;
        public float Offset;
        public float CurbDistance;
        public List<Vector3> Obstacles;
    }

    private readonly Dictionary<Vector2I, List<Vector3>> _obstacles = new();
    private PlayerController _player;

    private static readonly Color[] Shirts =
    {
        new(0.6f, 0.15f, 0.12f), new(0.15f, 0.3f, 0.55f), new(0.85f, 0.8f, 0.7f), new(0.2f, 0.45f, 0.25f),
        new(0.5f, 0.4f, 0.15f), new(0.35f, 0.2f, 0.4f), new(0.1f, 0.1f, 0.12f), new(0.75f, 0.45f, 0.2f),
    };
    private static readonly Color[] Skins =
    {
        new(0.9f, 0.75f, 0.6f), new(0.78f, 0.58f, 0.44f), new(0.55f, 0.38f, 0.26f), new(0.38f, 0.25f, 0.17f),
    };

    private readonly List<Walker> _walkers = new();
    private readonly Dictionary<WorldChunkCoordinate, List<Walker>> _chunkWalkers = new();
    private bool _streamingMode;
    private const uint PedestrianStream = 0x50454453;

    public override void _Ready()
    {
        if (City == null)
        {
            GD.PushWarning("Pedestrians: defina City no Inspector.");
            return;
        }

        if (City.UsesChunkStreaming)
        {
            _streamingMode = true;
            City.ChunkVisualized += OnChunkVisualized;
            City.ChunkUnloaded += OnChunkUnloaded;
            foreach (WorldChunkCoordinate coordinate in City.GetLoadedChunkCoordinates())
                OnChunkVisualized(coordinate);
            return;
        }

        // Obstáculos de calçada separados por quarteirão.
        foreach (Vector3 o in City.SidewalkObstacles)
        {
            var key = new Vector2I(Mathf.RoundToInt(o.X / City.Period), Mathf.RoundToInt(o.Z / City.Period));
            if (!_obstacles.TryGetValue(key, out List<Vector3> list))
                _obstacles[key] = list = new List<Vector3>();
            list.Add(o);
        }

        var rng = new RandomNumberGenerator { Seed = (ulong)(City.Seed * 31 + 7) };
        for (int i = 0; i < Count; i++)
        {
            int bx, bz;
            do
            {
                bx = rng.RandiRange(-City.Radius, City.Radius);
                bz = rng.RandiRange(-City.Radius, City.Radius);
            } while (bx == 0 && bz == 0);

            var walker = new Walker
            {
                Center = new Vector3(bx * City.Period, City.CurbHeight, bz * City.Period),
                Half = City.BlockSize * 0.5f - rng.RandfRange(1.0f, 2.2f),
                Speed = rng.RandfRange(1.0f, 1.6f),
                Phase = rng.RandfRange(0f, 1000f),
                Direction = rng.Randf() < 0.5f ? 1 : -1,
                Model = CharacterModel.Create(
                    Shirts[rng.RandiRange(0, Shirts.Length - 1)],
                    Shirts[rng.RandiRange(0, Shirts.Length - 1)].Darkened(0.5f),
                    Skins[rng.RandiRange(0, Skins.Length - 1)]),
            };
            walker.Model.Chatty = true;
            walker.Model.Scale = Vector3.One * rng.RandfRange(0.9f, 1.08f);
            walker.CurbDistance = City.BlockSize * 0.5f - walker.Half;
            walker.Obstacles = _obstacles.TryGetValue(new Vector2I(bx, bz), out List<Vector3> obs) ? obs : new List<Vector3>();
            AddChild(walker.Model);
            _walkers.Add(walker);
        }
    }

    public override void _ExitTree()
    {
        if (City != null && GodotObject.IsInstanceValid(City))
        {
            City.ChunkVisualized -= OnChunkVisualized;
            City.ChunkUnloaded -= OnChunkUnloaded;
        }
    }

    private void OnChunkVisualized(WorldChunkCoordinate coordinate)
    {
        if (!_streamingMode || _chunkWalkers.ContainsKey(coordinate) ||
            (coordinate.X == 0 && coordinate.Z == 0))
            return;

        int totalBlocks = Mathf.Max(1, (City.Radius * 2 + 1) * (City.Radius * 2 + 1) - 1);
        float expectedPerBlock = (float)Mathf.Max(0, Count) / totalBlocks;
        int walkersInBlock = Mathf.FloorToInt(expectedPerBlock);
        float remainder = expectedPerBlock - walkersInBlock;

        var rng = new RandomNumberGenerator
        {
            Seed = WorldDataGenerator.DeriveChunkSeed(City.Seed, coordinate, PedestrianStream),
        };
        if (rng.Randf() < remainder)
            walkersInBlock++;

        var obstacles = new List<Vector3>();
        foreach (Vector3 obstacle in City.SidewalkObstacles)
        {
            var key = new Vector2I(
                Mathf.RoundToInt(obstacle.X / City.Period),
                Mathf.RoundToInt(obstacle.Z / City.Period));
            if (key.X == coordinate.X && key.Y == coordinate.Z)
                obstacles.Add(obstacle);
        }

        var spawned = new List<Walker>(walkersInBlock);
        for (int i = 0; i < walkersInBlock; i++)
        {
            var walker = new Walker
            {
                Center = new Vector3(coordinate.X * City.Period, City.CurbHeight, coordinate.Z * City.Period),
                Half = City.BlockSize * 0.5f - rng.RandfRange(1.0f, 2.2f),
                Speed = rng.RandfRange(1.0f, 1.6f),
                Phase = rng.RandfRange(0f, 1000f),
                Direction = rng.Randf() < 0.5f ? 1 : -1,
                Model = CharacterModel.Create(
                    Shirts[rng.RandiRange(0, Shirts.Length - 1)],
                    Shirts[rng.RandiRange(0, Shirts.Length - 1)].Darkened(0.5f),
                    Skins[rng.RandiRange(0, Skins.Length - 1)]),
                Obstacles = obstacles,
            };
            walker.Model.Chatty = true;
            walker.Model.Scale = Vector3.One * rng.RandfRange(0.9f, 1.08f);
            walker.CurbDistance = City.BlockSize * 0.5f - walker.Half;
            AddChild(walker.Model);
            _walkers.Add(walker);
            spawned.Add(walker);
        }

        _chunkWalkers[coordinate] = spawned;
    }

    private void OnChunkUnloaded(WorldChunkCoordinate coordinate)
    {
        if (!_chunkWalkers.Remove(coordinate, out List<Walker> walkers))
            return;

        foreach (Walker walker in walkers)
        {
            _walkers.Remove(walker);
            if (walker.Model != null && GodotObject.IsInstanceValid(walker.Model))
                walker.Model.QueueFree();
        }
    }

    /// <summary>Ponto no contorno do quarteirão (retângulo) a partir da distância percorrida.</summary>
    private static (Vector3 position, Vector3 forward, Vector3 outward) OnLoop(Walker w, float distance)
    {
        float side = w.Half * 2f;
        float perimeter = side * 4f;
        float d = Mathf.PosMod(distance * w.Direction, perimeter);
        int edge = (int)(d / side);
        float along = d - edge * side;
        Vector3 start, dir, outward;
        switch (edge)
        {
            case 0: start = new Vector3(-w.Half, 0f, -w.Half); dir = Vector3.Right; outward = Vector3.Forward; break;
            case 1: start = new Vector3(w.Half, 0f, -w.Half); dir = Vector3.Back; outward = Vector3.Right; break;
            case 2: start = new Vector3(w.Half, 0f, w.Half); dir = Vector3.Left; outward = Vector3.Back; break;
            default: start = new Vector3(-w.Half, 0f, w.Half); dir = Vector3.Forward; outward = Vector3.Left; break;
        }
        return (w.Center + start + dir * along, dir * w.Direction, outward);
    }

    public override void _Process(double delta)
    {
        float time = (float)(Time.GetUnixTimeFromSystem() % 100000.0);
        float dt = (float)delta;
        if (_player == null || !IsInstanceValid(_player))
            _player = GetTree().CurrentScene?.GetNodeOrNull<PlayerController>("Player");
        Vector3 playerPos = _player != null && _player.State == PlayerState.Walking ? _player.GlobalPosition : new Vector3(1e6f, 0f, 1e6f);

        foreach (Walker w in _walkers)
        {
            (Vector3 basePos, Vector3 forward, Vector3 outward) = OnLoop(w, w.Phase + time * w.Speed);

            // Desvio: procura o que está logo à frente na faixa por onde ele anda e passa do lado com mais espaço.
            float maxOut = w.CurbDistance - 0.35f;
            float maxIn = 2.7f - w.CurbDistance;
            float target = 0f;
            foreach (Vector3 o in w.Obstacles)
                target = Avoid(basePos, forward, outward, o, 0.75f, w.Offset, maxOut, maxIn, target);
            if (playerPos.DistanceSquaredTo(basePos) < 25f)
                target = Avoid(basePos, forward, outward, playerPos, 0.9f, w.Offset, maxOut, maxIn, target);
            float previous = w.Offset;
            w.Offset = Mathf.MoveToward(w.Offset, target, 1.8f * dt);

            w.Model.Position = basePos + outward * w.Offset;
            // Olha um pouco para o lado enquanto desvia.
            Vector3 facing = forward * w.Speed + outward * ((w.Offset - previous) / Mathf.Max(dt, 0.001f));
            float targetYaw = Mathf.Atan2(-facing.X, -facing.Z);
            w.Model.Rotation = new Vector3(0f, Mathf.LerpAngle(w.Model.Rotation.Y, targetYaw, 1f - Mathf.Exp(-10f * dt)), 0f);
            w.Model.Animate(dt, w.Speed);
        }
    }

    /// <summary>Se o obstáculo está à frente e na mesma linha, devolve o deslocamento lateral para passar por ele.</summary>
    private static float Avoid(Vector3 basePos, Vector3 forward, Vector3 outward, Vector3 obstacle, float radius,
        float current, float maxOut, float maxIn, float target)
    {
        Vector3 d = obstacle - basePos;
        d.Y = 0f;
        float along = d.Dot(forward);
        if (along < -0.6f || along > 2.6f)
            return target;
        float lat = d.Dot(outward);
        if (Mathf.Abs(lat - current) > radius + 0.3f && Mathf.Abs(lat - target) > radius + 0.3f)
            return target;
        float inward = lat - radius - 0.35f;
        float outwardSide = lat + radius + 0.35f;
        bool inOk = inward >= -maxIn;
        bool outOk = outwardSide <= maxOut;
        float choice;
        if (inOk && outOk)
            choice = Mathf.Abs(inward) < Mathf.Abs(outwardSide) ? inward : outwardSide;
        else if (inOk)
            choice = inward;
        else if (outOk)
            choice = outwardSide;
        else
            choice = Mathf.Clamp(lat > 0 ? -maxIn : maxOut, -maxIn, maxOut);
        // Mantém o desvio que já estava maior (vários obstáculos seguidos).
        return Mathf.Abs(choice) > Mathf.Abs(target) ? choice : target;
    }
}
