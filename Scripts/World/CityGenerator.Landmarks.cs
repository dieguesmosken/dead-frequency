// Scripts/World/CityGenerator.Landmarks.cs
// Parte da CityGenerator (mesma classe, outro arquivo): detalhes de rua e prédios especiais.
//  - Lojas no térreo com paródias de redes famosas do Brasil
//  - Ar-condicionados de parede alinhados com as janelas
//  - Orelhões (usáveis: dão dicas ou mensagens estranhas)
//  - Hidrantes, Corpo de Bombeiros e Delegacia
// Tudo que é sorteado aqui usa _decoRng, separado do sorteio principal da cidade.
using System.Collections.Generic;
using Godot;

namespace DeadFrequency;

public partial class CityGenerator
{
    [ExportGroup("Prédios especiais")]
    /// <summary>Quarteirão do Corpo de Bombeiros (fachada para +Z, de frente para a avenida do depósito).</summary>
    [Export] public Vector2I FireStationBlock { get; set; } = new(1, -1);
    /// <summary>Quarteirão da Delegacia (fachada para -Z).</summary>
    [Export] public Vector2I PoliceStationBlock { get; set; } = new(-1, 1);
    [Export(PropertyHint.Range, "0,1,0.05")] public float StorefrontChance { get; set; } = 0.45f;
    [Export(PropertyHint.Range, "0,1,0.05")] public float WallAcChance { get; set; } = 0.55f;
    [Export(PropertyHint.Range, "0,1,0.05")] public float PhoneBoothChance { get; set; } = 0.5f;

    private RandomNumberGenerator _decoRng;

    private sealed class StoreBrand
    {
        public string Id = "";
        public string Name = "";
        public string Slogan = "";
        public Color Sign;
        public Color Text;
        public StandardMaterial3D SignMaterial;
        public StandardMaterial3D AwningMaterial;
        /// <summary>Letreiro desenhado (Assets/Textures/Stores/sign_ID.png), aceso à noite.</summary>
        public StandardMaterial3D FaceMaterial;
    }

    /// <summary>Paródias (nomes inventados) de lojas populares no Brasil.</summary>
    private readonly List<StoreBrand> _brands = new()
    {
        new() { Id = "casas_baixa", Name = "CASAS BAIXA", Slogan = "dedicação parcial a você", Sign = new Color(0.05f, 0.18f, 0.6f), Text = new Color(1f, 0.85f, 0.1f) },
        new() { Id = "magazine_luzia", Name = "MAGAZINE LUZIA", Slogan = "vem ser feliz (no carnê)", Sign = new Color(0.0f, 0.45f, 0.9f), Text = Colors.White },
        new() { Id = "lojas_marcianas", Name = "LOJAS MARCIANAS", Slogan = "tem tudo, menos troco", Sign = new Color(0.85f, 0.08f, 0.1f), Text = Colors.White },
        new() { Id = "pao_de_sal", Name = "PÃO DE SAL", Slogan = "supermercado", Sign = new Color(0.1f, 0.45f, 0.2f), Text = new Color(1f, 0.95f, 0.8f) },
        new() { Id = "droga_arraia", Name = "DROGA ARRAIA", Slogan = "farmácia 24h · quase sempre", Sign = new Color(0.05f, 0.25f, 0.5f), Text = new Color(1f, 0.3f, 0.3f) },
        new() { Id = "rabibs", Name = "RABIB'S", Slogan = "esfiha a R$ 0,99 (em 1996)", Sign = new Color(0.8f, 0.1f, 0.08f), Text = new Color(1f, 0.85f, 0.2f) },
        new() { Id = "bobos", Name = "BOBO'S", Slogan = "o milkshake mais ou menos", Sign = new Color(0.9f, 0.1f, 0.15f), Text = Colors.White },
        new() { Id = "bota_cheiro", Name = "O BOTA-CHEIRO", Slogan = "perfumaria", Sign = new Color(0.04f, 0.3f, 0.24f), Text = new Color(0.95f, 0.8f, 0.45f) },
        new() { Id = "riacho_velho", Name = "RIACHO VELHO", Slogan = "moda para toda a família", Sign = new Color(0.08f, 0.08f, 0.08f), Text = Colors.White },
        new() { Id = "ravan", Name = "RAVAN", Slogan = "a loja mais patriota do bairro", Sign = new Color(0.95f, 0.95f, 0.95f), Text = new Color(0.05f, 0.2f, 0.6f) },
        new() { Id = "correrios", Name = "CORRERIOS", Slogan = "entregamos... um dia", Sign = new Color(0.98f, 0.8f, 0.1f), Text = new Color(0.05f, 0.25f, 0.6f) },
        new() { Id = "acai_atacadista", Name = "AÇAÍ ATACADISTA", Slogan = "compre 12, leve 12", Sign = new Color(0.95f, 0.45f, 0.05f), Text = new Color(0.05f, 0.15f, 0.45f) },
        // Bancos, lotéricas, padarias e mercados (v0.8).
        new() { Id = "caixote_economica", Name = "CAIXOTE ECONÔMICA", Slogan = "o banco que guarda tudo no colchão", Sign = new Color(0f, 0.34f, 0.61f), Text = Colors.White },
        new() { Id = "etau", Name = "ETAÚ", Slogan = "feito para você pagar a tarifa", Sign = new Color(0.93f, 0.44f, 0f), Text = new Color(0.05f, 0.16f, 0.43f) },
        new() { Id = "bravesco", Name = "BRAVESCO", Slogan = "completo, irritado e sem fila", Sign = new Color(0.75f, 0.04f, 0.16f), Text = Colors.White },
        new() { Id = "santo_ander", Name = "SANTO ANDER", Slogan = "o banco abençoado pelos juros", Sign = new Color(0.86f, 0.08f, 0.08f), Text = Colors.White },
        new() { Id = "sorte_morta", Name = "LOTÉRICA SORTE MORTA", Slogan = "Mega-Semana acumulada", Sign = new Color(0.08f, 0.47f, 0.27f), Text = new Color(1f, 0.84f, 0.12f) },
        new() { Id = "trevo_torto", Name = "LOTERIAS TREVO TORTO", Slogan = "aposte: é quase impossível", Sign = new Color(0.31f, 0.12f, 0.47f), Text = new Color(1f, 0.9f, 0.47f) },
        new() { Id = "pao_dormido", Name = "PADARIA PÃO DORMIDO", Slogan = "pão de ontem, preço de hoje", Sign = new Color(0.41f, 0.24f, 0.12f), Text = new Color(1f, 0.93f, 0.78f) },
        new() { Id = "trigo_torto", Name = "PANIFICADORA TRIGO TORTO", Slogan = "sonho, carolina e fofoca", Sign = new Color(0.92f, 0.75f, 0.27f), Text = new Color(0.35f, 0.18f, 0.06f) },
        new() { Id = "extravio", Name = "SUPERMERCADO EXTRAVIO", Slogan = "sua compra pode ou não chegar", Sign = new Color(0.96f, 0.96f, 0.96f), Text = new Color(0.82f, 0.08f, 0.12f) },
        new() { Id = "carretour", Name = "CARRETOUR", Slogan = "o carrinho dá a volta no mercado", Sign = new Color(0.08f, 0.27f, 0.63f), Text = Colors.White },
    };
    private readonly Queue<StoreBrand> _brandQueue = new();
    private readonly List<StandardMaterial3D> _signFaces = new();

