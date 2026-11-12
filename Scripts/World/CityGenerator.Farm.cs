// Scripts/World/CityGenerator.Farm.cs
// Fase 2 — Rancho. Mesmo CityGenerator, outro bioma: estradas de terra (desenhadas pelo shader
// do chão), sítios e fazendas com casa, celeiro, silo, plantação, curral, trator e cata-vento,
// um lago com pesqueiro, a capela, árvores por todo lado e a cooperativa (o depósito) no centro.
// Os sítios são os pontos de entrega. Tudo sai da Seed, então no online todos veem o mesmo rancho.
//
// Repetições (árvores, milho, cercas) usam MultiMesh: milhares de peças em poucas chamadas de desenho.
using System.Collections.Generic;
using Godot;

namespace DeadFrequency;

public enum WorldBiome
{
    City,
    Farm,
}

public partial class CityGenerator
{
    [ExportGroup("Bioma")]
    /// <summary>City = Fase 1 (cidade). Farm = Fase 2 (rancho).</summary>
    [Export] public WorldBiome Biome { get; set; } = WorldBiome.City;
    [Export] public float FarmExtent { get; set; } = 320f;

    /// <summary>Estradas para o minimapa (vazio na cidade, que usa a grade).</summary>
    public List<(Vector2 A, Vector2 B, float Width)> MapRoads { get; } = new();
    /// <summary>Nome de cada estrada de MapRoads (mesma ordem).</summary>
    public List<string> MapRoadNames { get; } = new();
    public bool IsFarm => Biome == WorldBiome.Farm;

    private static readonly string[] FarmNames =
    {
        "Sítio do Picapau Marelo", "Fazenda Rancho Fundo", "Haras Cavalo Doido", "Granja Ovo de Pé",
        "Sítio Vô Zico", "Fazenda Três Porteiras", "Chácara Recanto do Sossego", "Laticínio Leite Morno",
        "Apiário Zum Zum", "Fazenda Boi Bandido", "Sítio Pé de Pequi", "Fazenda Sinal Fraco",
        "Sítio Antena Torta", "Rancho do Chiado", "Fazenda Estática", "Sítio Galinha Morta",
        "Fazenda Vaca Mugindo", "Chácara do Seu Quinzinho", "Sítio Onda Curta", "Fazenda Válvula Queimada",
    };
    internal static IReadOnlyList<string> FarmNameCatalog => FarmNames;


    private static readonly Color[] HousePalette =
    {
        new(0.92f, 0.9f, 0.82f), new(0.95f, 0.85f, 0.55f), new(0.7f, 0.82f, 0.88f), new(0.9f, 0.72f, 0.7f),
        new(0.75f, 0.85f, 0.65f), new(0.85f, 0.8f, 0.72f),
    };

    private sealed class Batch
    {
        public Mesh Mesh;
        public Material Material;
        public readonly List<Transform3D> Transforms = new();
        public readonly List<Color> Colors = new();
    }

    private readonly Dictionary<string, Batch> _batches = new();
    private StaticBody3D _farmBody;
    private StaticBody3D _activeFarmBody;
    private StandardMaterial3D _farmWindow, _roofRed, _roofBrown, _barnRed, _barnWhite, _metalSilo, _hay, _cornGreen,
        _soil, _cowWhite, _cowBlack, _pigPink, _waterMat, _tractorGreen, _tractorRed, _treeTint, _wheelBlack, _reed;
    private PrismMesh _roofPrism;
    private Vector3 _lakeCenter = new(-95f, 0f, -120f);
    private const float LakeRadius = 32f;

    private readonly struct FarmRoad
    {
        public readonly string Name;
        public readonly Vector2 A, B;
        public readonly float Width;
        public FarmRoad(string name, Vector2 a, Vector2 b, float width = 8f)
        {
            Name = name;
            A = a;
            B = b;
            Width = width;
        }
    }

    private readonly List<FarmRoad> _farmRoads = new();
    private readonly List<(Vector2 Center, float Radius)> _occupied = new();

    // ------------------------------------------------------------------ entrada

    private void BuildFarm()
    {
        BuildFarmResources();
        _farmBody = new StaticBody3D { Name = "FarmCollision_Global", CollisionLayer = 1, CollisionMask = 0 };
        _staticRoot.AddChild(_farmBody);

        DefineFarmRoads();
        BuildFarmGround();

        // Cooperativa, lago, capela e outros pontos centrais permanecem como estrutura global leve.
        BuildDepot(farm: true);
        _occupied.Add((Vector2.Zero, 34f));
        _occupied.Add((new Vector2(_lakeCenter.X, _lakeCenter.Z), LakeRadius + 10f));

        BuildLake();
        BuildChapel(new Vector3(17f, 0f, 146f));
        _occupied.Add((new Vector2(17f, 146f), 18f));
        _occupied.Add((new Vector2(-14f, 140f), 10f)); // barraca na encruzilhada
        _occupied.Add((new Vector2(-10f, 26f), 8f)); // barraca da entrada
        _occupied.Add((new Vector2(36f, 146f), 14f)); // cemitério
        BuildFarmProps();

        if (_useLogicalWorldData && WorldData != null)
        {
            // Todos os sítios reservam espaço desde o início, inclusive os que ainda não têm mesh.
            foreach (WorldFarmSitePlan site in WorldData.FarmSites)
                _occupied.Add((new Vector2(site.Center.X, site.Center.Z), site.OccupiedRadius));
        }
        else
        {
            PlaceFarms();
        }

        BuildRoadLamps();
        BuildRoadSigns();
        BuildFarmPhones();

        // No modo lógico, sítios, árvores, mato e capim são renderizados sob demanda por chunk.
        if (!_useLogicalWorldData)
        {
            BuildTrees();
            BuildUndergrowth();
            BuildFarmGrass();
        }

        BuildFarmBoundary();
        BuildRadioTower(new Vector3(-FarmExtent + 25f, 0f, -FarmExtent + 25f));

        FlushBatches(_staticRoot);
        OptimizeVisibility(this);
        AddChild(new Mosquitoes { Name = "Pernilongos" });
    }

    /// <summary>Renderiza apenas os sítios e a vegetação que pertencem ao chunk solicitado.</summary>
    private bool BuildFarmChunkVisuals(WorldChunkData chunk)
    {
        if (Biome != WorldBiome.Farm || WorldData == null || IsChunkVisualized(chunk.Coordinate))
            return false;

        var root = new Node3D { Name = $"FarmChunk_{chunk.Coordinate.X}_{chunk.Coordinate.Z}" };
        _staticRoot.AddChild(root);
        _chunkRoots[chunk.Coordinate] = root;

        Node3D previousRoot = _activeChunkRoot;
        WorldChunkCoordinate? previousCoordinate = _activeChunkCoordinate;
        StaticBody3D previousFarmBody = _activeFarmBody;
        RandomNumberGenerator previousRng = _rng;
        _activeChunkRoot = root;
        _activeChunkCoordinate = chunk.Coordinate;
        _activeFarmBody = new StaticBody3D
        {
            Name = $"FarmCollision_{chunk.Coordinate.X}_{chunk.Coordinate.Z}",
            CollisionLayer = 1,
            CollisionMask = 0,
        };
        root.AddChild(_activeFarmBody);

        try
        {
            foreach (WorldFarmSitePlan site in WorldData.FarmSites)
            {
                if (GetFarmPlanChunkCoordinate(site.Center) != chunk.Coordinate)
                    continue;

                _rng = new RandomNumberGenerator { Seed = site.VisualSeed };
                BuildFarmstead(
                    site.GatePosition,
                    site.TowardRoad,
                    site.Width,
                    site.Depth,
                    site.Name,
                    site.Address);
            }

            foreach (WorldFarmTreePlan treePlan in WorldData.FarmTrees)
            {
                if (GetFarmPlanChunkCoordinate(treePlan.Position) != chunk.Coordinate)
                    continue;

                RenderFarmTreePlan(treePlan);
            }

            foreach (WorldFarmUndergrowthPlan plan in WorldData.FarmUndergrowth)
            {
                if (GetFarmPlanChunkCoordinate(plan.Position) != chunk.Coordinate)
                    continue;

                if (NatureKit.Get(plan.Model) is not Mesh mesh)
                    continue;

                var transform = new Transform3D(
                    new Basis(Vector3.Up, plan.RotationY) * Basis.FromScale(Vector3.One * plan.Scale),
                    plan.Position);
                AddInstance("qg_" + plan.Model, mesh, null, transform, Colors.White);
            }

            BuildFarmGrassForChunk(chunk);
            FlushBatches(root);
            OptimizeVisibility(root);
            ChunkVisualized?.Invoke(chunk.Coordinate);
            return true;
        }
        catch (Exception ex)
        {
            GD.PushError($"Falha ao construir chunk rural {chunk.Coordinate.X},{chunk.Coordinate.Z}: {ex}");
            _batches.Clear();
            UnloadChunkVisuals(chunk.Coordinate);
            return false;
        }
        finally
        {
            _activeChunkRoot = previousRoot;
            _activeChunkCoordinate = previousCoordinate;
            _activeFarmBody = previousFarmBody;
            _rng = previousRng;
        }
    }

    private WorldChunkCoordinate GetFarmPlanChunkCoordinate(Vector3 position)
    {
        WorldChunkCoordinate coordinate = WorldData.GetChunkCoordinate(position);
        if (WorldData.TryGetChunk(coordinate, out _))
            return coordinate;

        // Árvores da mata de borda recebem jitter e podem passar alguns centímetros
        // do limite lógico do terreno. Atribuímos esses elementos ao chunk de borda mais próximo.
        int min = Mathf.FloorToInt(-FarmExtent / WorldData.ChunkSize);
        int max = Mathf.CeilToInt(FarmExtent / WorldData.ChunkSize) - 1;
        return new WorldChunkCoordinate(
            Mathf.Clamp(coordinate.X, min, max),
            Mathf.Clamp(coordinate.Z, min, max));
    }

