// Scripts/World/StreetVendor.cs
// Barraquinha de rua com comerciante. Mire no balcão e aperte E para abrir a loja.
// A barraca é montada em código; a frente fica virada para -Z do nó.
using Godot;

namespace DeadFrequency;

public partial class StreetVendor : Area3D, IInteractable
{
    [Export] public string VendorName { get; set; } = "Barraca do Zé";
    [Export] public Color CanopyColor { get; set; } = new(0.8f, 0.15f, 0.12f);
    [Export] public Color ShirtColor { get; set; } = new(0.55f, 0.35f, 0.15f);
    /// <summary>Churrasquinho do Duvidoso (v0.9.5): chapa com fumaça depois das 18h, gente em volta e só comida.</summary>
    [Export] public bool Churrasco { get; set; }
    /// <summary>O que a barraca vende (v0.9.9). O Churrasco acima continua valendo para o churrasquinho.</summary>
    [Export] public ShopStock Stock { get; set; } = ShopStock.Ze;

    /// <summary>Barracas na fase (o gari procura o Conserta-Tudo do Primo para vender sucata).</summary>
    public static readonly System.Collections.Generic.List<StreetVendor> All = new();
    public override void _EnterTree() => All.Add(this);
    public override void _ExitTree() => All.Remove(this);

    private CpuParticles3D _smoke;
    private AudioStreamPlayer3D _sizzle;
    private Node3D _crowd;
    private StandardMaterial3D _coals;
    private OmniLight3D _grillLight;
    private bool _nightOn;

    private CharacterModel _vendor;
    private float _idle;

