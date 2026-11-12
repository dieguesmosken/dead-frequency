// Scripts/World/CityGenerator.cs
// Gera a cidade ao rodar: grade de quarteirões separados por ruas, prédios com janelas acesas,
// calçadas, postes de luz, carros estacionados, parques, o depósito no centro e uma torre de rádio.
// Tudo é determinístico pela Seed: mesma seed = mesma cidade.
//
// Layout: quarteirões centrados em múltiplos de Period (BlockSize + RoadWidth).
// As ruas ficam entre eles, centradas em k*Period + Period/2. O quarteirão (0,0) é o depósito.
using System;
using System.Collections.Generic;
using Godot;

namespace DeadFrequency;

public struct DeliveryPoint
{
    public Vector3 Position;
    public string Address;
}

public partial class CityGenerator : Node3D
{
    [ExportGroup("Grade")]
    [Export] public int Seed { get; set; } = 1996;
    /// <summary>Quarteirões de -Radius a +Radius em cada eixo.</summary>
    [Export(PropertyHint.Range, "1,6,1")] public int Radius { get; set; } = 3;
    [Export] public float BlockSize { get; set; } = 36f;
    [Export] public float RoadWidth { get; set; } = 12f;
    [Export] public float SidewalkWidth { get; set; } = 3f;
    [Export] public float CurbHeight { get; set; } = 0.15f;

    [ExportGroup("Detalhes")]
    [Export(PropertyHint.Range, "0,1,0.05")] public float ParkedCarChance { get; set; } = 0.45f;
    [Export(PropertyHint.Range, "0,1,0.05")] public float ParkChance { get; set; } = 0.12f;
    [Export(PropertyHint.Range, "0,1,0.05")] public float ParkingLotChance { get; set; } = 0.08f;
    [Export] public float LampLightRange { get; set; } = 16f;

    public float Period => BlockSize + RoadWidth;
    public bool IsDay { get; private set; }
    public IReadOnlyList<DeliveryPoint> DeliveryPoints => _deliveryPoints;

    /// <summary>Plano lógico calculado antes da criação de qualquer geometria do mundo.</summary>
    public WorldGenerationData WorldData { get; private set; }
    public event Action<WorldGenerationProgress> WorldDataProgress;

    private static readonly string[] StreetNamesX =
        { "Rua das Antenas", "Rua Estática", "Rua do Chiado", "Rua Ruído Branco", "Rua da Interferência", "Rua Onda Curta", "Rua do Sintonizador", "Rua Válvula", "Rua do Eco" };
    private static readonly string[] StreetNamesZ =
        { "Av. Marconi", "Av. Hertz", "Av. Tesla", "Av. Landell", "Av. Faraday", "Av. Maxwell", "Av. Morse", "Av. Volta", "Av. Ampère" };

    private static readonly Color[] WallPalette =
    {
        new(0.42f, 0.40f, 0.37f), new(0.50f, 0.47f, 0.42f), new(0.36f, 0.30f, 0.27f), new(0.45f, 0.28f, 0.22f),
        new(0.30f, 0.34f, 0.35f), new(0.55f, 0.52f, 0.45f), new(0.28f, 0.33f, 0.30f), new(0.40f, 0.36f, 0.30f),
    };

    private static readonly Color[] CarPalette =
    {
        new(0.45f, 0.08f, 0.07f), new(0.12f, 0.18f, 0.32f), new(0.55f, 0.55f, 0.5f), new(0.15f, 0.15f, 0.15f),
        new(0.25f, 0.32f, 0.2f), new(0.6f, 0.45f, 0.2f), new(0.35f, 0.4f, 0.45f),
    };

    private readonly List<DeliveryPoint> _deliveryPoints = new();
    private RandomNumberGenerator _rng;

    private BoxMesh _unitBox;
    private CylinderMesh _unitCylinder;
    private CylinderMesh _treeTop;
    private ShaderMaterial _buildingMaterial;
    private ShaderMaterial _groundMaterial;
    private ShaderMaterial _sidewalkMaterial;
    private StandardMaterial3D _darkMetal;
    private StandardMaterial3D _lampHead;
    private StandardMaterial3D _grass;
    private StandardMaterial3D _leaves;
    private StandardMaterial3D _trunk;
    private StandardMaterial3D _warehouse;
    private StandardMaterial3D _concrete;
    private StandardMaterial3D _redBeacon;
    private StandardMaterial3D _glass;
    private StandardMaterial3D _stopRed;
    private StandardMaterial3D _signBlue;
    private StandardMaterial3D _signWhite;
    private StandardMaterial3D _curbWhite;
    private StandardMaterial3D _curbYellow;
    private CylinderMesh _octagon;
    private readonly Dictionary<Color, StandardMaterial3D> _carMaterials = new();

    private Node3D _staticRoot;
    private Node3D _lightsRoot;
    private bool _useLogicalWorldData;
    private bool _useLogicalBlockPlans;
    private Node3D _activeChunkRoot;
    private WorldChunkCoordinate? _activeChunkCoordinate;
    private readonly Dictionary<WorldChunkCoordinate, Node3D> _chunkRoots = new();
    private readonly Dictionary<WorldChunkCoordinate, Node3D> _chunkLightRoots = new();

    private Node3D ActiveLightsRoot
    {
        get
        {
            if (!_activeChunkCoordinate.HasValue)
                return _lightsRoot;

            WorldChunkCoordinate coordinate = _activeChunkCoordinate.Value;
            if (!_chunkLightRoots.TryGetValue(coordinate, out Node3D root) || !GodotObject.IsInstanceValid(root))
            {
                root = new Node3D { Name = $"ChunkLights_{coordinate.X}_{coordinate.Z}" };
                _lightsRoot.AddChild(root);
                _chunkLightRoots[coordinate] = root;
            }
            return root;
        }
    }

    public bool IsChunkVisualized(WorldChunkCoordinate coordinate) =>
        _chunkRoots.TryGetValue(coordinate, out Node3D root)
        && GodotObject.IsInstanceValid(root)
        && !root.IsQueuedForDeletion();

    public IReadOnlyList<WorldChunkCoordinate> GetLoadedChunkCoordinates() =>
        new List<WorldChunkCoordinate>(_chunkRoots.Keys);

    /// <summary>Cria a geometria de um único chunk na thread principal.</summary>
    public bool BuildChunkVisuals(WorldChunkData chunk)
    {
        if (!_useLogicalBlockPlans || Biome != WorldBiome.City || IsChunkVisualized(chunk.Coordinate))
            return false;

        var root = new Node3D { Name = $"Chunk_{chunk.Coordinate.X}_{chunk.Coordinate.Z}" };
        _staticRoot.AddChild(root);
        _chunkRoots[chunk.Coordinate] = root;

        Node3D previousRoot = _activeChunkRoot;
        WorldChunkCoordinate? previousCoordinate = _activeChunkCoordinate;
        _activeChunkRoot = root;
        _activeChunkCoordinate = chunk.Coordinate;

        try
        {
            if (chunk.Coordinate.X == 0 && chunk.Coordinate.Z == 0)
                BuildDepot(parent: root);
            else
                BuildBlock(chunk.Coordinate.X, chunk.Coordinate.Z);

            OptimizeVisibility(root);
            return true;
        }
        catch (Exception ex)
        {
            GD.PushError($"Falha ao construir chunk {chunk.Coordinate.X},{chunk.Coordinate.Z}: {ex}");
            root.QueueFree();
            _chunkRoots.Remove(chunk.Coordinate);
            if (_chunkLightRoots.TryGetValue(chunk.Coordinate, out Node3D lights))
            {
                lights.QueueFree();
                _chunkLightRoots.Remove(chunk.Coordinate);
            }
            return false;
        }
        finally
        {
            _activeChunkRoot = previousRoot;
            _activeChunkCoordinate = previousCoordinate;
        }
    }

    /// <summary>Descarta apenas a geometria do chunk; os dados lógicos permanecem na memória.</summary>
    public void UnloadChunkVisuals(WorldChunkCoordinate coordinate)
    {
        if (_chunkRoots.TryGetValue(coordinate, out Node3D root))
        {
            _chunkRoots.Remove(coordinate);
            if (GodotObject.IsInstanceValid(root))
                root.QueueFree();
        }

        if (_chunkLightRoots.TryGetValue(coordinate, out Node3D lights))
        {
            _chunkLightRoots.Remove(coordinate);
            if (GodotObject.IsInstanceValid(lights))
                lights.QueueFree();
        }
    }

