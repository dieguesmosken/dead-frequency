using System;
using System.Collections.Generic;
using Godot;

namespace DeadFrequency;

/// <summary>
/// Mantém os chunks próximos ao jogador carregados. A fila é processada na thread principal,
/// em lotes pequenos, para evitar criar grandes quantidades de Nodes no mesmo frame.
/// </summary>
public partial class WorldChunkManager : Node
{
    [Export(PropertyHint.Range, "0,5,1")]
    public int LoadRadius { get; set; } = 1;

    [Export(PropertyHint.Range, "1,8,1")]
    public int UnloadRadius { get; set; } = 2;

    [Export(PropertyHint.Range, "1,8,1")]
    public int BuildsPerFrame { get; set; } = 1;

    [Export(PropertyHint.Range, "1,8,1")]
    public int UnloadsPerFrame { get; set; } = 1;

    public CityGenerator City { get; set; }
    public Node3D Target { get; set; }
    public Vector3 InitialPosition { get; set; }
    public bool HasInitialPosition { get; set; }

    public event Action<int, int> ChunkCountsChanged;

    private readonly Queue<WorldChunkCoordinate> _loadQueue = new();
    private readonly Queue<WorldChunkCoordinate> _unloadQueue = new();
    private readonly HashSet<WorldChunkCoordinate> _desired = new();
    private readonly HashSet<WorldChunkCoordinate> _queuedLoads = new();
    private readonly HashSet<WorldChunkCoordinate> _queuedUnloads = new();

    private WorldChunkCoordinate _lastCenter;
    private bool _hasCenter;

    public override void _Ready()
    {
        if (City == null || !GodotObject.IsInstanceValid(City) ||
            Target == null || !GodotObject.IsInstanceValid(Target) ||
            City.WorldData == null || City.WorldData.Biome != WorldBiome.City)
        {
            GD.PushWarning("WorldChunkManager: cidade, alvo ou dados lógicos indisponíveis; streaming desativado.");
            SetProcess(false);
            return;
        }

        LoadRadius = Mathf.Max(0, LoadRadius);
        UnloadRadius = Mathf.Max(LoadRadius + 1, UnloadRadius);
        RebuildQueues();
        ProcessQueues();
    }

    public override void _Process(double delta)
    {
        if (City == null || !GodotObject.IsInstanceValid(City) ||
            Target == null || !GodotObject.IsInstanceValid(Target))
            return;

        Vector3 targetPosition = GetTargetPosition();
        WorldChunkCoordinate center = City.WorldData.GetChunkCoordinate(targetPosition);
        if (!_hasCenter || center != _lastCenter)
            RebuildQueues();

        ProcessQueues();
    }

    private Vector3 GetTargetPosition()
    {
        if (HasInitialPosition)
        {
            // Espera o ProfessionManager aplicar o teleporte inicial antes de seguir o player real.
            if (Target.GlobalPosition.DistanceTo(InitialPosition) <= 2f)
                HasInitialPosition = false;
            else
                return InitialPosition;
        }
        return Target.GlobalPosition;
    }

    private void RebuildQueues()
    {
        _hasCenter = true;
        Vector3 targetPosition = GetTargetPosition();
        _lastCenter = City.WorldData.GetChunkCoordinate(targetPosition);
        _desired.Clear();
        _loadQueue.Clear();
        _unloadQueue.Clear();
        _queuedLoads.Clear();
        _queuedUnloads.Clear();

        IReadOnlyList<WorldChunkData> nearby =
            City.WorldData.GetChunksAround(targetPosition, LoadRadius);
        var prioritized = new List<WorldChunkData>(nearby.Count);
        foreach (WorldChunkData chunk in nearby)
        {
            _desired.Add(chunk.Coordinate);
            prioritized.Add(chunk);
        }

        prioritized.Sort((a, b) =>
            targetPosition.DistanceSquaredTo(a.Center)
                .CompareTo(targetPosition.DistanceSquaredTo(b.Center)));

        foreach (WorldChunkData chunk in prioritized)
        {
            if (City.IsChunkVisualized(chunk.Coordinate))
                continue;
            if (_queuedLoads.Add(chunk.Coordinate))
                _loadQueue.Enqueue(chunk.Coordinate);
        }

        foreach (WorldChunkCoordinate loaded in City.GetLoadedChunkCoordinates())
        {
            int dx = Math.Abs(loaded.X - _lastCenter.X);
            int dz = Math.Abs(loaded.Z - _lastCenter.Z);
            if (Math.Max(dx, dz) <= UnloadRadius || _desired.Contains(loaded))
                continue;

            if (_queuedUnloads.Add(loaded))
                _unloadQueue.Enqueue(loaded);
        }
    }

    private void ProcessQueues()
    {
        int unloads = Mathf.Max(1, UnloadsPerFrame);
        while (unloads-- > 0 && _unloadQueue.Count > 0)
        {
            WorldChunkCoordinate coordinate = _unloadQueue.Dequeue();
            _queuedUnloads.Remove(coordinate);

            int dx = Math.Abs(coordinate.X - _lastCenter.X);
            int dz = Math.Abs(coordinate.Z - _lastCenter.Z);
            if (Math.Max(dx, dz) > UnloadRadius && !_desired.Contains(coordinate))
                City.UnloadChunkVisuals(coordinate);
        }

        int builds = Mathf.Max(1, BuildsPerFrame);
        while (builds-- > 0 && _loadQueue.Count > 0)
        {
            WorldChunkCoordinate coordinate = _loadQueue.Dequeue();
            _queuedLoads.Remove(coordinate);

            if (!_desired.Contains(coordinate) || City.IsChunkVisualized(coordinate))
                continue;

            if (City.WorldData.TryGetChunk(coordinate, out WorldChunkData chunk))
                City.BuildChunkVisuals(chunk);
        }

        ChunkCountsChanged?.Invoke(City.GetLoadedChunkCoordinates().Count, City.WorldData.Chunks.Count);
    }
}