    public override void _Ready()
    {
        AddToGroup(WorldChunkManager.TrackedObjectGroup);
        CollisionLayer = 4; // Interactables
        CollisionMask = 0;
        Monitoring = false;
        AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(2.6f, 1.4f, 1.2f) }, Position = new Vector3(0f, 0.7f, -0.2f) });

        AddToGroup(MinimapView.MarkerGroup);
        SetMeta("map_color", new Color(1f, 0.85f, 0.2f));
        SetMeta("map_kind", "vendor");

        BuildStall();
    }

    private static StandardMaterial3D Mat(Color c, float rough = 0.9f) => new() { AlbedoColor = c, Roughness = rough };

    private void Box(Node3D parent, Vector3 pos, Vector3 size, Material mat)
    {
        parent.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = size }, Position = pos, MaterialOverride = mat });
    }

    /// <summary>Conserta-Tudo do Primo: pilha de pneus, TV velha e ferro-velho do lado da barraca.</summary>
    private void BuildScrapPile(StandardMaterial3D metal)
    {
        var rubber = Mat(new Color(0.06f, 0.06f, 0.06f));
        var rust = Mat(new Color(0.45f, 0.24f, 0.12f), 0.8f);
        var tvCase = Mat(new Color(0.3f, 0.26f, 0.22f), 0.6f);
        for (int i = 0; i < 3; i++)
            AddChild(new MeshInstance3D { Mesh = new TorusMesh { InnerRadius = 0.2f, OuterRadius = 0.36f, Rings = 10, RingSegments = 6 }, MaterialOverride = rubber, Position = new Vector3(1.9f, 0.08f + i * 0.17f, 0.2f) });
        Box(this, new Vector3(-1.85f, 0.25f, 0.25f), new Vector3(0.55f, 0.45f, 0.45f), tvCase);
        Box(this, new Vector3(-1.85f, 0.27f, 0.025f), new Vector3(0.42f, 0.32f, 0.01f), Mat(new Color(0.12f, 0.14f, 0.13f), 0.2f));
        var pipe = new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 0.05f, BottomRadius = 0.05f, Height = 1.4f }, MaterialOverride = rust, Position = new Vector3(1.6f, 0.06f, 0.75f), Rotation = new Vector3(0f, 0.4f, Mathf.Pi * 0.5f) };
        AddChild(pipe);
        Box(this, new Vector3(-1.6f, 0.12f, 0.85f), new Vector3(0.6f, 0.24f, 0.4f), metal);
        AddChild(new Label3D
        {
            Text = "COMPRO SUCATA",
            FontSize = 40,
            PixelSize = 0.004f,
            Modulate = new Color(1f, 0.85f, 0.2f),
            OutlineSize = 6,
            Position = new Vector3(1.9f, 0.75f, 0.2f),
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
        });
    }

    private void BuildStall()
    {
        // Modelos do Meshy (v0.9.9): churrasquinho completo ou barraca com o toldo na cor do vendedor.
        string prop = Churrasco ? "churrasco" : "barraca";
        bool meshy = MeshyVehicles.HasProp(prop);
        var wood = Mat(new Color(0.42f, 0.28f, 0.16f));
        var metal = Mat(new Color(0.2f, 0.2f, 0.22f), 0.5f);
        var white = Mat(new Color(0.9f, 0.88f, 0.82f));
        var canopy = Mat(CanopyColor);

        // Corpo físico (balcão) para não atravessar.
        var body = new StaticBody3D { CollisionLayer = 1, CollisionMask = 0 };
        body.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(2.4f, 1f, 0.8f) }, Position = new Vector3(0f, 0.5f, 0f) });
        AddChild(body);

        if (meshy)
        {
            // Frente (lado do freguês) = -Z, como o balcão antigo; o vendedor fica atrás, em +Z.
            var holder = new Node3D { Name = "Barraca", Position = Churrasco ? new Vector3(-0.6f, 0f, 0.25f) : new Vector3(0f, 0f, 0.2f) };
            AddChild(holder);
            if (Churrasco)
                MeshyVehicles.BuildProp(holder, prop, new Vector3(2.6f, 2.4f, 2.0f), -1);
            else
                MeshyVehicles.BuildProp(holder, prop, new Vector3(2.6f, 2.5f, 2.0f), -1, CanopyColor, 0.1225f);
            if (Stock == ShopStock.ConsertaTudo)
                BuildScrapPile(metal);
        }
        else
        {
        // Balcão com mercadorias.
        Box(this, new Vector3(0f, 0.45f, 0f), new Vector3(2.4f, 0.9f, 0.8f), wood);
        Box(this, new Vector3(0f, 0.92f, 0f), new Vector3(2.5f, 0.05f, 0.9f), white);
        string[] goods = Churrasco
            ? new[] { ItemCatalog.Guarana, ItemCatalog.Coffee, ItemCatalog.Marmitex, ItemCatalog.Guarana, ItemCatalog.Marmitex }
            : Stock == ShopStock.ConsertaTudo
            ? new[] { ItemCatalog.StrongFlashlight, ItemCatalog.RepairKit, ItemCatalog.DuctTape, ItemCatalog.Nail, ItemCatalog.StrongFlashlight }
            : new[] { ItemCatalog.Radar, ItemCatalog.NightVision, ItemCatalog.Gloves, ItemCatalog.EnergyDrink, ItemCatalog.RepairKit };
        if (Stock == ShopStock.ConsertaTudo)
            BuildScrapPile(metal);
        for (int i = 0; i < goods.Length; i++)
        {
            ItemDef def = ItemCatalog.Get(goods[i]);
            var m = Mat(def.Color, 0.5f);
            m.EmissionEnabled = goods[i] is ItemCatalog.Radar or ItemCatalog.NightVision;
            m.Emission = def.Color;
            m.EmissionEnergyMultiplier = 0.6f;
            Box(this, new Vector3(-0.9f + i * 0.45f, 1.02f, -0.1f), new Vector3(0.28f, 0.16f + (i % 2) * 0.08f, 0.24f), m);
        }

        // Postes e toldo listrado.
        foreach (float x in new[] { -1.2f, 1.2f })
            foreach (float z in new[] { -0.45f, 0.9f })
                Box(this, new Vector3(x, 1.2f, z), new Vector3(0.06f, 2.4f, 0.06f), metal);
        for (int i = 0; i < 6; i++)
            Box(this, new Vector3(-1.25f + i * 0.5f + 0.25f, 2.42f, 0.22f), new Vector3(0.5f, 0.06f, 1.6f), i % 2 == 0 ? canopy : white);
        Box(this, new Vector3(0f, 2.3f, -0.58f), new Vector3(2.6f, 0.2f, 0.04f), canopy);

        // Caixotes ao lado.
        Box(this, new Vector3(1.6f, 0.25f, 0.4f), new Vector3(0.5f, 0.5f, 0.5f), wood);
        Box(this, new Vector3(1.6f, 0.7f, 0.45f), new Vector3(0.4f, 0.4f, 0.4f), wood);
        }

        // Placa e luz.
        AddChild(new Label3D
        {
            Text = VendorName.ToUpperInvariant() + "\n[E] COMPRAR",
            FontSize = 56,
            PixelSize = 0.006f,
            Modulate = new Color(1f, 0.9f, 0.5f),
            OutlineSize = 10,
            Position = new Vector3(0f, meshy ? 2.55f : 2.3f, meshy ? -0.85f : -0.62f),
            RotationDegrees = new Vector3(0f, 180f, 0f),
        });
        var bulb = Mat(new Color(1f, 0.85f, 0.5f));
        bulb.EmissionEnabled = true;
        bulb.Emission = new Color(1f, 0.8f, 0.45f);
        bulb.EmissionEnergyMultiplier = 3f;
        if (!meshy)
            Box(this, new Vector3(0f, 2.25f, 0.3f), new Vector3(0.12f, 0.12f, 0.12f), bulb);
        AddChild(new OmniLight3D { Position = new Vector3(0f, 2.1f, 0f), LightColor = new Color(1f, 0.8f, 0.5f), LightEnergy = 1.6f, OmniRange = 6f });

        // Comerciante atrás do balcão, de frente para a rua.
        _vendor = CharacterModel.Create(ShirtColor, new Color(0.2f, 0.2f, 0.25f), new Color(0.65f, 0.48f, 0.36f));
        _vendor.Position = new Vector3(0f, 0f, 0.75f);
        _vendor.Chatty = true;
        _vendor.Lines = Churrasco
            ? new[] { "Olha o espetinho! Carne, frango e... misterioso.", "Garanto que é de boi. Quase certeza.", "Dá um pedaço pro caramelo que ele vira teu amigo!", "Marmitex da Tia chegou quentinha!" }
            : new[] { "Chega mais, freguês! Tem de tudo aqui.", "Chinelo Legítimas, o original! Ploc-ploc garantido.", "Prego avulso, dez reais. Não pergunta.", "Guaraná Pinguim trincando de gelado!" };
        AddChild(_vendor);
        if (Churrasco)
            BuildGrill(metal, meshy);
    }

    /// <summary>Churrasqueira de lata ao lado do balcão, com brasa, fumaça e o crepitar da chapa.</summary>
    private void BuildGrill(Material metal, bool meshy = false)
    {
        // Com o modelo do Meshy, a churrasqueira já está desenhada: aqui só ficam brasa, luz, fumaça e som.
        var grill = new Node3D { Name = "Churrasqueira", Position = meshy ? new Vector3(-0.75f, 0.35f, 0.25f) : new Vector3(-1.75f, 0f, 0.1f) };
        AddChild(grill);
        _coals = Mat(new Color(1f, 0.35f, 0.08f));
        _coals.EmissionEnabled = true;
        _coals.Emission = new Color(1f, 0.3f, 0.05f);
        _coals.EmissionEnergyMultiplier = 0.2f;
        if (meshy)
            Box(grill, new Vector3(0f, 0.5f, 0f), new Vector3(0.62f, 0.02f, 0.34f), _coals);
        else
        {
        Box(grill, new Vector3(0f, 0.45f, 0f), new Vector3(0.9f, 0.08f, 0.45f), metal);
        foreach (float x in new[] { -0.4f, 0.4f })
            foreach (float z in new[] { -0.18f, 0.18f })
                Box(grill, new Vector3(x, 0.22f, z), new Vector3(0.04f, 0.45f, 0.04f), metal);
        Box(grill, new Vector3(0f, 0.5f, 0f), new Vector3(0.8f, 0.03f, 0.38f), _coals);
        var meat = Mat(new Color(0.45f, 0.2f, 0.1f));
        var stick = Mat(new Color(0.8f, 0.7f, 0.5f));
        for (int i = 0; i < 6; i++)
        {
            float x = -0.3f + i * 0.12f;
            Box(grill, new Vector3(x, 0.55f, 0f), new Vector3(0.015f, 0.015f, 0.5f), stick);
            Box(grill, new Vector3(x, 0.56f, 0.02f), new Vector3(0.06f, 0.05f, 0.22f), meat);
        }
        }
        _grillLight = new OmniLight3D { Position = new Vector3(0f, 0.8f, 0f), LightColor = new Color(1f, 0.45f, 0.15f), LightEnergy = 0f, OmniRange = 4f };
        grill.AddChild(_grillLight);

        _smoke = new CpuParticles3D
        {
            Position = new Vector3(0f, 0.6f, 0f),
            Amount = 40,
            Lifetime = 3.5,
            Emitting = false,
            Mesh = new QuadMesh { Size = new Vector2(0.35f, 0.35f) },
            Direction = Vector3.Up,
            Spread = 18f,
            Gravity = new Vector3(0.25f, 0.55f, 0f),
            InitialVelocityMin = 0.4f,
            InitialVelocityMax = 0.8f,
            ScaleAmountMin = 0.8f,
            ScaleAmountMax = 2.4f,
            EmissionShape = CpuParticles3D.EmissionShapeEnum.Box,
            EmissionBoxExtents = new Vector3(0.35f, 0.02f, 0.15f),
        };
        var smokeMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.75f, 0.75f, 0.72f, 0.35f),
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            BillboardMode = BaseMaterial3D.BillboardModeEnum.Particles,
            VertexColorUseAsAlbedo = true,
        };
        _smoke.MaterialOverride = smokeMat;
        var ramp = new Gradient();
        ramp.SetColor(0, new Color(1f, 1f, 1f, 0.5f));
        ramp.SetColor(1, new Color(1f, 1f, 1f, 0f));
        _smoke.ColorRamp = ramp;
        grill.AddChild(_smoke);

        _sizzle = new AudioStreamPlayer3D { Position = new Vector3(0f, 0.6f, 0f), Bus = AudioManager.SfxBus, UnitSize = 3f, MaxDistance = 18f, VolumeDb = -6f };
        if (ResourceLoader.Exists("res://Audio/SFX/chapa.wav"))
            _sizzle.Stream = GD.Load<AudioStream>("res://Audio/SFX/chapa.wav");
        grill.AddChild(_sizzle);
        _sizzle.Finished += () => { if (_nightOn) _sizzle.Play(); };

        // Freguesia que aparece à noite (fica em pé em volta da barraca, conversando).
        _crowd = new Node3D { Name = "Freguesia", Visible = false };
        AddChild(_crowd);
        Color[] shirts = { new(0.2f, 0.35f, 0.6f), new(0.7f, 0.2f, 0.15f), new(0.85f, 0.8f, 0.3f) };
        Vector3[] spots = { new(-1.6f, 0f, -1.3f), new(-0.6f, 0f, -1.5f), new(0.9f, 0f, -1.4f) };
        for (int i = 0; i < spots.Length; i++)
        {
            CharacterModel c = CharacterModel.Create(shirts[i], new Color(0.15f, 0.15f, 0.2f), new Color(0.55f + i * 0.12f, 0.4f + i * 0.06f, 0.3f));
            c.Position = spots[i];
            c.Rotation = new Vector3(0f, Mathf.Pi + (i - 1) * 0.5f, 0f); // virados para a barraca (+Z)
            c.Chatty = true;
            c.Lines = new[] { "Esse espetinho é o melhor da cidade. Não pergunta do quê.", "Mais um de frango, Duvidoso!", "Vim pela fumaça, fiquei pelo cheiro.", "Já é o terceiro, mas quem tá contando?" };
            _crowd.AddChild(c);
        }
        UpdateNight(true);
    }

    /// <summary>Depois das 18h (até as 5h) a chapa acende, sai fumaça e junta gente.</summary>
    private void UpdateNight(bool force = false)
    {
        float h = DayNightController.Hour;
        bool night = h >= 18f || h < 5f;
        if (night == _nightOn && !force)
            return;
        _nightOn = night;
        _smoke.Emitting = night;
        _crowd.Visible = night;
        _coals.EmissionEnergyMultiplier = night ? 2.5f : 0.2f;
        _grillLight.LightEnergy = night ? 1.4f : 0f;
        if (night && _sizzle.Stream != null)
            _sizzle.Play();
        else
            _sizzle.Stop();
    }

    public override void _Process(double delta)
    {
        // Mexe a cabeça de vez em quando (parado, mas vivo).
        _idle += (float)delta;
        _vendor?.SetHeadPitch(Mathf.Sin(_idle * 0.7f) * 0.15f);
        _vendor?.Animate((float)delta, 0f);
        if (Churrasco && _smoke != null)
        {
            UpdateNight();
            if (_crowd.Visible)
                foreach (Node n in _crowd.GetChildren())
                    ((CharacterModel)n).Animate((float)delta, 0f);
        }
    }

    public string GetInteractionPrompt(PlayerController player) => $"[E] Comprar na {VendorName}";

    public bool CanInteract(PlayerController player) => ShopScreen.Instance != null && !player.IsHolding;

    public void Interact(PlayerController player) => ShopScreen.Instance?.OpenFor(VendorName, Churrasco ? ShopStock.Churrasco : Stock);
}
