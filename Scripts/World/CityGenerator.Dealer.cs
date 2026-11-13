// Scripts/World/CityGenerator.Dealer.cs
// Auto Center Dois Mil (v0.9.7): concessionária de seminovos ao lado do depósito. Três carros no pátio
// com o preço no para-brisa; o vendedor na porta do salão vende. Carro comprado vira dirigível ali
// mesmo (online é da sala inteira, comprado com o dinheiro do grupo). Sem sorteio do _rng.
using System.Collections.Generic;
using Godot;

namespace DeadFrequency;

public partial class CityGenerator
{
    [Export] public Vector2I DealerBlock { get; set; } = new(1, 0);

    private Node3D _dealer;
    private readonly Dictionary<string, Node3D> _dealerDisplay = new();
    private readonly List<Transform3D> _dealerSpots = new();

    private void BuildDealer(Node3D block, Vector3 center)
    {
        // Frente virada para a rua do depósito (-X): no espaço local, +Z = frente.
        _dealer = new Node3D { Name = "AutoCenter", Position = center + new Vector3(0f, CurbHeight, 0f), Rotation = new Vector3(0f, -Mathf.Pi * 0.5f, 0f) };
        block.AddChild(_dealer);
        Node3D d = _dealer;
        float half = BlockSize * 0.5f - SidewalkWidth;

        var asphalt = Flat(new Color(0.16f, 0.16f, 0.17f));
        var white = Flat(new Color(0.85f, 0.85f, 0.82f));
        var blue = Flat(new Color(0.1f, 0.25f, 0.6f));
        var yellow = Flat(new Color(0.95f, 0.75f, 0.1f));
        var tile = Flat(new Color(0.75f, 0.74f, 0.7f), 0.4f);

        // Pátio asfaltado com vagas pintadas.
        AddBox(d, new Vector3(0f, 0.02f, 4f), new Vector3(half * 2f, 0.04f, half * 2f - 8f), asphalt, false);
        for (int i = 0; i < 4; i++)
            AddBox(d, new Vector3(-9f + i * 6f, 0.045f, 6f), new Vector3(0.12f, 0.01f, 5.5f), white, false);

        // Salão: piso, paredes, telhado e vitrine de vidro na frente (com vão da porta no meio).
        const float z0 = -15.5f, z1 = -4f, hx = 11f, h = 4.6f;
        AddBox(d, new Vector3(0f, 0.05f, (z0 + z1) * 0.5f), new Vector3(hx * 2f, 0.1f, z1 - z0), tile, false);
        AddBox(d, new Vector3(0f, h * 0.5f, z0), new Vector3(hx * 2f, h, 0.3f), white);
        AddBox(d, new Vector3(-hx, h * 0.5f, (z0 + z1) * 0.5f), new Vector3(0.3f, h, z1 - z0), white);
        AddBox(d, new Vector3(hx, h * 0.5f, (z0 + z1) * 0.5f), new Vector3(0.3f, h, z1 - z0), white);
        AddBox(d, new Vector3(0f, h + 0.15f, (z0 + z1) * 0.5f), new Vector3(hx * 2f + 0.6f, 0.3f, z1 - z0 + 0.6f), blue);
        foreach (float side in new[] { -1f, 1f })
            AddBox(d, new Vector3(side * (hx + 1.6f) * 0.5f, h * 0.5f, z1), new Vector3(hx - 1.6f, h, 0.08f), _glass);
        AddBox(d, new Vector3(0f, h - 0.5f, z1), new Vector3(3.2f, 1f, 0.12f), white); // bandeira da porta
        // Letreiro.
        AddBox(d, new Vector3(0f, h + 1.0f, z1 + 0.2f), new Vector3(12f, 1.4f, 0.25f), blue);
        d.AddChild(new Label3D { Text = "AUTO CENTER DOIS MIL", FontSize = 96, PixelSize = 0.009f, Modulate = new Color(1f, 0.85f, 0.15f), OutlineSize = 10, OutlineModulate = new Color(0.05f, 0.1f, 0.3f), Position = new Vector3(0f, h + 1.12f, z1 + 0.34f) });
        d.AddChild(new Label3D { Text = "SEMINOVOS · TROCA · FINANCIA", FontSize = 40, PixelSize = 0.007f, Modulate = Colors.White, OutlineSize = 6, OutlineModulate = new Color(0.05f, 0.1f, 0.3f), Position = new Vector3(0f, h + 0.55f, z1 + 0.34f) });

        // Dentro: mesa do vendedor e um carro "zero" de vitrine (esse não vende: é do dono).
        AddBox(d, new Vector3(6f, 0.4f, -12f), new Vector3(2f, 0.8f, 0.9f), Flat(new Color(0.35f, 0.24f, 0.15f)));
        var showCar = new Node3D { Position = new Vector3(-4.5f, 0.1f, -10f), Rotation = new Vector3(0f, 0.7f, 0f) };
        CarModels.Build(showCar, CarType.Sedan, new Color(0.08f, 0.08f, 0.1f));
        d.AddChild(showCar);

        // Bandeirinhas de festa de concessionária entre os postes da frente.
        var poleMat = Flat(new Color(0.6f, 0.6f, 0.62f), 0.5f);
        Color[] flagColors = { new(0.9f, 0.15f, 0.12f), new(0.95f, 0.8f, 0.1f), new(0.1f, 0.35f, 0.85f), new(0.15f, 0.7f, 0.25f) };
        float front = half - 0.6f;
        foreach (float x in new[] { -half + 0.5f, half - 0.5f })
            AddCylinder(d, new Vector3(x, 2.5f, front), 0.06f, 5f, poleMat);
        for (int i = 0; i < 26; i++)
        {
            float t = (i + 0.5f) / 26f;
            float x = Mathf.Lerp(-half + 0.5f, half - 0.5f, t);
            float y = 4.8f - Mathf.Sin(t * Mathf.Pi) * 0.9f;
            AddBox(d, new Vector3(x, y, front), new Vector3(0.35f, 0.4f, 0.02f), Flat(flagColors[i % 4]), false, 0f);
        }

        // Vendedor na porta.
        CharacterModel seller = CharacterModel.Create(new Color(0.9f, 0.9f, 0.88f), new Color(0.12f, 0.12f, 0.15f), new Color(0.62f, 0.44f, 0.31f));
        seller.Position = new Vector3(1.8f, 0f, -2.6f);
        seller.Chatty = true;
        seller.Lines = new[] { "Esse aí é de garagem, dono único, só ia na missa!", "Faço um precinho bom pra você, chefe.", "Aceita a van na troca? Brincadeira.", "Revisado, ar quente e rádio AM/FM!" };
        d.AddChild(seller);
        ActionArea.Create(d, new Vector3(1.8f, 1f, -2.6f), new Vector3(2.4f, 2f, 2.4f),
            _ => "[E] Falar com o vendedor do Auto Center",
            _ => ShopScreen.Instance?.OpenFor("Auto Center Dois Mil", ShopStock.Dealer),
            p => ShopScreen.Instance != null && !p.IsHolding);

        // Vagas dos carros à venda (de frente para a rua).
        for (int i = 0; i < CarGarage.ForSale.Length; i++)
        {
            var local = new Transform3D(new Basis(Vector3.Up, Mathf.Pi), new Vector3(-6f + i * 6f, 0f, 6f));
            _dealerSpots.Add(d.Transform * local);
            BuildDealerSlot(i);
        }

        // Saída dos carros: proibido estacionar na rua da frente (dos dois lados).
        // A placa vai para a ponta do trecho: no meio ela ficaria na frente da saída dos carros.
        ForbidParking(center + new Vector3(-Period * 0.5f, 0f, 0f), Vector3.Back, BlockSize * 0.5f, null, BlockSize * 0.5f - 2.5f);

        if (NetClient.Instance?.IsOnline != true)
            CarGarage.SetOffline();
        CarGarage.Bought += OnCarBought;
        TreeExiting += () => CarGarage.Bought -= OnCarBought;
    }

