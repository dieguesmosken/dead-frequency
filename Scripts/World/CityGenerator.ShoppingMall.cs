using Godot;

namespace DeadFrequency;

public partial class CityGenerator
{
    [ExportGroup("Shopping")]
    [Export] public Vector2I ShoppingMallBlock { get; set; } = new(0, 2);

    private const float MallUpperFloorY = 3.6f;

    private void BuildShoppingMall(Node3D block, Vector3 center)
    {
        var mall = new Node3D
        {
            Name = "ShoppingMarconi",
            Position = center + new Vector3(0f, CurbHeight, 0f),
        };
        block.AddChild(mall);

        StandardMaterial3D facade = Flat(new Color(0.3f, 0.34f, 0.36f), 0.8f);
        StandardMaterial3D trim = Flat(new Color(0.75f, 0.63f, 0.32f), 0.5f);
        StandardMaterial3D glass = Flat(new Color(0.16f, 0.34f, 0.4f), 0.25f);
        StandardMaterial3D floor = Flat(new Color(0.54f, 0.52f, 0.47f), 0.7f);
        StandardMaterial3D tile = Flat(new Color(0.68f, 0.65f, 0.58f), 0.55f);
        StandardMaterial3D metal = Flat(new Color(0.17f, 0.2f, 0.22f), 0.45f);

        AddBox(mall, new Vector3(0f, 0.05f, 0f), new Vector3(24.5f, 0.1f, 21.8f), tile);

        const float sideX = 12.25f;
        const float frontZ = 11.05f;
        const float wallHeight = 7.2f;
        AddBox(mall, new Vector3(-sideX, wallHeight * 0.5f, 0f), new Vector3(0.3f, wallHeight, 22.4f), facade);
        AddBox(mall, new Vector3(sideX, wallHeight * 0.5f, 0f), new Vector3(0.3f, wallHeight, 22.4f), facade);
        AddBox(mall, new Vector3(0f, wallHeight * 0.5f, -frontZ), new Vector3(24.5f, wallHeight, 0.3f), facade);
        AddBox(mall, new Vector3(-7.6f, wallHeight * 0.5f, frontZ), new Vector3(9.3f, wallHeight, 0.3f), facade);
        AddBox(mall, new Vector3(7.6f, wallHeight * 0.5f, frontZ), new Vector3(9.3f, wallHeight, 0.3f), facade);
        AddBox(mall, new Vector3(0f, 5.4f, frontZ), new Vector3(6.1f, 3.6f, 0.3f), facade);

        AddBox(mall, new Vector3(0f, 7.35f, 0f), new Vector3(24.8f, 0.3f, 22.7f), facade);
        BuildMallUpperFloor(mall, floor);

        foreach (float x in new[] { -10f, -5f, 5f, 10f })
        {
            foreach (float y in new[] { 1.8f, 5.3f })
                AddBox(mall, new Vector3(x, y, frontZ + 0.17f), new Vector3(2.8f, 1.8f, 0.06f), glass, false);
        }

        AddBox(mall, new Vector3(0f, 6.65f, frontZ + 0.22f), new Vector3(9.2f, 1.3f, 0.25f), trim, false);
        mall.AddChild(new Label3D
        {
            Text = "SHOPPING MARCONI",
            FontSize = 96,
            PixelSize = 0.009f,
            Modulate = new Color(0.08f, 0.11f, 0.13f),
            OutlineSize = 8,
            OutlineModulate = new Color(0.9f, 0.78f, 0.43f),
            Position = new Vector3(0f, 6.62f, frontZ + 0.36f),
        });

        AddBox(mall, new Vector3(-3.05f, 1.8f, frontZ), new Vector3(0.2f, 3.6f, 0.45f), trim);
        AddBox(mall, new Vector3(3.05f, 1.8f, frontZ), new Vector3(0.2f, 3.6f, 0.45f), trim);
        BuildMallStairs(mall, metal, trim);

        BuildMallShop(mall, "CASAS BAIXA", ShopStock.Ze, new Color(0.1f, 0.25f, 0.62f), new Vector3(-10f, 0f, 6.5f), Mathf.Pi * 0.5f);
        BuildMallShop(mall, "PÃO DE SAL", ShopStock.Market, new Color(0.12f, 0.46f, 0.22f), new Vector3(10f, 0f, 6.5f), -Mathf.Pi * 0.5f);
        BuildMallShop(mall, "RABIB'S", ShopStock.Churrasco, new Color(0.8f, 0.12f, 0.1f), new Vector3(-7f, 0f, -8.8f), 0f);
        // v0.9.9: a Lã de Aço 1001 Utilidades ficou com o ponto das Lojas Marcianas (o Zé já vende o mesmo).
        BuildMallShop(mall, "LÃ DE AÇO 1001 UTILIDADES", ShopStock.Utilidades, new Color(0.86f, 0.62f, 0.08f), new Vector3(7f, 0f, -8.8f), 0f);
        BuildMallShop(mall, "MAGAZINE LUZIA", ShopStock.Ze, new Color(0.04f, 0.42f, 0.82f), new Vector3(-10f, MallUpperFloorY, -1.6f), Mathf.Pi * 0.5f);
        BuildMallShop(mall, "DROGA ARRAIA", ShopStock.Ze, new Color(0.05f, 0.26f, 0.52f), new Vector3(10f, MallUpperFloorY, -1.6f), -Mathf.Pi * 0.5f);
        BuildMallShop(mall, "AÇAÍ ATACADISTA", ShopStock.Market, new Color(0.94f, 0.47f, 0.08f), new Vector3(-7f, MallUpperFloorY, -8.8f), 0f);
        BuildMallShop(mall, "BOBO'S", ShopStock.Churrasco, new Color(0.9f, 0.12f, 0.15f), new Vector3(7f, MallUpperFloorY, -8.8f), 0f);

        foreach (float y in new[] { 3.1f, 6.8f })
            mall.AddChild(new OmniLight3D
            {
                Position = new Vector3(0f, y, 0f),
                LightColor = new Color(1f, 0.83f, 0.59f),
                LightEnergy = 1.3f,
                OmniRange = 14f,
                ShadowEnabled = false,
            });

        ForbidParking(center + new Vector3(0f, 0f, BlockSize * 0.5f), Vector3.Right, 9f, "SHOPPING");
    }

