using System;
using System.Collections.Generic;
using Godot;

namespace DeadFrequency;

/// <summary>
/// Prepara dados espaciais sem criar Nodes ou recursos visuais.
/// A geração visual existente continua separada e mantém seu RNG legado nesta etapa de migração.
/// </summary>
public static class WorldDataGenerator
{
    public const int CurrentGeneratorVersion = 1;
    public const float FarmChunkSize = 64f;

    private const ulong FnvOffset = 14695981039346656037UL;
    private const ulong FnvPrime = 1099511628211UL;
    private const uint DeliveryPointStream = 0x444C5652;
    private static readonly string[] StreetNamesX =
        { "Rua das Antenas", "Rua Estática", "Rua do Chiado", "Rua Ruído Branco", "Rua da Interferência", "Rua Onda Curta", "Rua do Sintonizador", "Rua Válvula", "Rua do Eco" };
    private static readonly string[] StreetNamesZ =
        { "Av. Marconi", "Av. Hertz", "Av. Tesla", "Av. Landell", "Av. Faraday", "Av. Maxwell", "Av. Morse", "Av. Volta", "Av. Ampère" };

    public static WorldGenerationData Generate(
        int seed,
        WorldBiome biome,
        int cityRadius,
        float blockSize,
        float roadWidth,
        float farmExtent,
        float curbHeight,
        Vector2I marketBlock,
        Vector2I radioStationBlock,
        IReadOnlyList<(Vector2I Block, string Id)> gasStations,
        Action<WorldGenerationProgress> progress = null)
    {
        float chunkSize = biome == WorldBiome.City ? blockSize + roadWidth : FarmChunkSize;
        bool centeredGrid = biome == WorldBiome.City;
        var chunks = new List<WorldChunkData>();
        var deliveryPoints = new List<WorldDeliveryPointData>();
        var spawnPoints = new Dictionary<string, WorldSpawnPoint>(StringComparer.Ordinal);

        if (centeredGrid)
        {
            int total = (cityRadius * 2 + 1) * (cityRadius * 2 + 1);
            int completed = 0;

            for (int x = -cityRadius; x <= cityRadius; x++)
            {
                for (int z = -cityRadius; z <= cityRadius; z++)
                {
                    var coordinate = new WorldChunkCoordinate(x, z);
                    var center = new Vector3(x * chunkSize, 0f, z * chunkSize);
                    chunks.Add(new WorldChunkData(
                        coordinate,
                        DeriveChunkSeed(seed, coordinate, (uint)biome),
                        center,
                        new Vector2(chunkSize, chunkSize)));

                    completed++;
                    progress?.Invoke(new WorldGenerationProgress("Indexando chunks da cidade", completed, total));
                }
            }

            BuildCityDeliveryPoints(seed, cityRadius, blockSize, chunkSize, curbHeight, deliveryPoints, progress);
            BuildCitySpawnPoints(
                spawnPoints, blockSize, chunkSize, curbHeight, cityRadius,
                marketBlock, radioStationBlock, gasStations);
        }
        else
        {
            int min = Mathf.FloorToInt(-farmExtent / chunkSize);
            int maxExclusive = Mathf.CeilToInt(farmExtent / chunkSize);
            int width = maxExclusive - min;
            int total = width * width;
            int completed = 0;

            for (int x = min; x < maxExclusive; x++)
            {
                for (int z = min; z < maxExclusive; z++)
                {
                    var coordinate = new WorldChunkCoordinate(x, z);
                    var center = new Vector3((x + 0.5f) * chunkSize, 0f, (z + 0.5f) * chunkSize);
                    chunks.Add(new WorldChunkData(
                        coordinate,
                        DeriveChunkSeed(seed, coordinate, (uint)biome),
                        center,
                        new Vector2(chunkSize, chunkSize)));

                    completed++;
                    progress?.Invoke(new WorldGenerationProgress("Indexando chunks do rancho", completed, total));
                }
            }

            BuildFarmSpawnPoints(spawnPoints, blockSize);
        }

        progress?.Invoke(new WorldGenerationProgress("Preparando pontos de início", 1, 1));
        return new WorldGenerationData(
            seed, CurrentGeneratorVersion, biome, chunkSize, centeredGrid, chunks, deliveryPoints, spawnPoints);
    }

    private static void BuildCityDeliveryPoints(
        int worldSeed,
        int radius,
        float blockSize,
        float period,
        float curbHeight,
        List<WorldDeliveryPointData> destination,
        Action<WorldGenerationProgress> progress)
    {
        int total = (radius * 2 + 1) * (radius * 2 + 1) - 1;
        int completed = 0;
        float edge = blockSize * 0.5f - 1.5f;

        // Mantém a mesma ordem do gerador visual legado: X externo, Z interno, sem o depósito.
        for (int bx = -radius; bx <= radius; bx++)
        {
            for (int bz = -radius; bz <= radius; bz++)
            {
                if (bx == 0 && bz == 0)
                    continue;

                var coordinate = new WorldChunkCoordinate(bx, bz);
                var rng = new RandomNumberGenerator
                {
                    Seed = DeriveChunkSeed(worldSeed, coordinate, DeliveryPointStream),
                };

                int side = rng.RandiRange(0, 3);
                Vector3 offset;
                string street;
                switch (side)
                {
                    case 0:
                        offset = new Vector3(edge, 0f, 0f);
                        street = StreetX(bx);
                        break;
                    case 1:
                        offset = new Vector3(-edge, 0f, 0f);
                        street = StreetX(bx - 1);
                        break;
                    case 2:
                        offset = new Vector3(0f, 0f, edge);
                        street = StreetZ(bz);
                        break;
                    default:
                        offset = new Vector3(0f, 0f, -edge);
                        street = StreetZ(bz - 1);
                        break;
                }

                int addressNumber = rng.RandiRange(10, 999);
                Vector3 center = new(bx * period, 0f, bz * period);
                destination.Add(new WorldDeliveryPointData(
                    bx,
                    bz,
                    center + offset + new Vector3(0f, curbHeight, 0f),
                    $"{street}, nº {addressNumber}"));

                completed++;
                progress?.Invoke(new WorldGenerationProgress("Calculando endereços e entregas", completed, total));
            }
        }
    }