    private string GetInitialProfessionForWorld()
    {
        string wanted = Professions.Current;
        if (NetClient.Instance?.IsOnline != true)
        {
            SaveData save = SaveManager.Load();
            if (save != null && !string.IsNullOrEmpty(save.Profession) &&
                GameSettings.Instance?.Profession == save.Profession)
                wanted = save.Profession;
        }
        return wanted;
    }

    public override void _Ready()
    {
        HomeGuide.Clear(); // a casa só existe na cidade (o prédio registra de novo)
        // No multiplayer todos usam a seed da sala, então a cidade (e os endereços) são iguais para todos.
        if (NetClient.Instance?.IsOnline == true)
            Seed = NetClient.Instance.Seed;

        // Primeira etapa: indexa o mundo e calcula os spawns sem criar Nodes, malhas ou colisões.
        // O RNG legado de renderização permanece separado para preservar o mapa visual atual.
        // V7 precisa preservar exatamente os pontos de entrega do gerador legado.
        // V8 pode usar o plano lógico, já que seus clientes usam a mesma geração atualizada.
        _useLogicalWorldData = NetClient.Instance?.IsOnline != true
            || NetClient.Instance.ActiveProtocolVersion >= NetClient.PreferredProtocolVersion;
        _useLogicalBlockPlans = _useLogicalWorldData;

        WorldData = WorldDataGenerator.Generate(
            Seed, Biome, Radius, BlockSize, RoadWidth, FarmExtent, CurbHeight,
            MarketBlock, RadioStationBlock, FireStationBlock, PoliceStationBlock,
            ShoppingMallBlock, ApartmentBlock, DealerBlock, WaterTowerBlock, CarWashBlock,
            ParkChance, ParkingLotChance, GasStations, _useLogicalWorldData,
            progress => WorldDataProgress?.Invoke(progress));

        if (Biome == WorldBiome.City && _useLogicalWorldData)
        {
            _deliveryPoints.Clear();
            foreach (WorldDeliveryPointData point in WorldData.DeliveryPoints)
            {
                _deliveryPoints.Add(new DeliveryPoint
                {
                    Position = point.Position,
                    Address = point.Address,
                });
            }

            // Pontos de interação ficam disponíveis mesmo quando os modelos 3D não estiverem carregados.
            DumpsterSpots.Clear();
            DumpsterSpots.AddRange(WorldData.Services.DumpsterSpots);
            GasSpots.Clear();
            foreach (WorldGasSpotData spot in WorldData.Services.GasSpots)
                GasSpots.Add((spot.Position, spot.Brand));
            MarketShelfSlots.Clear();
            foreach (WorldMarketShelfSlotData slot in WorldData.Services.MarketShelfSlots)
                MarketShelfSlots.Add((slot.Position, slot.RotationY));
            MarketStockPoint = WorldData.Services.MarketStockPoint;
            MarketEntrance = WorldData.Services.MarketEntrance;
            RadioConsolePoint = WorldData.Services.RadioConsolePoint;
        }

        _rng = new RandomNumberGenerator { Seed = (ulong)Seed };
        // Detalhes (lojas, ar-condicionados, orelhões) usam outro gerador para não mudar o resto da cidade.
        _decoRng = new RandomNumberGenerator { Seed = (ulong)Seed * 7919UL + 13UL };
        BuildResources();
        BuildLandmarkResources();

        _staticRoot = new Node3D { Name = "Static" };
        _lightsRoot = new Node3D { Name = "Lights" };
        AddChild(_staticRoot);
        AddChild(_lightsRoot);

        if (Biome == WorldBiome.Farm)
        {
            BuildFarm();
            return;
        }

        BuildGround();

        if (_useLogicalBlockPlans)
        {
            PlayerController player = GetParent()?.GetNodeOrNull<PlayerController>("Player");
            Vector3 fallback = player != null
                ? player.GlobalPosition
                : new Vector3(-3f, 0f, 8f);
            string profession = GetInitialProfessionForWorld();
            IReadOnlyList<WorldChunkData> initialChunks =
                WorldData.GetInitialChunks(profession, fallback, radius: 1);
            foreach (WorldChunkData chunk in initialChunks)
                BuildChunkVisuals(chunk);
        }
        else
        {
            for (int bx = -Radius; bx <= Radius; bx++)
            {
                for (int bz = -Radius; bz <= Radius; bz++)
                {
                    if (bx == 0 && bz == 0)
                        BuildDepot();
                    else
                        BuildBlock(bx, bz);
                }
            }
        }

        BuildCurbMarkings();
        BuildBikeLanes();
        BuildStreetDrainage();
        BuildStreetLamps();
        BuildStreetSigns();
        BuildPhoneBooths();
        BuildHydrants();
        BuildStreetProps();
        BuildStrayDogs();
        BuildParkedCars();
        float outside = Radius * Period + Period * 0.5f + RoadWidth * 0.5f + 6f; // além da última rua
        BuildRadioTower(new Vector3(-outside, 0f, -outside));
        BuildBoundary();
        BuildBillboards();
        OptimizeVisibility(this);

        if (_useLogicalBlockPlans)
        {
            PlayerController player = GetParent()?.GetNodeOrNull<PlayerController>("Player");
            if (player != null)
                AddChild(new WorldChunkManager { City = this, Target = player });
        }
    }

    /// <summary>Otimização (v0.9.7): peça pequena (banco, placa, ar-condicionado, lixeira...) some além de
    /// 90 m e média além de 170 m, dentro da neblina; luz de poste apaga suave além de 70 m. Prédios e
    /// chão ficam sempre. Quem já tem distância própria (capim, árvores) não é mexido.</summary>
    public static void OptimizeVisibility(Node root)
    {
        foreach (Node n in root.FindChildren("*", "", true, false))
        {
            switch (n)
            {
                case GeometryInstance3D g when g.VisibilityRangeEnd <= 0f && g is not MultiMeshInstance3D:
                {
                    Aabb box = g is VisualInstance3D v ? v.GetAabb() : new Aabb();
                    Vector3 size = box.Size * g.GlobalTransform.Basis.Scale;
                    float biggest = Mathf.Max(size.X, Mathf.Max(size.Y, size.Z));
                    if (biggest < 1.6f)
                        g.VisibilityRangeEnd = 110f;
                    else if (biggest < 5f)
                        g.VisibilityRangeEnd = 170f;
                    if (g.VisibilityRangeEnd > 0f)
                        g.VisibilityRangeEndMargin = 10f;
                    if (biggest < 0.6f)
                        g.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
                    break;
                }
                case OmniLight3D or SpotLight3D:
                {
                    var l = (Light3D)n;
                    if (!l.DistanceFadeEnabled)
                    {
                        l.DistanceFadeEnabled = true;
                        l.DistanceFadeBegin = 70f;
                        l.DistanceFadeLength = 20f;
                        l.DistanceFadeShadow = 30f;
                    }
                    break;
                }
            }
        }
    }

    /// <summary>Dia: postes e holofotes apagados, poucas janelas acesas.</summary>
    public void SetDayMode(bool day) => SetNightAmount(day ? 0f : 1f);

    /// <summary>0 = dia claro, 1 = noite fechada. Janelas acendem aos poucos; postes ligam no fim da tarde.</summary>
    public void SetNightAmount(float night)
    {
        night = Mathf.Clamp(night, 0f, 1f);
        IsDay = night < 0.5f;
        bool lampsOn = night > 0.3f;
        if (_lightsRoot != null && _lightsRoot.Visible != lampsOn)
            _lightsRoot.Visible = lampsOn;
        _buildingMaterial?.SetShaderParameter("night", night);
        if (_lampHead != null)
            _lampHead.EmissionEnergyMultiplier = lampsOn ? Mathf.Lerp(1.2f, 3f, night) : 0.2f;
        UpdateSignGlow(night);
        if (_farmWindow != null)
            _farmWindow.EmissionEnergyMultiplier = Mathf.Lerp(0f, 1.6f, Mathf.Clamp((night - 0.2f) / 0.6f, 0f, 1f));
        DepotNightChanged?.Invoke(night);
    }

