using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Godot;

namespace DeadFrequency;

/// <summary>Coordenada inteira de um chunk no plano X/Z.</summary>
public readonly record struct WorldChunkCoordinate(int X, int Z);

/// <summary>
/// Descrição lógica de um chunk. Não contém Nodes nem recursos gráficos e pode ser
/// calculada antes de qualquer construção da SceneTree.
/// </summary>
public sealed class WorldChunkData
{
    public WorldChunkCoordinate Coordinate { get; }
    public ulong Seed { get; }
    public Vector3 Center { get; }
    public Vector2 Size { get; }

    internal WorldChunkData(WorldChunkCoordinate coordinate, ulong seed, Vector3 center, Vector2 size)
    {
        Coordinate = coordinate;
        Seed = seed;
        Center = center;
        Size = size;
    }

    public Vector3 Min => Center - new Vector3(Size.X * 0.5f, 0f, Size.Y * 0.5f);
    public Vector3 Max => Center + new Vector3(Size.X * 0.5f, 0f, Size.Y * 0.5f);
}

/// <summary>Ponto lógico de início de uma profissão, sem depender de um Job ou Node visual.</summary>
public readonly record struct WorldSpawnPoint(string Profession, Vector3 Position, float Yaw);

/// <summary>Ponto de entrega lógico de um quarteirão, calculado sem objetos da SceneTree.</summary>
public readonly record struct WorldDeliveryPointData(int BlockX, int BlockZ, Vector3 Position, string Address);

public readonly record struct WorldGasSpotData(Vector3 Position, string Brand);
public readonly record struct WorldNoParkingZoneData(Vector3 Center, Vector3 Direction, float HalfLength, WorldChunkCoordinate Owner);
public readonly record struct WorldMarketShelfSlotData(Vector3 Position, float RotationY);

/// <summary>Dados lógicos dos pontos usados pelos sistemas de profissão.</summary>
public sealed class WorldServiceData
{
    public IReadOnlyList<Vector3> DumpsterSpots { get; }
    public IReadOnlyList<WorldGasSpotData> GasSpots { get; }
    public IReadOnlyList<WorldMarketShelfSlotData> MarketShelfSlots { get; }
    public IReadOnlyList<WorldNoParkingZoneData> NoParkingZones { get; }
    public Vector3 MarketStockPoint { get; }
    public Vector3 MarketEntrance { get; }
    public Vector3 RadioConsolePoint { get; }

    internal WorldServiceData(
        List<Vector3> dumpsterSpots,
        List<WorldGasSpotData> gasSpots,
        List<WorldMarketShelfSlotData> marketShelfSlots,
        List<WorldNoParkingZoneData> noParkingZones,
        Vector3 marketStockPoint,
        Vector3 marketEntrance,
        Vector3 radioConsolePoint)
    {
        DumpsterSpots = Array.AsReadOnly(dumpsterSpots.ToArray());
        GasSpots = Array.AsReadOnly(gasSpots.ToArray());
        MarketShelfSlots = Array.AsReadOnly(marketShelfSlots.ToArray());
        NoParkingZones = Array.AsReadOnly(noParkingZones.ToArray());
        MarketStockPoint = marketStockPoint;
        MarketEntrance = marketEntrance;
        RadioConsolePoint = radioConsolePoint;
    }

    public static WorldServiceData Empty() =>
        new(new List<Vector3>(), new List<WorldGasSpotData>(), new List<WorldMarketShelfSlotData>(),
            new List<WorldNoParkingZoneData>(), Vector3.Zero, Vector3.Zero, Vector3.Zero);
}

/// <summary>Tipo lógico de um quarteirão da cidade, sem dependência de geometria.</summary>
public enum WorldBlockKind
{
    Depot,
    FireStation,
    PoliceStation,
    RadioStation,
    Supermarket,
    ShoppingMall,
    ApartmentBuilding,
    VehicleDealer,
    WaterTower,
    GasStation,
    CarWash,
    Park,
    ParkingLot,
    Buildings,
}

/// <summary>Layout lógico de um prédio comum, calculado antes da criação dos Nodes.</summary>
public readonly record struct WorldBuildingPlan(
    Vector3 BaseCenter,
    float Width,
    float Depth,
    float Height,
    Vector3 Front,
    bool HasStorefront,
    ulong VisualSeed,
    ulong DecorationSeed,
    int StoreBrandIndex);

public readonly record struct WorldParkTreePlan(Vector3 Position, float Scale, string Model, float RotationY);
public readonly record struct WorldParkBenchPlan(Vector3 Position, float RotationY);
public readonly record struct WorldParkingCarPlan(Vector3 Position, float RotationY, ulong VisualSeed);

