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
    public const string TrackedObjectGroup = "streamed_world_objects";
    [Export(PropertyHint.Range, "0,5,1")]
    public int LoadRadius { get; set; } = 1;

    [Export(PropertyHint.Range, "1,8,1")]
    public int UnloadRadius { get; set; } = 2;

    [Export(PropertyHint.Range, "1,8,1")]
    public int BuildsPerFrame { get; set; } = 1;

    [Export(PropertyHint.Range, "1,8,1")]
    public int UnloadsPerFrame { get; set; } = 1;

    /// <summary>Intervalo entre construções de chunks durante o jogo. 2 = um chunk a cada dois frames.</summary>
    [Export(PropertyHint.Range, "1,6,1")]
    public int BuildIntervalFrames { get; set; } = 2;

    /// <summary>Evita varrer os grupos de objetos e alocar coleções em todos os frames.</summary>
    [Export(PropertyHint.Range, "1,30,1")]
    public int TrackedObjectScanIntervalFrames { get; set; } = 6;

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
    private readonly Dictionary<ulong, Vector3> _trackedObjectPins = new();
    private readonly HashSet<ulong> _activeTrackedIds = new();
    private readonly List<ulong> _staleTrackedIds = new();

    private WorldChunkCoordinate _lastCenter;
    private int _lastPinVersion = -1;
    private int _buildFrameCountdown;
    private int _trackedObjectScanCountdown;
    private bool _hasCenter;
    private bool _hasReportedChunkCounts;

    public override void _Ready()
    {
        if (City == null || !GodotObject.IsInstanceValid(City) ||
            Target == null || !GodotObject.IsInstanceValid(Target) ||
            City.WorldData == null || !City.UsesChunkStreaming)
        {
            GD.PushWarning("WorldChunkManager: mundo, alvo ou dados lógicos indisponíveis; streaming desativado.");
            SetProcess(false);
            return;
        }

        LoadRadius = Mathf.Max(0, LoadRadius);
        UnloadRadius = Mathf.Max(LoadRadius, UnloadRadius);
        RebuildQueues();
        _lastPinVersion = City.ChunkPinVersion;
        ProcessQueues();
    }

    /// <summary>Atualiza a fila quando uma missão reserva ou libera uma região distante.</summary>
    public void RequestRefresh()
    {
        if (!IsInsideTree() || City == null || !GodotObject.IsInstanceValid(City) ||
            City.WorldData == null || !City.UsesChunkStreaming ||
            Target == null || !GodotObject.IsInstanceValid(Target))
            return;

        RebuildQueues();
        _lastPinVersion = City.ChunkPinVersion;
        ProcessQueues();
    }

    public override void _Process(double delta)
    {
        if (City == null || !GodotObject.IsInstanceValid(City) ||
            City.WorldData == null || !City.UsesChunkStreaming ||
            Target == null || !GodotObject.IsInstanceValid(Target))
        {
            SetProcess(false);
            return;
        }

        bool pinsChanged = false;
        if (_trackedObjectScanCountdown > 0)
        {
            _trackedObjectScanCountdown--;
        }
        else
        {
            pinsChanged = RefreshTrackedObjectPins();
            _trackedObjectScanCountdown = Mathf.Max(0, TrackedObjectScanIntervalFrames - 1);
        }

        Vector3 targetPosition = GetTargetPosition();
        WorldChunkCoordinate center = City.WorldData.GetChunkCoordinate(targetPosition);
        if (!_hasCenter || center != _lastCenter || pinsChanged ||
            _lastPinVersion != City.ChunkPinVersion)
        {
            RebuildQueues();
            _lastPinVersion = City.ChunkPinVersion;
        }

        ProcessQueues();
    }

    /// <summary>
    /// Mantém o chão sob veículos, cargas e itens largados mesmo quando o jogador se afasta.
    /// A reserva acompanha o objeto entre chunks e é liberada quando ele é removido.
    /// </summary>
    private bool RefreshTrackedObjectPins()
    {
        bool changed = false;
        _activeTrackedIds.Clear();
        changed |= TrackGroup(VanController.GroupName, _activeTrackedIds);
        changed |= TrackGroup(CargoItem.GroupName, _activeTrackedIds);
        changed |= TrackGroup(WorldItem.GroupName, _activeTrackedIds);
        changed |= TrackGroup(TrackedObjectGroup, _activeTrackedIds);

        _staleTrackedIds.Clear();
        foreach (KeyValuePair<ulong, Vector3> entry in _trackedObjectPins)
        {
            if (!_activeTrackedIds.Contains(entry.Key))
                _staleTrackedIds.Add(entry.Key);
        }

        foreach (ulong instanceId in _staleTrackedIds)
        {
            if (_trackedObjectPins.TryGetValue(instanceId, out Vector3 previousPosition))
                City.UnpinChunkForPosition(previousPosition);
            _trackedObjectPins.Remove(instanceId);
            changed = true;
        }

        return changed;
    }

    private bool TrackGroup(string groupName, HashSet<ulong> activeIds)
    {
        bool changed = false;
        foreach (Node node in GetTree().GetNodesInGroup(groupName))
        {
            if (node is not Node3D spatial || !GodotObject.IsInstanceValid(spatial))
                continue;

            ulong instanceId = spatial.GetInstanceId();
            activeIds.Add(instanceId);
            Vector3 position = spatial.GlobalPosition;
            WorldChunkCoordinate next = City.WorldData.GetChunkCoordinate(position);

            if (_trackedObjectPins.TryGetValue(instanceId, out Vector3 previousPosition))
            {
                WorldChunkCoordinate previous = City.WorldData.GetChunkCoordinate(previousPosition);
                if (previous == next)
                    continue;

                City.UnpinChunkForPosition(previousPosition);
                _trackedObjectPins.Remove(instanceId);
                changed = true;
            }

            if (City.PinChunkForPosition(position))
            {
                _trackedObjectPins[instanceId] = position;
                changed = true;
            }
        }

        return changed;
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
            if (_desired.Add(chunk.Coordinate))
                prioritized.Add(chunk);
        }

        // Missões podem manter um chunk distante carregado enquanto houver objetos ativos nele.
        foreach (WorldChunkCoordinate pinned in City.GetPinnedChunkCoordinates())
        {
            if (!_desired.Add(pinned))
                continue;
            if (City.WorldData.TryGetChunk(pinned, out WorldChunkData chunk))
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
            if (Math.Max(dx, dz) <= UnloadRadius || _desired.Contains(loaded) || City.IsChunkPinned(loaded))
                continue;

            if (_queuedUnloads.Add(loaded))
                _unloadQueue.Enqueue(loaded);
        }
    }

    private void ProcessQueues()
    {
        bool chunksChanged = false;
        int unloads = Mathf.Max(1, UnloadsPerFrame);
        while (unloads-- > 0 && _unloadQueue.Count > 0)
        {
            WorldChunkCoordinate coordinate = _unloadQueue.Dequeue();
            _queuedUnloads.Remove(coordinate);

            int dx = Math.Abs(coordinate.X - _lastCenter.X);
            int dz = Math.Abs(coordinate.Z - _lastCenter.Z);
            if (Math.Max(dx, dz) > UnloadRadius && !_desired.Contains(coordinate) && !City.IsChunkPinned(coordinate))
            {
                City.UnloadChunkVisuals(coordinate);
                chunksChanged = true;
            }
        }

        if (_buildFrameCountdown > 0)
        {
            _buildFrameCountdown--;
        }
        else
        {
            int builds = Mathf.Max(1, BuildsPerFrame);
            while (builds-- > 0 && _loadQueue.Count > 0)
            {
                WorldChunkCoordinate coordinate = _loadQueue.Dequeue();
                _queuedLoads.Remove(coordinate);

                if (!_desired.Contains(coordinate) || City.IsChunkVisualized(coordinate))
                    continue;

                if (City.WorldData.TryGetChunk(coordinate, out WorldChunkData chunk))
                    chunksChanged |= City.BuildChunkVisuals(chunk);

                // Construções maiores são separadas por frames para evitar uma sequência
                // contínua de picos enquanto o jogador atravessa a cidade.
                if (BuildIntervalFrames > 1)
                {
                    _buildFrameCountdown = BuildIntervalFrames - 1;
                    break;
                }
            }
        }

        // Evita alocar uma lista e notificar observadores em todos os frames sem mudança.
        if (chunksChanged || !_hasReportedChunkCounts)
        {
            _hasReportedChunkCounts = true;
            ChunkCountsChanged?.Invoke(City.GetLoadedChunkCoordinates().Count, City.WorldData.Chunks.Count);
        }
    }
}
