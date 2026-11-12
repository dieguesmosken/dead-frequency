// Scripts/World/CityGenerator.AgroTudao.cs
// Agro-Tudão (v0.9.9): casa de rações na Estrada do Rancho, logo depois da cooperativa (Fase 2 · Rancho).
// Galpão de telha com placa verde, pilhas de sacos de ração na varanda e balcão com o Seu Tonho
// (loja: repelente, luva e conserto). Os sacos de ração das entregas nascem na frente (AgroTudaoSpawn)
// e vão para os sítios. Parte da estrutura global leve do rancho, como a cooperativa e a capela.
using Godot;

namespace DeadFrequency;

public partial class CityGenerator
{
    /// <summary>Centro do Agro-Tudão no chão do rancho (mesmo valor nas listas do WorldDataGenerator).</summary>
    public static readonly Vector2 AgroTudaoCenter = new(-17f, 58f);
    public const float AgroTudaoRadius = 13f;

    /// <summary>Onde os sacos de ração das entregas aparecem (na frente do galpão). Nulo fora do rancho.</summary>
    public Vector3? AgroTudaoSpawn { get; private set; }

    private void BuildAgroTudao()
    {
        // Frente do galpão virada para a estrada (+X): o local +Z aponta para +X.
        var agro = new Node3D
        {
            Name = "AgroTudao",
            Position = new Vector3(AgroTudaoCenter.X, 0f, AgroTudaoCenter.Y),
            Rotation = new Vector3(0f, Mathf.Pi * 0.5f, 0f),
        };
        _staticRoot.AddChild(agro);

        StandardMaterial3D wall = Flat(new Color(0.86f, 0.82f, 0.7f), 0.9f);
        StandardMaterial3D barra = Flat(new Color(0.16f, 0.42f, 0.2f), 0.8f);
        StandardMaterial3D roof = Flat(new Color(0.55f, 0.57f, 0.58f), 0.5f);
        StandardMaterial3D wood = Flat(new Color(0.5f, 0.36f, 0.22f), 0.9f);
        StandardMaterial3D raffia = Flat(new Color(0.88f, 0.86f, 0.78f), 0.95f);
        StandardMaterial3D floor = Flat(new Color(0.55f, 0.53f, 0.5f), 0.95f);

        // Galpão 12 x 8 m, aberto na frente (portão de enrolar levantado).
        AddBox(agro, new Vector3(0f, 0.05f, 0.5f), new Vector3(12f, 0.1f, 9f), floor, false);
        AddBox(agro, new Vector3(0f, 2.1f, -4f), new Vector3(12f, 4.2f, 0.25f), wall);
        foreach (float side in new[] { -1f, 1f })
        {
            AddBox(agro, new Vector3(side * 6f, 2.1f, 0f), new Vector3(0.25f, 4.2f, 8f), wall);
            AddBox(agro, new Vector3(side * 6f, 0.45f, 0f), new Vector3(0.27f, 0.9f, 8.02f), barra, false);
            AddBox(agro, new Vector3(side * 4.2f, 2.1f, 4f), new Vector3(3.6f, 4.2f, 0.25f), wall); // pilares da frente
        }
        AddBox(agro, new Vector3(0f, 3.85f, 4f), new Vector3(4.8f, 0.7f, 0.25f), Flat(new Color(0.4f, 0.42f, 0.44f), 0.4f), false); // porta de enrolar
        AddBox(agro, new Vector3(0f, 4.35f, 0.4f), new Vector3(12.8f, 0.15f, 9.6f), roof, false);

        // Placa verde no alto da fachada.
        AddBox(agro, new Vector3(0f, 5.0f, 4.1f), new Vector3(9f, 1.2f, 0.12f), barra, false);
        agro.AddChild(new Label3D
        {
            Text = "AGRO-TUDÃO",
            FontSize = 96,
            PixelSize = 0.008f,
            Modulate = new Color(1f, 0.92f, 0.35f),
            OutlineSize = 8,
            OutlineModulate = new Color(0.05f, 0.2f, 0.08f),
            Position = new Vector3(0f, 5.12f, 4.18f),
        });
        agro.AddChild(new Label3D
        {
            Text = "RAÇÕES · SEMENTES · FERRAGENS · VETERINÁRIA",
            FontSize = 36,
            PixelSize = 0.006f,
            Modulate = Colors.White,
            OutlineSize = 4,
            OutlineModulate = new Color(0.05f, 0.2f, 0.08f),
            Position = new Vector3(0f, 4.62f, 4.18f),
        });

        // Pilhas de sacos de ração (dentro e na varanda) e paletes.
        for (int pile = 0; pile < 4; pile++)
        {
            float x = -4.6f + pile * 1.4f;
            AddBox(agro, new Vector3(x, 0.07f, -3f), new Vector3(1.2f, 0.14f, 1.0f), wood, false);
            int layers = 3 + pile % 3;
            for (int k = 0; k < layers; k++)
            {
                AddBox(agro, new Vector3(x, 0.28f + k * 0.27f, -3f), new Vector3(1.1f, 0.26f, 0.7f), raffia, false);
                AddBox(agro, new Vector3(x, 0.28f + k * 0.27f, -2.64f), new Vector3(0.5f, 0.12f, 0.01f), barra, false);
            }
        }
        AddBox(agro, new Vector3(-3.9f, 0.6f, -3f), new Vector3(2.6f, 1.2f, 1.0f), wood); // colisão das pilhas
        AddBox(agro, new Vector3(3.6f, 0.07f, 5.4f), new Vector3(1.2f, 0.14f, 1.0f), wood, false); // palete na varanda
        for (int k = 0; k < 2; k++)
            AddBox(agro, new Vector3(3.6f, 0.28f + k * 0.27f, 5.4f), new Vector3(1.1f, 0.26f, 0.7f), raffia, false);

        // Prateleira de ferragens na parede da esquerda e balcão com registradora.
        AddBox(agro, new Vector3(-5.5f, 1.4f, 0.5f), new Vector3(0.6f, 2.8f, 4f), wood);
        for (int shelf = 0; shelf < 4; shelf++)
            for (int i = 0; i < 5; i++)
                AddBox(agro, new Vector3(-5.2f, 0.45f + shelf * 0.62f, -1.1f + i * 0.75f), new Vector3(0.2f, 0.28f, 0.4f),
                    Flat(new Color(0.2f + 0.15f * ((i + shelf) % 3), 0.35f, 0.25f + 0.2f * (i % 2)), 0.7f), false);
        Vector3 counter = new(1.5f, 0.55f, 0.6f);
        AddBox(agro, counter, new Vector3(3.2f, 1.1f, 0.8f), wood);
        AddBox(agro, counter + new Vector3(0.9f, 0.7f, 0f), new Vector3(0.4f, 0.28f, 0.35f), _darkMetal, false);
        BuildCashier(agro, new Vector3(1.5f, 0f, -0.4f), 0f, new Color(0.16f, 0.42f, 0.2f));

        ActionArea.Create(agro, counter + new Vector3(0f, 0.5f, 1.0f), new Vector3(3.4f, 1.6f, 1.0f),
            _ => "[E] Comprar no Agro-Tudão",
            _ => ShopScreen.Instance?.OpenFor("Agro-Tudão", ShopStock.AgroTudao),
            player => ShopScreen.Instance != null && !player.IsHolding);

        // Luz da varanda (a roça é escura) e marcador no minimapa.
        var light = new OmniLight3D { Position = new Vector3(0f, 3.9f, 4.6f), LightColor = new Color(1f, 0.85f, 0.6f), LightEnergy = 1.2f, OmniRange = 10f };
        agro.AddChild(light);
        MarkPoi(agro, "R", new Color(0.3f, 0.8f, 0.35f));

        // Sacos das entregas: na frente do galpão, entre o portão e a estrada.
        Transform3D root = _staticRoot.IsInsideTree() ? _staticRoot.GlobalTransform : Transform3D.Identity;
        AgroTudaoSpawn = root * agro.Transform * new Vector3(-1.5f, 0.6f, 6.4f);
    }
}