/// <summary>Plano lógico de um sítio rural, incluindo destino de entrega e seed visual própria.</summary>
public readonly record struct WorldFarmTreePlan(
    Vector3 Position,
    bool Pine,
    bool Dead,
    int VariantIndex,
    float RotationY,
    float Scale,
    ulong VisualSeed);

public readonly record struct WorldFarmReedPlan(Vector3 Position, float Height);
public readonly record struct WorldFarmUndergrowthPlan(Vector3 Position, string Model, float Scale, float RotationY, bool IsRock);

public readonly record struct WorldFarmSitePlan(
    Vector3 GatePosition,
    Vector3 TowardRoad,
    Vector3 Center,
    float Width,
    float Depth,
    float OccupiedRadius,
    string Name,
    string Address,
    ulong VisualSeed);


/// <summary>Plano determinístico de um quarteirão, com streams aleatórios independentes.</summary>
public sealed class WorldBlockData
{
    public WorldChunkCoordinate Coordinate { get; }
    public WorldBlockKind Kind { get; }
    public ulong VisualSeed { get; }
    public ulong DecorationSeed { get; }
    public IReadOnlyList<WorldBuildingPlan> Buildings { get; }
    public IReadOnlyList<WorldParkTreePlan> ParkTrees { get; }
    public IReadOnlyList<WorldParkBenchPlan> ParkBenches { get; }
    public IReadOnlyList<WorldParkingCarPlan> ParkingCars { get; }

    internal WorldBlockData(
        WorldChunkCoordinate coordinate,
        WorldBlockKind kind,
        ulong visualSeed,
        ulong decorationSeed,
        List<WorldBuildingPlan> buildings = null,
        List<WorldParkTreePlan> parkTrees = null,
        List<WorldParkBenchPlan> parkBenches = null,
        List<WorldParkingCarPlan> parkingCars = null)
    {
        Coordinate = coordinate;
        Kind = kind;
        VisualSeed = visualSeed;
        DecorationSeed = decorationSeed;
        Buildings = Array.AsReadOnly((buildings ?? new List<WorldBuildingPlan>()).ToArray());
        ParkTrees = Array.AsReadOnly((parkTrees ?? new List<WorldParkTreePlan>()).ToArray());
        ParkBenches = Array.AsReadOnly((parkBenches ?? new List<WorldParkBenchPlan>()).ToArray());
        ParkingCars = Array.AsReadOnly((parkingCars ?? new List<WorldParkingCarPlan>()).ToArray());
    }
}

/// <summary>Progresso de uma etapa real do preparo lógico do mundo.</summary>
public readonly record struct WorldGenerationProgress(string Stage, int Completed, int Total)
{
    public float Ratio => Total <= 0 ? 1f : Mathf.Clamp((float)Completed / Total, 0f, 1f);
}

/// <summary>
/// Resultado da primeira etapa de preparação do mundo. Nesta versão contém o índice espacial
/// e os pontos de spawn conhecidos; a composição procedural detalhada será migrada por etapas.
/// </summary>
public sealed class WorldGenerationData
{
    private readonly Dictionary<WorldChunkCoordinate, WorldChunkData> _chunkIndex;
    private readonly Dictionary<WorldChunkCoordinate, WorldBlockData> _blockIndex;
    private readonly IReadOnlyDictionary<string, WorldSpawnPoint> _spawnPoints;

    public int Seed { get; }
    public int GeneratorVersion { get; }
    public WorldBiome Biome { get; }
    public float ChunkSize { get; }
    public bool UsesCenteredGrid { get; }
    public IReadOnlyList<WorldChunkData> Chunks { get; }
    public IReadOnlyList<WorldBlockData> Blocks { get; }
    public IReadOnlyList<WorldDeliveryPointData> DeliveryPoints { get; }
    public IReadOnlyList<WorldFarmSitePlan> FarmSites { get; }
    public IReadOnlyList<WorldFarmTreePlan> FarmTrees { get; }
    public IReadOnlyList<WorldFarmReedPlan> FarmReeds { get; }
    public IReadOnlyList<WorldFarmUndergrowthPlan> FarmUndergrowth { get; }
    public WorldServiceData Services { get; }
    public IReadOnlyDictionary<string, WorldSpawnPoint> SpawnPoints => _spawnPoints;