    /// <summary>Letreiros brilham mais à noite (chamado pelo SetNightAmount).</summary>
    private void UpdateSignGlow(float night)
    {
        foreach (StandardMaterial3D m in _signFaces)
            m.EmissionEnergyMultiplier = Mathf.Lerp(0.08f, 0.9f, night);
    }

    private StandardMaterial3D _acBody, _acGrille, _storeWindow, _doorDark, _brick, _whiteWall, _policeBlue, _fireRed, _redLight, _blueLight, _hydrant, _phoneGrey;
    private BoxMesh _acBox;

    private void BuildLandmarkResources()
    {
        _acBody = Flat(new Color(0.78f, 0.78f, 0.75f), 0.6f);
        _acGrille = Flat(new Color(0.25f, 0.26f, 0.27f), 0.8f);
        _storeWindow = Flat(new Color(1f, 0.92f, 0.75f), 0.2f);
        _storeWindow.EmissionEnabled = true;
        _storeWindow.Emission = new Color(1f, 0.88f, 0.65f);
        _storeWindow.EmissionEnergyMultiplier = 0.9f;
        _doorDark = Flat(new Color(0.06f, 0.07f, 0.08f), 0.3f);
        _brick = Flat(new Color(0.55f, 0.2f, 0.13f));
        _whiteWall = Flat(new Color(0.86f, 0.86f, 0.83f));
        _policeBlue = Flat(new Color(0.08f, 0.18f, 0.45f), 0.6f);
        _fireRed = Flat(new Color(0.75f, 0.06f, 0.05f), 0.45f);
        _fireRed.Metallic = 0.3f;
        _redLight = Emissive(new Color(1f, 0.08f, 0.05f), 4f);
        _blueLight = Emissive(new Color(0.15f, 0.35f, 1f), 4f);
        _hydrant = Flat(new Color(0.85f, 0.12f, 0.08f), 0.5f);
        _phoneGrey = Flat(new Color(0.2f, 0.21f, 0.23f), 0.6f);
        _acBox = new BoxMesh { Size = Vector3.One };

        foreach (StoreBrand brand in _brands)
        {
            brand.SignMaterial = Flat(brand.Sign, 0.4f);
            brand.SignMaterial.EmissionEnabled = true;
            brand.SignMaterial.Emission = brand.Sign;
            brand.SignMaterial.EmissionEnergyMultiplier = 0.35f;
            brand.AwningMaterial = Flat(brand.Sign.Darkened(0.15f), 0.8f);
            string signPath = $"res://Assets/Textures/Stores/sign_{brand.Id}.png";
            if (ResourceLoader.Exists(signPath))
            {
                var tex = GD.Load<Texture2D>(signPath);
                brand.FaceMaterial = new StandardMaterial3D
                {
                    AlbedoTexture = tex,
                    EmissionEnabled = true,
                    EmissionTexture = tex,
                    EmissionEnergyMultiplier = 0.6f,
                    Roughness = 0.35f,
                    TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmapsAnisotropic,
                };
                _signFaces.Add(brand.FaceMaterial);
            }
        }
    }