    private void RenderFarmTreePlan(WorldFarmTreePlan treePlan)
    {
        if (treePlan.Dead)
        {
            string model = NatureKit.DeadTrees[Mathf.PosMod(treePlan.VariantIndex, NatureKit.DeadTrees.Length)];
            if (NatureKit.Get(model) is Mesh deadMesh)
            {
                var transform = new Transform3D(
                    new Basis(Vector3.Up, treePlan.RotationY) *
                    Basis.FromScale(Vector3.One * treePlan.Scale),
                    treePlan.Position);
                AddInstance("q_" + model, deadMesh, null, transform, Colors.White);
            }

            AddShape(
                new CylinderShape3D { Radius = 0.35f, Height = 4f },
                new Transform3D(Basis.Identity, treePlan.Position + new Vector3(0f, 2f, 0f)));
            return;
        }

        RandomNumberGenerator previousRng = _rng;
        _rng = new RandomNumberGenerator { Seed = treePlan.VisualSeed };
        try
        {
            Tree(new Vector2(treePlan.Position.X, treePlan.Position.Z), treePlan.Pine);
        }
        finally
        {
            _rng = previousRng;
        }
    }

    private void BuildFarmGrassForChunk(WorldChunkData chunk)
    {
        Vector3 center = chunk.Center;
        Vector2 size = chunk.Size;
        GrassTufts.Fill(
            _activeChunkRoot ?? _staticRoot,
            center,
            size,
            1.25f,
            new Color(0.95f, 0.9f, 0.7f),
            point => point.X >= chunk.Min.X && point.X < chunk.Max.X
                && point.Y >= chunk.Min.Z && point.Y < chunk.Max.Z
                && Mathf.Max(Mathf.Abs(point.X), Mathf.Abs(point.Y)) > 20f
                && DistanceToRoads(point) > 1.2f
                && IsFree(point, 0.3f));
    }