    internal WorldGenerationData(
        int seed,
        int generatorVersion,
        WorldBiome biome,
        float chunkSize,
        bool usesCenteredGrid,
        List<WorldChunkData> chunks,
        List<WorldBlockData> blocks,
        List<WorldDeliveryPointData> deliveryPoints,
        List<WorldFarmSitePlan> farmSites,
        List<WorldFarmTreePlan> farmTrees,
        List<WorldFarmReedPlan> farmReeds,
        List<WorldFarmUndergrowthPlan> farmUndergrowth,
        WorldServiceData services,
        Dictionary<string, WorldSpawnPoint> spawnPoints)
    {
        Seed = seed;
        GeneratorVersion = generatorVersion;
        Biome = biome;
        ChunkSize = chunkSize;
        UsesCenteredGrid = usesCenteredGrid;
        Chunks = Array.AsReadOnly(chunks.ToArray());
        Blocks = Array.AsReadOnly(blocks.ToArray());
        _blockIndex = new Dictionary<WorldChunkCoordinate, WorldBlockData>(blocks.Count);
        foreach (WorldBlockData block in blocks)
            _blockIndex[block.Coordinate] = block;
        DeliveryPoints = Array.AsReadOnly(deliveryPoints.ToArray());
        FarmSites = Array.AsReadOnly(farmSites.ToArray());
        FarmTrees = Array.AsReadOnly(farmTrees.ToArray());
        FarmReeds = Array.AsReadOnly(farmReeds.ToArray());
        FarmUndergrowth = Array.AsReadOnly(farmUndergrowth.ToArray());
        Services = services ?? WorldServiceData.Empty();
        _chunkIndex = new Dictionary<WorldChunkCoordinate, WorldChunkData>(chunks.Count);
        foreach (WorldChunkData chunk in chunks)
            _chunkIndex[chunk.Coordinate] = chunk;
        _spawnPoints = new ReadOnlyDictionary<string, WorldSpawnPoint>(
            new Dictionary<string, WorldSpawnPoint>(spawnPoints, StringComparer.Ordinal));
    }

    public bool TryGetProfessionSpawn(string profession, out WorldSpawnPoint spawn) =>
        _spawnPoints.TryGetValue(profession, out spawn);

    /// <summary>Resolve os chunks iniciais a partir do spawn calculado da profissão.</summary>
    public IReadOnlyList<WorldChunkData> GetInitialChunks(
        string profession,
        Vector3 fallbackPosition,
        int radius = 1)
    {
        Vector3 spawnPosition = _spawnPoints.TryGetValue(profession, out WorldSpawnPoint spawn)
            ? spawn.Position
            : fallbackPosition;
        return GetChunksAround(spawnPosition, radius);
    }

    public bool TryGetChunk(WorldChunkCoordinate coordinate, out WorldChunkData chunk) =>
        _chunkIndex.TryGetValue(coordinate, out chunk);

    public bool TryGetBlock(WorldChunkCoordinate coordinate, out WorldBlockData block) =>
        _blockIndex.TryGetValue(coordinate, out block);

    /// <summary>
    /// Converte uma posição do mundo para a coordenada de chunk.
    /// Cidade: chunks centrados nos quarteirões. Rancho: grade iniciada na borda negativa.
    /// </summary>
    public WorldChunkCoordinate GetChunkCoordinate(Vector3 position)
    {
        if (UsesCenteredGrid)
        {
            int x = Mathf.FloorToInt((position.X + ChunkSize * 0.5f) / ChunkSize);
            int z = Mathf.FloorToInt((position.Z + ChunkSize * 0.5f) / ChunkSize);
            return new WorldChunkCoordinate(x, z);
        }

        return new WorldChunkCoordinate(
            Mathf.FloorToInt(position.X / ChunkSize),
            Mathf.FloorToInt(position.Z / ChunkSize));
    }

    /// <summary>Retorna os chunks existentes ao redor de uma posição, em ordem determinística.</summary>
    public IReadOnlyList<WorldChunkData> GetChunksAround(Vector3 position, int radius = 1)
    {
        int safeRadius = Mathf.Max(0, radius);
        WorldChunkCoordinate center = GetChunkCoordinate(position);
        var result = new List<WorldChunkData>((safeRadius * 2 + 1) * (safeRadius * 2 + 1));

        for (int x = center.X - safeRadius; x <= center.X + safeRadius; x++)
        {
            for (int z = center.Z - safeRadius; z <= center.Z + safeRadius; z++)
            {
                if (_chunkIndex.TryGetValue(new WorldChunkCoordinate(x, z), out WorldChunkData chunk))
                    result.Add(chunk);
            }
        }

        return Array.AsReadOnly(result.ToArray());
    }
}
