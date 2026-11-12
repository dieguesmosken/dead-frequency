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
    public const int CurrentGeneratorVersion = 2;
    public const float FarmChunkSize = 64f;

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
        bool prepareLogicalCityData,
        Action<WorldGenerationProgress> progress = null)
    {
        float chunkSize = biome == WorldBiome.City ? blockSize + roadWidth : FarmChunkSize;
        bool centeredGrid = biome == WorldBiome.City;
        var chunks = new List<WorldChunkData>();
        var blocks = new List<WorldBlockData>();
        var deliveryPoints = new List<WorldDeliveryPointData>();
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

            BuildCityBlockPlans(
                seed, cityRadius, blockSize, sidewalkWidth, roadWidth, curbHeight, parkChance, parkingLotChance,
                storefrontChance, storeBrandCount, blocks,
                fireStationBlock, policeStationBlock, radioStationBlock, marketBlock,
                shoppingMallBlock, apartmentBlock, dealerBlock, waterTowerBlock, carWashBlock,
                gasStations, progress);
            // Regras de estacionamento e pontos de profissão ficam disponíveis antes de qualquer mesh.
            List<WorldNoParkingZoneData> noParkingZones = BuildCityNoParkingZones(
                cityRadius, blockSize, chunkSize, fireStationBlock, policeStationBlock, gasStations, blocks);
            // Os endereços V7 continuam sendo gerados pelo RNG legado, sem alterar a sequência visual.
            services = BuildCityServiceData(
                cityRadius, blockSize, chunkSize, curbHeight, marketBlock,
                radioStationBlock, gasStations, noParkingZones, progress);

            if (prepareLogicalCityData)
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
            seed, CurrentGeneratorVersion, biome, chunkSize, centeredGrid, chunks, blocks, deliveryPoints, services, spawnPoints);
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
        var brandRng = new RandomNumberGenerator
        {
            Seed = DeriveChunkSeed(worldSeed, new WorldChunkCoordinate(0, 0), StoreBrandStream),
        };

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
                    var typeRng = new RandomNumberGenerator
                    {
                        Seed = DeriveChunkSeed(worldSeed, coordinate, BlockTypeStream),
                    };
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
        RandomNumberGenerator brandRng)
    {
        float inner = blockSize - sidewalkWidth * 2f;
        float half = inner * 0.5f;
        Vector3 center = new(coordinate.X * (blockSize + roadWidth), 0f, coordinate.Z * (blockSize + roadWidth));

        var layoutRng = new RandomNumberGenerator
        {
            Seed = DeriveChunkSeed(worldSeed, coordinate, BlockLayoutStream),
        };
        var lotDecorationRng = new RandomNumberGenerator
        {
            Seed = DeriveChunkSeed(worldSeed, coordinate, BlockDecorationStream),
        };

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
        var rng = new RandomNumberGenerator
        {
            Seed = DeriveChunkSeed(worldSeed, coordinate, ParkLayoutStream),
        };

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
        var rng = new RandomNumberGenerator
        {
            Seed = DeriveChunkSeed(worldSeed, coordinate, ParkLayoutStream ^ 0x42454E43u),
        };

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
        var rng = new RandomNumberGenerator
        {
            Seed = DeriveChunkSeed(worldSeed, coordinate, ParkingLayoutStream),
        };

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
                float alongOffset = (building.BaseCenter - blockCenter).Dot(along);
                Vector3 curb = blockCenter + building.Front * (blockSize * 0.5f) + along * alongOffset;
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