    private void BuildFarmResources()
    {
        _farmWindow = Flat(new Color(1f, 0.85f, 0.55f), 0.4f);
        _farmWindow.EmissionEnabled = true;
        _farmWindow.Emission = new Color(1f, 0.72f, 0.38f);
        _farmWindow.EmissionEnergyMultiplier = 1.4f;
        _roofRed = Flat(new Color(0.55f, 0.2f, 0.12f), 0.8f);
        _roofBrown = Flat(new Color(0.35f, 0.25f, 0.18f), 0.8f);
        _barnRed = Flat(new Color(0.55f, 0.1f, 0.08f), 0.85f);
        _barnWhite = Flat(new Color(0.9f, 0.88f, 0.82f), 0.8f);
        _metalSilo = Flat(new Color(0.62f, 0.64f, 0.66f), 0.35f);
        _metalSilo.Metallic = 0.6f;
        _hay = Flat(new Color(0.78f, 0.65f, 0.3f));
        _cornGreen = Flat(new Color(0.32f, 0.5f, 0.15f));
        _cornGreen.VertexColorUseAsAlbedo = true;
        _soil = Flat(new Color(0.3f, 0.2f, 0.12f));
        _cowWhite = Flat(new Color(0.9f, 0.88f, 0.84f));
        _cowBlack = Flat(new Color(0.08f, 0.07f, 0.07f));
        _pigPink = Flat(new Color(0.92f, 0.62f, 0.6f));
        _waterMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.08f, 0.16f, 0.2f, 0.92f),
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            Roughness = 0.05f,
            Metallic = 0.3f,
        };
        _tractorGreen = Flat(new Color(0.15f, 0.42f, 0.15f), 0.5f);
        _tractorRed = Flat(new Color(0.65f, 0.1f, 0.08f), 0.5f);
        _treeTint = Flat(Colors.White);
        _treeTint.VertexColorUseAsAlbedo = true;
        _wheelBlack = Flat(new Color(0.05f, 0.05f, 0.05f));
        _reed = Flat(new Color(0.3f, 0.38f, 0.15f));
        _roofPrism = new PrismMesh { LeftToRight = 0.5f, Size = Vector3.One };
    }

    // ------------------------------------------------------------------ estradas e chão

    private void DefineFarmRoads()
    {
        _farmRoads.Add(new FarmRoad("Estrada do Rancho", new Vector2(0f, 14f), new Vector2(0f, 300f)));
        _farmRoads.Add(new FarmRoad("Estrada da Cooperativa", new Vector2(14f, 0f), new Vector2(300f, 0f)));
        _farmRoads.Add(new FarmRoad("Estrada Velha", new Vector2(-300f, 130f), new Vector2(300f, 130f)));
        _farmRoads.Add(new FarmRoad("Estrada do Açude", new Vector2(-185f, -265f), new Vector2(-185f, 130f)));
        _farmRoads.Add(new FarmRoad("Estrada da Serra", new Vector2(-185f, -265f), new Vector2(225f, -265f)));
        _farmRoads.Add(new FarmRoad("Estrada da Serra", new Vector2(225f, -265f), new Vector2(225f, 0f)));
        _farmRoads.Add(new FarmRoad("Estrada do Pequi", new Vector2(150f, 130f), new Vector2(150f, 300f)));
        _farmRoads.Add(new FarmRoad("Trilha do Pesqueiro", new Vector2(-185f, -120f), new Vector2(_lakeCenter.X - LakeRadius + 2f, _lakeCenter.Z), 5f));
    }

    private void BuildFarmGround()
    {
        float size = FarmExtent * 2f + 200f;
        var mat = new ShaderMaterial { Shader = GD.Load<Shader>("res://Shaders/farm_ground.gdshader") };
        _groundMaterial = mat;
        var body = new StaticBody3D { Name = "Ground", CollisionLayer = 1, CollisionMask = 0 };
        body.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(size, 1f, size) }, Position = new Vector3(0f, -0.5f, 0f) });
        body.AddChild(new MeshInstance3D { Mesh = new PlaneMesh { Size = new Vector2(size, size) }, MaterialOverride = mat });
        _staticRoot.AddChild(body);
        UpdateGroundRoads();
    }

    /// <summary>Manda as estradas (e as entradas dos sítios) para o shader do chão.</summary>
    private void UpdateGroundRoads()
    {
        if (_groundMaterial is not ShaderMaterial mat)
            return;
        var data = new float[320];
        int count = 0;
        MapRoads.Clear();
        MapRoadNames.Clear();
        foreach (FarmRoad r in _farmRoads)
        {
            if (count >= 64)
                break;
            int k = count * 5;
            data[k] = r.A.X;
            data[k + 1] = r.A.Y;
            data[k + 2] = r.B.X;
            data[k + 3] = r.B.Y;
            data[k + 4] = r.Width;
            count++;
            MapRoads.Add((r.A, r.B, r.Width));
            MapRoadNames.Add(r.Name);
        }
        mat.SetShaderParameter("road_data", data);
        mat.SetShaderParameter("road_count", count);
        mat.SetShaderParameter("lake", new Vector3(_lakeCenter.X, _lakeCenter.Z, LakeRadius));
        mat.SetShaderParameter("yard_half", 18f);
        mat.SetShaderParameter("grass_tex", GD.Load<Texture2D>("res://Assets/Textures/City/lawn.png"));
    }

    private float DistanceToRoads(Vector2 p, int skip = -1)
    {
        float best = float.MaxValue;
        for (int i = 0; i < _farmRoads.Count; i++)
        {
            if (i == skip)
                continue;
            FarmRoad r = _farmRoads[i];
            best = Mathf.Min(best, SegmentDistance(p, r.A, r.B) - r.Width * 0.5f);
        }
        return best;
    }

    private static float SegmentDistance(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float t = Mathf.Clamp((p - a).Dot(ab) / Mathf.Max(ab.LengthSquared(), 0.0001f), 0f, 1f);
        return p.DistanceTo(a + ab * t);
    }

    private bool IsFree(Vector2 p, float radius)
    {
        foreach ((Vector2 c, float r) in _occupied)
            if (p.DistanceTo(c) < r + radius)
                return false;
        return Mathf.Abs(p.X) < FarmExtent - radius && Mathf.Abs(p.Y) < FarmExtent - radius;
    }

    // ------------------------------------------------------------------ sítios

    private void PlaceFarms()
    {
        if (_useLogicalWorldData && WorldData != null)
        {
            RandomNumberGenerator previousRng = _rng;
            try
            {
                foreach (WorldFarmSitePlan site in WorldData.FarmSites)
                {
                    _occupied.Add((new Vector2(site.Center.X, site.Center.Z), site.OccupiedRadius));
                    _rng = new RandomNumberGenerator { Seed = site.VisualSeed };
                    BuildFarmstead(
                        site.GatePosition,
                        site.TowardRoad,
                        site.Width,
                        site.Depth,
                        site.Name,
                        site.Address);
                }
            }
            finally
            {
                _rng = previousRng;
            }
            return;
        }

        var names = new List<string>(FarmNames);
        for (int i = names.Count - 1; i > 0; i--)
        {
            int j = _rng.RandiRange(0, i);
            (names[i], names[j]) = (names[j], names[i]);
        }

        const float width = 46f, depth = 56f;
        float radius = Mathf.Sqrt(width * width + depth * depth) * 0.5f;
        int placed = 0;
        for (int ri = 0; ri < _farmRoads.Count; ri++)
        {
            FarmRoad road = _farmRoads[ri];
            if (road.Width < 6f)
                continue;
            Vector2 dir = (road.B - road.A).Normalized();
            Vector2 normal = new(-dir.Y, dir.X);
            float length = road.A.DistanceTo(road.B);
            for (float along = 40f; along < length - 30f; along += _rng.RandfRange(62f, 84f))
            {
                float side = _rng.Randf() < 0.5f ? 1f : -1f;
                for (int attempt = 0; attempt < 2; attempt++, side = -side)
                {
                    Vector2 onRoad = road.A + dir * along;
                    Vector2 gate = onRoad + normal * side * (road.Width * 0.5f + 3f);
                    Vector2 center = gate + normal * side * (depth * 0.5f);
                    if (!IsFree(center, radius) || DistanceToRoads(center, ri) < radius + 2f)
                        continue;

                    _occupied.Add((center, radius));
                    string name = placed < names.Count ? names[placed] : names[placed % names.Count] + " II";
                    float km = (onRoad.Length() / 1000f) * 10f; // "km" de mentira para soar rural
                    BuildFarmstead(new Vector3(gate.X, 0f, gate.Y), new Vector3(-normal.X * side, 0f, -normal.Y * side), width, depth, name, $"{name} · {road.Name}, km {km:0.0}");
                    placed++;
                    break;
                }
            }
        }
    }

    /// <summary>Um sítio. gate = meio da cerca da frente; towardRoad = direção da porteira para a estrada.</summary>
    private void BuildFarmstead(Vector3 gate, Vector3 towardRoad, float width, float depth, string name, string address)
    {
        var farm = new Node3D { Name = "Farm_" + _deliveryPoints.Count, Position = gate, Rotation = new Vector3(0f, Mathf.Atan2(towardRoad.X, towardRoad.Z), 0f) };
        (_activeChunkRoot ?? _staticRoot).AddChild(farm);
        Transform3D xf = farm.Transform;
        float hw = width * 0.5f;

        // Em mundos lógicos o destino já foi registrado antes de carregar as meshes.
        if (!_useLogicalWorldData)
            _deliveryPoints.Add(new DeliveryPoint { Position = xf * new Vector3(0f, 0f, -2.5f), Address = address });

        // Cerca com porteira aberta na frente.
        Fence(xf, new Vector3(-hw, 0f, 0f), new Vector3(-4f, 0f, 0f));
        Fence(xf, new Vector3(4f, 0f, 0f), new Vector3(hw, 0f, 0f));
        Fence(xf, new Vector3(-hw, 0f, 0f), new Vector3(-hw, 0f, -depth));
        Fence(xf, new Vector3(hw, 0f, 0f), new Vector3(hw, 0f, -depth));
        Fence(xf, new Vector3(-hw, 0f, -depth), new Vector3(hw, 0f, -depth));
        BuildGateArch(farm, name);

        float side = _rng.Randf() < 0.5f ? 1f : -1f;
        BuildFarmhouse(farm, xf, new Vector3(_rng.RandfRange(-3f, 3f), 0f, -17f));
        // Caminho de terra até a casa.
        AddBox(farm, new Vector3(0f, 0.015f, -6.5f), new Vector3(3.2f, 0.02f, 9f), _soil, false);

        if (_rng.Randf() < 0.7f)
            BuildBarn(farm, xf, new Vector3(side * 13f, 0f, -41f));
        if (_rng.Randf() < 0.45f)
            BuildSilo(farm, xf, new Vector3(side * 2.5f, 0f, -47f));
        if (_rng.Randf() < 0.75f)
        {
            int crop = _rng.RandiRange(0, 2);
            BuildField(xf, -side, crop);
            if (crop == 2)
                PumpkinPatch(xf, -side); // terra arada vira canteiro de abóbora
        }
        if (_rng.Randf() < 0.55f)
            BuildCorral(farm, xf, side, _rng.Randf() < 0.65f);
        if (_rng.Randf() < 0.5f)
            for (int i = 0; i < _rng.RandiRange(3, 6); i++)
                HayBale(farm, xf, new Vector3(side * _rng.RandfRange(16f, 20f), 0f, _rng.RandfRange(-32f, -27f)), _rng.Randf() * Mathf.Tau);
        float vehicleRoll = _rng.Randf();
        if (vehicleRoll < 0.35f)
            BuildTractor(farm, xf, new Vector3(-side * 4.5f, 0f, -8f), _rng.RandfRange(-0.6f, 0.6f), _rng.Randf() < 0.5f ? _tractorGreen : _tractorRed);
        else if (vehicleRoll < 0.55f)
            PlaceKenney(farm, xf, _rng.Randf() < 0.5f ? "vehicle-truck-green.glb" : "vehicle-truck-yellow.glb", new Vector3(-side * 6f, 0f, -9f), _rng.RandfRange(-0.5f, 0.5f), 1.7f);
        if (_rng.Randf() < 0.3f)
            BuildWindmill(farm, xf, new Vector3(-side * (hw - 4f), 0f, -6f));

        // Poste com lâmpada na porteira (a roça é escura de noite).
        BuildFarmLamp(xf * new Vector3(5.5f, 0f, 0.6f), towardRoad);
        MarkFarmGate(farm, name);
    }

    private void MarkFarmGate(Node3D farm, string name)
    {
        var marker = new Node3D { Position = new Vector3(0f, 0f, -2.5f) };
        farm.AddChild(marker);
        marker.AddToGroup(MinimapView.MarkerGroup);
        marker.SetMeta("map_kind", "farm");
        marker.SetMeta("map_color", new Color(0.75f, 0.55f, 0.3f));
    }

    private void BuildGateArch(Node3D farm, string name)
    {
        foreach (float x in new[] { -4.2f, 4.2f })
            AddBox(farm, new Vector3(x, 1.8f, 0f), new Vector3(0.3f, 3.6f, 0.3f), _trunk);
        AddBox(farm, new Vector3(0f, 3.4f, 0f), new Vector3(9.2f, 0.25f, 0.3f), _trunk, false);
        AddBox(farm, new Vector3(0f, 2.95f, 0.02f), new Vector3(6.4f, 0.65f, 0.08f), _wood, false);
        foreach ((float z, float rot) in new[] { (0.08f, 0f), (-0.04f, Mathf.Pi) })
        {
            farm.AddChild(new Label3D
            {
                Text = name.ToUpperInvariant(),
                FontSize = 48,
                PixelSize = 0.0075f,
                Modulate = new Color(0.98f, 0.92f, 0.8f),
                OutlineSize = 6,
                OutlineModulate = new Color(0.15f, 0.08f, 0.04f),
                Position = new Vector3(0f, 2.95f, z),
                Rotation = new Vector3(0f, rot, 0f),
                Width = 820f,
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
            });
        }
        // Caixa de correio.
        AddBox(farm, new Vector3(-5.6f, 0.6f, 0.6f), new Vector3(0.08f, 1.2f, 0.08f), _trunk, false);
        AddBox(farm, new Vector3(-5.6f, 1.25f, 0.6f), new Vector3(0.3f, 0.25f, 0.45f), _stopRed, false);
    }

    // ------------------------------------------------------------------ construções

    private void Roof(Node3D parent, Vector3 center, Vector3 size, Material mat)
    {
        parent.AddChild(new MeshInstance3D { Mesh = _roofPrism, Position = center, Scale = size, MaterialOverride = mat });
    }

    private void BuildFarmhouse(Node3D farm, Transform3D xf, Vector3 pos)
    {
        var house = new Node3D { Position = pos };
        farm.AddChild(house);
        Color wall = HousePalette[_rng.RandiRange(0, HousePalette.Length - 1)];
        var wallMat = Flat(wall, 0.9f);
        const float w = 9f, h = 3.2f, d = 7f;
        AddBox(house, new Vector3(0f, h * 0.5f, 0f), new Vector3(w, h, d), wallMat);
        Roof(house, new Vector3(0f, h + 1.0f, 0f), new Vector3(w + 0.8f, 2.0f, d + 0.8f), _rng.Randf() < 0.6f ? _roofRed : _roofBrown);
        // Barra de baixo, porta e janelas acesas.
        AddBox(house, new Vector3(0f, 0.25f, d * 0.5f + 0.01f), new Vector3(w, 0.5f, 0.02f), Flat(wall.Darkened(0.35f)), false);
        AddBox(house, new Vector3(0f, 1.05f, d * 0.5f + 0.02f), new Vector3(1.0f, 2.1f, 0.04f), _trunk, false);
        foreach (float x in new[] { -2.8f, 2.8f })
        {
            AddBox(house, new Vector3(x, 1.6f, d * 0.5f + 0.02f), new Vector3(1.2f, 1.0f, 0.04f), _farmWindow, false);
            AddBox(house, new Vector3(x - 0.75f, 1.6f, d * 0.5f + 0.04f), new Vector3(0.3f, 1.1f, 0.04f), _signBlue, false); // janela de madeira
            AddBox(house, new Vector3(x + 0.75f, 1.6f, d * 0.5f + 0.04f), new Vector3(0.3f, 1.1f, 0.04f), _signBlue, false);
        }
        foreach (float sx in new[] { -1f, 1f })
            AddBox(house, new Vector3(sx * (w * 0.5f + 0.02f), 1.6f, -0.8f), new Vector3(0.04f, 1.0f, 1.2f), _farmWindow, false);
        // Varanda com telhadinho e banco.
        AddBox(house, new Vector3(0f, 2.75f, d * 0.5f + 1.2f), new Vector3(w - 1f, 0.12f, 2.4f), _roofBrown, false);
        foreach (float x in new[] { -3.9f, 3.9f })
            AddBox(house, new Vector3(x, 1.35f, d * 0.5f + 2.3f), new Vector3(0.15f, 2.7f, 0.15f), _trunk, false);
        AddBox(house, new Vector3(2f, 0.45f, d * 0.5f + 0.5f), new Vector3(1.6f, 0.1f, 0.45f), _wood, false);
        // Às vezes uma abóbora iluminada na varanda (KayKit Halloween, CC0).
        if (_rng.Randf() < 0.35f)
            AssetProps.Spawn(house, AssetProps.Halloween + "pumpkin_orange_jackolantern.gltf", new Vector3(-3.2f, 0f, d * 0.5f + 0.9f), _rng.RandfRange(-0.5f, 0.5f), 0.45f);
        // Caramelo deitado na varanda e, às vezes, um cavalo ou jegue amarrado do lado da casa.
        int pet = AssetProps.Hash(xf * pos, 13);
        if (pet % 10 < 6)
            house.AddChild(FarmAnimal.Create("ShibaInu.gltf", 0.95f, new Vector3(-1.6f, 0f, d * 0.5f + 1.4f), 2.6f, 1.2f));
        if (pet % 7 < 3)
            house.AddChild(FarmAnimal.Create(pet % 2 == 0 ? "Horse.gltf" : "Donkey.gltf", pet % 2 == 0 ? 2.3f : 1.8f, new Vector3(w * 0.5f + 2.5f, 0f, 1.5f), Mathf.Pi * 0.5f));
        // Chaminé do fogão a lenha e caixa d'água.
        AddBox(house, new Vector3(-2.6f, h + 1.6f, -1.5f), new Vector3(0.6f, 2.2f, 0.6f), Flat(new Color(0.5f, 0.3f, 0.22f)), false);
        AddCylinder(house, new Vector3(2.5f, h + 1.9f, -2f), 0.7f, 0.9f, _signBlue);
    }

    private void BuildBarn(Node3D farm, Transform3D xf, Vector3 pos)
    {
        var barn = new Node3D { Position = pos };
        farm.AddChild(barn);
        const float w = 10f, h = 6f, d = 13f;
        // Celeiro vermelho da Quaternius (Farm Buildings), escalado para o mesmo espaço de antes.
        string[] barns = { "Barn.fbx", "BigBarn.fbx", "SmallBarn.fbx" };
        string file = barns[AssetProps.Hash(xf * pos, 14) % barns.Length];
        float k = file == "SmallBarn.fbx" ? 1.6f : 1.3f;
        if (AssetProps.Spawn(barn, "res://Assets/Quaternius/Farm/" + file, Vector3.Zero, 0f, k) != null)
        {
            AddShape(new BoxShape3D { Size = new Vector3(w, h + 2f, 10.5f) }, xf * barn.Transform * new Transform3D(Basis.Identity, new Vector3(0f, (h + 2f) * 0.5f, 0f)));
            return;
        }
        AddBox(barn, new Vector3(0f, h * 0.5f, 0f), new Vector3(w, h, d), _barnRed);
        Roof(barn, new Vector3(0f, h + 1.5f, 0f), new Vector3(w + 0.8f, 3f, d + 0.6f), _roofBrown);
        // Portão branco com "X" e janela do palheiro.
        float fz = d * 0.5f + 0.03f;
        AddBox(barn, new Vector3(0f, 2f, fz), new Vector3(4.2f, 4f, 0.05f), Flat(new Color(0.42f, 0.07f, 0.06f), 0.85f), false);
        AddBox(barn, new Vector3(0f, 4.05f, fz + 0.02f), new Vector3(4.4f, 0.2f, 0.06f), _barnWhite, false);
        foreach (float x in new[] { -2.1f, 0f, 2.1f })
            AddBox(barn, new Vector3(x, 2f, fz + 0.02f), new Vector3(0.2f, 4f, 0.06f), _barnWhite, false);
        foreach (float sx in new[] { -1f, 1f })
            AddBox(barn, new Vector3(sx * 1.05f, 2f, fz + 0.04f), new Vector3(0.18f, 4.4f, 0.05f), _barnWhite, false).Rotation = new Vector3(0f, 0f, sx * 0.43f);
        AddBox(barn, new Vector3(0f, h + 0.6f, fz + 0.32f), new Vector3(1.4f, 1.1f, 0.05f), _hay, false);
    }

    private void BuildSilo(Node3D farm, Transform3D xf, Vector3 pos)
    {
        var silo = new Node3D { Position = pos };
        farm.AddChild(silo);
        const float r = 2.4f, h = 10f;
        if (AssetProps.Spawn(silo, "res://Assets/Quaternius/Farm/Silo.fbx", Vector3.Zero, 0f, 1.15f) != null)
        {
            AddShape(new CylinderShape3D { Radius = 2.1f, Height = h }, xf * silo.Transform * new Transform3D(Basis.Identity, new Vector3(0f, h * 0.5f, 0f)));
            return;
        }
        AddCylinder(silo, new Vector3(0f, h * 0.5f, 0f), r, h, _metalSilo, new CylinderMesh { TopRadius = 0.5f, BottomRadius = 0.5f, Height = 1f, RadialSegments = 16 });
        silo.AddChild(new MeshInstance3D
        {
            Mesh = new SphereMesh { Radius = r, Height = r, IsHemisphere = true, RadialSegments = 16, Rings = 5 },
            Position = new Vector3(0f, h, 0f),
            MaterialOverride = _metalSilo,
        });
        AddBox(silo, new Vector3(0f, h * 0.5f, r + 0.15f), new Vector3(0.5f, h, 0.05f), _darkMetal, false); // escada
        AddShape(new CylinderShape3D { Radius = r, Height = h }, xf * silo.Transform * new Transform3D(Basis.Identity, new Vector3(0f, h * 0.5f, 0f)));
    }

    private void BuildField(Transform3D xf, float side, int crop)
    {
        // Zona do lado oposto ao celeiro, atrás da casa.
        float x0 = side * 7f, x1 = side * 21f;
        float z0 = -27f, z1 = -52f;
        float minX = Mathf.Min(x0, x1), maxX = Mathf.Max(x0, x1);
        for (float x = minX; x <= maxX; x += 1.1f)
        {
            // Leira de terra.
            var ridgeXf = xf * new Transform3D(Basis.FromScale(new Vector3(0.7f, 0.12f, Mathf.Abs(z1 - z0))), new Vector3(x, 0.06f, (z0 + z1) * 0.5f));
            AddInstance("ridge", _unitBox, _soil, ridgeXf, Colors.White);
            if (crop == 2)
                continue; // terra arada, sem plantas
            for (float z = z1 + 0.6f; z < z0 - 0.4f; z += crop == 0 ? 0.9f : 0.6f)
            {
                float hgt = crop == 0 ? _rng.RandfRange(1.6f, 2.3f) : _rng.RandfRange(0.35f, 0.6f);
                float wid = crop == 0 ? 0.35f : 0.45f;
                var plant = xf * new Transform3D(new Basis(Vector3.Up, _rng.Randf() * Mathf.Pi) * Basis.FromScale(new Vector3(wid, hgt, wid)), new Vector3(x + _rng.RandfRange(-0.1f, 0.1f), hgt * 0.5f + 0.1f, z));
                Color tint = crop == 0 ? new Color(0.9f + _rng.Randf() * 0.2f, 1f, 0.7f + _rng.Randf() * 0.3f) : new Color(0.7f, 1.1f, 0.6f);
                AddInstance("crop", _treeTop, _cornGreen, plant, tint);
            }
        }
    }

    private void BuildCorral(Node3D farm, Transform3D xf, float side, bool cows)
    {
        float cx = side * 13f, cz = -15f;
        const float hw = 6f, hd = 5f;
        Fence(xf, new Vector3(cx - hw, 0f, cz - hd), new Vector3(cx + hw, 0f, cz - hd));
        Fence(xf, new Vector3(cx - hw, 0f, cz + hd), new Vector3(cx + hw, 0f, cz + hd));
        Fence(xf, new Vector3(cx - hw, 0f, cz - hd), new Vector3(cx - hw, 0f, cz + hd));
        Fence(xf, new Vector3(cx + hw, 0f, cz - hd), new Vector3(cx + hw, 0f, cz + hd));
        // Cocho.
        AddBox(farm, new Vector3(cx, 0.35f, cz - hd + 0.8f), new Vector3(3f, 0.5f, 0.7f), _wood, false);
        int count = _rng.RandiRange(2, 5);
        for (int i = 0; i < count; i++)
        {
            var p = new Vector3(cx + _rng.RandfRange(-hw + 1.5f, hw - 1.5f), 0f, cz + _rng.RandfRange(-hd + 1.5f, hd - 1.5f));
            if (cows)
                BuildCow(farm, p, _rng.Randf() * Mathf.Tau);
            else
                BuildPig(farm, p, _rng.Randf() * Mathf.Tau);
        }
    }

    private void BuildCow(Node3D parent, Vector3 pos, float rotY)
    {
        // Vacas e às vezes um boi, animados (Quaternius). Passeiam dentro do cercado.
        if (ResourceLoader.Exists(FarmAnimal.Dir + "Cow.gltf"))
        {
            bool bull = AssetProps.Hash(pos, 11) % 6 == 0;
            parent.AddChild(FarmAnimal.Create(bull ? "Bull.gltf" : "Cow.gltf", bull ? 2.5f : 2.3f, pos, rotY, 2.2f));
            return;
        }
        var cow = new Node3D { Position = pos, Rotation = new Vector3(0f, rotY, 0f) };
        parent.AddChild(cow);
        AddBox(cow, new Vector3(0f, 1.05f, 0f), new Vector3(0.75f, 0.75f, 1.6f), _cowWhite, false);
        // Manchas pretas nas laterais e no lombo.
        AddBox(cow, new Vector3(0.385f, 1.1f, 0.2f), new Vector3(0.02f, 0.45f, 0.6f), _cowBlack, false);
        AddBox(cow, new Vector3(-0.385f, 1.0f, -0.3f), new Vector3(0.02f, 0.4f, 0.5f), _cowBlack, false);
        AddBox(cow, new Vector3(0.1f, 1.435f, 0.45f), new Vector3(0.4f, 0.02f, 0.45f), _cowBlack, false);
        AddBox(cow, new Vector3(0f, 1.3f, -0.95f), new Vector3(0.42f, 0.45f, 0.5f), _cowWhite, false); // cabeça
        AddBox(cow, new Vector3(0f, 1.18f, -1.22f), new Vector3(0.3f, 0.22f, 0.06f), _pigPink, false); // focinho
        foreach (float x in new[] { -0.22f, 0.22f })
        {
            AddBox(cow, new Vector3(x, 1.56f, -0.9f), new Vector3(0.06f, 0.14f, 0.06f), _barnWhite, false); // chifres
            foreach (float z in new[] { -0.6f, 0.6f })
                AddBox(cow, new Vector3(x, 0.34f, z), new Vector3(0.16f, 0.68f, 0.16f), _cowWhite, false);
        }
        AddBox(cow, new Vector3(0f, 1.05f, 0.85f), new Vector3(0.05f, 0.6f, 0.05f), _cowBlack, false); // rabo
    }

    private void BuildPig(Node3D parent, Vector3 pos, float rotY)
    {
        // Porcos (e umas ovelhas) da Quaternius.
        if (ResourceLoader.Exists(FarmAnimal.Dir + "Pig.fbx"))
        {
            bool sheep = AssetProps.Hash(pos, 12) % 4 == 0;
            parent.AddChild(FarmAnimal.Create(sheep ? "Sheep.fbx" : "Pig.fbx", sheep ? 1.3f : 1.2f, pos, rotY, 2f));
            return;
        }
        var pig = new Node3D { Position = pos, Rotation = new Vector3(0f, rotY, 0f) };
        parent.AddChild(pig);
        AddBox(pig, new Vector3(0f, 0.45f, 0f), new Vector3(0.55f, 0.5f, 0.95f), _pigPink, false);
        AddBox(pig, new Vector3(0f, 0.5f, -0.55f), new Vector3(0.38f, 0.36f, 0.25f), _pigPink, false);
        AddBox(pig, new Vector3(0f, 0.46f, -0.7f), new Vector3(0.2f, 0.14f, 0.06f), Flat(new Color(0.75f, 0.45f, 0.45f)), false);
        foreach (float x in new[] { -0.18f, 0.18f })
            foreach (float z in new[] { -0.3f, 0.3f })
                AddBox(pig, new Vector3(x, 0.12f, z), new Vector3(0.12f, 0.24f, 0.12f), _pigPink, false);
    }

    private void HayBale(Node3D farm, Transform3D xf, Vector3 pos, float rotY)
    {
        var bale = AddCylinder(farm, pos + new Vector3(0f, 0.7f, 0f), 0.7f, 1.2f, _hay);
        bale.Rotation = new Vector3(0f, rotY, Mathf.Pi * 0.5f);
        AddShape(new BoxShape3D { Size = new Vector3(1.2f, 1.4f, 1.4f) }, xf * new Transform3D(new Basis(Vector3.Up, rotY), pos + new Vector3(0f, 0.7f, 0f)));
    }

    private void BuildTractor(Node3D farm, Transform3D xf, Vector3 pos, float rotY, Material paint)
    {
        var t = new Node3D { Position = pos, Rotation = new Vector3(0f, rotY, 0f) };
        farm.AddChild(t);
        AddBox(t, new Vector3(0f, 1.0f, -0.6f), new Vector3(0.9f, 0.8f, 1.8f), paint, false); // capô
        AddBox(t, new Vector3(0f, 1.2f, 0.7f), new Vector3(1.1f, 0.5f, 1.0f), paint, false); // base da cabine
        AddBox(t, new Vector3(0f, 1.6f, 0.75f), new Vector3(0.5f, 0.1f, 0.5f), _darkMetal, false); // banco
        foreach (float x in new[] { -0.55f, 0.55f })
            foreach (float z in new[] { 0.25f, 1.2f })
                AddBox(t, new Vector3(x, 2.15f, z), new Vector3(0.06f, 1.5f, 0.06f), _darkMetal, false);
        AddBox(t, new Vector3(0f, 2.9f, 0.72f), new Vector3(1.3f, 0.08f, 1.2f), paint, false);
        AddBox(t, new Vector3(0.25f, 1.9f, -1.2f), new Vector3(0.08f, 0.9f, 0.08f), _darkMetal, false); // escapamento
        foreach (float x in new[] { -0.8f, 0.8f })
        {
            AddCylinder(t, new Vector3(x, 0.75f, 0.75f), 0.75f, 0.45f, _wheelBlack).Rotation = new Vector3(0f, 0f, Mathf.Pi * 0.5f);
            AddCylinder(t, new Vector3(x * 0.75f, 0.4f, -1.2f), 0.4f, 0.28f, _wheelBlack).Rotation = new Vector3(0f, 0f, Mathf.Pi * 0.5f);
            AddCylinder(t, new Vector3(x * 1.02f, 0.75f, 0.75f), 0.4f, 0.04f, _hay).Rotation = new Vector3(0f, 0f, Mathf.Pi * 0.5f); // aro
        }
        AddShape(new BoxShape3D { Size = new Vector3(2.1f, 2.9f, 3.2f) }, xf * t.Transform * new Transform3D(Basis.Identity, new Vector3(0f, 1.45f, 0f)));
    }

    private void BuildWindmill(Node3D farm, Transform3D xf, Vector3 pos)
    {
        var mill = new Node3D { Position = pos };
        farm.AddChild(mill);
        const float h = 9f;
        // Catavento da Quaternius; as pás giram em volta do cubo.
        if (AssetProps.Spawn(mill, "res://Assets/Quaternius/Farm/Windmill.fbx", Vector3.Zero, AssetProps.Hash(xf * pos, 15) % 628 / 100f, 0.85f) is Node3D model
            && model.FindChild("Windmill_Blades", true, false) is Node3D blades)
        {
            var hub = new Spinner { Position = blades.Position, Axis = Vector3.Forward, Speed = _rng.RandfRange(1.2f, 2.6f) };
            model.AddChild(hub);
            blades.Reparent(hub, false);
            blades.Position = Vector3.Zero;
            AddShape(new BoxShape3D { Size = new Vector3(2f, h, 2f) }, xf * mill.Transform * new Transform3D(Basis.Identity, new Vector3(0f, h * 0.5f, 0f)));
            return;
        }
        foreach (float x in new[] { -0.9f, 0.9f })
            foreach (float z in new[] { -0.9f, 0.9f })
            {
                var leg = AddBox(mill, new Vector3(x * 0.55f, h * 0.5f, z * 0.55f), new Vector3(0.1f, h, 0.1f), _darkMetal, false);
                leg.Rotation = new Vector3(-z * 0.1f, 0f, x * 0.1f);
            }
        for (float y = 1.5f; y < h; y += 2.5f)
            AddBox(mill, new Vector3(0f, y, 0f), new Vector3(1.8f - y * 0.1f, 0.06f, 1.8f - y * 0.1f), _darkMetal, false);
        AddBox(mill, new Vector3(0f, h + 0.1f, 0.4f), new Vector3(0.3f, 0.3f, 1.4f), _darkMetal, false);
        AddBox(mill, new Vector3(0f, h + 0.3f, 1.3f), new Vector3(0.05f, 0.9f, 0.9f), _signWhite, false); // leme
        var rotor = new Spinner { Position = new Vector3(0f, h + 0.1f, -0.35f), Axis = Vector3.Forward, Speed = _rng.RandfRange(1.2f, 2.6f) };
        mill.AddChild(rotor);
        for (int i = 0; i < 12; i++)
        {
            var blade = new MeshInstance3D { Mesh = _unitBox, MaterialOverride = _metalSilo, Scale = new Vector3(0.22f, 1.5f, 0.02f) };
            var arm = new Node3D { Rotation = new Vector3(0f, 0f, i * Mathf.Tau / 12f) };
            blade.Position = new Vector3(0f, 1.0f, 0f);
            blade.Rotation = new Vector3(0f, 0.4f, 0f);
            arm.AddChild(blade);
            rotor.AddChild(arm);
        }
        AddShape(new BoxShape3D { Size = new Vector3(2f, h, 2f) }, xf * mill.Transform * new Transform3D(Basis.Identity, new Vector3(0f, h * 0.5f, 0f)));
    }

    private void PlaceKenney(Node3D parent, Transform3D xf, string file, Vector3 pos, float rotY, float scale)
    {
        string path = "res://Assets/Kenney/Racing/" + file;
        if (!ResourceLoader.Exists(path) || GD.Load<PackedScene>(path)?.Instantiate() is not Node3D model)
            return;
        model.Position = pos;
        model.Rotation = new Vector3(0f, rotY, 0f);
        model.Scale = Vector3.One * scale;
        parent.AddChild(model);
        var size = new Vector3(1.5f, 1.55f, 2.8f) * scale;
        AddShape(new BoxShape3D { Size = size }, xf * new Transform3D(new Basis(Vector3.Up, rotY), pos + new Vector3(0f, size.Y * 0.5f, 0f)));
    }

    // ------------------------------------------------------------------ lugares especiais

    private void BuildLake()
    {
        var lake = new Node3D { Name = "Lake", Position = _lakeCenter };
        _staticRoot.AddChild(lake);
        lake.AddChild(new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = LakeRadius, BottomRadius = LakeRadius, Height = 0.05f, RadialSegments = 40, Rings = 1 },
            Position = new Vector3(0f, 0.04f, 0f),
            MaterialOverride = _waterMat,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        });
        // Não deixa a van entrar no lago: borda invisível (o barro e as taboas mostram onde é).
        AddShape(new CylinderShape3D { Radius = LakeRadius - 1f, Height = 1.2f }, new Transform3D(Basis.Identity, _lakeCenter + new Vector3(0f, 0.6f, 0f)));
        if (_useLogicalWorldData && WorldData != null)
        {
            foreach (WorldFarmReedPlan reedPlan in WorldData.FarmReeds)
            {
                var reed = new Transform3D(
                    Basis.FromScale(new Vector3(0.12f, reedPlan.Height, 0.12f)),
                    reedPlan.Position);
                AddInstance("reed", _unitBox, _cornGreen, reed, new Color(0.8f, 0.9f, 0.6f));
            }
        }
        else
        {
            for (int i = 0; i < 70; i++)
            {
                float a = _rng.Randf() * Mathf.Tau;
                float r = LakeRadius + _rng.RandfRange(-1.2f, 1.5f);
                float hgt = _rng.RandfRange(0.6f, 1.4f);
                var reed = new Transform3D(Basis.FromScale(new Vector3(0.12f, hgt, 0.12f)),
                    _lakeCenter + new Vector3(Mathf.Cos(a) * r, hgt * 0.5f, Mathf.Sin(a) * r));
                AddInstance("reed", _unitBox, _cornGreen, reed, new Color(0.8f, 0.9f, 0.6f));
            }
        }

        // Píer do pesqueiro, virado para a trilha (lado -X).
        var pier = new Node3D { Position = new Vector3(-LakeRadius + 1f, 0f, 0f) };
        lake.AddChild(pier);
        AddBox(pier, new Vector3(5f, 0.35f, 0f), new Vector3(10f, 0.12f, 2.2f), _wood, false);
        for (float x = 0.5f; x < 10f; x += 2.4f)
            foreach (float z in new[] { -1f, 1f })
                AddBox(pier, new Vector3(x, 0.1f, z), new Vector3(0.18f, 0.7f, 0.18f), _trunk, false);
        AddBox(pier, new Vector3(-3f, 1.3f, 3.5f), new Vector3(3.2f, 2.6f, 3f), _wood); // barraquinha de iscas
        Roof(pier, new Vector3(-3f, 3.05f, 3.5f), new Vector3(3.8f, 0.9f, 3.6f), _roofRed);
        lake.AddChild(new Label3D
        {
            Text = "PESQUEIRO LAMBARI FELIZ\nisca · gelo · minhoca",
            FontSize = 48,
            PixelSize = 0.006f,
            Modulate = new Color(0.95f, 0.95f, 0.85f),
            OutlineSize = 6,
            Position = pier.Position + new Vector3(-4.65f, 2.0f, 3.5f),
            Rotation = new Vector3(0f, -Mathf.Pi * 0.5f, 0f),
        });
        // Barquinho.
        var boat = new Node3D { Position = new Vector3(6f, 0.15f, 9f), Rotation = new Vector3(0f, 0.7f, 0f) };
        lake.AddChild(boat);
        AddBox(boat, new Vector3(0f, 0.2f, 0f), new Vector3(1.2f, 0.4f, 3f), _signWhite, false);
        AddBox(boat, new Vector3(0f, 0.32f, 0f), new Vector3(1.0f, 0.2f, 2.8f), _signBlue, false);

        if (!_useLogicalWorldData)
        {
            _deliveryPoints.Add(new DeliveryPoint
            {
                Position = _lakeCenter + new Vector3(-LakeRadius - 4f, 0f, 0f),
                Address = "Pesqueiro Lambari Feliz · Trilha do Pesqueiro",
            });
        }
        var poi = new Node3D { Position = new Vector3(-LakeRadius - 4f, 0f, 0f) };
        lake.AddChild(poi);
        MarkPoi(poi, "P", new Color(0.2f, 0.55f, 0.85f));
        BuildFarmLamp(_lakeCenter + new Vector3(-LakeRadius - 7f, 0f, 4f), Vector3.Right);
    }

    private void BuildChapel(Vector3 pos)
    {
        var chapel = new Node3D { Name = "Chapel", Position = pos, Rotation = new Vector3(0f, -Mathf.Pi * 0.5f, 0f) }; // porta para -X (estrada)
        _staticRoot.AddChild(chapel);
        const float w = 6f, h = 5f, d = 10f;
        AddBox(chapel, new Vector3(0f, h * 0.5f, 0f), new Vector3(w, h, d), _barnWhite);
        Roof(chapel, new Vector3(0f, h + 1.2f, 0f), new Vector3(w + 0.6f, 2.4f, d + 0.4f), _roofRed);
        AddBox(chapel, new Vector3(0f, 1.3f, d * 0.5f + 0.02f), new Vector3(1.6f, 2.6f, 0.05f), _signBlue, false);
        // Torre do sino com cruz.
        AddBox(chapel, new Vector3(0f, h + 3.2f, d * 0.5f - 1f), new Vector3(1.8f, 3.2f, 1.8f), _barnWhite);
        Roof(chapel, new Vector3(0f, h + 5.3f, d * 0.5f - 1f), new Vector3(2.1f, 1.0f, 2.1f), _roofRed);
        AddBox(chapel, new Vector3(0f, h + 6.4f, d * 0.5f - 1f), new Vector3(0.12f, 1.2f, 0.12f), _darkMetal, false);
        AddBox(chapel, new Vector3(0f, h + 6.6f, d * 0.5f - 1f), new Vector3(0.6f, 0.12f, 0.12f), _darkMetal, false);
        AddBox(chapel, new Vector3(0f, h + 3.4f, d * 0.5f - 0.08f), new Vector3(0.7f, 0.9f, 0.05f), _farmWindow, false);
        foreach (float z in new[] { -2.5f, 0f, 2.5f })
            foreach (float sx in new[] { -1f, 1f })
                AddBox(chapel, new Vector3(sx * (w * 0.5f + 0.02f), 2.6f, z), new Vector3(0.04f, 1.4f, 0.7f), _farmWindow, false);
        chapel.AddChild(new Label3D
        {
            Text = "CAPELA SÃO FREQUÊNCIO",
            FontSize = 48,
            PixelSize = 0.006f,
            Modulate = new Color(0.25f, 0.2f, 0.15f),
            OutlineSize = 0,
            Position = new Vector3(0f, 3.4f, d * 0.5f + 0.04f),
        });
        MarkPoi(chapel, "C", new Color(0.9f, 0.85f, 0.6f));
        BuildFarmLamp(pos + new Vector3(-6.5f, 0f, -6f), Vector3.Left);
    }

    // ------------------------------------------------------------------ estrada: postes, placas, orelhões

    private void BuildFarmLamp(Vector3 basePos, Vector3 towardRoad)
    {
        const float height = 6.2f;
        var lamp = new Node3D { Position = basePos };
        (_activeChunkRoot ?? _staticRoot).AddChild(lamp);
        AddCylinder(lamp, new Vector3(0f, height * 0.5f, 0f), 0.12f, height, _trunk);
        Vector3 arm = towardRoad.Normalized();
        AddBox(lamp, arm * 0.7f + new Vector3(0f, height - 0.2f, 0f), new Vector3(Mathf.Abs(arm.X) * 1.4f + 0.08f, 0.08f, Mathf.Abs(arm.Z) * 1.4f + 0.08f), _trunk, false);
        AddBox(lamp, arm * 1.35f + new Vector3(0f, height - 0.35f, 0f), new Vector3(0.45f, 0.14f, 0.45f), _lampHead, false);
        AddShape(new CylinderShape3D { Radius = 0.15f, Height = height }, new Transform3D(Basis.Identity, basePos + new Vector3(0f, height * 0.5f, 0f)));
        ActiveLightsRoot.AddChild(new OmniLight3D
        {
            Position = basePos + arm * 1.35f + new Vector3(0f, height - 0.7f, 0f),
            LightColor = new Color(1f, 0.75f, 0.45f),
            LightEnergy = 1.6f,
            OmniRange = 14f,
            OmniAttenuation = 1.3f,
            DistanceFadeEnabled = true,
            DistanceFadeBegin = 90f,
            DistanceFadeLength = 30f,
        });
    }

    private void BuildRoadLamps()
    {
        // Postes perto da cooperativa, iluminando as duas saídas.
        for (float z = 40f; z <= 120f; z += 40f)
            BuildFarmLamp(new Vector3(-5.5f, 0f, z), Vector3.Right);
        for (float x = 40f; x <= 120f; x += 40f)
            BuildFarmLamp(new Vector3(x, 0f, -5.5f), Vector3.Back);
    }

    private void BuildRoadSigns()
    {
        foreach (FarmRoad r in _farmRoads)
        {
            if (r.Width < 6f)
                continue;
            Vector2 dir = (r.B - r.A).Normalized();
            Vector2 normal = new(-dir.Y, dir.X);
            Vector2 p = r.A + dir * 14f + normal * (r.Width * 0.5f + 1.2f);
            var sign = new Node3D { Position = new Vector3(p.X, 0f, p.Y), Rotation = new Vector3(0f, Mathf.Atan2(dir.X, dir.Y) + Mathf.Pi, 0f) };
            _staticRoot.AddChild(sign);
            AddBox(sign, new Vector3(0f, 1.1f, 0f), new Vector3(0.12f, 2.2f, 0.12f), _trunk, false);
            AddBox(sign, new Vector3(0f, 2.0f, 0f), new Vector3(2.4f, 0.45f, 0.06f), _wood, false);
            foreach ((float z, float rot) in new[] { (0.04f, 0f), (-0.04f, Mathf.Pi) })
                sign.AddChild(new Label3D
                {
                    Text = r.Name.ToUpperInvariant(),
                    FontSize = 40,
                    PixelSize = 0.0045f,
                    Modulate = new Color(0.98f, 0.95f, 0.85f),
                    OutlineSize = 4,
                    OutlineModulate = new Color(0.2f, 0.1f, 0.05f),
                    Position = new Vector3(0f, 2.0f, z),
                    Rotation = new Vector3(0f, rot, 0f),
                });
        }
        // Placa grande na saída da cooperativa.
        var big = new Node3D { Position = new Vector3(6.5f, 0f, 24f), Rotation = new Vector3(0f, Mathf.Pi, 0f) };
        _staticRoot.AddChild(big);
        AddBox(big, new Vector3(0f, 1.5f, 0f), new Vector3(0.15f, 3f, 0.15f), _trunk, false);
        AddBox(big, new Vector3(0f, 2.6f, 0f), new Vector3(3f, 1.2f, 0.08f), _signBlue, false);
        big.AddChild(new Label3D
        {
            Text = "ESTRADA VELHA: 130 m em frente\nCOOPERATIVA: entrada à direita",
            FontSize = 40,
            PixelSize = 0.0055f,
            Modulate = Colors.White,
            OutlineSize = 0,
            Position = new Vector3(0f, 2.6f, -0.05f),
            Rotation = new Vector3(0f, Mathf.Pi, 0f),
        });
    }

    private void BuildFarmPhones()
    {
        var booths = new Node3D { Name = "PhoneBooths" };
        _staticRoot.AddChild(booths);
        // No sítio só tem dois: um que funciona (tchau ou morto) no portão da cooperativa
        // e um velho da Telefóbica, desativado, na frente da capela.
        AddPhoneBooth(booths, new Vector3(-5.5f, 0f, 21f), Vector3.Right, PhoneBrand.Tchau);
        AddPhoneBooth(booths, new Vector3(9.5f, 0f, 137f), Vector3.Left, PhoneBrand.Telefobica);
    }

    // ------------------------------------------------------------------ árvores e limites

    private void BuildTrees()
    {
        if (_useLogicalWorldData && WorldData != null)
        {
            foreach (WorldFarmTreePlan treePlan in WorldData.FarmTrees)
            {
                Vector2 position = new(treePlan.Position.X, treePlan.Position.Z);
                if (treePlan.Dead)
                {
                    string model = NatureKit.DeadTrees[Mathf.PosMod(treePlan.VariantIndex, NatureKit.DeadTrees.Length)];
                    var transform = new Transform3D(
                        new Basis(Vector3.Up, treePlan.RotationY) *
                        Basis.FromScale(Vector3.One * treePlan.Scale),
                        treePlan.Position);
                    if (NatureKit.Get(model) is Mesh deadMesh)
                        AddInstance("q_" + model, deadMesh, null, transform, Colors.White);
                    AddShape(new CylinderShape3D { Radius = 0.35f, Height = 4f },
                        new Transform3D(Basis.Identity, treePlan.Position + new Vector3(0f, 2f, 0f)));
                }
                else
                {
                    RandomNumberGenerator previousRng = _rng;
                    _rng = new RandomNumberGenerator { Seed = treePlan.VisualSeed };
                    try
                    {
                        Tree(position, treePlan.Pine);
                    }
                    finally
                    {
                        _rng = previousRng;
                    }
                }
            }
            return;
        }

        int count = 0;
        for (int i = 0; i < 1600 && count < 900; i++)
        {
            var p = new Vector2(_rng.RandfRange(-FarmExtent + 6f, FarmExtent - 6f), _rng.RandfRange(-FarmExtent + 6f, FarmExtent - 6f));
            if (DistanceToRoads(p) < 4f || !IsFree(p, 3f))
                continue;
            if (_rng.Randf() < 0.05f)
            {
                // Árvore seca de vez em quando (Quaternius Stylized Nature, CC0). Mesmo número de sorteios de antes.
                int kind = _rng.RandiRange(0, 2) + AssetProps.Hash(new Vector3(p.X, 0f, p.Y), 3) % 3;
                float rot = _rng.Randf() * Mathf.Tau, sc = _rng.RandfRange(1.2f, 1.8f) * 0.3f;
                var xfDead = new Transform3D(new Basis(Vector3.Up, rot) * Basis.FromScale(Vector3.One * sc), new Vector3(p.X, 0f, p.Y));
                if (NatureKit.Get(NatureKit.DeadTrees[kind % 5]) is Mesh deadMesh)
                    AddInstance("q_" + NatureKit.DeadTrees[kind % 5], deadMesh, null, xfDead, Colors.White);
                AddShape(new CylinderShape3D { Radius = 0.35f, Height = 4f }, new Transform3D(Basis.Identity, new Vector3(p.X, 2f, p.Y)));
            }
            else
                Tree(p, _rng.Randf() < 0.45f);
            count++;
        }
        // Mata fechada nas bordas do mapa.
        for (float t = -FarmExtent; t <= FarmExtent; t += 7f)
        {
            foreach (Vector2 p in new[] { new Vector2(t, -FarmExtent), new Vector2(t, FarmExtent), new Vector2(-FarmExtent, t), new Vector2(FarmExtent, t) })
            {
                Vector2 jitter = new(_rng.RandfRange(-2.5f, 2.5f), _rng.RandfRange(-2.5f, 2.5f));
                Tree(p + jitter, _rng.Randf() < 0.6f);
            }
        }
    }

    private void Tree(Vector2 p, bool pine)
    {
        float s = _rng.RandfRange(0.8f, 1.5f);
        var basePos = new Vector3(p.X, 0f, p.Y);
        float trunkH = (pine ? 2.2f : 2.6f) * s;
        // Modelos da Quaternius (v0.9.4). Os sorteios continuam os mesmos para o resto do mapa não mudar.
        string[] kinds = pine ? NatureKit.Pines : NatureKit.CommonTrees;
        string model = kinds[AssetProps.Hash(basePos, 5) % kinds.Length];
        if (NatureKit.Get(model) is Mesh treeMesh)
        {
            _rng.Randf(); _rng.Randf(); _rng.Randf(); // (antes: tom das folhas)
            float rot = pine ? AssetProps.Hash(basePos, 6) % 628 / 100f : _rng.Randf() * 3f;
            float k = s * (pine ? 0.85f : 0.8f);
            AddInstance("q_" + model, treeMesh, null, new Transform3D(new Basis(Vector3.Up, rot) * Basis.FromScale(Vector3.One * k), basePos), Colors.White);
            AddShape(new CylinderShape3D { Radius = 0.3f * s, Height = trunkH }, new Transform3D(Basis.Identity, basePos + new Vector3(0f, trunkH * 0.5f, 0f)));
            return;
        }
        AddInstance("trunk", _unitCylinder, _trunk, new Transform3D(Basis.FromScale(new Vector3(0.35f * s, trunkH, 0.35f * s)), basePos + new Vector3(0f, trunkH * 0.5f, 0f)), Colors.White);
        Color tint = new Color(0.75f + _rng.Randf() * 0.35f, 0.85f + _rng.Randf() * 0.3f, 0.7f + _rng.Randf() * 0.3f);
        if (pine)
        {
            AddInstance("pine", _treeTop, _treeTint, new Transform3D(Basis.FromScale(new Vector3(3.2f * s, 4.5f * s, 3.2f * s)), basePos + new Vector3(0f, trunkH + 1.6f * s, 0f)), _leaves.AlbedoColor * tint);
            AddInstance("pine", _treeTop, _treeTint, new Transform3D(Basis.FromScale(new Vector3(2.3f * s, 3.2f * s, 2.3f * s)), basePos + new Vector3(0f, trunkH + 3.4f * s, 0f)), _leaves.AlbedoColor * tint);
        }
        else
        {
            AddInstance("crown", _treeCrown ??= new SphereMesh { Radius = 0.5f, Height = 1f, RadialSegments = 8, Rings = 4 }, _treeTint,
                new Transform3D(new Basis(Vector3.Up, _rng.Randf() * 3f) * Basis.FromScale(new Vector3(4.2f * s, 3.4f * s, 4.0f * s)), basePos + new Vector3(0f, trunkH + 1.2f * s, 0f)),
                new Color(0.16f, 0.3f, 0.12f) * tint);
        }
        AddShape(new CylinderShape3D { Radius = 0.3f * s, Height = trunkH }, new Transform3D(Basis.Identity, basePos + new Vector3(0f, trunkH * 0.5f, 0f)));
    }

    private SphereMesh _treeCrown;

    /// <summary>Capim, samambaias, moitas, flores e pedras espalhados (sorteio próprio: não mexe no mapa).</summary>
    private void BuildUndergrowth()
    {
        if (_useLogicalWorldData && WorldData != null)
        {
            foreach (WorldFarmUndergrowthPlan plan in WorldData.FarmUndergrowth)
            {
                if (NatureKit.Get(plan.Model) is not Mesh mesh)
                    return;

                var transform = new Transform3D(
                    new Basis(Vector3.Up, plan.RotationY) * Basis.FromScale(Vector3.One * plan.Scale),
                    plan.Position);
                AddInstance("qg_" + plan.Model, mesh, null, transform, Colors.White);
            }
            return;
        }

        var rng = new RandomNumberGenerator { Seed = 96601 };
        int placed = 0;
        for (int i = 0; i < 9000 && placed < 2600; i++)
        {
            var p = new Vector2(rng.RandfRange(-FarmExtent + 4f, FarmExtent - 4f), rng.RandfRange(-FarmExtent + 4f, FarmExtent - 4f));
            if (DistanceToRoads(p) < 3.2f || !IsFree(p, 1.2f))
                continue;
            bool rock = rng.Randf() < 0.06f;
            string model = rock ? NatureKit.Rocks[rng.RandiRange(0, NatureKit.Rocks.Length - 1)] : NatureKit.Ground[rng.RandiRange(0, NatureKit.Ground.Length - 1)];
            if (NatureKit.Get(model) is not Mesh mesh)
                return;
            float sc = rock ? rng.RandfRange(0.25f, 0.6f) : rng.RandfRange(0.6f, 1.2f);
            AddInstance("qg_" + model, mesh, null, new Transform3D(new Basis(Vector3.Up, rng.Randf() * Mathf.Tau) * Basis.FromScale(Vector3.One * sc), new Vector3(p.X, 0f, p.Y)), Colors.White);
            placed++;
        }
    }

    /// <summary>Capim baixo no sítio todo (v0.9.7), fora das estradas, construções, lago e pátio.</summary>
    private void BuildFarmGrass()
    {
        var root = new Node3D { Name = "Capim" };
        _staticRoot.AddChild(root);
        GrassTufts.Fill(root, Vector3.Zero, new Vector2(FarmExtent * 2f - 6f, FarmExtent * 2f - 6f), 1.25f, new Color(0.95f, 0.9f, 0.7f),
            p => Mathf.Max(Mathf.Abs(p.X), Mathf.Abs(p.Y)) > 20f && DistanceToRoads(p) > 1.2f && IsFree(p, 0.3f));
    }

    private void BuildFarmBoundary()
    {
        float limit = FarmExtent + 4f;
        const float h = 8f;
        foreach ((Vector3 c, Vector3 size) in new[]
                 {
                     (new Vector3(limit, h * 0.5f, 0f), new Vector3(1f, h, limit * 2f)),
                     (new Vector3(-limit, h * 0.5f, 0f), new Vector3(1f, h, limit * 2f)),
                     (new Vector3(0f, h * 0.5f, limit), new Vector3(limit * 2f, h, 1f)),
                     (new Vector3(0f, h * 0.5f, -limit), new Vector3(limit * 2f, h, 1f)),
                 })
            AddShape(new BoxShape3D { Size = size }, new Transform3D(Basis.Identity, c));

        // v0.9.6: o limite deixa de ser uma parede invisível — muro de pedra com pilares e, atrás, a serra.
        var rng = new RandomNumberGenerator { Seed = 3220 };
        var stone = Flat(new Color(0.48f, 0.46f, 0.42f), 0.95f);
        var cap = Flat(new Color(0.38f, 0.36f, 0.33f), 0.95f);
        var mountainMat = Flat(new Color(0.22f, 0.33f, 0.17f), 1f);
        var cone = new CylinderMesh { TopRadius = 0.05f, BottomRadius = 0.5f, Height = 1f, RadialSegments = 7, Rings = 2 };

        if (_useLogicalWorldData && WorldData != null)
        {
            foreach (WorldFarmBoundaryPlan plan in WorldData.FarmBoundary)
            {
                switch (plan.Kind)
                {
                    case "wall":
                        AddInstance("muro", _unitBox, stone,
                            new Transform3D(Basis.FromScale(plan.Scale), plan.Position), plan.Tint);
                        break;
                    case "pillar":
                        AddInstance("muro_pilar", _unitBox, cap,
                            new Transform3D(Basis.FromScale(plan.Scale), plan.Position), Colors.White);
                        break;
                    case "mountain":
                        AddInstance("serra", cone, mountainMat,
                            new Transform3D(
                                new Basis(Vector3.Up, plan.RotationY) * Basis.FromScale(plan.Scale),
                                plan.Position),
                            plan.Tint);
                        break;
                }
            }
            return;
        }

        float wallX = limit - 0.6f;
        foreach ((Vector3 dir, Vector3 along) in new[] { (Vector3.Right, Vector3.Back), (Vector3.Left, Vector3.Back), (Vector3.Back, Vector3.Right), (Vector3.Forward, Vector3.Right) })
        {
            for (float t = -limit; t < limit; t += 6f)
            {
                float wh = rng.RandfRange(2.6f, 3.1f);
                Vector3 c = dir * wallX + along * (t + 3f);
                var size = dir.X != 0f ? new Vector3(0.8f, wh, 6.05f) : new Vector3(6.05f, wh, 0.8f);
                AddInstance("muro", _unitBox, stone, new Transform3D(Basis.FromScale(size), c + new Vector3(0f, wh * 0.5f, 0f)), new Color(0.9f + rng.Randf() * 0.2f, 0.9f + rng.Randf() * 0.2f, 0.9f + rng.Randf() * 0.15f));
                if (Mathf.PosMod(t, 12f) < 0.5f)
                    AddInstance("muro_pilar", _unitBox, cap, new Transform3D(Basis.FromScale(new Vector3(1.1f, 3.6f, 1.1f)), c - along * 3f + new Vector3(0f, 1.8f, 0f)), Colors.White);
            }
            // Serra: morros cônicos logo depois do muro (dois "anéis", os de trás maiores).
            for (int ring = 0; ring < 2; ring++)
            {
                for (float t = -limit - 40f; t < limit + 40f; t += ring == 0 ? 26f : 38f)
                {
                    float r = ring == 0 ? rng.RandfRange(18f, 30f) : rng.RandfRange(30f, 48f);
                    float hgt = ring == 0 ? rng.RandfRange(22f, 40f) : rng.RandfRange(40f, 75f);
                    Vector3 c = dir * (limit + r * 0.75f + ring * 30f) + along * (t + rng.RandfRange(-8f, 8f));
                    var tint = new Color(0.8f + rng.Randf() * 0.35f, 0.8f + rng.Randf() * 0.3f, 0.75f + rng.Randf() * 0.3f);
                    AddInstance("serra", cone, mountainMat, new Transform3D(new Basis(Vector3.Up, rng.Randf() * 3f) * Basis.FromScale(new Vector3(r * 2f, hgt, r * 2f)), c + new Vector3(0f, hgt * 0.5f - 1f, 0f)), tint);
                }
            }
        }
    }

    // ------------------------------------------------------------------ cercas, colisão e MultiMesh

    /// <summary>Cerca de madeira de a até b (coordenadas locais de xf): mourões + 2 réguas, com colisão.</summary>
    private void Fence(Transform3D xf, Vector3 a, Vector3 b)
    {
        Vector3 wa = xf * a, wb = xf * b;
        Vector3 d = wb - wa;
        float length = d.Length();
        if (length < 0.5f)
            return;
        float yaw = Mathf.Atan2(d.X, d.Z);
        var rot = new Basis(Vector3.Up, yaw);
        int posts = Mathf.Max(1, Mathf.CeilToInt(length / 2.6f));
        for (int i = 0; i <= posts; i++)
        {
            Vector3 p = wa + d * (i / (float)posts);
            AddInstance("post", _unitBox, _trunk, new Transform3D(rot * Basis.FromScale(new Vector3(0.14f, 1.3f, 0.14f)), p + new Vector3(0f, 0.65f, 0f)), Colors.White);
        }
        Vector3 mid = (wa + wb) * 0.5f;
        foreach (float y in new[] { 0.55f, 1.05f })
            AddInstance("rail", _unitBox, _wood, new Transform3D(rot * Basis.FromScale(new Vector3(0.05f, 0.1f, length)), mid + new Vector3(0f, y, 0f)), Colors.White);
        AddShape(new BoxShape3D { Size = new Vector3(0.2f, 1.3f, length) }, new Transform3D(rot, mid + new Vector3(0f, 0.65f, 0f)));
    }

    private void AddShape(Shape3D shape, Transform3D transform)
    {
        (_activeFarmBody ?? _farmBody).AddChild(new CollisionShape3D { Shape = shape, Transform = transform });
    }

    private void AddInstance(string key, Mesh mesh, Material material, Transform3D transform, Color color)
    {
        if (!_batches.TryGetValue(key, out Batch batch))
        {
            batch = new Batch { Mesh = mesh, Material = material };
            _batches[key] = batch;
        }
        batch.Transforms.Add(transform);
        batch.Colors.Add(color);
    }

    private static void FlushChunked(Node3D parent, string key, Batch batch, float range)
    {
        const float chunk = 64f;
        var groups = new Dictionary<Vector2I, List<int>>();
        for (int i = 0; i < batch.Transforms.Count; i++)
        {
            Vector3 o = batch.Transforms[i].Origin;
            var c = new Vector2I(Mathf.FloorToInt(o.X / chunk), Mathf.FloorToInt(o.Z / chunk));
            if (!groups.TryGetValue(c, out List<int> list))
                groups[c] = list = new List<int>();
            list.Add(i);
        }
        foreach ((Vector2I c, List<int> list) in groups)
        {
            var mm = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, Mesh = batch.Mesh, InstanceCount = list.Count };
            for (int i = 0; i < list.Count; i++)
                mm.SetInstanceTransform(i, batch.Transforms[list[i]]);
            parent.AddChild(new MultiMeshInstance3D
            {
                Name = $"MM_{key}_{c.X}_{c.Y}",
                Multimesh = mm,
                VisibilityRangeEnd = range,
                VisibilityRangeEndMargin = 8f,
                CastShadow = key.StartsWith("qg_") ? GeometryInstance3D.ShadowCastingSetting.Off : GeometryInstance3D.ShadowCastingSetting.On,
            });
        }
    }

    private void FlushBatches(Node3D parent)
    {
        foreach ((string key, Batch batch) in _batches)
        {
            var mm = new MultiMesh
            {
                TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
                UseColors = true,
                Mesh = batch.Mesh,
                InstanceCount = batch.Transforms.Count,
            };
            for (int i = 0; i < batch.Transforms.Count; i++)
            {
                mm.SetInstanceTransform(i, batch.Transforms[i]);
                mm.SetInstanceColor(i, batch.Colors[i]);
            }
            var material = batch.Material;
            if (material == null)
            {
                // Modelos da Quaternius (árvores e mato): em pedaços de 64 m, cada um some além da neblina,
                // senão a placa de vídeo desenharia todas as milhares de árvores do mapa o tempo todo.
                FlushChunked(parent, key, batch, key.StartsWith("qg_") ? 60f : 240f);
                continue;
            }
            if (material is StandardMaterial3D std && !std.VertexColorUseAsAlbedo)
            {
                // Sem isso a cor por instância seria ignorada; copia para não afetar os outros usos.
                std = (StandardMaterial3D)std.Duplicate();
                std.VertexColorUseAsAlbedo = true;
                material = std;
            }
            parent.AddChild(new MultiMeshInstance3D { Name = "MM_" + key, Multimesh = mm, MaterialOverride = material });
        }
        _batches.Clear();
    }
}
