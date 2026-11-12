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
    public const int CurrentGeneratorVersion = 6;
    public const float FarmChunkSize = 64f;

    /// <summary>
    /// RNG managed, determinístico e sem dependência do motor. O planejador lógico pode rodar
    /// em worker thread; a renderização continua usando os RNGs próprios da SceneTree.
    /// </summary>
    private sealed class WorldRandom
    {
        private ulong _state;
        public WorldRandom(ulong seed) => _state = seed;

        private uint NextUInt()
        {
            unchecked
            {
                _state += 0x9E3779B97F4A7C15UL;
                ulong value = _state;
                value = (value ^ (value >> 30)) * 0xBF58476D1CE4E5B9UL;
                value = (value ^ (value >> 27)) * 0x94D049BB133111EBUL;
                value ^= value >> 31;
                return (uint)(value >> 32);
            }
        }

        public float Randf() => (NextUInt() >> 8) * (1f / 16777216f);
        public float RandfRange(float from, float to) => from + (to - from) * Randf();

        public int RandiRange(int from, int to)
        {
            if (to < from)
                throw new ArgumentOutOfRangeException(nameof(to), "O limite superior precisa ser maior ou igual ao inferior.");
            uint range = (uint)((long)to - from + 1L);
            return from + (int)(NextUInt() % range);
        }
    }

    private const ulong FnvOffset = 14695981039346656037UL;
    private const ulong FnvPrime = 1099511628211UL;
    private const uint DeliveryPointStream = 0x444C5652;
    private const uint BlockTypeStream = 0x424C4B54;
    private const uint BlockLayoutStream = 0x4C41594F;
    private const uint BuildingVisualStream = 0x42564953;
    private const uint BuildingDecorationStream = 0x42444543;
    private const uint ParkLayoutStream = 0x5041524B;
    private const uint ParkingLayoutStream = 0x5041524C;
    private const uint ParkingCarStream = 0x50434152;
    private const uint FarmSiteStream = 0x4641524D;
    private const uint FarmTreeStream = 0x46545245;
    private const uint FarmTreeVisualStream = 0x46545649;
    private const uint FarmReedStream = 0x46524545;
    private const uint StoreBrandStream = 0x53544252;
    private const uint BlockVisualStream = 0x56495355;
    private const uint BlockDecorationStream = 0x4445434F;
    private static readonly string[] StreetNamesX =
        { "Rua das Antenas", "Rua Estática", "Rua do Chiado", "Rua Ruído Branco", "Rua da Interferência", "Rua Onda Curta", "Rua do Sintonizador", "Rua Válvula", "Rua do Eco" };
    private static readonly string[] StreetNamesZ =
        { "Av. Marconi", "Av. Hertz", "Av. Tesla", "Av. Landell", "Av. Faraday", "Av. Maxwell", "Av. Morse", "Av. Volta", "Av. Ampère" };

    public static WorldGenerationData Generate(
        int seed,
        WorldBiome biome,
        int cityRadius,
        float blockSize,
        float sidewalkWidth,
        float roadWidth,
        float farmExtent,
        float curbHeight,
        Vector2I marketBlock,
        Vector2I radioStationBlock,
        Vector2I fireStationBlock,
        Vector2I policeStationBlock,
        Vector2I shoppingMallBlock,
        Vector2I apartmentBlock,
        Vector2I dealerBlock,
        Vector2I waterTowerBlock,
        Vector2I carWashBlock,
        float parkChance,
        float parkingLotChance,
        IReadOnlyList<(Vector2I Block, string Id)> gasStations,
        int storeBrandCount,
        float storefrontChance,
        float parkedCarChance,
        IReadOnlyList<string> farmNames,
        bool prepareLogicalCityData,
        Action<WorldGenerationProgress> progress = null)
    {
        float chunkSize = biome == WorldBiome.City ? blockSize + roadWidth : FarmChunkSize;
        bool centeredGrid = biome == WorldBiome.City;
        var chunks = new List<WorldChunkData>();
        var blocks = new List<WorldBlockData>();
        var deliveryPoints = new List<WorldDeliveryPointData>();
        var farmSites = new List<WorldFarmSitePlan>();
        var farmTrees = new List<WorldFarmTreePlan>();
        var farmReeds = new List<WorldFarmReedPlan>();
        var farmUndergrowth = new List<WorldFarmUndergrowthPlan>();
        var farmBoundary = new List<WorldFarmBoundaryPlan>();
        WorldServiceData services = WorldServiceData.Empty();
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

            // V7 segue o gerador visual legado e só precisa dos pontos fixos usados pelos trabalhos.
            // Offline/V8 prepara todos os dados antes de construir qualquer geometria.
            List<WorldNoParkingZoneData> noParkingZones = new();
            if (prepareLogicalCityData)
            {
                BuildCityBlockPlans(
                    seed, cityRadius, blockSize, sidewalkWidth, roadWidth, curbHeight, parkChance, parkingLotChance,
                    storefrontChance, storeBrandCount, blocks,
                    fireStationBlock, policeStationBlock, radioStationBlock, marketBlock,
                    shoppingMallBlock, apartmentBlock, dealerBlock, waterTowerBlock, carWashBlock,
                    gasStations, progress);

                noParkingZones = BuildCityNoParkingZones(
                    cityRadius, blockSize, chunkSize, fireStationBlock, policeStationBlock, gasStations, blocks);
                progress?.Invoke(new WorldGenerationProgress(
                    "Calculando regras de estacionamento", 1, 1));

                Dictionary<WorldChunkCoordinate, List<WorldRoadCarPlan>> roadCars =
                    BuildCityRoadCarPlans(seed, cityRadius, blockSize, roadWidth, parkedCarChance, noParkingZones);
                for (int i = 0; i < blocks.Count; i++)
                {
                    WorldBlockData block = blocks[i];
                    roadCars.TryGetValue(block.Coordinate, out List<WorldRoadCarPlan> blockRoadCars);
                    blocks[i] = new WorldBlockData(
                        block.Coordinate, block.Kind, block.VisualSeed, block.DecorationSeed,
                        new List<WorldBuildingPlan>(block.Buildings),
                        new List<WorldParkTreePlan>(block.ParkTrees),
                        new List<WorldParkBenchPlan>(block.ParkBenches),
                        new List<WorldParkingCarPlan>(block.ParkingCars),
                        blockRoadCars ?? new List<WorldRoadCarPlan>());
                }

                progress?.Invoke(new WorldGenerationProgress(
                    "Calculando veículos estacionados", 1, 1));

                BuildCityDeliveryPoints(seed, cityRadius, blockSize, chunkSize, curbHeight, deliveryPoints);
            }

            // Coordenadas dos trabalhos são dados lógicos leves e também ficam disponíveis no V7.
            services = BuildCityServiceData(
                cityRadius, blockSize, chunkSize, curbHeight, marketBlock,
                radioStationBlock, gasStations, noParkingZones, progress);
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

            if (prepareLogicalCityData)
            {
                progress?.Invoke(new WorldGenerationProgress("Calculando sítios e destinos do rancho", 0, 1));
                BuildFarmSitePlans(seed, farmExtent, farmNames, farmSites, deliveryPoints);
                progress?.Invoke(new WorldGenerationProgress("Calculando sítios e destinos do rancho", 1, 1));

                progress?.Invoke(new WorldGenerationProgress("Planejando árvores e vegetação do rancho", 0, 1));
                BuildFarmTreePlans(seed, farmExtent, farmSites, farmTrees);
                BuildFarmReedPlans(seed, farmReeds);
                BuildFarmUndergrowthPlans(farmExtent, farmSites, farmUndergrowth);
                BuildFarmBoundaryPlans(farmExtent, farmBoundary);
                progress?.Invoke(new WorldGenerationProgress("Planejando árvores e vegetação do rancho", 1, 1));
            }

            BuildFarmSpawnPoints(spawnPoints, blockSize);
        }

        progress?.Invoke(new WorldGenerationProgress("Preparando pontos de início", 1, 1));
        return new WorldGenerationData(
            seed, CurrentGeneratorVersion, biome, chunkSize, centeredGrid,
            chunks, blocks, deliveryPoints, farmSites, farmTrees, farmReeds, farmUndergrowth, farmBoundary, services, spawnPoints);
    }

    private static void BuildCityBlockPlans(
        int worldSeed,
        int radius,
        float blockSize,
        float sidewalkWidth,
        float roadWidth,
        float curbHeight,
        float parkChance,
        float parkingLotChance,
        float storefrontChance,
        int storeBrandCount,
        List<WorldBlockData> destination,
        Vector2I fireStationBlock,
        Vector2I policeStationBlock,
        Vector2I radioStationBlock,
        Vector2I marketBlock,
        Vector2I shoppingMallBlock,
        Vector2I apartmentBlock,
        Vector2I dealerBlock,
        Vector2I waterTowerBlock,
        Vector2I carWashBlock,
        IReadOnlyList<(Vector2I Block, string Id)> gasStations,
        Action<WorldGenerationProgress> progress)
    {
        int width = radius * 2 + 1;
        int total = width * width;
        int completed = 0;
        int safeBrandCount = Mathf.Max(1, storeBrandCount);
        var brandQueue = new Queue<int>();
        var brandRng = new WorldRandom(DeriveChunkSeed(worldSeed, new WorldChunkCoordinate(0, 0), StoreBrandStream));

        // A classificação segue a prioridade original de CityGenerator.BuildBlock/TryBuildServiceBlock.
        for (int bx = -radius; bx <= radius; bx++)
        {
            for (int bz = -radius; bz <= radius; bz++)
            {
                var coordinate = new WorldChunkCoordinate(bx, bz);
                var block = new Vector2I(bx, bz);
                WorldBlockKind kind;

                if (bx == 0 && bz == 0)
                    kind = WorldBlockKind.Depot;
                else if (block == fireStationBlock)
                    kind = WorldBlockKind.FireStation;
                else if (block == policeStationBlock)
                    kind = WorldBlockKind.PoliceStation;
                else if (block == radioStationBlock)
                    kind = WorldBlockKind.RadioStation;
                else if (block == marketBlock)
                    kind = WorldBlockKind.Supermarket;
                else if (block == shoppingMallBlock)
                    kind = WorldBlockKind.ShoppingMall;
                else if (block == apartmentBlock)
                    kind = WorldBlockKind.ApartmentBuilding;
                else if (block == dealerBlock)
                    kind = WorldBlockKind.VehicleDealer;
                else if (block == waterTowerBlock)
                    kind = WorldBlockKind.WaterTower;
                else if (TryGetGasStation(gasStations, block, out _))
                    kind = block == carWashBlock ? WorldBlockKind.CarWash : WorldBlockKind.GasStation;
                else
                {
                    var typeRng = new WorldRandom(DeriveChunkSeed(worldSeed, coordinate, BlockTypeStream));
                    float roll = typeRng.Randf();
                    kind = roll < parkChance
                        ? WorldBlockKind.Park
                        : roll < parkChance + parkingLotChance
                            ? WorldBlockKind.ParkingLot
                            : WorldBlockKind.Buildings;
                }

                List<WorldBuildingPlan> buildings = kind == WorldBlockKind.Buildings
                    ? BuildCityBuildingPlans(
                        worldSeed, coordinate, radius, blockSize, sidewalkWidth, roadWidth, curbHeight,
                        storefrontChance, safeBrandCount, brandQueue, brandRng)
                    : new List<WorldBuildingPlan>();
                float period = blockSize + roadWidth;
                List<WorldParkTreePlan> parkTrees = kind == WorldBlockKind.Park
                    ? BuildCityParkTrees(worldSeed, coordinate, blockSize, sidewalkWidth, curbHeight, period)
                    : new List<WorldParkTreePlan>();
                List<WorldParkBenchPlan> parkBenches = kind == WorldBlockKind.Park
                    ? BuildCityParkBenches(worldSeed, coordinate, blockSize, sidewalkWidth, curbHeight, period)
                    : new List<WorldParkBenchPlan>();
                List<WorldParkingCarPlan> parkingCars = kind == WorldBlockKind.ParkingLot
                    ? BuildCityParkingCars(worldSeed, coordinate, blockSize, sidewalkWidth, curbHeight, period)
                    : new List<WorldParkingCarPlan>();

                destination.Add(new WorldBlockData(
                    coordinate,
                    kind,
                    DeriveChunkSeed(worldSeed, coordinate, BlockVisualStream),
                    DeriveChunkSeed(worldSeed, coordinate, BlockDecorationStream),
                    buildings, parkTrees, parkBenches, parkingCars));

                completed++;
                progress?.Invoke(new WorldGenerationProgress("Planejando quarteirões e prédios", completed, total));
            }
        }
    }

    private static List<WorldBuildingPlan> BuildCityBuildingPlans(
        int worldSeed,
        WorldChunkCoordinate coordinate,
        int radius,
        float blockSize,
        float sidewalkWidth,
        float roadWidth,
        float curbHeight,
        float storefrontChance,
        int storeBrandCount,
        Queue<int> brandQueue,
        WorldRandom brandRng)
    {
        float inner = blockSize - sidewalkWidth * 2f;
        float half = inner * 0.5f;
        Vector3 center = new(coordinate.X * (blockSize + roadWidth), 0f, coordinate.Z * (blockSize + roadWidth));

        var layoutRng = new WorldRandom(DeriveChunkSeed(worldSeed, coordinate, BlockLayoutStream));
        var lotDecorationRng = new WorldRandom(DeriveChunkSeed(worldSeed, coordinate, BlockDecorationStream));

        var lots = new List<Rect2>();
        float split = layoutRng.Randf();
        if (split < 0.2f)
        {
            lots.Add(new Rect2(-half, -half, inner, inner));
        }
        else if (split < 0.5f)
        {
            if (layoutRng.Randf() < 0.5f)
            {
                lots.Add(new Rect2(-half, -half, inner, half));
                lots.Add(new Rect2(-half, 0f, inner, half));
            }
            else
            {
                lots.Add(new Rect2(-half, -half, half, inner));
                lots.Add(new Rect2(0f, -half, half, inner));
            }
        }
        else
        {
            lots.Add(new Rect2(-half, -half, half, half));
            lots.Add(new Rect2(0f, -half, half, half));
            lots.Add(new Rect2(-half, 0f, half, half));
            lots.Add(new Rect2(0f, 0f, half, half));
        }

        float ring = Mathf.Max(Mathf.Abs(coordinate.X), Mathf.Abs(coordinate.Z));
        float heightBias = Mathf.Lerp(1.4f, 0.6f, (ring - 1f) / Mathf.Max(1f, radius - 1f));
        var result = new List<WorldBuildingPlan>(lots.Count);

        for (int i = 0; i < lots.Count; i++)
        {
            Rect2 lot = lots[i];
            float margin = layoutRng.RandfRange(0.6f, 2.0f);
            float width = lot.Size.X - margin * 2f;
            float depth = lot.Size.Y - margin * 2f;
            if (width < 5f || depth < 5f)
                continue;

            float height = layoutRng.RandfRange(7f, 20f) * heightBias;
            if (layoutRng.Randf() < 0.18f)
                height = layoutRng.RandfRange(26f, 48f) * heightBias;
            height = Mathf.Max(4f, Mathf.Round(height / 3.2f) * 3.2f + 0.4f);

            Vector3 lotCenter = center + new Vector3(
                lot.Position.X + lot.Size.X * 0.5f,
                curbHeight,
                lot.Position.Y + lot.Size.Y * 0.5f);

            Vector3 toLot = lotCenter - center;
            Vector3 front;
            if (Mathf.Abs(toLot.X) < 0.5f && Mathf.Abs(toLot.Z) < 0.5f)
                front = lotDecorationRng.Randf() < 0.5f ? Vector3.Back : Vector3.Right;
            else if (Mathf.Abs(toLot.X) > Mathf.Abs(toLot.Z))
                front = new Vector3(Mathf.Sign(toLot.X), 0f, 0f);
            else
                front = new Vector3(0f, 0f, Mathf.Sign(toLot.Z));

            bool hasStorefront = lotDecorationRng.Randf() < storefrontChance;
            float buildingHeight = hasStorefront ? 4.4f : height;
            int brandIndex = -1;
            if (hasStorefront)
            {
                if (brandQueue.Count == 0)
                {
                    var shuffled = new List<int>(storeBrandCount);
                    for (int brand = 0; brand < storeBrandCount; brand++)
                        shuffled.Add(brand);
                    for (int j = shuffled.Count - 1; j > 0; j--)
                    {
                        int swap = brandRng.RandiRange(0, j);
                        (shuffled[j], shuffled[swap]) = (shuffled[swap], shuffled[j]);
                    }
                    foreach (int brand in shuffled)
                        brandQueue.Enqueue(brand);
                }
                brandIndex = brandQueue.Dequeue();
            }

            uint index = unchecked((uint)i);
            result.Add(new WorldBuildingPlan(
                lotCenter,
                width,
                depth,
                buildingHeight,
                front,
                hasStorefront,
                DeriveChunkSeed(worldSeed, coordinate, BuildingVisualStream + index),
                DeriveChunkSeed(worldSeed, coordinate, BuildingDecorationStream + index),
                brandIndex));
        }

        return result;
    }

    private static List<WorldParkTreePlan> BuildCityParkTrees(
        int worldSeed,
        WorldChunkCoordinate coordinate,
        float blockSize,
        float sidewalkWidth,
        float curbHeight,
        float period)
    {
        float inner = blockSize - sidewalkWidth * 2f;
        Vector3 center = new(coordinate.X * period, 0f, coordinate.Z * period);
        var rng = new WorldRandom(DeriveChunkSeed(worldSeed, coordinate, ParkLayoutStream));

        int count = rng.RandiRange(7, 14);
        var result = new List<WorldParkTreePlan>(count);
        for (int i = 0; i < count; i++)
        {
            Vector3 position = center + new Vector3(
                rng.RandfRange(-inner * 0.45f, inner * 0.45f),
                curbHeight,
                rng.RandfRange(-inner * 0.45f, inner * 0.45f));
            float scale = rng.RandfRange(0.8f, 1.4f);
            string model = NatureKit.CommonTrees[AssetProps.Hash(position, 4) % NatureKit.CommonTrees.Length];
            float rotation = AssetProps.Hash(position, 7) % 628 / 100f;
            result.Add(new WorldParkTreePlan(position, scale, model, rotation));
        }

        return result;
    }

    private static List<WorldParkBenchPlan> BuildCityParkBenches(
        int worldSeed,
        WorldChunkCoordinate coordinate,
        float blockSize,
        float sidewalkWidth,
        float curbHeight,
        float period)
    {
        float inner = blockSize - sidewalkWidth * 2f;
        Vector3 center = new(coordinate.X * period, 0f, coordinate.Z * period);
        var rng = new WorldRandom(DeriveChunkSeed(worldSeed, coordinate, ParkLayoutStream ^ 0x42454E43u));

        var result = new List<WorldParkBenchPlan>(3);
        for (int i = 0; i < 3; i++)
        {
            Vector3 position = center + new Vector3(
                rng.RandfRange(-inner * 0.35f, inner * 0.35f),
                curbHeight + 0.25f,
                rng.RandfRange(-inner * 0.35f, inner * 0.35f));
            result.Add(new WorldParkBenchPlan(position, rng.RandfRange(0f, Mathf.Pi)));
        }

        return result;
    }

    private static List<WorldParkingCarPlan> BuildCityParkingCars(
        int worldSeed,
        WorldChunkCoordinate coordinate,
        float blockSize,
        float sidewalkWidth,
        float curbHeight,
        float period)
    {
        float inner = blockSize - sidewalkWidth * 2f;
        Vector3 center = new(coordinate.X * period, 0f, coordinate.Z * period);
        var rng = new WorldRandom(DeriveChunkSeed(worldSeed, coordinate, ParkingLayoutStream));

        int count = rng.RandiRange(3, 7);
        int stalls = Mathf.FloorToInt(inner * 0.86f / 3f);
        float slotStart = -stalls * 3f * 0.5f;
        var result = new List<WorldParkingCarPlan>(count);
        for (int i = 0; i < count; i++)
        {
            float x = -inner * 0.4f + i * (inner * 0.8f / Mathf.Max(1, count - 1));
            // Encaixa no meio de uma vaga pintada (a primeira é a PCD, fica livre).
            int slot = Mathf.Clamp(Mathf.RoundToInt((x - slotStart - 1.5f) / 3f), 1, stalls - 1);
            x = slotStart + slot * 3f + 1.5f;
            float z = (rng.Randf() < 0.5f ? -1f : 1f) * inner * 0.25f;
            result.Add(new WorldParkingCarPlan(
                center + new Vector3(x, curbHeight, z),
                0f,
                DeriveChunkSeed(worldSeed, coordinate, ParkingCarStream + unchecked((uint)i))));
        }

        return result;
    }

    private static bool TryGetGasStation(
        IReadOnlyList<(Vector2I Block, string Id)> gasStations,
        Vector2I block,
        out string id)
    {
        if (gasStations != null)
        {
            foreach ((Vector2I stationBlock, string stationId) in gasStations)
            {
                if (stationBlock == block)
                {
                    id = stationId;
                    return true;
                }
            }
        }

        id = null;
        return false;
    }

    private static Dictionary<WorldChunkCoordinate, List<WorldRoadCarPlan>> BuildCityRoadCarPlans(
        int worldSeed,
        int radius,
        float blockSize,
        float roadWidth,
        float parkedCarChance,
        IReadOnlyList<WorldNoParkingZoneData> noParkingZones)
    {
        var result = new Dictionary<WorldChunkCoordinate, List<WorldRoadCarPlan>>();
        float period = blockSize + roadWidth;
        float laneOffset = roadWidth * 0.5f - 1.4f;
        float reach = blockSize * 0.5f - 4f;

        for (int road = -radius - 1; road <= radius; road++)
        {
            for (int segment = -radius; segment <= radius; segment++)
            {
                foreach (int side in new[] { -1, 1 })
                {
                    bool hasCurb = side < 0
                        ? road >= -radius && road <= radius
                        : road + 1 >= -radius && road + 1 <= radius;
                    float roadPosition = road * period + period * 0.5f;
                    float along = segment * period;

                    AddRoadCarPlan(
                        worldSeed, radius, road, segment, side, true,
                        hasCurb, roadPosition, along, laneOffset, reach, parkedCarChance,
                        noParkingZones, result);
                    AddRoadCarPlan(
                        worldSeed, radius, road, segment, side, false,
                        hasCurb, roadPosition, along, laneOffset, reach, parkedCarChance,
                        noParkingZones, result);
                }
            }
        }

        return result;
    }

    private static void AddRoadCarPlan(
        int worldSeed,
        int radius,
        int road,
        int segment,
        int side,
        bool vertical,
        bool hasCurb,
        float roadPosition,
        float along,
        float laneOffset,
        float reach,
        float parkedCarChance,
        IReadOnlyList<WorldNoParkingZoneData> noParkingZones,
        Dictionary<WorldChunkCoordinate, List<WorldRoadCarPlan>> destination)
    {
        int owner = side < 0 ? road : road + 1;
        if (owner < -radius || owner > radius)
            return;

        int seedX = vertical ? road : segment;
        int seedZ = vertical ? segment : road;
        uint stream = unchecked(0x43415200u
            + (uint)(road + radius + 1) * 4u
            + (side > 0 ? 2u : 0u)
            + (vertical ? 0u : 1u));
        var random = new WorldRandom(DeriveChunkSeed(worldSeed, new WorldChunkCoordinate(seedX, seedZ), stream));
        if (random.Randf() >= parkedCarChance)
            return;

        float offset = random.RandfRange(-reach, reach);
        Vector3 position = vertical
            ? new Vector3(roadPosition + side * laneOffset, 0f, along + offset)
            : new Vector3(along + offset, 0f, roadPosition + side * laneOffset);
        if (IsInLogicalNoParkingZone(position, noParkingZones))
            return;

        WorldChunkCoordinate ownerCoordinate = vertical
            ? new WorldChunkCoordinate(owner, segment)
            : new WorldChunkCoordinate(segment, owner);
        if (!destination.TryGetValue(ownerCoordinate, out List<WorldRoadCarPlan> cars))
        {
            cars = new List<WorldRoadCarPlan>();
            destination[ownerCoordinate] = cars;
        }

        uint visualStream = unchecked(stream ^ 0x56495355u);
        ulong visualSeed = DeriveChunkSeed(
            worldSeed, new WorldChunkCoordinate(seedX, seedZ), visualStream);
        cars.Add(new WorldRoadCarPlan(
            position,
            vertical ? 0f : Mathf.Pi * 0.5f,
            offset,
            hasCurb,
            visualSeed));
    }

    private static bool IsInLogicalNoParkingZone(
        Vector3 position,
        IReadOnlyList<WorldNoParkingZoneData> zones)
    {
        foreach (WorldNoParkingZoneData zone in zones)
        {
            Vector3 delta = position - zone.Center;
            delta.Y = 0f;
            float along = delta.Dot(zone.Direction);
            float across = (delta - zone.Direction * along).Length();
            if (Mathf.Abs(along) < zone.HalfLength && across < 6f)
                return true;
        }

        return false;
    }

    private static List<WorldNoParkingZoneData> BuildCityNoParkingZones(
        int radius,
        float blockSize,
        float period,
        Vector2I fireStationBlock,
        Vector2I policeStationBlock,
        IReadOnlyList<(Vector2I Block, string Id)> gasStations,
        IReadOnlyList<WorldBlockData> blocks)
    {
        var result = new List<WorldNoParkingZoneData>();
        if (gasStations != null)
        {
            foreach ((Vector2I block, string _) in gasStations)
            {
                if (!IsInsideCity(block, radius))
                    continue;

                Vector3 center = new(block.X * period, 0f, block.Y * period);
                var owner = new WorldChunkCoordinate(block.X, block.Y);
                result.Add(new WorldNoParkingZoneData(
                    center + new Vector3(12f, 0f, blockSize * 0.5f), Vector3.Right, 5.5f, owner));
                result.Add(new WorldNoParkingZoneData(
                    center + new Vector3(-13f, 0f, blockSize * 0.5f), Vector3.Right, 5.5f, owner));
            }
        }

        if (IsInsideCity(fireStationBlock, radius))
        {
            Vector3 center = new(fireStationBlock.X * period, 0f, fireStationBlock.Y * period);
            result.Add(new WorldNoParkingZoneData(
                center + new Vector3(0f, 0f, -blockSize * 0.5f),
                Vector3.Right,
                13f,
                new WorldChunkCoordinate(fireStationBlock.X, fireStationBlock.Y)));
        }

        if (IsInsideCity(policeStationBlock, radius))
        {
            Vector3 center = new(policeStationBlock.X * period, 0f, policeStationBlock.Y * period);
            result.Add(new WorldNoParkingZoneData(
                center + new Vector3(0f, 0f, blockSize * 0.5f),
                Vector3.Right,
                4.5f,
                new WorldChunkCoordinate(policeStationBlock.X, policeStationBlock.Y)));
        }

        // Fachadas comerciais também reservam a guia, mesmo que o quarteirão esteja descarregado.
        foreach (WorldBlockData block in blocks)
        {
            Vector3 blockCenter = new(block.Coordinate.X * period, 0f, block.Coordinate.Z * period);
            foreach (WorldBuildingPlan building in block.Buildings)
            {
                if (!building.HasStorefront)
                    continue;

                Vector3 along = new(building.Front.Z, 0f, -building.Front.X);
                float facadeWidth = Mathf.Abs(building.Front.X) > 0.5f
                    ? building.Depth
                    : building.Width;
                float halfDepth = Mathf.Abs(building.Front.X) > 0.5f
                    ? building.Width * 0.5f
                    : building.Depth * 0.5f;
                float signWidth = Mathf.Min(facadeWidth * 0.85f, 9f);
                // Mesma posição de guia usada por BuildStorefront: centro do prédio + normal * meia profundidade.
                Vector3 curb = building.BaseCenter + building.Front * halfDepth;
                result.Add(new WorldNoParkingZoneData(
                    curb,
                    along,
                    Mathf.Max(2.5f, signWidth * 0.3f),
                    block.Coordinate));
            }
        }

        return result;
    }

    private static WorldServiceData BuildCityServiceData(
        int radius,
        float blockSize,
        float period,
        float curbHeight,
        Vector2I marketBlock,
        Vector2I radioStationBlock,
        IReadOnlyList<(Vector2I Block, string Id)> gasStations,
        IReadOnlyList<WorldNoParkingZoneData> noParkingZones,
        Action<WorldGenerationProgress> progress)
    {
        var dumpsters = new List<Vector3>();
        var gasSpots = new List<WorldGasSpotData>();
        var shelfSlots = new List<WorldMarketShelfSlotData>();

        int completed = 0;
        int total = (radius * 2 + 1) * (radius * 2 + 1) - 1;
        float edge = blockSize * 0.5f - 1f;

        // Reproduz os pontos de caçamba criados por BuildStreetProps sem carregar modelos.
        for (int bx = -radius; bx <= radius; bx++)
        {
            for (int bz = -radius; bz <= radius; bz++)
            {
                if (bx == 0 && bz == 0)
                    continue;

                Vector3 center = new(bx * period, curbHeight, bz * period);
                int hash = AssetProps.Hash(center, 11);
                if (hash / 100 % 100 < 40)
                {
                    Vector3 dumpster = center + new Vector3(-edge - 0.1f, 0f, -blockSize * 0.3f);
                    dumpsters.Add(dumpster + new Vector3(0.9f, 0f, 0f));
                }

                if (TryGetGasStation(gasStations, new Vector2I(bx, bz), out string brand))
                    gasSpots.Add(new WorldGasSpotData(center + new Vector3(0f, 0f, 5f), brand));

                completed++;
                progress?.Invoke(new WorldGenerationProgress("Calculando locais de serviço", completed, total));
            }
        }

        Vector3 marketStockPoint = Vector3.Zero;
        Vector3 marketEntrance = Vector3.Zero;
        if (IsInsideCity(marketBlock, radius))
        {
            Vector3 marketPosition = new(marketBlock.X * period, curbHeight, marketBlock.Y * period);

            // Mesmas coordenadas usadas em BuildSupermarket: z0=-13, z1=7.
            marketStockPoint = marketPosition + new Vector3(7f, 0f, -11.6f);
            marketEntrance = marketPosition + new Vector3(0f, 0f, 7.5f);

            float[] shelfX = { -10f, -6f, -2f, 2f, 6f, 10f };
            float[] shelfZ = { -7f, -4.5f, -2f };
            float[] shelfY = { 0.6f, 1.05f };
            foreach (float x in shelfX)
            {
                foreach (int side in new[] { -1, 1 })
                {
                    foreach (float z in shelfZ)
                    {
                        foreach (float y in shelfY)
                        {
                            Vector3 position = marketPosition + new Vector3(x + side * 0.5f, y, z);
                            shelfSlots.Add(new WorldMarketShelfSlotData(position, Mathf.Pi * 0.5f));
                        }
                    }
                }
            }
        }

        Vector3 radioConsolePoint = Vector3.Zero;
        if (IsInsideCity(radioStationBlock, radius))
        {
            Vector3 stationPosition = new(radioStationBlock.X * period, curbHeight, radioStationBlock.Y * period);
            radioConsolePoint = stationPosition + new Vector3(-6f, 0f, -0.4f);
        }

        return new WorldServiceData(
            dumpsters, gasSpots, shelfSlots,
            new List<WorldNoParkingZoneData>(noParkingZones),
            marketStockPoint, marketEntrance, radioConsolePoint);
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
                var rng = new WorldRandom(DeriveChunkSeed(worldSeed, coordinate, DeliveryPointStream));

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
        // Entregador: posição inicial original da cena, dentro da área do depósito.
        Vector3 deliverySpawn = new(-3f, 0f, 8f);
        spawns[Professions.Entregador] =
            new WorldSpawnPoint(Professions.Entregador, deliverySpawn, 0f);

        // Taxista: calçada junto ao portão do depósito, pronto para sair para a rua.
        Vector3 taxiSpawn = new(0f, curbHeight + 0.1f, blockSize * 0.5f - 1.2f);
        spawns[Professions.Taxista] =
            new WorldSpawnPoint(Professions.Taxista, taxiSpawn, 0f);

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

    private readonly struct FarmRoadPlan
    {
        public readonly string Name;
        public readonly Vector2 A;
        public readonly Vector2 B;
        public readonly float Width;

        public FarmRoadPlan(string name, Vector2 a, Vector2 b, float width)
        {
            Name = name;
            A = a;
            B = b;
            Width = width;
        }
    }

    private static void BuildFarmSitePlans(
        int worldSeed,
        float farmExtent,
        IReadOnlyList<string> farmNames,
        List<WorldFarmSitePlan> sites,
        List<WorldDeliveryPointData> deliveryPoints)
    {
        List<FarmRoadPlan> roads = CreateFarmRoadPlans();

        string[] fallbackNames = farmNames is { Count: > 0 }
            ? CopyNames(farmNames)
            : new[] { "Fazenda Rancho Fundo" };
        var names = new List<string>(fallbackNames);
        var rng = new WorldRandom(DeriveChunkSeed(worldSeed, new WorldChunkCoordinate(0, 0), FarmSiteStream));

        for (int i = names.Count - 1; i > 0; i--)
        {
            int j = rng.RandiRange(0, i);
            (names[i], names[j]) = (names[j], names[i]);
        }

        const float width = 46f;
        const float depth = 56f;
        float occupiedRadius = Mathf.Sqrt(width * width + depth * depth) * 0.5f;
        var occupied = new List<(Vector2 Center, float Radius)>
        {
            (Vector2.Zero, 34f),
            (new Vector2(-95f, -120f), 42f),
            (new Vector2(17f, 146f), 18f),
            (new Vector2(-14f, 140f), 10f),
            (new Vector2(-10f, 26f), 8f),
            (new Vector2(36f, 146f), 14f),
        };

        // Mantém a ordem de destinos do mundo antigo: pesqueiro primeiro, depois os sítios.
        Vector3 lakeCenter = new(-95f, 0f, -120f);
        Vector3 fishingPoint = lakeCenter + new Vector3(-36f, 0f, 0f);
        deliveryPoints.Add(new WorldDeliveryPointData(-1, 0, fishingPoint,
            "Pesqueiro Lambari Feliz · Trilha do Pesqueiro"));

        int placed = 0;
        for (int roadIndex = 0; roadIndex < roads.Count; roadIndex++)
        {
            FarmRoadPlan road = roads[roadIndex];
            if (road.Width < 6f)
                continue;

            Vector2 direction = (road.B - road.A).Normalized();
            Vector2 normal = new(-direction.Y, direction.X);
            float length = road.A.DistanceTo(road.B);
            for (float along = 40f; along < length - 30f; along += rng.RandfRange(62f, 84f))
            {
                float side = rng.Randf() < 0.5f ? 1f : -1f;
                for (int attempt = 0; attempt < 2; attempt++, side = -side)
                {
                    Vector2 onRoad = road.A + direction * along;
                    Vector2 gate = onRoad + normal * side * (road.Width * 0.5f + 3f);
                    Vector2 center = gate + normal * side * (depth * 0.5f);
                    if (!IsFarmSiteFree(center, occupiedRadius, farmExtent, occupied) ||
                        DistanceToFarmRoads(center, roads, roadIndex) < occupiedRadius + 2f)
                        continue;

                    occupied.Add((center, occupiedRadius));
                    string name = placed < names.Count
                        ? names[placed]
                        : names[placed % names.Count] + " II";
                    float km = (onRoad.Length() / 1000f) * 10f;
                    string address = $"{name} · {road.Name}, km {km:0.0}";
                    Vector3 gatePosition = new(gate.X, 0f, gate.Y);
                    Vector3 towardRoad = new(-normal.X * side, 0f, -normal.Y * side);
                    float yaw = Mathf.Atan2(towardRoad.X, towardRoad.Z);
                    Transform3D transform = new(new Basis(Vector3.Up, yaw), gatePosition);
                    Vector3 deliveryPosition = transform * new Vector3(0f, 0f, -2.5f);

                    WorldChunkCoordinate siteCoordinate = new(roadIndex, placed);
                    ulong siteSeed = DeriveChunkSeed(worldSeed, siteCoordinate, FarmSiteStream + 1u);
                    sites.Add(new WorldFarmSitePlan(
                        gatePosition,
                        towardRoad,
                        new Vector3(center.X, 0f, center.Y),
                        width,
                        depth,
                        occupiedRadius,
                        name,
                        address,
                        siteSeed));
                    deliveryPoints.Add(new WorldDeliveryPointData(
                        siteCoordinate.X,
                        siteCoordinate.Z,
                        deliveryPosition,
                        address));
                    placed++;
                    break;
                }
            }
        }

    }

    private static List<FarmRoadPlan> CreateFarmRoadPlans()
    {
        return new List<FarmRoadPlan>
        {
            new("Estrada do Rancho", new Vector2(0f, 14f), new Vector2(0f, 300f), 8f),
            new("Estrada da Cooperativa", new Vector2(14f, 0f), new Vector2(300f, 0f), 8f),
            new("Estrada Velha", new Vector2(-300f, 130f), new Vector2(300f, 130f), 8f),
            new("Estrada do Açude", new Vector2(-185f, -265f), new Vector2(-185f, 130f), 8f),
            new("Estrada da Serra", new Vector2(-185f, -265f), new Vector2(225f, -265f), 8f),
            new("Estrada da Serra", new Vector2(225f, -265f), new Vector2(225f, 0f), 8f),
            new("Estrada do Pequi", new Vector2(150f, 130f), new Vector2(150f, 300f), 8f),
            new("Trilha do Pesqueiro", new Vector2(-185f, -120f), new Vector2(-125f, -120f), 5f),
        };
    }

    private static void BuildFarmTreePlans(
        int worldSeed,
        float farmExtent,
        IReadOnlyList<WorldFarmSitePlan> farmSites,
        List<WorldFarmTreePlan> destination)
    {
        List<FarmRoadPlan> roads = CreateFarmRoadPlans();
        var occupied = new List<(Vector2 Center, float Radius)>
        {
            (Vector2.Zero, 34f),
            (new Vector2(-95f, -120f), 42f),
            (new Vector2(17f, 146f), 18f),
            (new Vector2(-14f, 140f), 10f),
            (new Vector2(-10f, 26f), 8f),
            (new Vector2(36f, 146f), 14f),
        };
        foreach (WorldFarmSitePlan site in farmSites)
            occupied.Add((new Vector2(site.Center.X, site.Center.Z), site.OccupiedRadius));

        var rng = new WorldRandom(DeriveChunkSeed(worldSeed, new WorldChunkCoordinate(0, 0), FarmTreeStream));

        int placed = 0;
        for (int attempt = 0; attempt < 1600 && placed < 900; attempt++)
        {
            Vector2 point = new(
                rng.RandfRange(-farmExtent + 6f, farmExtent - 6f),
                rng.RandfRange(-farmExtent + 6f, farmExtent - 6f));
            if (DistanceToFarmRoads(point, roads) < 4f ||
                !IsFarmSiteFree(point, 3f, farmExtent, occupied))
                continue;

            Vector3 basePosition = new(point.X, 0f, point.Y);
            if (rng.Randf() < 0.05f)
            {
                int variant = rng.RandiRange(0, 2) + AssetProps.Hash(basePosition, 3) % 3;
                float rotation = rng.Randf() * Mathf.Tau;
                float scale = rng.RandfRange(1.2f, 1.8f) * 0.3f;
                destination.Add(new WorldFarmTreePlan(basePosition, false, true, variant,
                    rotation, scale, 0UL));
            }
            else
            {
                bool pine = rng.Randf() < 0.45f;
                ulong visualSeed = DeriveChunkSeed(
                    worldSeed,
                    new WorldChunkCoordinate(placed, 1),
                    FarmTreeVisualStream);
                destination.Add(new WorldFarmTreePlan(basePosition, pine, false, 0,
                    0f, 1f, visualSeed));
            }

            placed++;
        }

        // Mata fechada na borda: preserva o mesmo contorno de quatro lados do gerador visual.
        for (float t = -farmExtent; t <= farmExtent; t += 7f)
        {
            foreach (Vector2 edgePoint in new[]
            {
                new Vector2(t, -farmExtent),
                new Vector2(t, farmExtent),
                new Vector2(-farmExtent, t),
                new Vector2(farmExtent, t),
            })
            {
                Vector2 jitter = new(rng.RandfRange(-2.5f, 2.5f), rng.RandfRange(-2.5f, 2.5f));
                bool pine = rng.Randf() < 0.6f;
                ulong visualSeed = DeriveChunkSeed(
                    worldSeed,
                    new WorldChunkCoordinate(destination.Count, 2),
                    FarmTreeVisualStream);
                destination.Add(new WorldFarmTreePlan(
                    new Vector3(edgePoint.X + jitter.X, 0f, edgePoint.Y + jitter.Y),
                    pine, false, 0, 0f, 1f, visualSeed));
            }
        }
    }

    private static void BuildFarmBoundaryPlans(
        float farmExtent,
        List<WorldFarmBoundaryPlan> destination)
    {
        var rng = new WorldRandom(3220);
        float limit = farmExtent + 4f;
        float wallX = limit - 0.6f;

        foreach ((Vector3 direction, Vector3 along) in new[]
        {
            (Vector3.Right, Vector3.Back),
            (Vector3.Left, Vector3.Back),
            (Vector3.Back, Vector3.Right),
            (Vector3.Forward, Vector3.Right),
        })
        {
            for (float t = -limit; t < limit; t += 6f)
            {
                float wallHeight = rng.RandfRange(2.6f, 3.1f);
                Vector3 center = direction * wallX + along * (t + 3f);
                Vector3 wallSize = direction.X != 0f
                    ? new Vector3(0.8f, wallHeight, 6.05f)
                    : new Vector3(6.05f, wallHeight, 0.8f);
                Color tint = new(
                    0.9f + rng.Randf() * 0.2f,
                    0.9f + rng.Randf() * 0.2f,
                    0.9f + rng.Randf() * 0.15f);
                destination.Add(new WorldFarmBoundaryPlan(
                    "wall",
                    center + new Vector3(0f, wallHeight * 0.5f, 0f),
                    wallSize,
                    0f,
                    tint));

                if (Mathf.PosMod(t, 12f) < 0.5f)
                {
                    destination.Add(new WorldFarmBoundaryPlan(
                        "pillar",
                        center - along * 3f + new Vector3(0f, 1.8f, 0f),
                        new Vector3(1.1f, 3.6f, 1.1f),
                        0f,
                        Colors.White));
                }
            }

            for (int ring = 0; ring < 2; ring++)
            {
                for (float t = -limit - 40f; t < limit + 40f; t += ring == 0 ? 26f : 38f)
                {
                    float radius = ring == 0 ? rng.RandfRange(18f, 30f) : rng.RandfRange(30f, 48f);
                    float height = ring == 0 ? rng.RandfRange(22f, 40f) : rng.RandfRange(40f, 75f);
                    Vector3 center = direction * (limit + radius * 0.75f + ring * 30f)
                        + along * (t + rng.RandfRange(-8f, 8f));
                    Color tint = new(
                        0.8f + rng.Randf() * 0.35f,
                        0.8f + rng.Randf() * 0.3f,
                        0.75f + rng.Randf() * 0.3f);
                    destination.Add(new WorldFarmBoundaryPlan(
                        "mountain",
                        center + new Vector3(0f, height * 0.5f - 1f, 0f),
                        new Vector3(radius * 2f, height, radius * 2f),
                        rng.Randf() * 3f,
                        tint));
                }
            }
        }
    }

    private static void BuildFarmUndergrowthPlans(
        float farmExtent,
        IReadOnlyList<WorldFarmSitePlan> farmSites,
        List<WorldFarmUndergrowthPlan> destination)
    {
        List<FarmRoadPlan> roads = CreateFarmRoadPlans();
        var occupied = new List<(Vector2 Center, float Radius)>
        {
            (Vector2.Zero, 34f),
            (new Vector2(-95f, -120f), 42f),
            (new Vector2(17f, 146f), 18f),
            (new Vector2(-14f, 140f), 10f),
            (new Vector2(-10f, 26f), 8f),
            (new Vector2(36f, 146f), 14f),
        };
        foreach (WorldFarmSitePlan site in farmSites)
            occupied.Add((new Vector2(site.Center.X, site.Center.Z), site.OccupiedRadius));

        var rng = new WorldRandom(96601);
        int placed = 0;
        for (int attempt = 0; attempt < 9000 && placed < 2600; attempt++)
        {
            Vector2 point = new(
                rng.RandfRange(-farmExtent + 4f, farmExtent - 4f),
                rng.RandfRange(-farmExtent + 4f, farmExtent - 4f));
            if (DistanceToFarmRoads(point, roads) < 3.2f ||
                !IsFarmSiteFree(point, 1.2f, farmExtent, occupied))
                continue;

            bool isRock = rng.Randf() < 0.06f;
            string model = isRock
                ? NatureKit.Rocks[rng.RandiRange(0, NatureKit.Rocks.Length - 1)]
                : NatureKit.Ground[rng.RandiRange(0, NatureKit.Ground.Length - 1)];
            float scale = isRock
                ? rng.RandfRange(0.25f, 0.6f)
                : rng.RandfRange(0.6f, 1.2f);
            float rotation = rng.Randf() * Mathf.Tau;
            destination.Add(new WorldFarmUndergrowthPlan(
                new Vector3(point.X, 0f, point.Y), model, scale, rotation, isRock));
            placed++;
        }
    }

    private static void BuildFarmReedPlans(int worldSeed, List<WorldFarmReedPlan> destination)
    {
        var rng = new WorldRandom(DeriveChunkSeed(worldSeed, new WorldChunkCoordinate(-1, -1), FarmReedStream));
        Vector3 lakeCenter = new(-95f, 0f, -120f);
        const float lakeRadius = 32f;
        for (int i = 0; i < 70; i++)
        {
            float angle = rng.Randf() * Mathf.Tau;
            float radius = lakeRadius + rng.RandfRange(-1.2f, 1.5f);
            float height = rng.RandfRange(0.6f, 1.4f);
            Vector3 position = lakeCenter + new Vector3(
                Mathf.Cos(angle) * radius,
                height * 0.5f,
                Mathf.Sin(angle) * radius);
            destination.Add(new WorldFarmReedPlan(position, height));
        }
    }

    private static string[] CopyNames(IReadOnlyList<string> names)
    {
        var copy = new string[names.Count];
        for (int i = 0; i < names.Count; i++)
            copy[i] = names[i];
        return copy;
    }

    private static bool IsFarmSiteFree(
        Vector2 position,
        float radius,
        float farmExtent,
        List<(Vector2 Center, float Radius)> occupied)
    {
        foreach ((Vector2 center, float occupiedRadius) in occupied)
        {
            if (position.DistanceTo(center) < occupiedRadius + radius)
                return false;
        }

        return Mathf.Abs(position.X) < farmExtent - radius &&
               Mathf.Abs(position.Y) < farmExtent - radius;
    }

    private static float DistanceToFarmRoads(Vector2 point, List<FarmRoadPlan> roads, int skip = -1)
    {
        float best = float.MaxValue;
        for (int i = 0; i < roads.Count; i++)
        {
            if (i == skip)
                continue;

            FarmRoadPlan road = roads[i];
            Vector2 segment = road.B - road.A;
            float t = Mathf.Clamp(
                (point - road.A).Dot(segment) / Mathf.Max(segment.LengthSquared(), 0.0001f),
                0f,
                1f);
            float distance = point.DistanceTo(road.A + segment * t) - road.Width * 0.5f;
            best = Mathf.Min(best, distance);
        }
        return best;
    }

    private static void BuildFarmSpawnPoints(Dictionary<string, WorldSpawnPoint> spawns, float blockSize)
    {
        // O rancho também preserva a posição inicial original do entregador.
        Vector3 deliverySpawn = new(-3f, 0f, 8f);
        spawns[Professions.Entregador] =
            new WorldSpawnPoint(Professions.Entregador, deliverySpawn, 0f);
        spawns[Professions.Taxista] =
            new WorldSpawnPoint(Professions.Taxista, deliverySpawn, 0f);

        Vector3 garbageSpawn = new(
            blockSize * 0.5f - 4f,
            0.1f,
            blockSize * 0.5f - 2f);
        spawns[Professions.Gari] = new WorldSpawnPoint(Professions.Gari, garbageSpawn, Mathf.Pi * 0.5f);
    }

    private static bool IsInsideCity(Vector2I block, int radius) =>
        Mathf.Abs(block.X) <= radius && Mathf.Abs(block.Y) <= radius;
}
