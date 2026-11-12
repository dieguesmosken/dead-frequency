// Scripts/World/CityGenerator.Props.cs
// Decoração com modelos prontos CC0 (KayKit, de Kay Lousberg): bancos e caçambas nas calçadas da cidade;
// caixa d'água da cooperativa, cemitério ao lado da capela, abóboras e árvores secas no rancho.
// As escolhas usam AssetProps.Hash(posição), então não mexem nos sorteios da cidade
// (endereços e layout continuam iguais para todos no online).
using Godot;

namespace DeadFrequency;

public partial class CityGenerator
{
    // ------------------------------------------------------------------ cidade

    private void BuildStreetProps()
    {
        if (_useLogicalBlockPlans)
            return; // Bancos e caçambas acompanham o chunk.

        var root = new Node3D { Name = "StreetProps" };
        _staticRoot.AddChild(root);
        float edge = BlockSize * 0.5f - 1.0f;
        for (int bx = -Radius; bx <= Radius; bx++)
        {
            for (int bz = -Radius; bz <= Radius; bz++)
            {
                if (bx == 0 && bz == 0)
                    continue;
                Vector3 c = BlockCenter(bx, bz) + new Vector3(0f, CurbHeight, 0f);
                int h = AssetProps.Hash(c, 11);
                // Banco virado para a avenida (+Z).
                if (h % 100 < 55)
                {
                    RegisterObstacle(c + new Vector3(BlockSize * 0.08f - 0.7f, 0f, edge));
                    RegisterObstacle(c + new Vector3(BlockSize * 0.08f + 0.7f, 0f, edge));
                    AssetProps.Spawn(root, AssetProps.City + "bench.gltf", c + new Vector3(BlockSize * 0.08f, 0f, edge), 0f, 5f, new Vector3(2f, 0.5f, 0.7f));
                }
                // Caçamba de lixo encostada no lado -X, às vezes com caixas de papelão do lado.
                if (h / 100 % 100 < 40)
                {
                    Vector3 p = c + new Vector3(-edge - 0.1f, 0f, -BlockSize * 0.3f);
                    if (!_useLogicalServiceData)
                        DumpsterSpots.Add(p + new Vector3(0.9f, 0f, 0f));
                    RegisterObstacle(p + new Vector3(0f, 0f, -0.5f));
                    RegisterObstacle(p + new Vector3(0f, 0f, 0.5f));
                    AssetProps.Spawn(root, AssetProps.City + "dumpster.gltf", p, Mathf.Pi * 0.5f, 3.2f, new Vector3(1.8f, 1.05f, 1.15f));
                    if (h % 3 == 0)
                    {
                        AssetProps.Spawn(root, AssetProps.City + "box_A.gltf", p + new Vector3(0.1f, 0f, 1.6f), 0.4f, 4f);
                        AssetProps.Spawn(root, AssetProps.City + "box_B.gltf", p + new Vector3(0.2f, 0f, 2.3f), -0.3f, 4f);
                    }
                }
            }
        }
    }

    private void BuildStreetPropsForBlock(int bx, int bz)
    {
        if (bx == 0 && bz == 0)
            return;

        var root = new Node3D { Name = $"StreetProps_{bx}_{bz}" };
        (_activeChunkRoot ?? _staticRoot).AddChild(root);
        float edge = BlockSize * 0.5f - 1.0f;
        Vector3 center = BlockCenter(bx, bz) + new Vector3(0f, CurbHeight, 0f);
        int hash = AssetProps.Hash(center, 11);

        if (hash % 100 < 55)
        {
            RegisterObstacle(center + new Vector3(BlockSize * 0.08f - 0.7f, 0f, edge));
            RegisterObstacle(center + new Vector3(BlockSize * 0.08f + 0.7f, 0f, edge));
            AssetProps.Spawn(root, AssetProps.City + "bench.gltf",
                center + new Vector3(BlockSize * 0.08f, 0f, edge), 0f, 5f, new Vector3(2f, 0.5f, 0.7f));
        }

        if (hash / 100 % 100 < 40)
        {
            Vector3 p = center + new Vector3(-edge - 0.1f, 0f, -BlockSize * 0.3f);
            RegisterObstacle(p + new Vector3(0f, 0f, -0.5f));
            RegisterObstacle(p + new Vector3(0f, 0f, 0.5f));
            AssetProps.Spawn(root, AssetProps.City + "dumpster.gltf",
                p, Mathf.Pi * 0.5f, 3.2f, new Vector3(1.8f, 1.05f, 1.15f));
            if (hash % 3 == 0)
            {
                AssetProps.Spawn(root, AssetProps.City + "box_A.gltf", p + new Vector3(0.1f, 0f, 1.6f), 0.4f, 4f);
                AssetProps.Spawn(root, AssetProps.City + "box_B.gltf", p + new Vector3(0.2f, 0f, 2.3f), -0.3f, 4f);
            }
        }
    }