    /// <summary>Carro de vitrine (estático, com plaquinha de preço) ou o carro dirigível, se já foi comprado.</summary>
    private void BuildDealerSlot(int i)
    {
        CarGarage.Model model = CarGarage.ForSale[i];
        Transform3D spot = _dealerSpots[i];
        if (CarGarage.Owns(model.Id))
        {
            SpawnOwnedCar(i);
            return;
        }
        // O carro de vitrine olha para a rua (frente do CarModels = -Z local).
        var car = new StaticBody3D { Name = "Vitrine_" + model.Id, Transform = new Transform3D(spot.Basis * new Basis(Vector3.Up, Mathf.Pi), spot.Origin), CollisionLayer = 1, CollisionMask = 0 };
        Vector3 size = CarModels.Build(car, model.Type, model.Color);
        car.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(size.X, size.Y - 0.2f, size.Z) }, Position = new Vector3(0f, size.Y * 0.5f + 0.1f, 0f) });
        ItemDef def = ItemCatalog.Get(model.Id);
        car.AddChild(new Label3D
        {
            Text = $"{def?.Name}\n$ {def?.Price}",
            FontSize = 48,
            PixelSize = 0.005f,
            Modulate = new Color(1f, 0.9f, 0.2f),
            OutlineSize = 8,
            OutlineModulate = new Color(0.1f, 0.05f, 0f),
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            Position = new Vector3(0f, size.Y + 0.5f, 0f),
        });
        _dealer.GetParent().AddChild(car);
        car.SetMeta(StaticBatcher.SkipMeta, true); // some quando o carro é comprado
        _dealerDisplay[model.Id] = car;
    }

    private void SpawnOwnedCar(int i)
    {
        CarGarage.Model model = CarGarage.ForSale[i];
        if (GetNodeOrNull(model.Id) != null)
            return;
        VanController v = VehicleFactory.BuildCar(model);
        // O VanController anda para +Z; a vaga olha para a rua.
        v.Transform = new Transform3D(_dealerSpots[i].Basis * new Basis(Vector3.Up, Mathf.Pi), _dealerSpots[i].Origin + new Vector3(0f, 0.3f, 0f));
        AddChild(v); // fora do Static: é um corpo que se mexe
        NetSync.Instance?.RegisterVehicle(v);
    }

    private void OnCarBought(string id)
    {
        int i = System.Array.FindIndex(CarGarage.ForSale, m => m.Id == id);
        if (i < 0 || _dealer == null)
            return;
        if (_dealerDisplay.Remove(id, out Node3D display))
            display.QueueFree();
        SpawnOwnedCar(i);
    }
}