    private void BuildMallUpperFloor(Node3D mall, Material floor)
    {
        const float slabY = 3.48f;
        const float slabThickness = 0.24f;
        AddBox(mall, new Vector3(-6.95f, slabY, 0f), new Vector3(11f, slabThickness, 22.1f), floor);
        AddBox(mall, new Vector3(6.95f, slabY, 0f), new Vector3(11f, slabThickness, 22.1f), floor);
        AddBox(mall, new Vector3(0f, slabY, 7.75f), new Vector3(2.9f, slabThickness, 6.6f), floor);
        AddBox(mall, new Vector3(0f, slabY, -7.75f), new Vector3(2.9f, slabThickness, 6.6f), floor);
    }

    private void BuildMallStairs(Node3D mall, Material metal, Material stepMaterial)
    {
        const float rise = 3.5f;
        const float run = 8f;
        const float width = 2.6f;
        const int steps = 14;
        float angle = Mathf.Atan2(rise, run);
        float length = Mathf.Sqrt(rise * rise + run * run);

        var ramp = new StaticBody3D
        {
            Name = "MallStairRamp",
            Position = new Vector3(0f, rise * 0.5f, 0f),
            Rotation = new Vector3(-angle, 0f, 0f),
            CollisionLayer = 1,
            CollisionMask = 0,
        };
        ramp.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(width, 0.16f, length) } });
        mall.AddChild(ramp);

        var railings = new Node3D
        {
            Position = new Vector3(0f, rise * 0.5f, 0f),
            Rotation = new Vector3(-angle, 0f, 0f),
        };
        mall.AddChild(railings);
        foreach (float side in new[] { -1f, 1f })
        {
            AddBox(railings, new Vector3(side * (width * 0.5f + 0.12f), 0.55f, 0f), new Vector3(0.12f, 1.1f, length), metal, false);
            AddBox(railings, new Vector3(side * (width * 0.5f + 0.12f), 1.12f, 0f), new Vector3(0.16f, 0.08f, length), stepMaterial, false);
        }

        float stepHeight = rise / steps;
        float stepDepth = run / steps;
        for (int i = 0; i < steps; i++)
        {
            float top = (i + 1) * stepHeight;
            float z = -run * 0.5f + (i + 0.5f) * stepDepth;
            AddBox(mall, new Vector3(0f, top - stepHeight * 0.5f, z), new Vector3(width, stepHeight, stepDepth + 0.02f), stepMaterial, false);
        }

        AddBox(mall, new Vector3(0f, MallUpperFloorY - 0.05f, run * 0.5f + 0.2f), new Vector3(width, 0.12f, 0.6f), stepMaterial);
        AddBox(mall, new Vector3(-1.5f, MallUpperFloorY + 0.55f, 0f), new Vector3(0.12f, 1.1f, 8.6f), metal);
        AddBox(mall, new Vector3(1.5f, MallUpperFloorY + 0.55f, 0f), new Vector3(0.12f, 1.1f, 8.6f), metal);
    }

    private void BuildMallShop(Node3D mall, string name, ShopStock stock, Color accent, Vector3 position, float rotationY)
    {
        var shop = new Node3D
        {
            Name = name.Replace(" ", ""),
            Position = position,
            Rotation = new Vector3(0f, rotationY, 0f),
        };
        mall.AddChild(shop);

        StandardMaterial3D wall = Flat(new Color(0.78f, 0.75f, 0.68f), 0.85f);
        StandardMaterial3D counter = Flat(new Color(0.35f, 0.28f, 0.2f), 0.7f);
        StandardMaterial3D goods = Flat(accent, 0.55f);

        AddBox(shop, new Vector3(0f, 1.65f, -2.1f), new Vector3(5.8f, 3.3f, 0.2f), wall);
        foreach (float side in new[] { -1f, 1f })
            AddBox(shop, new Vector3(side * 2.8f, 1.65f, 0f), new Vector3(0.2f, 3.3f, 4.2f), wall);
        AddBox(shop, new Vector3(0f, 3f, -1.94f), new Vector3(5.5f, 0.72f, 0.16f), goods, false);
        shop.AddChild(new Label3D
        {
            Text = name,
            FontSize = 48,
            // Nome comprido (ex.: LÃ DE AÇO 1001 UTILIDADES) encolhe para caber na faixa de 5,5 m.
            PixelSize = 0.006f * Mathf.Min(1f, 17f / Mathf.Max(1, name.Length)),
            Modulate = Colors.White,
            OutlineSize = 6,
            OutlineModulate = new Color(0.08f, 0.08f, 0.08f),
            Position = new Vector3(0f, 3f, -1.84f),
        });

        AddBox(shop, new Vector3(0f, 0.55f, 0.85f), new Vector3(4.2f, 1.1f, 0.8f), counter);
        AddBox(shop, new Vector3(0f, 1.13f, 0.85f), new Vector3(4.4f, 0.08f, 0.9f), wall, false);
        foreach (int i in new[] { -1, 0, 1 })
            AddBox(shop, new Vector3(i * 0.85f, 1.35f, 0.1f), new Vector3(0.48f, 0.35f + (i == 0 ? 0.15f : 0f), 0.42f), goods, false);

        ActionArea.Create(shop, new Vector3(0f, 1.05f, 2.35f), new Vector3(4.4f, 2.1f, 0.7f),
            _ => $"[E] Comprar em {name}",
            _ => ShopScreen.Instance?.OpenFor(name, stock),
            player => ShopScreen.Instance != null && !player.IsHolding);
    }
}