    /// <summary>Avisado a cada mudança de claridade (luzes internas do depósito etc.).</summary>
    public event System.Action<float> DepotNightChanged;

    // ------------------------------------------------------------------ recursos

    private void BuildResources()
    {
        _unitBox = new BoxMesh { Size = Vector3.One };
        _unitCylinder = new CylinderMesh { TopRadius = 0.5f, BottomRadius = 0.5f, Height = 1f, RadialSegments = 8, Rings = 1 };
        _treeTop = new CylinderMesh { TopRadius = 0f, BottomRadius = 0.5f, Height = 1f, RadialSegments = 6, Rings = 1 };

        _buildingMaterial = new ShaderMaterial { Shader = GD.Load<Shader>("res://Shaders/building.gdshader") };
        _buildingMaterial.SetShaderParameter("wall_atlas", GD.Load<Texture2D>("res://Assets/Textures/City/wall_atlas.png"));
        _groundMaterial = new ShaderMaterial { Shader = GD.Load<Shader>("res://Shaders/road_ground.gdshader") };
        _groundMaterial.SetShaderParameter("period", Period);
        _groundMaterial.SetShaderParameter("road_half_width", RoadWidth * 0.5f);
        _groundMaterial.SetShaderParameter("asphalt_tex", GD.Load<Texture2D>("res://Assets/Textures/City/asphalt.png"));
        _sidewalkMaterial = new ShaderMaterial { Shader = GD.Load<Shader>("res://Shaders/sidewalk.gdshader") };
        _sidewalkMaterial.SetShaderParameter("slab_tex", GD.Load<Texture2D>("res://Assets/Textures/City/sidewalk.png"));
        _sidewalkMaterial.SetShaderParameter("pt_tex", GD.Load<Texture2D>("res://Assets/Textures/City/sidewalk_pt.svg"));
        _sidewalkMaterial.SetShaderParameter("block_period", Period);

        _darkMetal = Flat(new Color(0.12f, 0.12f, 0.13f), 0.6f);
        _concrete = Flat(new Color(0.38f, 0.37f, 0.35f));
        _grass = Flat(new Color(0.12f, 0.2f, 0.1f));
        // Gramado com textura (v0.9.7) projetada pelo mundo: qualquer tamanho de canteiro funciona.
        _grass.AlbedoColor = new Color(0.32f, 0.5f, 0.26f);
        _grass.AlbedoTexture = GD.Load<Texture2D>("res://Assets/Textures/City/lawn.png");
        _grass.Uv1Triplanar = true;
        _grass.Uv1WorldTriplanar = true;
        _grass.Uv1Scale = Vector3.One * 0.5f;
        _grass.TextureFilter = BaseMaterial3D.TextureFilterEnum.NearestWithMipmaps;
        _leaves = Flat(new Color(0.08f, 0.17f, 0.09f));
        _trunk = Flat(new Color(0.22f, 0.15f, 0.1f));
        _warehouse = Flat(new Color(0.28f, 0.33f, 0.36f), 0.7f);
        _glass = Flat(new Color(0.05f, 0.07f, 0.08f), 0.2f);
        _stopRed = Flat(new Color(0.75f, 0.06f, 0.05f), 0.5f);
        _signBlue = Flat(new Color(0.1f, 0.25f, 0.65f), 0.5f);
        _signWhite = Flat(new Color(0.92f, 0.92f, 0.9f), 0.5f);
        _octagon = new CylinderMesh { TopRadius = 0.5f, BottomRadius = 0.5f, Height = 1f, RadialSegments = 8, Rings = 1 };

        _lampHead = Flat(new Color(1f, 0.8f, 0.5f));
        _lampHead.EmissionEnabled = true;
        _lampHead.Emission = new Color(1f, 0.72f, 0.4f);
        _lampHead.EmissionEnergyMultiplier = 3f;

        _redBeacon = Flat(new Color(1f, 0.1f, 0.05f));
        _redBeacon.EmissionEnabled = true;
        _redBeacon.Emission = new Color(1f, 0.08f, 0.03f);
        _redBeacon.EmissionEnergyMultiplier = 4f;
    }

    private static StandardMaterial3D Flat(Color color, float roughness = 0.95f) =>
        new() { AlbedoColor = color, Roughness = roughness };

    private StandardMaterial3D CarMaterial(Color color)
    {
        if (!_carMaterials.TryGetValue(color, out StandardMaterial3D mat))
        {
            mat = Flat(color, 0.45f);
            mat.Metallic = 0.3f;
            _carMaterials[color] = mat;
        }
        return mat;
    }

    // ------------------------------------------------------------------ helpers