    private static StandardMaterial3D Emissive(Color color, float energy)
    {
        StandardMaterial3D m = Flat(color, 0.4f);
        m.EmissionEnabled = true;
        m.Emission = color;
        m.EmissionEnergyMultiplier = energy;
        return m;
    }

    /// <summary>Próxima marca da fila: todas aparecem antes de alguma repetir.</summary>
    private StoreBrand NextBrand()
    {
        if (_brandQueue.Count == 0)
        {
            var shuffled = new List<StoreBrand>(_brands);
            for (int i = shuffled.Count - 1; i > 0; i--)
            {
                int j = _decoRng.RandiRange(0, i);
                (shuffled[i], shuffled[j]) = (shuffled[j], shuffled[i]);
            }
            foreach (StoreBrand b in shuffled)
                _brandQueue.Enqueue(b);
        }
        return _brandQueue.Dequeue();
    }

    /// <summary>Nó com +Z local apontando para "outward" (fachada virada para a rua).</summary>
    private static Node3D FacadeFrame(Node3D parent, Vector3 position, Vector3 outward)
    {
        var frame = new Node3D { Position = position, Rotation = new Vector3(0f, Mathf.Atan2(outward.X, outward.Z), 0f) };
        parent.AddChild(frame);
        return frame;
    }

    // ================================================================ prédios comuns

    private void DecorateBuilding(Node3D parent, Vector3 baseCenter, float width, float depth, float height, Vector3 blockCenter)
    {
        // Fachada principal = lado do lote mais perto da rua.
        Vector3 toLot = baseCenter - blockCenter;
        Vector3 front;
        if (Mathf.Abs(toLot.X) < 0.5f && Mathf.Abs(toLot.Z) < 0.5f)
            front = _decoRng.Randf() < 0.5f ? Vector3.Back : Vector3.Right; // lote único: qualquer lado
        else if (Mathf.Abs(toLot.X) > Mathf.Abs(toLot.Z))
            front = new Vector3(Mathf.Sign(toLot.X), 0f, 0f);
        else
            front = new Vector3(0f, 0f, Mathf.Sign(toLot.Z));

        if (_decoRng.Randf() < StorefrontChance)
            BuildStorefront(parent, baseCenter, width, depth, front, blockCenter);

        if (_decoRng.Randf() < WallAcChance && height > 6f)
            BuildWallAcs(parent, baseCenter, width, depth, height, front);
    }

    private void BuildStorefront(Node3D parent, Vector3 baseCenter, float width, float depth, Vector3 front, Vector3 blockCenter)
    {
        StoreBrand brand = NextBrand();
        float facadeWidth = Mathf.Abs(front.X) > 0.5f ? depth : width;
        float halfDepth = Mathf.Abs(front.X) > 0.5f ? width * 0.5f : depth * 0.5f;
        float signWidth = Mathf.Min(facadeWidth * 0.85f, 9f);

        Node3D f = FacadeFrame(parent, baseCenter + front * halfDepth, front);

        // Letreiro: caixa com moldura escura e a arte da loja (textura 7:1) na frente; toldo, vitrine e porta.
        float signHeight = Mathf.Clamp(signWidth / 7f, 0.7f, 1.0f); // até 1 m: o 1º andar (ar-condicionado) começa em 3,66 m
        float signY = 2.58f + signHeight * 0.5f;
        AddBox(f, new Vector3(0f, signY, 0.16f), new Vector3(signWidth + 0.14f, signHeight + 0.14f, 0.26f), _doorDark, false);
        if (brand.FaceMaterial != null)
        {
            f.AddChild(new MeshInstance3D
            {
                Mesh = new QuadMesh { Size = new Vector2(signWidth, signHeight) },
                Position = new Vector3(0f, signY, 0.295f),
                MaterialOverride = brand.FaceMaterial,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            });
        }
        else
        {
            // Sem a textura: letreiro antigo com texto 3D.
            AddBox(f, new Vector3(0f, signY, 0.2f), new Vector3(signWidth, signHeight, 0.2f), brand.SignMaterial, false);
            f.AddChild(new Label3D
            {
                Text = brand.Name,
                FontSize = 96,
                PixelSize = Mathf.Min(0.011f, signWidth / (brand.Name.Length * 62f)),
                Modulate = brand.Text,
                OutlineSize = 0,
                Position = new Vector3(0f, signY, 0.31f),
            });
        }
        // Toldo caindo para a calçada (a borda da frente fica mais baixa e não esconde o letreiro).
        AddBox(f, new Vector3(0f, 2.36f, 0.62f), new Vector3(signWidth * 0.95f, 0.07f, 1.2f), brand.AwningMaterial, false)
            .Rotation = new Vector3(0.3f, 0f, 0f);
        AddBox(f, new Vector3(-signWidth * 0.15f, 1.3f, 0.04f), new Vector3(signWidth * 0.55f, 1.8f, 0.06f), _storeWindow, false);
        AddBox(f, new Vector3(signWidth * 0.32f, 1.1f, 0.05f), new Vector3(1.1f, 2.1f, 0.08f), _doorDark, false);

        Vector3 along = new(front.Z, 0f, -front.X);
        float alongOffset = (baseCenter - blockCenter).Dot(along);
        Vector3 curb = blockCenter + front * (BlockSize * 0.5f) + along * alongOffset;
        ForbidParking(curb, along, Mathf.Max(2.5f, signWidth * 0.3f));
    }