    // ------------------------------------------------------------------ rancho

    private void BuildFarmProps()
    {
        // Caixa d'água da cooperativa, no canto do pátio.
        AssetProps.Spawn(_staticRoot, AssetProps.City + "watertower.gltf", new Vector3(13f, 0f, 12.5f), 0.3f, 13f, new Vector3(5.5f, 8.7f, 5.5f));
        BuildCemetery(new Vector3(36f, 0f, 146f));
    }

    /// <summary>Cemitério da roça, ao lado da capela, com o portão virado para a Estrada Velha (-Z).</summary>
    private void BuildCemetery(Vector3 center)
    {
        var root = new Node3D { Name = "Cemetery", Position = center };
        _staticRoot.AddChild(root);
        const float half = 8f;
        const float seg = 2.667f; // 4 m do modelo x 0.667
        const float fenceScale = 0.667f;

        // Cerca de ferro: 6 módulos por lado; na frente, o do meio vira o arco do portão.
        for (int i = 0; i < 6; i++)
        {
            float t = -half + seg * (i + 0.5f);
            AssetProps.Spawn(root, AssetProps.Halloween + "fence.gltf", new Vector3(t, 0f, half), 0f, fenceScale); // fundo
            AssetProps.Spawn(root, AssetProps.Halloween + "fence.gltf", new Vector3(-half, 0f, t), Mathf.Pi * 0.5f, fenceScale);
            AssetProps.Spawn(root, AssetProps.Halloween + "fence.gltf", new Vector3(half, 0f, t), Mathf.Pi * 0.5f, fenceScale);
            if (i != 2 && i != 3)
                AssetProps.Spawn(root, AssetProps.Halloween + "fence.gltf", new Vector3(t, 0f, -half), 0f, fenceScale);
        }
        AssetProps.Spawn(root, AssetProps.Halloween + "arch_gate.gltf", new Vector3(0f, 0f, -half), 0f, 1.2f);
        foreach (Vector3 corner in new[] { new Vector3(-half, 0f, -half), new Vector3(half, 0f, -half), new Vector3(-half, 0f, half), new Vector3(half, 0f, half) })
            AssetProps.Spawn(root, AssetProps.Halloween + "fence_pillar.gltf", corner, 0f, fenceScale);
        // Colisão das cercas (frente com o vão do portão).
        Transform3D xf = root.Transform;
        AddShape(new BoxShape3D { Size = new Vector3(half * 2f, 1.5f, 0.3f) }, xf * new Transform3D(Basis.Identity, new Vector3(0f, 0.75f, half)));
        AddShape(new BoxShape3D { Size = new Vector3(0.3f, 1.5f, half * 2f) }, xf * new Transform3D(Basis.Identity, new Vector3(-half, 0.75f, 0f)));
        AddShape(new BoxShape3D { Size = new Vector3(0.3f, 1.5f, half * 2f) }, xf * new Transform3D(Basis.Identity, new Vector3(half, 0.75f, 0f)));
        foreach (float x in new[] { -half * 0.67f - 0.1f, half * 0.67f + 0.1f })
            AddShape(new BoxShape3D { Size = new Vector3(half * 0.67f, 1.5f, 0.3f) }, xf * new Transform3D(Basis.Identity, new Vector3(x, 0.75f, -half)));

        // Túmulo da família no fundo e as covas em fileiras.
        AssetProps.Spawn(root, AssetProps.Halloween + "crypt.gltf", new Vector3(0f, 0f, 4.6f), Mathf.Pi, 0.6f);
        AddShape(new BoxShape3D { Size = new Vector3(3.6f, 4.8f, 4.8f) }, xf * new Transform3D(Basis.Identity, new Vector3(0f, 2.4f, 4.6f)));
        string[] graves = { "grave_A.gltf", "grave_B.gltf", "gravestone.gltf", "gravemarker_A.gltf", "gravemarker_B.gltf" };
        for (int row = 0; row < 3; row++)
        {
            for (int col = 0; col < 4; col++)
            {
                var p = new Vector3(-5.4f + col * 3.6f, 0f, -4.5f + row * 3.2f);
                if (row == 2 && Mathf.Abs(p.X) < 3f)
                    continue; // espaço do túmulo
                int h = AssetProps.Hash(center + p, 5);
                string model = graves[h % graves.Length];
                float scale = model.StartsWith("grave_") ? 0.8f : 0.7f;
                AssetProps.Spawn(root, AssetProps.Halloween + model, p, (h % 7 - 3) * 0.04f, scale);
            }
        }

        // Árvores secas, lanternas e um oratório com velas.
        AssetProps.Spawn(root, AssetProps.Halloween + "tree_dead_large.gltf", new Vector3(-6f, 0f, 6f), 0.6f, 1.5f);
        AssetProps.Spawn(root, AssetProps.Halloween + "tree_dead_medium.gltf", new Vector3(6.2f, 0f, -5.5f), 2.1f, 1.4f);
        AssetProps.Spawn(root, AssetProps.Halloween + "shrine_candles.gltf", new Vector3(5.5f, 0f, 5.5f), -0.7f, 0.9f);
        foreach (float x in new[] { -2.2f, 2.2f })
        {
            AssetProps.Spawn(root, AssetProps.Halloween + "lantern_standing.gltf", new Vector3(x, 0f, -half - 0.8f), 0f, 0.9f);
            _lightsRoot.AddChild(new OmniLight3D
            {
                Position = center + new Vector3(x, 0.9f, -half - 0.8f),
                LightColor = new Color(0.55f, 1f, 0.6f),
                LightEnergy = 0.9f,
                OmniRange = 7f,
            });
        }
        root.AddChild(new Label3D
        {
            Text = "CEMITÉRIO DA FREQUÊNCIA\naqui jaz o sinal",
            FontSize = 48,
            PixelSize = 0.006f,
            Modulate = new Color(0.75f, 1f, 0.78f),
            OutlineSize = 8,
            OutlineModulate = new Color(0.02f, 0.05f, 0.03f),
            Position = new Vector3(0f, 5.8f, -half - 0.1f),
            Rotation = new Vector3(0f, Mathf.Pi, 0f),
        });
        MarkPoi(root, "+", new Color(0.55f, 0.65f, 0.6f));
    }

    /// <summary>Canteiro de abóboras numa terra arada (coordenadas locais do sítio).</summary>
    private void PumpkinPatch(Transform3D xf, float side)
    {
        float minX = Mathf.Min(side * 7f, side * 21f), maxX = Mathf.Max(side * 7f, side * 21f);
        string[] models = { "pumpkin_orange.gltf", "pumpkin_yellow.gltf", "pumpkin_orange_small.gltf" };
        for (int i = 0; i < 18; i++)
        {
            var local = new Vector3(_rng.RandfRange(minX, maxX), 0.08f, _rng.RandfRange(-51f, -28f));
            Vector3 world = xf * local;
            AssetProps.Spawn(_staticRoot, AssetProps.Halloween + models[_rng.RandiRange(0, models.Length - 1)], world, _rng.Randf() * Mathf.Tau, _rng.RandfRange(0.45f, 0.75f));
        }
    }
}
