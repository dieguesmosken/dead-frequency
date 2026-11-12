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
    private readonly IReadOnlyDictionary<string, WorldSpawnPoint> _spawnPoints;

    public int Seed { get; }
    public int GeneratorVersion { get; }
    public WorldBiome Biome { get; }
    public float ChunkSize { get; }
    public bool UsesCenteredGrid { get; }
    public IReadOnlyList<WorldChunkData> Chunks { get; }
    public IReadOnlyDictionary<string, WorldSpawnPoint> SpawnPoints => _spawnPoints;

    internal WorldGenerationData(
        int seed,
        int generatorVersion,
        WorldBiome biome,
        float chunkSize,
        bool usesCenteredGrid,
        List<WorldChunkData> chunks,
        Dictionary<string, WorldSpawnPoint> spawnPoints)
    {
        Seed = seed;
        GeneratorVersion = generatorVersion;
        Biome = biome;
        ChunkSize = chunkSize;
        UsesCenteredGrid = usesCenteredGrid;
        Chunks = Array.AsReadOnly(chunks.ToArray());
        _chunkIndex = new Dictionary<WorldChunkCoordinate, WorldChunkData>(chunks.Count);
        foreach (WorldChunkData chunk in chunks)
            _chunkIndex[chunk.Coordinate] = chunk;
        _spawnPoints = new ReadOnlyDictionary<string, WorldSpawnPoint>(
            new Dictionary<string, WorldSpawnPoint>(spawnPoints, StringComparer.Ordinal));
    }

    public bool TryGetProfessionSpawn(string profession, out WorldSpawnPoint spawn) =>
        _spawnPoints.TryGetValue(profession, out spawn);

    public bool TryGetChunk(WorldChunkCoordinate coordinate, out WorldChunkData chunk) =>
        _chunkIndex.TryGetValue(coordinate, out chunk);

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