    /// <summary>Aparelhos de ar-condicionado na parede, logo abaixo de janelas (alinhados à grade do shader).</summary>
    private void BuildWallAcs(Node3D parent, Vector3 baseCenter, float width, float depth, float height, Vector3 front)
    {
        const float floorHeight = 3.2f;
        const float windowSpacing = 2.6f; // igual a building.gdshader
        int floors = Mathf.FloorToInt((height - 0.4f) / floorHeight);
        int count = _decoRng.RandiRange(2, 7);

        Vector3[] sides = { front, -front, new Vector3(front.Z, 0f, -front.X), new Vector3(-front.Z, 0f, front.X) };
        for (int n = 0; n < count; n++)
        {
            // Mais comuns nas laterais e no fundo; na frente só acima da loja.
            Vector3 side = sides[_decoRng.RandiRange(0, 3)];
            bool alongX = Mathf.Abs(side.Z) > 0.5f; // fachada paralela ao eixo X
            float halfFace = alongX ? width * 0.5f : depth * 0.5f;
            float halfOut = alongX ? depth * 0.5f : width * 0.5f;
            int floor = _decoRng.RandiRange(1, Mathf.Max(1, Mathf.Min(floors - 1, 9)));

            // Coordenada "u" do shader: x para fachadas em X, z para fachadas em Z.
            float faceCenterU = alongX ? baseCenter.X : baseCenter.Z;
            int firstWindow = Mathf.CeilToInt((faceCenterU - halfFace + 0.8f) / windowSpacing - 0.5f);
            int lastWindow = Mathf.FloorToInt((faceCenterU + halfFace - 0.8f) / windowSpacing - 0.5f);
            if (lastWindow < firstWindow)
                continue;
            float u = (_decoRng.RandiRange(firstWindow, lastWindow) + 0.5f) * windowSpacing;
            float y = baseCenter.Y + floor * floorHeight + 0.62f;

            Vector3 pos = alongX
                ? new Vector3(u, y, baseCenter.Z + side.Z * (halfOut + 0.24f))
                : new Vector3(baseCenter.X + side.X * (halfOut + 0.24f), y, u);
            Node3D ac = FacadeFrame(parent, pos, side);
            ac.AddChild(new MeshInstance3D { Mesh = _acBox, Scale = new Vector3(0.85f, 0.5f, 0.45f), MaterialOverride = _acBody });
            ac.AddChild(new MeshInstance3D { Mesh = _acBox, Scale = new Vector3(0.7f, 0.32f, 0.02f), Position = new Vector3(0f, 0f, 0.23f), MaterialOverride = _acGrille });
            ac.AddChild(new MeshInstance3D { Mesh = _acBox, Scale = new Vector3(0.75f, 0.04f, 0.4f), Position = new Vector3(0f, -0.28f, -0.02f), MaterialOverride = _darkMetal }); // suporte
        }
    }

    // ================================================================ orelhões e hidrantes

    private void BuildPhoneBooths()
    {
        float edge = BlockSize * 0.5f - 0.9f;
        var booths = new Node3D { Name = "PhoneBooths" };
        _staticRoot.AddChild(booths);
        for (int bx = -Radius; bx <= Radius; bx++)
        {
            for (int bz = -Radius; bz <= Radius; bz++)
            {
                if (bx == 0 && bz == 0)
                    continue;
                Vector3 c = BlockCenter(bx, bz) + new Vector3(0f, CurbHeight, 0f);
                // Lado -X (sem postes nem placas) e, às vezes, lado -Z.
                if (_decoRng.Randf() < PhoneBoothChance)
                    AddPhoneBooth(booths, c + new Vector3(-edge, 0f, BlockSize * 0.25f), Vector3.Left);
                if (_decoRng.Randf() < PhoneBoothChance * 0.5f)
                    AddPhoneBooth(booths, c + new Vector3(BlockSize * 0.25f, 0f, -edge), Vector3.Forward);
            }
        }
        // Um em frente ao portão do depósito, para quem acabou de chegar.
        AddPhoneBooth(booths, new Vector3(8f, 0f, BlockSize * 0.5f - 1.4f), Vector3.Back);
    }