    /// <summary>Caixa visual (cubo escalado) com colisão opcional. Retorna a malha para ajustes.</summary>
    private MeshInstance3D AddBox(Node3D parent, Vector3 center, Vector3 size, Material material, bool collide = true, float rotationY = 0f)
    {
        var mesh = new MeshInstance3D { Mesh = _unitBox, Scale = size, MaterialOverride = material };

        if (!collide)
        {
            mesh.Position = center;
            mesh.Rotation = new Vector3(0f, rotationY, 0f);
            parent.AddChild(mesh);
            return mesh;
        }

        var body = new StaticBody3D { Position = center, Rotation = new Vector3(0f, rotationY, 0f), CollisionLayer = 1, CollisionMask = 0 };
        body.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = size } });
        body.AddChild(mesh);
        parent.AddChild(body);
        return mesh;
    }

    private MeshInstance3D AddCylinder(Node3D parent, Vector3 center, float radius, float height, Material material, Mesh mesh = null)
    {
        var mi = new MeshInstance3D
        {
            Mesh = mesh ?? _unitCylinder,
            Scale = new Vector3(radius * 2f, height, radius * 2f),
            Position = center,
            MaterialOverride = material,
        };
        parent.AddChild(mi);
        return mi;
    }

    private Vector3 BlockCenter(int bx, int bz) => new(bx * Period, 0f, bz * Period);

    /// <summary>Nome da rua vertical (x = k·Period + Period/2) e da avenida horizontal (z = ...), para o minimapa.</summary>
    public string StreetNameX(int k) => StreetX(k);
    public string StreetNameZ(int k) => StreetZ(k);

    private string StreetX(int index) => StreetNamesX[Mathf.PosMod(index, StreetNamesX.Length)];
    private string StreetZ(int index) => StreetNamesZ[Mathf.PosMod(index, StreetNamesZ.Length)];

    // ------------------------------------------------------------------ chão e ruas

    private void BuildGround()
    {
        float size = (Radius * 2 + 3) * Period;
        var body = new StaticBody3D { Name = "Ground", CollisionLayer = 1, CollisionMask = 0 };
        body.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(size, 1f, size) }, Position = new Vector3(0f, -0.5f, 0f) });
        body.AddChild(new MeshInstance3D { Mesh = new PlaneMesh { Size = new Vector2(size, size) }, MaterialOverride = _groundMaterial });
        _staticRoot.AddChild(body);
    }

    // ------------------------------------------------------------------ quarteirões

    private void BuildBlock(int bx, int bz)
    {
        if (_useLogicalBlockPlans &&
            WorldData.TryGetBlock(new WorldChunkCoordinate(bx, bz), out WorldBlockData plan))
        {
            // Cada quarteirão recebe RNGs próprios. A ordem de construção ou carregamento dos
            // chunks deixa de alterar prédios, árvores, carros e decoração dos outros quarteirões.
            RandomNumberGenerator previousRng = _rng;
            RandomNumberGenerator previousDecoRng = _decoRng;
            _rng = new RandomNumberGenerator { Seed = plan.VisualSeed };
            _decoRng = new RandomNumberGenerator { Seed = plan.DecorationSeed };
            try
            {
                BuildBlockCore(bx, bz, plan.Kind);
            }
            finally
            {
                _rng = previousRng;
                _decoRng = previousDecoRng;
            }
            return;
        }

        BuildBlockCore(bx, bz, null);
    }

    private void BuildBlockCore(int bx, int bz, WorldBlockKind? plannedKind)
    {
        Vector3 center = BlockCenter(bx, bz);
        var block = new Node3D { Name = $"Block_{bx}_{bz}" };
        (_activeChunkRoot ?? _staticRoot).AddChild(block);

        // Calçada elevada (meio-fio).
        AddBox(block, center + new Vector3(0f, CurbHeight * 0.5f, 0f), new Vector3(BlockSize, CurbHeight, BlockSize), _sidewalkMaterial);

        RegisterDeliveryPoint(bx, bz);

        float inner = BlockSize - SidewalkWidth * 2f;

        // Quarteirões especiais perto do depósito.
        if (bx == FireStationBlock.X && bz == FireStationBlock.Y)
        {
            BuildFireStation(block, center);
            return;
        }
        if (bx == PoliceStationBlock.X && bz == PoliceStationBlock.Y)
        {
            BuildPoliceStation(block, center);
            return;
        }
        if (TryBuildServiceBlock(block, center, bx, bz))
            return;

        if (plannedKind.HasValue)
        {
            switch (plannedKind.Value)
            {
                case WorldBlockKind.Park:
                    BuildPark(block, center, inner);
                    break;
                case WorldBlockKind.ParkingLot:
                    BuildParkingLot(block, center, inner);
                    break;
                default:
                    BuildBuildings(block, center, inner, bx, bz);
                    break;
            }
            return;
        }

        float roll = _rng.Randf();
        if (roll < ParkChance)
            BuildPark(block, center, inner);
        else if (roll < ParkChance + ParkingLotChance)
            BuildParkingLot(block, center, inner);
        else
            BuildBuildings(block, center, inner, bx, bz);
    }

    private void BuildBuildings(Node3D block, Vector3 center, float inner, int bx, int bz)
    {
        // Divide o miolo do quarteirão em 1, 2 ou 4 lotes.
        float split = _rng.Randf();
        var lots = new List<Rect2>();
        float h = inner * 0.5f;
        if (split < 0.2f)
        {
            lots.Add(new Rect2(-h, -h, inner, inner));
        }
        else if (split < 0.5f)
        {
            bool alongX = _rng.Randf() < 0.5f;
            if (alongX)
            {
                lots.Add(new Rect2(-h, -h, inner, h));
                lots.Add(new Rect2(-h, 0f, inner, h));
            }
            else
            {
                lots.Add(new Rect2(-h, -h, h, inner));
                lots.Add(new Rect2(0f, -h, h, inner));
            }
        }
        else
        {
            lots.Add(new Rect2(-h, -h, h, h));
            lots.Add(new Rect2(0f, -h, h, h));
            lots.Add(new Rect2(-h, 0f, h, h));
            lots.Add(new Rect2(0f, 0f, h, h));
        }

        // Mais alto perto do "centro" (anel ao redor do depósito), mais baixo nas bordas.
        float ring = Mathf.Max(Mathf.Abs(bx), Mathf.Abs(bz));
        float heightBias = Mathf.Lerp(1.4f, 0.6f, (ring - 1f) / Mathf.Max(1f, Radius - 1f));

        foreach (Rect2 lot in lots)
        {
            float margin = _rng.RandfRange(0.6f, 2.0f);
            float w = lot.Size.X - margin * 2f;
            float d = lot.Size.Y - margin * 2f;
            if (w < 5f || d < 5f)
                continue;

            float height = _rng.RandfRange(7f, 20f) * heightBias;
            if (_rng.Randf() < 0.18f)
                height = _rng.RandfRange(26f, 48f) * heightBias;
            height = Mathf.Max(4f, Mathf.Round(height / 3.2f) * 3.2f + 0.4f);

            Vector3 lotCenter = center + new Vector3(lot.Position.X + lot.Size.X * 0.5f, CurbHeight, lot.Position.Y + lot.Size.Y * 0.5f);
            Vector3 front = GetBuildingFront(lotCenter, center);
            bool hasStorefront = _decoRng.Randf() < StorefrontChance;
            float buildingHeight = hasStorefront ? 4.4f : height;
            BuildBuilding(block, lotCenter, w, d, buildingHeight, hasStorefront);
            DecorateBuilding(block, lotCenter, w, d, buildingHeight, center, front, hasStorefront);
        }
    }

    private void BuildBuilding(Node3D parent, Vector3 baseCenter, float width, float depth, float height, bool isStorefront = false)
    {
        Color wall = WallPalette[_rng.RandiRange(0, WallPalette.Length - 1)];
        float lightSeed = _rng.Randf() * 100f;
        float litRatio = _rng.RandfRange(0.15f, 0.45f);

        MeshInstance3D main = AddBox(parent, baseCenter + new Vector3(0f, height * 0.5f, 0f), new Vector3(width, height, depth), _buildingMaterial);
        SetBuildingParams(main, wall, lightSeed, litRatio);

        float roofY = baseCenter.Y + height;

        // Recuo: um bloco menor em cima dos prédios altos.
        if (height > 18f && _rng.Randf() < 0.55f)
        {
            float topH = Mathf.Round(_rng.RandfRange(6f, 14f) / 3.2f) * 3.2f;
            float topW = width * _rng.RandfRange(0.5f, 0.75f);
            float topD = depth * _rng.RandfRange(0.5f, 0.75f);
            MeshInstance3D top = AddBox(parent, new Vector3(baseCenter.X, roofY + topH * 0.5f, baseCenter.Z), new Vector3(topW, topH, topD), _buildingMaterial);
            SetBuildingParams(top, wall * 0.92f, lightSeed + 7f, litRatio);
            roofY += topH;
            width = topW;
            depth = topD;
        }

        // Mureta no telhado (quatro paredes finas).
        Vector3 roofCenter = new(baseCenter.X, roofY + 0.4f, baseCenter.Z);
        AddBox(parent, roofCenter + new Vector3(0f, 0f, depth * 0.5f), new Vector3(width, 0.8f, 0.25f), _concrete, false);
        AddBox(parent, roofCenter - new Vector3(0f, 0f, depth * 0.5f), new Vector3(width, 0.8f, 0.25f), _concrete, false);
        AddBox(parent, roofCenter + new Vector3(width * 0.5f, 0f, 0f), new Vector3(0.25f, 0.8f, depth), _concrete, false);
        AddBox(parent, roofCenter - new Vector3(width * 0.5f, 0f, 0f), new Vector3(0.25f, 0.8f, depth), _concrete, false);

        if (!isStorefront)
        {
            // Ar-condicionados e caixas d'água.
            int props = _rng.RandiRange(0, 3);
            for (int i = 0; i < props; i++)
            {
                var offset = new Vector3(_rng.RandfRange(-width * 0.35f, width * 0.35f), 0f, _rng.RandfRange(-depth * 0.35f, depth * 0.35f));
                if (_rng.Randf() < 0.6f)
                    AddBox(parent, new Vector3(baseCenter.X, roofY + 0.6f, baseCenter.Z) + offset, new Vector3(1.6f, 1.2f, 1.2f), _darkMetal, false);
                else
                    AddCylinder(parent, new Vector3(baseCenter.X, roofY + 1.5f, baseCenter.Z) + offset, 1.1f, 3f, _darkMetal);
            }

            // Antena com luz vermelha.
            if (_rng.Randf() < 0.3f)
            {
                float antennaH = _rng.RandfRange(4f, 9f);
                Vector3 pos = new(baseCenter.X + width * 0.25f, roofY + antennaH * 0.5f, baseCenter.Z - depth * 0.25f);
                AddCylinder(parent, pos, 0.08f, antennaH, _darkMetal);
                AddBox(parent, pos + new Vector3(0f, antennaH * 0.5f + 0.15f, 0f), new Vector3(0.3f, 0.3f, 0.3f), _redBeacon, false);
            }
        }
    }

    private static void SetBuildingParams(GeometryInstance3D mesh, Color wall, float seed, float litRatio, int wallKind = -1)
    {
        // Acabamento (v0.9.7: reboco, tijolo, pastilha, chapisco) tirado do seed que já existe,
        // sem gastar sorteio do _rng (a cidade tem que sair igual pra todo mundo no online).
        if (wallKind < 0)
            wallKind = Mathf.PosMod((int)(seed * 13.7f), 4);
        mesh.SetInstanceShaderParameter("wall_kind", (float)wallKind);
        mesh.SetInstanceShaderParameter("wall_color", wall);
        mesh.SetInstanceShaderParameter("light_seed", seed);
        mesh.SetInstanceShaderParameter("lit_ratio", litRatio);
    }

    /// <summary>Caramelos de rua (v0.9.5) nas esquinas: dê um espetinho e um deles segue você.</summary>
    private void BuildStrayDogs()
    {
        var root = new Node3D { Name = "Caramelos" };
        _staticRoot.AddChild(root);
        float corner = BlockSize * 0.5f - 1.3f;
        int placed = 0;
        for (int bx = -Radius; bx <= Radius && placed < 8; bx++)
        {
            for (int bz = -Radius; bz <= Radius && placed < 8; bz++)
            {
                Vector3 c = BlockCenter(bx, bz);
                int h = AssetProps.Hash(c, 21);
                if (h % 5 != 0)
                    continue;
                float sx = h % 2 == 0 ? 1f : -1f, sz = (h / 2) % 2 == 0 ? 1f : -1f;
                var pos = c + new Vector3(sx * corner, CurbHeight, sz * corner);
                root.AddChild(FarmAnimal.Create("ShibaInu.gltf", 0.9f, pos, h % 628 / 100f, 1.2f));
                placed++;
            }
        }
        // Um sempre perto do depósito, do lado da barraca do Zé.
        root.AddChild(FarmAnimal.Create("ShibaInu.gltf", 0.9f, new Vector3(-4.5f, CurbHeight, 31.4f), 0.4f, 1.2f));
    }

    private void BuildPark(Node3D block, Vector3 center, float inner)
    {
        AddBox(block, center + new Vector3(0f, CurbHeight + 0.05f, 0f), new Vector3(inner, 0.1f, inner), _grass, false);
        GrassTufts.Fill(block, center + new Vector3(0f, CurbHeight + 0.1f, 0f), new Vector2(inner - 0.6f, inner - 0.6f), 0.55f, new Color(0.8f, 0.95f, 0.75f));

        int trees = _rng.RandiRange(7, 14);
        for (int i = 0; i < trees; i++)
        {
            var pos = center + new Vector3(_rng.RandfRange(-inner * 0.45f, inner * 0.45f), CurbHeight, _rng.RandfRange(-inner * 0.45f, inner * 0.45f));
            float scale = _rng.RandfRange(0.8f, 1.4f);
            // Árvore da Quaternius (Stylized Nature); se o modelo faltar, volta para a de cones.
            string model = NatureKit.CommonTrees[AssetProps.Hash(pos, 4) % NatureKit.CommonTrees.Length];
            if (NatureKit.Spawn(block, model, pos, AssetProps.Hash(pos, 7) % 628 / 100f, scale * 0.7f, 0.25f) != null)
                continue;
            var tree = new StaticBody3D { Position = pos, CollisionLayer = 1, CollisionMask = 0 };
            tree.AddChild(new CollisionShape3D { Shape = new CylinderShape3D { Radius = 0.25f, Height = 3f }, Position = new Vector3(0f, 1.5f, 0f) });
            AddCylinder(tree, new Vector3(0f, 1.2f * scale, 0f), 0.18f * scale, 2.4f * scale, _trunk);
            AddCylinder(tree, new Vector3(0f, 3.6f * scale, 0f), 1.6f * scale, 3.6f * scale, _leaves, _treeTop);
            AddCylinder(tree, new Vector3(0f, 5.0f * scale, 0f), 1.1f * scale, 2.6f * scale, _leaves, _treeTop);
            block.AddChild(tree);
        }

        // Bancos.
        for (int i = 0; i < 3; i++)
        {
            var pos = center + new Vector3(_rng.RandfRange(-inner * 0.35f, inner * 0.35f), CurbHeight + 0.25f, _rng.RandfRange(-inner * 0.35f, inner * 0.35f));
            AddBox(block, pos, new Vector3(1.8f, 0.5f, 0.5f), _trunk, true, _rng.RandfRange(0f, Mathf.Pi));
        }
    }

    private void BuildParkingLot(Node3D block, Vector3 center, float inner)
    {
        AddBox(block, center + new Vector3(0f, CurbHeight + 0.02f, 0f), new Vector3(inner, 0.04f, inner), _darkMetal, false);
        PaintParkingStripes(block, center, inner);
        int cars = _rng.RandiRange(3, 7);
        for (int i = 0; i < cars; i++)
        {
            float x = -inner * 0.4f + i * (inner * 0.8f / Mathf.Max(1, cars - 1));
            // Encaixa no meio de uma vaga pintada (a primeira é a PCD, fica livre).
            int stalls = Mathf.FloorToInt(inner * 0.86f / 3f);
            float sx0 = -stalls * 3f * 0.5f;
            int slot = Mathf.Clamp(Mathf.RoundToInt((x - sx0 - 1.5f) / 3f), 1, stalls - 1);
            x = sx0 + slot * 3f + 1.5f;
            float z = (_rng.Randf() < 0.5f ? -1f : 1f) * inner * 0.25f;
            BuildCar(block, center + new Vector3(x, CurbHeight, z), 0f);
        }
    }

    private StandardMaterial3D _stripeYellow, _stripeWhite, _stripeBlue;

    /// <summary>Faixas coloridas das vagas (amarelas), corredor branco no meio e uma vaga azul PCD em cada fileira.
    /// Não usa sorteio (a cidade continua igual para todos).</summary>
    private void PaintParkingStripes(Node3D block, Vector3 center, float inner)
    {
        _stripeYellow ??= Flat(new Color(0.95f, 0.78f, 0.12f), 0.7f);
        _stripeWhite ??= Flat(new Color(0.9f, 0.9f, 0.88f), 0.7f);
        _stripeBlue ??= Flat(new Color(0.12f, 0.35f, 0.75f), 0.7f);
        float y = CurbHeight + 0.045f;
        const float stall = 3.0f, depth = 5.2f;
        int count = Mathf.FloorToInt(inner * 0.86f / stall);
        float x0 = -count * stall * 0.5f;
        foreach (float row in new[] { -1f, 1f })
        {
            float zc = row * inner * 0.25f;
            for (int i = 0; i <= count; i++)
                AddBox(block, center + new Vector3(x0 + i * stall, y, zc), new Vector3(0.14f, 0.012f, depth), _stripeYellow, false);
            // Linha do fundo da vaga e vaga PCD azul na ponta.
            AddBox(block, center + new Vector3(0f, y, zc + row * depth * 0.5f), new Vector3(count * stall, 0.012f, 0.14f), _stripeYellow, false);
            AddBox(block, center + new Vector3(x0 + stall * 0.5f, y - 0.002f, zc), new Vector3(stall - 0.2f, 0.01f, depth - 0.2f), _stripeBlue, false);
            block.AddChild(new Label3D
            {
                Text = "PCD",
                FontSize = 64,
                PixelSize = 0.012f,
                Modulate = Colors.White,
                OutlineSize = 0,
                Position = center + new Vector3(x0 + stall * 0.5f, y + 0.01f, zc),
                Rotation = new Vector3(-Mathf.Pi * 0.5f, 0f, 0f),
            });
        }
        // Corredor: faixa branca tracejada no meio.
        for (float x = x0; x < -x0; x += 2.4f)
            AddBox(block, center + new Vector3(x + 0.6f, y, 0f), new Vector3(1.2f, 0.012f, 0.14f), _stripeWhite, false);
    }

    private void RegisterDeliveryPoint(int bx, int bz)
    {
        if (_useLogicalWorldData)
            return; // Os pontos da cidade já foram calculados na fase lógica.

        // Compatibilidade V7/offline: mantém os pontos legados até a migração completa do gerador.
        int side = _rng.RandiRange(0, 3);
        Vector3 center = BlockCenter(bx, bz);
        float edge = BlockSize * 0.5f - 1.5f;
        Vector3 offset;
        string street;
        switch (side)
        {
            case 0: offset = new Vector3(edge, 0f, 0f); street = StreetX(bx); break;
            case 1: offset = new Vector3(-edge, 0f, 0f); street = StreetX(bx - 1); break;
            case 2: offset = new Vector3(0f, 0f, edge); street = StreetZ(bz); break;
            default: offset = new Vector3(0f, 0f, -edge); street = StreetZ(bz - 1); break;
        }

        _deliveryPoints.Add(new DeliveryPoint
        {
            Position = center + offset + new Vector3(0f, CurbHeight, 0f),
            Address = $"{street}, nº {_rng.RandiRange(10, 999)}",
        });
    }

    // ------------------------------------------------------------------ depósito

    private void BuildDepot(bool farm = false)
    {
        var depot = new Node3D { Name = "Depot" };
        _staticRoot.AddChild(depot);
        float half = BlockSize * 0.5f;

        // Galpão ao fundo, virado para +Z: oco, com interior e escritório (CityGenerator.Depot.cs).
        BuildWarehouse(depot);
        AddBox(depot, new Vector3(0f, 9.4f, -half + 7f), new Vector3(27f, 0.8f, 13f), _darkMetal); // telhado
        AddBox(depot, new Vector3(9.5f, 5.5f, -half + 13.05f), new Vector3(4f, 1.2f, 0.1f), _glass, false);
        BuildDepotFleet(depot);

        var sign = new Label3D
        {
            Text = farm ? "COOPERATIVA RANCHO FUNDO · DEAD FREQUENCY" : "DEAD FREQUENCY · ENTREGAS",
            FontSize = 96,
            PixelSize = 0.01f,
            Modulate = UiStyle.Phosphor,
            OutlineSize = 0,
            Position = new Vector3(0f, 7.6f, -half + 13.1f),
        };
        depot.AddChild(sign);

        // Feixe laranja visível de longe: referência para voltar ao depósito.
        var beaconMaterial = new ShaderMaterial { Shader = GD.Load<Shader>("res://Shaders/beacon_beam.gdshader") };
        beaconMaterial.SetShaderParameter("beam_color", new Color(1f, 0.55f, 0.12f));
        beaconMaterial.SetShaderParameter("intensity", 0.8f);
        depot.AddChild(new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = 0.5f, BottomRadius = 0.5f, Height = 1f, RadialSegments = 12, Rings = 1 },
            MaterialOverride = beaconMaterial,
            Scale = new Vector3(1.6f, 140f, 1.6f),
            Position = new Vector3(0f, 79f, -half + 7f),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        });

        // Holofotes iluminando o pátio.
        foreach (float x in new[] { -9f, 9f })
        {
            AddBox(depot, new Vector3(x, 8f, -half + 13.3f), new Vector3(0.8f, 0.4f, 0.5f), _lampHead, false);
            var spot = new SpotLight3D
            {
                Position = new Vector3(x, 8f, -half + 13.6f),
                RotationDegrees = new Vector3(-50f, 180f, 0f),
                LightColor = new Color(1f, 0.92f, 0.8f),
                LightEnergy = 5f,
                SpotRange = 30f,
                SpotAngle = 50f,
                ShadowEnabled = true,
            };
            _lightsRoot.AddChild(spot);
        }

        // Muro baixo (cerca de madeira no rancho) com portões para as ruas de +X e +Z.
        float wallH = farm ? 1.2f : 1.3f;
        float gate = 10f;
        float seg = (BlockSize - gate) * 0.5f;
        Material wallMat = farm ? _trunk : _concrete;
        AddBox(depot, new Vector3(0f, wallH * 0.5f, -half), new Vector3(BlockSize, wallH, 0.3f), wallMat);
        AddBox(depot, new Vector3(-half, wallH * 0.5f, 0f), new Vector3(0.3f, wallH, BlockSize), wallMat);
        AddBox(depot, new Vector3(-half + seg * 0.5f, wallH * 0.5f, half), new Vector3(seg, wallH, 0.3f), wallMat);
        AddBox(depot, new Vector3(half - seg * 0.5f, wallH * 0.5f, half), new Vector3(seg, wallH, 0.3f), wallMat);
        AddBox(depot, new Vector3(half, wallH * 0.5f, -half + seg * 0.5f), new Vector3(0.3f, wallH, seg), wallMat);
        AddBox(depot, new Vector3(half, wallH * 0.5f, half - seg * 0.5f), new Vector3(0.3f, wallH, seg), wallMat);

        // Pilha de paletes e tambores decorativos.
        for (int i = 0; i < 4; i++)
            AddBox(depot, new Vector3(-12f + i * 1.3f, 0.1f + i % 2 * 0.2f, -half + 15f), new Vector3(1.2f, 0.2f + i % 2 * 0.2f, 1.0f), _trunk);
        for (int i = 0; i < 3; i++)
            AddCylinder(depot, new Vector3(13f, 0.45f, -half + 15f + i * 0.9f), 0.35f, 0.9f, _darkMetal);
    }

    // ------------------------------------------------------------------ postes

    private void BuildStreetLamps()
    {
        float edge = BlockSize * 0.5f - 0.7f;
        for (int bx = -Radius; bx <= Radius; bx++)
        {
            for (int bz = -Radius; bz <= Radius; bz++)
            {
                Vector3 c = BlockCenter(bx, bz);
                float y = (bx == 0 && bz == 0) ? 0f : CurbHeight;
                // Lado +X (ilumina a rua vertical) e lado +Z (ilumina a avenida).
                BuildLamp(c + new Vector3(edge, y, -BlockSize * 0.25f), Vector3.Right);
                BuildLamp(c + new Vector3(-BlockSize * 0.25f, y, edge), Vector3.Back);
            }
        }
    }

    private void BuildLamp(Vector3 basePos, Vector3 towardRoad)
    {
        RegisterObstacle(basePos);
        const float height = 6.5f;
        var lamp = new StaticBody3D { Position = basePos, CollisionLayer = 1, CollisionMask = 0 };
        lamp.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(0.25f, height, 0.25f) }, Position = new Vector3(0f, height * 0.5f, 0f) });
        AddCylinder(lamp, new Vector3(0f, height * 0.5f, 0f), 0.09f, height, _darkMetal);
        Vector3 armCenter = towardRoad * 0.8f + new Vector3(0f, height - 0.1f, 0f);
        var armSize = new Vector3(Mathf.Abs(towardRoad.X) * 1.6f + 0.1f, 0.1f, Mathf.Abs(towardRoad.Z) * 1.6f + 0.1f);
        AddBox(lamp, armCenter, armSize, _darkMetal, false);
        Vector3 headPos = towardRoad * 1.5f + new Vector3(0f, height - 0.25f, 0f);
        AddBox(lamp, headPos, new Vector3(0.6f, 0.15f, 0.6f), _lampHead, false);
        _staticRoot.AddChild(lamp);

        var light = new OmniLight3D
        {
            Position = basePos + headPos + new Vector3(0f, -0.3f, 0f),
            LightColor = new Color(1f, 0.72f, 0.42f),
            LightEnergy = 1.8f,
            OmniRange = LampLightRange,
            OmniAttenuation = 1.2f,
            DistanceFadeEnabled = true,
            DistanceFadeBegin = 70f,
            DistanceFadeLength = 25f,
        };
        _lightsRoot.AddChild(light);
    }

    // ------------------------------------------------------------------ carros

    private void BuildParkedCars()
    {
        float laneOffset = RoadWidth * 0.5f - 1.4f;
        float reach = BlockSize * 0.5f - 4f;
        for (int k = -Radius - 1; k <= Radius; k++)
        {
            float roadPos = k * Period + Period * 0.5f;
            for (int j = -Radius; j <= Radius; j++)
            {
                float along = j * Period;
                for (int side = -1; side <= 1; side += 2)
                {
                    bool hasCurb = side < 0
                        ? k >= -Radius && k <= Radius
                        : k + 1 >= -Radius && k + 1 <= Radius;
                    // Ruas paralelas a Z (x fixo).
                    if (_rng.Randf() < ParkedCarChance)
                    {
                        float offset = _rng.RandfRange(-reach, reach);
                        BuildCar(_staticRoot, new Vector3(roadPos + side * laneOffset, 0f, along + offset), 0f, offset, hasCurb);
                    }
                    // Avenidas paralelas a X (z fixo).
                    if (_rng.Randf() < ParkedCarChance)
                    {
                        float offset = _rng.RandfRange(-reach, reach);
                        BuildCar(_staticRoot, new Vector3(along + offset, 0f, roadPos + side * laneOffset), Mathf.Pi * 0.5f, offset, hasCurb);
                    }
                }
            }
        }
    }

    private readonly List<Node3D> _parkedCars = new();
    private readonly List<(Vector3 center, Vector3 dir, float halfLength)> _noParking = new();
    private bool _curbMarkingsBuilt;

    /// <summary>Proíbe estacionar num trecho de rua e pinta o meio-fio correspondente de amarelo.</summary>
    public void ForbidParking(Vector3 center, Vector3 dir, float halfLength, string signLabel = null)
    {
        Vector3 normalizedDir = dir.Normalized();
        _noParking.Add((center, normalizedDir, halfLength));
        for (int i = _parkedCars.Count - 1; i >= 0; i--)
        {
            if (IsInstanceValid(_parkedCars[i]) && InNoParking(_parkedCars[i].Position))
            {
                _parkedCars[i].QueueFree();
                _parkedCars.RemoveAt(i);
            }
        }
        if (_curbMarkingsBuilt)
            PaintNoParkingCurb(center, normalizedDir, halfLength);
        BuildNoParkingSigns(center, normalizedDir, signLabel);
    }

    private void BuildCurbMarkings()
    {
        _curbWhite = Flat(new Color(0.88f, 0.87f, 0.82f), 0.8f);
        _curbYellow = Flat(new Color(0.95f, 0.74f, 0.08f), 0.75f);
        for (int bx = -Radius; bx <= Radius; bx++)
        {
            for (int bz = -Radius; bz <= Radius; bz++)
            {
                Vector3 center = BlockCenter(bx, bz);
                PaintCurbEdge(center, Vector3.Right, Vector3.Back);
                PaintCurbEdge(center, Vector3.Left, Vector3.Forward);
                PaintCurbEdge(center, Vector3.Back, Vector3.Left);
                PaintCurbEdge(center, Vector3.Forward, Vector3.Right);
            }
        }

        _curbMarkingsBuilt = true;
        foreach ((Vector3 center, Vector3 dir, float halfLength) in _noParking)
            PaintNoParkingCurb(center, dir, halfLength);
    }

    private void PaintCurbEdge(Vector3 blockCenter, Vector3 outward, Vector3 along)
    {
        const float cornerClearance = 4f;
        const float markLength = 5f;
        const float markPitch = 7f;
        float half = BlockSize * 0.5f;
        float first = -half + cornerClearance;
        float last = half - cornerClearance;

        PaintCurbStripe(blockCenter, outward, along, -half + cornerClearance * 0.5f, cornerClearance, _curbYellow);
        PaintCurbStripe(blockCenter, outward, along, half - cornerClearance * 0.5f, cornerClearance, _curbYellow);

        for (float start = first; start < last; start += markPitch)
        {
            float length = Mathf.Min(markLength, last - start);
            if (length <= 0f)
                continue;
            float midpoint = start + length * 0.5f;
            Vector3 parkingSpot = blockCenter + outward * (half + RoadWidth * 0.5f - 1.4f) + along * midpoint;
            Material material = InNoParking(parkingSpot) ? _curbYellow : _curbWhite;
            PaintCurbStripe(blockCenter, outward, along, midpoint, length, material);
        }
    }

    private void PaintCurbStripe(Vector3 blockCenter, Vector3 outward, Vector3 along, float midpoint, float length, Material material)
    {
        Vector3 position = blockCenter + outward * (BlockSize * 0.5f + 0.015f) + along * midpoint;
        position.Y = CurbHeight * 0.5f;
        Vector3 size = Mathf.Abs(outward.X) > 0.5f
            ? new Vector3(0.03f, CurbHeight, length)
            : new Vector3(length, CurbHeight, 0.03f);
        AddBox(_staticRoot, position, size, material, false);
    }

    private void PaintNoParkingCurb(Vector3 center, Vector3 dir, float halfLength)
    {
        float length = Mathf.Max(1f, halfLength * 2f + 0.2f);
        if (Mathf.Abs(dir.Z) > 0.5f)
        {
            float roadX = Mathf.Round((center.X - Period * 0.5f) / Period) * Period + Period * 0.5f;
            foreach (float side in new[] { -1f, 1f })
            {
                Vector3 parkingSpot = new(roadX + side * (RoadWidth * 0.5f - 1.4f), 0f, center.Z);
                if (!InNoParking(parkingSpot))
                    continue;
                Vector3 curb = new(roadX + side * (RoadWidth * 0.5f - 0.045f), CurbHeight * 0.5f, center.Z);
                AddBox(_staticRoot, curb, new Vector3(0.03f, CurbHeight, length), _curbYellow, false);
            }
        }
        else
        {
            float roadZ = Mathf.Round((center.Z - Period * 0.5f) / Period) * Period + Period * 0.5f;
            foreach (float side in new[] { -1f, 1f })
            {
                Vector3 parkingSpot = new(center.X, 0f, roadZ + side * (RoadWidth * 0.5f - 1.4f));
                if (!InNoParking(parkingSpot))
                    continue;
                Vector3 curb = new(center.X, CurbHeight * 0.5f, roadZ + side * (RoadWidth * 0.5f - 0.045f));
                AddBox(_staticRoot, curb, new Vector3(length, CurbHeight, 0.03f), _curbYellow, false);
            }
        }
    }

    private bool InNoParking(Vector3 p)
    {
        foreach (var z in _noParking)
        {
            Vector3 d = p - z.center;
            d.Y = 0f;
            float along = d.Dot(z.dir);
            float across = (d - z.dir * along).Length();
            if (Mathf.Abs(along) < z.halfLength && across < 6f)
                return true;
        }
        return false;
    }

    private bool HasWhiteCurbMarking(float along)
    {
        const float cornerClearance = 4f;
        const float markLength = 5f;
        const float markPitch = 7f;
        float usableLength = BlockSize - cornerClearance * 2f;
        float offset = along + BlockSize * 0.5f - cornerClearance;
        if (offset < 0f || offset >= usableLength)
            return false;

        float start = Mathf.Floor(offset / markPitch) * markPitch;
        float length = Mathf.Min(markLength, usableLength - start);
        return offset - start < length;
    }

    private void BuildCar(Node3D parent, Vector3 groundPos, float rotationY, float? curbOffset = null, bool hasCurb = true)
    {
        Color color = CarPalette[_rng.RandiRange(0, CarPalette.Length - 1)];
        float roll = _rng.Randf();
        // Mesma quantidade de sorteios de antes (a cidade não muda no online); o Fuska pega uma fatia.
        CarType type = roll < 0.3f ? CarType.Sedan : roll < 0.52f ? CarType.Hatch : roll < 0.7f ? CarType.Fusca : roll < 0.88f ? CarType.Pickup : CarType.Kombi;
        var car = new StaticBody3D { Position = groundPos, Rotation = new Vector3(0f, rotationY + (_rng.Randf() < 0.5f ? 0f : Mathf.Pi), 0f), CollisionLayer = 1, CollisionMask = 0 };
        Vector3 size = CarModels.Build(car, type, color);
        car.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(size.X, size.Y - 0.2f, size.Z) }, Position = new Vector3(0f, size.Y * 0.5f + 0.1f, 0f) });
        if (InNoParking(groundPos) || (curbOffset.HasValue && (!hasCurb || !HasWhiteCurbMarking(curbOffset.Value))))
        {
            car.QueueFree();
            return;
        }
        parent.AddChild(car);
        _parkedCars.Add(car);
    }

    // ------------------------------------------------------------------ placas

    /// <summary>Placa de PARE em uma esquina de cada cruzamento e semáforo de pedestre na esquina oposta.</summary>
    private void BuildStreetSigns()
    {
        float corner = BlockSize * 0.5f - 0.6f;
        for (int bx = -Radius; bx <= Radius; bx++)
        {
            for (int bz = -Radius; bz <= Radius; bz++)
            {
                if (bx == 0 && bz == 0)
                    continue;
                Vector3 c = BlockCenter(bx, bz) + new Vector3(0f, CurbHeight, 0f);
                // PARE virada para quem vem pela rua vertical (indo para +Z).
                BuildStopSign(c + new Vector3(corner, 0f, corner - 1.2f), 0f);
                BuildCrosswalkSign(c + new Vector3(-corner, 0f, -corner + 1.2f), Mathf.Pi);
            }
        }
    }

    private void BuildNoParkingSigns(Vector3 center, Vector3 dir, string label)
    {
        const float laneOffset = 1.4f;
        const float sidewalkOffset = 1.1f;
        if (Mathf.Abs(dir.Z) > 0.5f)
        {
            float roadX = Mathf.Round((center.X - Period * 0.5f) / Period) * Period + Period * 0.5f;
            foreach (float side in new[] { -1f, 1f })
            {
                Vector3 parkingSpot = new(roadX + side * (RoadWidth * 0.5f - laneOffset), 0f, center.Z);
                if (!InNoParking(parkingSpot))
                    continue;

                Vector3 roadNormal = new(-side, 0f, 0f);
                Vector3 basePos = new(roadX + side * (RoadWidth * 0.5f + sidewalkOffset), CurbHeight, center.Z);
                BuildNoParkingSign(basePos, roadNormal, label);
            }
        }
        else
        {
            float roadZ = Mathf.Round((center.Z - Period * 0.5f) / Period) * Period + Period * 0.5f;
            foreach (float side in new[] { -1f, 1f })
            {
                Vector3 parkingSpot = new(center.X, 0f, roadZ + side * (RoadWidth * 0.5f - laneOffset));
                if (!InNoParking(parkingSpot))
                    continue;

                Vector3 roadNormal = new(0f, 0f, -side);
                Vector3 basePos = new(center.X, CurbHeight, roadZ + side * (RoadWidth * 0.5f + sidewalkOffset));
                BuildNoParkingSign(basePos, roadNormal, label);
            }
        }
    }

    private void BuildNoParkingSign(Vector3 basePos, Vector3 roadNormal, string label)
    {
        RegisterObstacle(basePos);
        float rotationY = Mathf.Atan2(-roadNormal.X, -roadNormal.Z);
        var sign = new StaticBody3D
        {
            Position = basePos,
            Rotation = new Vector3(0f, rotationY, 0f),
            CollisionLayer = 1,
            CollisionMask = 0,
        };
        sign.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(0.12f, 2.7f, 0.12f) }, Position = new Vector3(0f, 1.35f, 0f) });
        AddCylinder(sign, new Vector3(0f, 1.35f, 0f), 0.045f, 2.7f, _darkMetal);

        const float faceY = 2.15f;
        AddCylinder(sign, new Vector3(0f, faceY, 0f), 0.58f, 0.08f, _stopRed).Rotation = new Vector3(Mathf.Pi * 0.5f, 0f, 0f);
        AddCylinder(sign, new Vector3(0f, faceY, -0.045f), 0.48f, 0.08f, _signWhite).Rotation = new Vector3(Mathf.Pi * 0.5f, 0f, 0f);
        sign.AddChild(new Label3D
        {
            Text = "E",
            FontSize = 100,
            PixelSize = 0.006f,
            Modulate = Colors.Black,
            OutlineSize = 0,
            Position = new Vector3(0f, faceY, -0.09f),
            RotationDegrees = new Vector3(0f, 180f, 0f),
        });
        AddBox(sign, new Vector3(0f, faceY, -0.12f), new Vector3(0.07f, 1.05f, 0.03f), _stopRed, false)
            .Rotation = new Vector3(0f, 0f, -0.65f);

        if (!string.IsNullOrEmpty(label))
        {
            float panelWidth = Mathf.Max(1.2f, label.Length * 0.16f);
            const float panelY = 1.15f;
            AddBox(sign, new Vector3(0f, panelY, 0f), new Vector3(panelWidth, 0.48f, 0.08f), _stopRed, false);
            AddBox(sign, new Vector3(0f, panelY, -0.05f), new Vector3(panelWidth - 0.1f, 0.38f, 0.03f), _signWhite, false);
            sign.AddChild(new Label3D
            {
                Text = label,
                FontSize = 40,
                PixelSize = 0.005f,
                Modulate = Colors.Black,
                OutlineSize = 0,
                Position = new Vector3(0f, panelY, -0.075f),
                RotationDegrees = new Vector3(0f, 180f, 0f),
            });
        }

        _staticRoot.AddChild(sign);
    }

    private void BuildStopSign(Vector3 basePos, float rotationY)
    {
        RegisterObstacle(basePos);
        var sign = new StaticBody3D { Position = basePos, Rotation = new Vector3(0f, rotationY, 0f), CollisionLayer = 1, CollisionMask = 0 };
        sign.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(0.12f, 2.6f, 0.12f) }, Position = new Vector3(0f, 1.3f, 0f) });
        AddCylinder(sign, new Vector3(0f, 1.25f, 0f), 0.04f, 2.5f, _darkMetal);
        // Octógono: cilindro de 8 lados deitado.
        // Primeiro gira em volta do próprio eixo (lado reto em cima), depois deita de frente para a rua.
        // Antes a ordem estava invertida: o disco ficava inclinado e cobria metade do "PARE".
        Basis octagonBasis = new Basis(Vector3.Right, Mathf.Pi * 0.5f) * new Basis(Vector3.Up, Mathf.Pi / 8f) * Basis.FromScale(new Vector3(0.75f, 0.04f, 0.75f));
        var octagon = new MeshInstance3D
        {
            Mesh = _octagon,
            Transform = new Transform3D(octagonBasis, new Vector3(0f, 2.55f, -0.05f)),
            MaterialOverride = _stopRed,
        };
        sign.AddChild(octagon);
        sign.AddChild(new Label3D
        {
            Text = "PARE",
            FontSize = 64,
            PixelSize = 0.0032f,
            Modulate = Colors.White,
            OutlineSize = 0,
            Position = new Vector3(0f, 2.55f, -0.08f),
            RotationDegrees = new Vector3(0f, 180f, 0f),
        });
        _staticRoot.AddChild(sign);
    }

    /// <summary>Semáforo de pedestre (fica vermelho quando vem veículo). Era uma placa "faixa de pedestre".</summary>
    private void BuildCrosswalkSign(Vector3 basePos, float rotationY)
    {
        RegisterObstacle(basePos);
        _staticRoot.AddChild(new PedestrianSignal { Name = "SemaforoPedestre", Position = basePos });
    }

    // ------------------------------------------------------------------ marcos e limites

    private void BuildRadioTower(Vector3 basePos)
    {
        const float height = 70f;
        var tower = new StaticBody3D { Name = "RadioTower", Position = basePos, CollisionLayer = 1, CollisionMask = 0 };
        tower.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(2f, height, 2f) }, Position = new Vector3(0f, height * 0.5f, 0f) });
        // Treliça simplificada: quatro pernas + travessas.
        foreach (float x in new[] { -0.9f, 0.9f })
            foreach (float z in new[] { -0.9f, 0.9f })
                AddCylinder(tower, new Vector3(x, height * 0.5f, z), 0.08f, height, _darkMetal);
        for (float y = 4f; y < height; y += 6f)
            AddBox(tower, new Vector3(0f, y, 0f), new Vector3(2f, 0.12f, 2f), _darkMetal, false);
        for (float y = 20f; y < height; y += 25f)
            AddBox(tower, new Vector3(0f, y, 0f), new Vector3(0.35f, 0.35f, 0.35f), _redBeacon, false);
        AddBox(tower, new Vector3(0f, height + 0.3f, 0f), new Vector3(0.5f, 0.5f, 0.5f), _redBeacon, false);
        _staticRoot.AddChild(tower);

        var beacon = new OmniLight3D { Position = basePos + new Vector3(0f, height + 0.5f, 0f), LightColor = new Color(1f, 0.1f, 0.05f), LightEnergy = 3f, OmniRange = 20f };
        _lightsRoot.AddChild(beacon);
        Tween blink = CreateTween().SetLoops();
        blink.TweenProperty(beacon, "light_energy", 0.0, 0.6);
        blink.TweenProperty(beacon, "light_energy", 3.0, 0.1);
        blink.TweenInterval(0.6);
    }

    private void BuildBoundary()
    {
        float limit = (Radius + 1) * Period;
        float length = limit * 2f;
        const float h = 6f;
        AddBox(_staticRoot, new Vector3(limit, h * 0.5f, 0f), new Vector3(1f, h, length), _concrete);
        AddBox(_staticRoot, new Vector3(-limit, h * 0.5f, 0f), new Vector3(1f, h, length), _concrete);
        AddBox(_staticRoot, new Vector3(0f, h * 0.5f, limit), new Vector3(length, h, 1f), _concrete);
        AddBox(_staticRoot, new Vector3(0f, h * 0.5f, -limit), new Vector3(length, h, 1f), _concrete);
    }
}