    private static string StreetX(int index) => StreetNamesX[Mathf.PosMod(index, StreetNamesX.Length)];
    private static string StreetZ(int index) => StreetNamesZ[Mathf.PosMod(index, StreetNamesZ.Length)];

    /// <summary>
    /// Seed estável independente da ordem em que os chunks forem carregados.
    /// Não usa HashCode, cujo resultado não deve ser usado como identidade persistente.
    /// </summary>
    public static ulong DeriveChunkSeed(int worldSeed, WorldChunkCoordinate coordinate, uint stream = 0)
    {
        ulong hash = FnvOffset;
        Add(ref hash, unchecked((uint)worldSeed));
        Add(ref hash, unchecked((uint)coordinate.X));
        Add(ref hash, unchecked((uint)coordinate.Z));
        Add(ref hash, stream);

        // Finalização avalanche para espalhar bits próximos entre chunks vizinhos.
        unchecked
        {
            hash ^= hash >> 30;
            hash *= 0xBF58476D1CE4E5B9UL;
            hash ^= hash >> 27;
            hash *= 0x94D049BB133111EBUL;
            hash ^= hash >> 31;
        }
        return hash == 0 ? 1UL : hash;
    }

    private static void Add(ref ulong hash, uint value)
    {
        unchecked
        {
            for (int i = 0; i < sizeof(uint); i++)
            {
                hash ^= (byte)(value >> (i * 8));
                hash *= FnvPrime;
            }
        }
    }

    private static void BuildCitySpawnPoints(
        Dictionary<string, WorldSpawnPoint> spawns,
        float blockSize,
        float period,
        float curbHeight,
        int radius,
        Vector2I marketBlock,
        Vector2I radioStationBlock,
        IReadOnlyList<(Vector2I Block, string Id)> gasStations)
    {
        Vector3 garbageSpawn = new(
            blockSize * 0.5f - 4f,
            curbHeight + 0.1f,
            blockSize * 0.5f - 2f);
        spawns[Professions.Gari] = new WorldSpawnPoint(Professions.Gari, garbageSpawn, Mathf.Pi * 0.5f);

        if (IsInsideCity(marketBlock, radius))
        {
            Vector3 marketCenter = new(marketBlock.X * period, curbHeight, marketBlock.Y * period);
            // Mesma referência usada em CityGenerator.Market.cs + RepositorJob.TryGetSpawn().
            Vector3 marketEntrance = marketCenter + new Vector3(0f, 0f, 7.5f);
            Vector3 repositorSpawn = marketEntrance + new Vector3(0f, 0.1f, 9f);
            spawns[Professions.Repositor] = new WorldSpawnPoint(Professions.Repositor, repositorSpawn, 0f);
        }

        if (IsInsideCity(radioStationBlock, radius))
        {
            Vector3 station = new(radioStationBlock.X * period, curbHeight, radioStationBlock.Y * period);
            // Mesa da rádio: st.Position + (-6, 0, z0 + 1.6), com z0 = -2.
            Vector3 console = station + new Vector3(-6f, 0f, -0.4f);
            Vector3 locutorSpawn = console + new Vector3(0f, 0.1f, 2.6f);
            spawns[Professions.Locutor] = new WorldSpawnPoint(Professions.Locutor, locutorSpawn, 0f);
        }

        if (gasStations == null || gasStations.Count == 0)
            return;

        // BuildBlock percorre X e depois Z em ordem crescente; GasSpots[0] segue essa ordem,
        // não necessariamente a ordem em que GasStations foi declarada.
        Vector2I firstStation = default;
        bool foundStation = false;
        foreach ((Vector2I block, string _) in gasStations)
        {
            if (!IsInsideCity(block, radius))
                continue;
            if (!foundStation || block.X < firstStation.X ||
                (block.X == firstStation.X && block.Y < firstStation.Y))
            {
                firstStation = block;
                foundStation = true;
            }
        }
        if (!foundStation)
            return;

        Vector3 gasStation = new(firstStation.X * period, curbHeight, firstStation.Y * period);
        Vector3 gasSpot = gasStation + new Vector3(0f, 0f, 5f);
        Vector3 frentistaSpawn = gasSpot + new Vector3(2.2f, 0.1f, 1.5f);
        spawns[Professions.Frentista] = new WorldSpawnPoint(Professions.Frentista, frentistaSpawn, Mathf.Pi);
    }

    private static void BuildFarmSpawnPoints(Dictionary<string, WorldSpawnPoint> spawns, float blockSize)
    {
        Vector3 garbageSpawn = new(
            blockSize * 0.5f - 4f,
            0.1f,
            blockSize * 0.5f - 2f);
        spawns[Professions.Gari] = new WorldSpawnPoint(Professions.Gari, garbageSpawn, Mathf.Pi * 0.5f);
    }

    private static bool IsInsideCity(Vector2I block, int radius) =>
        Mathf.Abs(block.X) <= radius && Mathf.Abs(block.Y) <= radius;
}