    private void AddPhoneBooth(Node3D parent, Vector3 position, Vector3 towardRoad, PhoneBrand? forced = null)
    {
        RegisterObstacle(position);
        // Operadora e cor pela posição (não gasta sorteio, então a cidade continua igual para todos).
        int hash = Mathf.Abs(Mathf.RoundToInt(position.X * 7.3f) * 31 + Mathf.RoundToInt(position.Z * 3.7f) * 17);
        (PhoneBrand brand, string color) = PhoneBooth.Pick(AssetProps.Hash(position, 66));
        if (forced == PhoneBrand.Telefobica)
            (brand, color) = (PhoneBrand.Telefobica, "lime");
        else if (forced != null && brand == PhoneBrand.Telefobica)
            (brand, color) = hash % 2 == 0 ? (PhoneBrand.Tchau, "cyan") : (PhoneBrand.Morto, "purple");
        var booth = new PhoneBooth
        {
            Brand = brand,
            ShellColor = color,
            Position = position,
            Rotation = new Vector3(0f, Mathf.Atan2(towardRoad.X, towardRoad.Z), 0f),
            PhoneMaterial = _phoneGrey,
            PoleMaterial = _darkMetal,
        };
        parent.AddChild(booth);
    }

    private void BuildHydrants()
    {
        var root = new Node3D { Name = "Hydrants" };
        _staticRoot.AddChild(root);
        float corner = BlockSize * 0.5f - 1.0f;
        for (int bx = -Radius; bx <= Radius; bx++)
        {
            for (int bz = -Radius; bz <= Radius; bz++)
            {
                if ((bx == 0 && bz == 0) || _decoRng.Randf() > 0.35f)
                    continue;
                Vector3 c = BlockCenter(bx, bz) + new Vector3(0f, CurbHeight, 0f);
                AddHydrant(root, c + new Vector3(corner, 0f, -corner + 2.5f));
            }
        }
    }

    private void AddHydrant(Node3D parent, Vector3 position)
    {
        RegisterObstacle(position);
        var hydrant = new StaticBody3D { Position = position, CollisionLayer = 1, CollisionMask = 0 };
        hydrant.AddChild(new CollisionShape3D { Shape = new CylinderShape3D { Radius = 0.18f, Height = 0.8f }, Position = new Vector3(0f, 0.4f, 0f) });
        AddCylinder(hydrant, new Vector3(0f, 0.32f, 0f), 0.14f, 0.64f, _hydrant);
        AddCylinder(hydrant, new Vector3(0f, 0.7f, 0f), 0.1f, 0.14f, _hydrant);
        AddCylinder(hydrant, new Vector3(0f, 0.45f, 0f), 0.06f, 0.42f, _hydrant).Rotation = new Vector3(0f, 0f, Mathf.Pi * 0.5f);
        parent.AddChild(hydrant);
    }

    // ================================================================ Corpo de Bombeiros

    private void BuildFireStation(Node3D block, Vector3 center)
    {
        float inner = BlockSize - SidewalkWidth * 2f;
        float half = inner * 0.5f;
        // Frente dos bombeiros = +Z (FacadeFrame com Vector3.Back): saída dos caminhões.
        ForbidParking(center + new Vector3(0f, 0f, BlockSize * 0.5f), Vector3.Right, 13f, "BOMBEIROS");
        // Frame local: +Z = rua da frente.
        Node3D f = FacadeFrame(block, center + new Vector3(0f, CurbHeight, 0f), Vector3.Back);
        f.Name = "FireStation";
        MarkPoi(f, "B", new Color(1f, 0.25f, 0.2f));

        // Pátio de concreto.
        AddBox(f, new Vector3(0f, 0.03f, 0f), new Vector3(inner, 0.06f, inner), _concrete, false);

        // Garagem (fundo do terreno) com 3 portões.
        const float hallW = 22f, hallH = 8f, hallD = 12f;
        float hallZ = -half + hallD * 0.5f;
        AddBox(f, new Vector3(0f, hallH * 0.5f, hallZ), new Vector3(hallW, hallH, hallD), _brick);
        AddBox(f, new Vector3(0f, hallH + 0.25f, hallZ), new Vector3(hallW + 0.4f, 0.5f, hallD + 0.4f), _whiteWall, false);
        AddBox(f, new Vector3(0f, 5.2f, hallZ + hallD * 0.5f + 0.03f), new Vector3(hallW, 0.35f, 0.06f), _whiteWall, false);
        for (int i = -1; i <= 1; i++)
        {
            float x = i * 6.8f;
            AddBox(f, new Vector3(x, 2.35f, hallZ + hallD * 0.5f + 0.04f), new Vector3(5.2f, 4.7f, 0.08f), _whiteWall, false);
            AddBox(f, new Vector3(x, 2.25f, hallZ + hallD * 0.5f + 0.09f), new Vector3(4.6f, 4.4f, 0.06f), _fireRed, false);
            for (float y = 0.6f; y < 4.4f; y += 0.55f)
                AddBox(f, new Vector3(x, y, hallZ + hallD * 0.5f + 0.13f), new Vector3(4.6f, 0.04f, 0.02f), _doorDark, false);
        }
        f.AddChild(new Label3D
        {
            Text = "CORPO DE BOMBEIROS",
            FontSize = 96,
            PixelSize = 0.014f,
            Modulate = Colors.White,
            OutlineSize = 12,
            OutlineModulate = new Color(0.4f, 0.03f, 0.02f),
            Position = new Vector3(0f, 6.6f, hallZ + hallD * 0.5f + 0.1f),
        });
        f.AddChild(new Label3D
        {
            Text = "EMERGÊNCIA 193",
            FontSize = 64,
            PixelSize = 0.01f,
            Modulate = new Color(1f, 0.85f, 0.2f),
            OutlineSize = 8,
            Position = new Vector3(-8.2f, 5.6f, hallZ + hallD * 0.5f + 0.1f),
        });

        // Torre de treino.
        AddBox(f, new Vector3(hallW * 0.5f + 2.6f, 8f, hallZ - 1f), new Vector3(4.2f, 16f, 4.2f), _brick);
        for (float y = 3f; y < 15f; y += 3.2f)
            AddBox(f, new Vector3(hallW * 0.5f + 2.6f, y, hallZ + 1.12f), new Vector3(1.4f, 1.6f, 0.06f), _doorDark, false);
        AddBox(f, new Vector3(hallW * 0.5f + 2.6f, 16.3f, hallZ - 1f), new Vector3(4.6f, 0.4f, 4.6f), _whiteWall, false);

        // Dois caminhões no pátio, de frente para a rua.
        BuildFireTruck(f, new Vector3(-6.8f, 0f, 3.5f), true);
        BuildFireTruck(f, new Vector3(6.8f, 0f, 4.5f), false);

        // Faixa amarela de "não estacione" no pátio.
        AddBox(f, new Vector3(0f, 0.07f, half - 1.2f), new Vector3(inner - 2f, 0.02f, 0.3f), Emissive(new Color(0.9f, 0.75f, 0.1f), 0.1f), false);
    }

    private void BuildFireTruck(Node3D parent, Vector3 position, bool lightsOn)
    {
        // Frente do caminhão = +Z local (para a rua).
        var truck = new StaticBody3D { Position = position, CollisionLayer = 1, CollisionMask = 0 };
        truck.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(2.5f, 3f, 8.4f) }, Position = new Vector3(0f, 1.6f, 0f) });
        parent.AddChild(truck);

        AddBox(truck, new Vector3(0f, 1.6f, 2.95f), new Vector3(2.5f, 2.3f, 2.3f), _fireRed, false);           // cabine
        AddBox(truck, new Vector3(0f, 2.15f, 4.12f), new Vector3(2.2f, 0.9f, 0.05f), _glass, false);          // para-brisa
        AddBox(truck, new Vector3(0f, 1.65f, -1.25f), new Vector3(2.5f, 2.4f, 6.0f), _fireRed, false);         // baú
        AddBox(truck, new Vector3(0f, 1.1f, 0.5f), new Vector3(2.52f, 0.22f, 8.2f), _whiteWall, false);        // faixa branca
        AddBox(truck, new Vector3(0f, 0.55f, 0.6f), new Vector3(2.3f, 0.4f, 8.2f), _darkMetal, false);         // chassi
        AddBox(truck, new Vector3(0f, 0.6f, 4.2f), new Vector3(2.6f, 0.3f, 0.2f), _darkMetal, false);          // para-choque
        // Escada no teto.
        foreach (float x in new[] { -0.45f, 0.45f })
            AddBox(truck, new Vector3(x, 3.0f, -1.0f), new Vector3(0.08f, 0.08f, 6.6f), _whiteWall, false);
        for (float z = -4.1f; z <= 2.1f; z += 0.45f)
            AddBox(truck, new Vector3(0f, 3.0f, z), new Vector3(0.9f, 0.05f, 0.05f), _whiteWall, false);
        // Rodas.
        foreach (float x in new[] { -1.15f, 1.15f })
            foreach (float z in new[] { 2.7f, -1.6f, -3.0f })
            {
                MeshInstance3D wheel = AddCylinder(truck, new Vector3(x, 0.5f, z), 0.5f, 0.4f, _darkMetal);
                wheel.Rotation = new Vector3(0f, 0f, Mathf.Pi * 0.5f);
            }
        // Giroflex.
        StandardMaterial3D beacon = Emissive(new Color(1f, 0.1f, 0.05f), lightsOn ? 4f : 0.4f);
        AddBox(truck, new Vector3(0f, 2.85f, 3.2f), new Vector3(1.8f, 0.18f, 0.35f), beacon, false);
        if (lightsOn)
        {
            var light = new OmniLight3D { Position = new Vector3(0f, 3.3f, 3.2f), LightColor = new Color(1f, 0.1f, 0.05f), LightEnergy = 3f, OmniRange = 12f };
            truck.AddChild(light);
            Blink(light, beacon, 3f, 0.35f);
        }
    }

    // ================================================================ Delegacia

    private void BuildPoliceStation(Node3D block, Vector3 center)
    {
        float inner = BlockSize - SidewalkWidth * 2f;
        float half = inner * 0.5f;
        // Frente da delegacia = -Z (FacadeFrame com Vector3.Forward): o trecho proibido é na rua da frente.
        ForbidParking(center + new Vector3(0f, 0f, -BlockSize * 0.5f), Vector3.Right, 4.5f, "POLÍCIA");
        Node3D f = FacadeFrame(block, center + new Vector3(0f, CurbHeight, 0f), Vector3.Forward);
        f.Name = "PoliceStation";
        MarkPoi(f, "P", new Color(0.3f, 0.5f, 1f));

        AddBox(f, new Vector3(0f, 0.03f, 0f), new Vector3(inner, 0.06f, inner), _darkMetal, false); // estacionamento

        // Prédio de 2 andares no fundo.
        const float w = 24f, h = 7.2f, d = 11f;
        float z = -half + d * 0.5f;
        float frontZ = z + d * 0.5f;
        AddBox(f, new Vector3(0f, h * 0.5f, z), new Vector3(w, h, d), _whiteWall);
        AddBox(f, new Vector3(0f, 1.1f, frontZ + 0.03f), new Vector3(w, 0.5f, 0.06f), _policeBlue, false);
        AddBox(f, new Vector3(0f, h + 0.2f, z), new Vector3(w + 0.4f, 0.4f, d + 0.4f), _policeBlue, false);
        f.AddChild(new Label3D
        {
            Text = "POLÍCIA",
            FontSize = 40,
            PixelSize = 0.005f,
            Modulate = Colors.White,
            OutlineSize = 0,
            Position = new Vector3(0f, 1.1f, frontZ + 0.08f),
        });
        // Janelas dos dois andares.
        for (int floor = 0; floor < 2; floor++)
            for (float x = -10f; x <= 10f; x += 2.5f)
            {
                if (floor == 0 && Mathf.Abs(x) < 2f)
                    continue; // espaço da porta
                AddBox(f, new Vector3(x, 2.2f + floor * 3.2f, frontZ + 0.04f), new Vector3(1.4f, 1.3f, 0.05f), floor == 0 ? _storeWindow : _glass, false);
            }
        // Entrada com marquise, colunas, degraus e porta de vidro.
        AddBox(f, new Vector3(0f, 3.4f, frontZ + 1.4f), new Vector3(5.5f, 0.25f, 2.8f), _policeBlue, false);
        foreach (float x in new[] { -2.4f, 2.4f })
            AddBox(f, new Vector3(x, 1.7f, frontZ + 2.5f), new Vector3(0.3f, 3.4f, 0.3f), _whiteWall, false);
        AddBox(f, new Vector3(0f, 0.12f, frontZ + 1.6f), new Vector3(5f, 0.24f, 3f), _concrete);
        AddBox(f, new Vector3(0f, 1.3f, frontZ + 0.05f), new Vector3(2.2f, 2.5f, 0.06f), _glass, false);
        f.AddChild(new Label3D
        {
            Text = "DELEGACIA DE POLÍCIA",
            FontSize = 96,
            PixelSize = 0.013f,
            Modulate = Colors.White,
            OutlineSize = 10,
            OutlineModulate = new Color(0.03f, 0.08f, 0.25f),
            Position = new Vector3(0f, 6.6f, frontZ + 0.08f),
        });
        f.AddChild(new Label3D
        {
            Text = "PLANTÃO 24H · 190",
            FontSize = 56,
            PixelSize = 0.01f,
            Modulate = Colors.White,
            OutlineSize = 0,
            Position = new Vector3(0f, 3.4f, frontZ + 2.82f),
        });
        // Luminária azul clássica na porta.
        StandardMaterial3D lamp = Emissive(new Color(0.2f, 0.4f, 1f), 3f);
        AddBox(f, new Vector3(0f, 2.9f, frontZ + 2.75f), new Vector3(0.35f, 0.35f, 0.35f), lamp, false);

        for (int i = 0; i < 2; i++)
        {
            Color skin = i == 0 ? new Color(0.68f, 0.47f, 0.33f) : new Color(0.42f, 0.28f, 0.2f);
            CharacterModel officer = CharacterModel.Create(
                new Color(0.08f, 0.18f, 0.48f),
                new Color(0.06f, 0.07f, 0.1f),
                skin,
                new Color(0.05f, 0.12f, 0.32f),
                i);
            officer.Name = $"Policial{i + 1}";
            officer.Position = new Vector3(i == 0 ? -3.4f : 3.4f, 0f, frontZ + 4.2f);
            officer.Rotation = new Vector3(0f, Mathf.Pi, 0f);
            officer.Scale = Vector3.One * 0.9f;
            AddBox(officer.Head, new Vector3(0f, 0.12f, 0f), new Vector3(0.32f, 0.1f, 0.3f), _policeBlue, false);
            f.AddChild(officer);
        }

        // Mastros com bandeiras genéricas.
        for (int i = 0; i < 3; i++)
        {
            float x = -7f + i * 1.6f;
            AddCylinder(f, new Vector3(x, 4f, frontZ + 4.5f), 0.05f, 8f, _darkMetal);
            Color flag = i switch { 0 => new Color(0.05f, 0.55f, 0.25f), 1 => new Color(0.95f, 0.8f, 0.1f), _ => new Color(0.1f, 0.25f, 0.6f) };
            AddBox(f, new Vector3(x + 0.6f, 7.4f, frontZ + 4.5f), new Vector3(1.1f, 0.7f, 0.03f), Flat(flag, 0.8f), false);
        }

        // Viaturas estacionadas de frente para a rua (+Z local).
        BuildPoliceCar(f, new Vector3(4.5f, 0f, half - 5f), true);
        BuildPoliceCar(f, new Vector3(8f, 0f, half - 5f), false);
        BuildPoliceCar(f, new Vector3(-8.5f, 0f, half - 6f), false);
    }

    private void BuildPoliceCar(Node3D parent, Vector3 position, bool lightsOn)
    {
        // CarModels tem a frente em -Z: gira 180° para a frente ficar para a rua.
        var car = new StaticBody3D { Position = position, Rotation = new Vector3(0f, Mathf.Pi, 0f), CollisionLayer = 1, CollisionMask = 0 };
        parent.AddChild(car);
        StandardMaterial3D red = Emissive(new Color(1f, 0.08f, 0.05f), lightsOn ? 4f : 0.3f);
        StandardMaterial3D blue = Emissive(new Color(0.15f, 0.35f, 1f), lightsOn ? 4f : 0.3f);
        // Viatura da Quaternius (Realistic Car Pack, frente em +Z: gira de volta).
        bool model = AssetProps.Spawn(car, "res://Assets/Quaternius/Vehicles/Cop.fbx", Vector3.Zero, Mathf.Pi, 1.05f) != null;
        if (model)
        {
            car.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(1.85f, 1.1f, 3.9f) }, Position = new Vector3(0f, 0.65f, 0f) });
            // O modelo já tem a barra de luz (giroflex): só as luzes piscando (sem desenhar outra por cima).
        }
        else
        {
            Vector3 size = CarModels.Build(car, CarType.Sedan, new Color(0.92f, 0.92f, 0.92f));
            car.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(size.X, size.Y - 0.2f, size.Z) }, Position = new Vector3(0f, size.Y * 0.5f + 0.1f, 0f) });
            // Portas pretas e giroflex vermelho/azul.
            foreach (float x in new[] { -0.91f, 0.91f })
                AddBox(car, new Vector3(x, 0.62f, 0.1f), new Vector3(0.03f, 0.42f, 1.8f), _doorDark, false);
            AddBox(car, new Vector3(-0.35f, 1.45f, 0.15f), new Vector3(0.6f, 0.14f, 0.3f), red, false);
            AddBox(car, new Vector3(0.35f, 1.45f, 0.15f), new Vector3(0.6f, 0.14f, 0.3f), blue, false);
        }
        car.AddChild(new Label3D
        {
            Text = "POLÍCIA",
            FontSize = 48,
            PixelSize = 0.006f,
            Modulate = new Color(0.05f, 0.1f, 0.3f),
            OutlineSize = 0,
            Position = new Vector3(model ? 0.95f : 0.93f, model ? 0.62f : 0.68f, 0.1f),
            RotationDegrees = new Vector3(0f, 90f, 0f),
        });
        if (lightsOn)
        {
            var redLight = new OmniLight3D { Position = new Vector3(-0.4f, 1.8f, 0.15f), LightColor = new Color(1f, 0.1f, 0.05f), LightEnergy = 2.5f, OmniRange = 9f };
            var blueLight = new OmniLight3D { Position = new Vector3(0.4f, 1.8f, 0.15f), LightColor = new Color(0.2f, 0.4f, 1f), LightEnergy = 0f, OmniRange = 9f };
            car.AddChild(redLight);
            car.AddChild(blueLight);
            Blink(redLight, red, 2.5f, 0.4f);
            Blink(blueLight, blue, 2.5f, 0.4f, startOn: false);
        }
    }

    // ================================================================ utilitários

    /// <summary>Pisca luz + material emissivo em loop (giroflex).</summary>
    private void Blink(OmniLight3D light, StandardMaterial3D material, float energy, float period, bool startOn = true)
    {
        float on = startOn ? energy : 0f, off = startOn ? 0f : energy;
        float matOn = startOn ? 4f : 0.3f, matOff = startOn ? 0.3f : 4f;
        Tween tween = CreateTween().SetLoops();
        tween.TweenCallback(Callable.From(() => { light.LightEnergy = on; material.EmissionEnergyMultiplier = matOn; }));
        tween.TweenInterval(period);
        tween.TweenCallback(Callable.From(() => { light.LightEnergy = off; material.EmissionEnergyMultiplier = matOff; }));
        tween.TweenInterval(period);
    }

    private static void MarkPoi(Node3D node, string letter, Color color)
    {
        node.AddToGroup(MinimapView.MarkerGroup);
        node.SetMeta("map_kind", "poi");
        node.SetMeta("map_label", letter);
        node.SetMeta("map_color", color);
    }
}
