// Scripts/Vehicle/VehicleFactory.cs
// Veículos extras do pátio do depósito (v0.9.3), montados em código com o mesmo VanController da van:
//   • moto de entrega (com baú: leva uma caixa de até 20 kg; dá para fazer entregas com ela);
//   • caminhões emprestados (amarelo e vermelho) para ir para casa ou passear;
//   • táxi amarelo da cooperativa (modelo PSX da GGBot, CC0) e van de viagem anos 80 (CarModels), v0.9.9.
// Os modelos são da Kenney (CC0). Frente = +Z, como a van. Cada um tem um NetId próprio no online.
using Godot;

namespace DeadFrequency;

public enum VehicleKind
{
    Moto,
    TruckYellow,
    TruckRed,
}

public static class VehicleFactory
{
    public readonly record struct Spec(string NetId, string Name, string Model, float ModelScale, float ModelYaw, Vector3 Body, float WheelX, float WheelZ, float WheelRadius,
        float Mass, float Engine, float Brake, float MaxKmh, Vector3 Seat, Vector3 Exit, Vector3 Lights, string Prompt);

    public static Spec Get(VehicleKind kind) => kind switch
    {
        VehicleKind.Moto => new("moto", "moto", "vehicle-motorcycle.glb", 0.95f, 0f, new Vector3(0.7f, 0.9f, 2.0f), 0.18f, 0.72f, 0.32f,
            260f, 900f, 14f, 85f, new Vector3(0f, 0.05f, -0.15f), new Vector3(1.1f, 0f, 0f), new Vector3(0f, 0.95f, 0.95f), "[E] Pilotar a moto de entrega"),
        VehicleKind.TruckRed => new("truck_red", "caminhão", "vehicle-truck-red.glb", 1.7f, 0f, new Vector3(2.4f, 2.2f, 4.6f), 1.0f, 1.45f, 0.45f,
            2600f, 4200f, 70f, 80f, new Vector3(0.5f, 0.85f, 1.2f), new Vector3(2.0f, 0f, 1.2f), new Vector3(0f, 1.1f, 2.35f), "[E] Pegar o caminhão emprestado"),
        _ => new("truck_yellow", "caminhão", "vehicle-truck-yellow.glb", 1.7f, 0f, new Vector3(2.4f, 2.2f, 4.6f), 1.0f, 1.45f, 0.45f,
            2600f, 4200f, 70f, 80f, new Vector3(0.5f, 0.85f, 1.2f), new Vector3(2.0f, 0f, 1.2f), new Vector3(0f, 1.1f, 2.35f), "[E] Pegar o caminhão emprestado"),
    };

    /// <summary>NetId do táxi da cooperativa (= VEHICLES no servidor).</summary>
    public const string TaxiNetId = "taxi";
    /// <summary>NetId da van de viagem do Modo História (= VEHICLES no servidor).</summary>
    public const string TravelVanNetId = "van_viagem";

    /// <summary>Nome fictício da cooperativa e do aplicativo (paródia) pintados no táxi.</summary>
    public const string TaxiCooperative = "RÁDIO TÁXI SINTONIA", TaxiApp = "66\nAPP";

    /// <summary>Carro da concessionária (v0.9.7), com o mesmo modelo dos carros da rua (CarModels).
    /// netId = id do item (car_gou...), que também é o id do veículo no servidor.</summary>
    public static VanController BuildCar(CarGarage.Model model) =>
        BuildCar(model.Id, ItemCatalog.Get(model.Id)?.Name ?? "carro", model.Type, model.Color,
            model.Type == CarType.Fusca ? 110f : model.Type == CarType.Sedan ? 140f : 125f);

    /// <summary>Interior mínimo para a primeira pessoa nos carros: painel escuro, volante e forro do teto.
    /// eye = posição dos olhos do motorista (local do veículo, frente +Z).</summary>
    private static void BuildSimpleCabin(VanController v, Vector3 eye, Vector3 size)
    {
        var cabin = new Node3D { Name = "CabinSimple" };
        v.AddChild(cabin);
        CabinView.Attach(v, cabin);
        var dark = new StandardMaterial3D { AlbedoColor = new Color(0.1f, 0.1f, 0.11f), Roughness = 0.9f };
        var grey = new StandardMaterial3D { AlbedoColor = new Color(0.2f, 0.2f, 0.22f), Roughness = 0.8f };
        float half = size.X * 0.5f - 0.08f;
        void Box(Vector3 pos, Vector3 sz, Material m, float tiltX = 0f) =>
            cabin.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = sz }, Position = pos, Rotation = new Vector3(tiltX, 0f, 0f), MaterialOverride = m,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
        // Painel de lado a lado, abaixo da linha dos olhos.
        Box(new Vector3(0f, eye.Y - 0.35f, eye.Z + 0.62f), new Vector3(half * 2f, 0.22f, 0.4f), dark);
        Box(new Vector3(eye.X, eye.Y - 0.22f, eye.Z + 0.6f), new Vector3(0.42f, 0.1f, 0.3f), grey); // capela dos mostradores
        // Forro do teto e colunas da frente (emolduram a vista como na caminhonete).
        Box(new Vector3(0f, eye.Y + 0.24f, eye.Z + 0.05f), new Vector3(half * 2f, 0.04f, 1.5f), grey);
        foreach (float x in new[] { -half, half })
            Box(new Vector3(x, eye.Y - 0.04f, eye.Z + 0.78f), new Vector3(0.07f, 0.62f, 0.07f), dark, -0.45f);
        // Volante.
        cabin.AddChild(new MeshInstance3D
        {
            Name = "SteeringWheel",
            Mesh = new TorusMesh { InnerRadius = 0.14f, OuterRadius = 0.18f, Rings = 12, RingSegments = 6 },
            Position = new Vector3(eye.X, eye.Y - 0.29f, eye.Z + 0.42f),
            Rotation = new Vector3(Mathf.DegToRad(68f), 0f, 0f),
            MaterialOverride = dark,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        });
        Box(new Vector3(eye.X, eye.Y - 0.33f, eye.Z + 0.52f), new Vector3(0.05f, 0.05f, 0.22f), dark, -0.38f); // coluna
    }

    /// <summary>Táxi amarelo da cooperativa (v0.9.9): "Car 05" do pacote PSX Style Cars (GGBot, CC0),
    /// com o nome fictício da cooperativa e o adesivo do aplicativo "66". Só ele faz corridas.</summary>
    public static VanController BuildTaxi() =>
        BuildCar(TaxiNetId, "táxi", CarType.Sedan, new Color(0.98f, 0.78f, 0.08f), 135f, customModel: TaxiModel);

    private const string TaxiObj = "res://Assets/GGBot/Cars/Car5_Taxi.obj", TaxiTexture = "res://Assets/GGBot/Cars/car5_taxi.png";

    /// <summary>Monta o táxi PSX (frente = +Z, como o VanController). Sem o modelo, devolve Vector3.Zero (usa o Opalinha amarelo).</summary>
    private static Vector3 TaxiModel(Node3D body)
    {
        if (!ResourceLoader.Exists(TaxiObj) || GD.Load<Mesh>(TaxiObj) is not Mesh mesh)
            return Vector3.Zero;
        const float scale = 0.64f; // o OBJ vem com 7,3 m de comprimento
        var mat = new StandardMaterial3D
        {
            AlbedoTexture = GD.Load<Texture2D>(TaxiTexture),
            TextureFilter = BaseMaterial3D.TextureFilterEnum.Nearest,
            Roughness = 0.6f,
        };
        body.AddChild(new MeshInstance3D { Name = "TaxiMesh", Mesh = mesh, MaterialOverride = mat, Scale = Vector3.One * scale });
        Vector3 size = mesh.GetAabb().Size * scale;

        // Nome da cooperativa e número nas portas, dos dois lados.
        foreach (float side in new[] { -1f, 1f })
        {
            var rot = new Vector3(0f, side * Mathf.Pi * 0.5f, 0f);
            float x = side * (size.X * 0.5f - 0.02f);
            body.AddChild(Decal(TaxiCooperative, 30, 0.0024f, new Vector3(x, 0.5f, 0.05f), rot));
            body.AddChild(Decal("Nº 096", 30, 0.0026f, new Vector3(x, 0.62f, -1.55f), rot));
        }
        // Adesivo do aplicativo na tampa do porta-malas (lado do passageiro), tipo adesivo de para-choque.
        body.AddChild(Decal(TaxiApp, 28, 0.0024f, new Vector3(-0.42f, 0.74f, -2.27f), new Vector3(0f, Mathf.Pi, 0f), new Color(0.98f, 0.82f, 0.1f)));
        return size;
    }

    private static Label3D Decal(string text, int fontSize, float pixel, Vector3 pos, Vector3 rot, Color? color = null) => new()
    {
        Text = text,
        FontSize = fontSize,
        PixelSize = pixel,
        Modulate = color ?? new Color(0.1f, 0.1f, 0.12f),
        OutlineSize = color.HasValue ? 6 : 0,
        OutlineModulate = new Color(0.08f, 0.08f, 0.08f),
        Position = pos,
        Rotation = rot,
    };

    /// <summary>Van de viagem anos 80 (v0.9.9), parada no pátio da central: a van do Modo História.</summary>
    public static VanController BuildTravelVan() =>
        BuildCar(TravelVanNetId, "van de viagem", CarType.Van80, new Color(0.5f, 0.28f, 0.14f), 105f);

    /// <param name="customModel">Monta outro modelo (frente = +Z) e devolve o tamanho; Vector3.Zero = usar o CarModels.</param>
    private static VanController BuildCar(string netId, string displayName, CarType type, Color color, float maxKmh,
        System.Func<Node3D, Vector3> customModel = null)
    {
        bool van = type == CarType.Van80;
        var v = new VanController
        {
            Name = netId,
            NetId = netId,
            Kind = "car",
            DisplayName = displayName,
            Mass = van ? 1500f : 1100f,
            CollisionLayer = 16,
            CollisionMask = 1 | 2 | 8,
            CenterOfMassMode = RigidBody3D.CenterOfMassModeEnum.Custom,
            CenterOfMass = new Vector3(0f, 0.15f, 0f), // baixo: carro de jogo não capota em qualquer curva
            MaxEngineForce = van ? 3200f : 2600f,
            MaxReverseForce = 1200f,
            BrakeForce = van ? 48f : 38f,
            HandbrakeForce = van ? 96f : 76f,
            ParkedBrake = van ? 34f : 27f,
            CoastBrake = 2.6f,
            MaxSpeedKmh = maxKmh,
            MaxSteerDegrees = van ? 28f : 30f,
        };
        var body = new Node3D { Name = "Model" };
        Vector3 size = customModel?.Invoke(body) ?? Vector3.Zero;
        if (size == Vector3.Zero)
        {
            // O CarModels monta com a frente em -Z; o VanController anda para +Z: gira meia volta.
            body.Rotation = new Vector3(0f, Mathf.Pi, 0f);
            size = CarModels.Build(body, type, color, Plates.For(netId));
        }
        v.AddChild(body);
        v.AddChild(new CollisionShape3D { Name = "BodyShape", Shape = new BoxShape3D { Size = new Vector3(size.X, size.Y - 0.35f, size.Z) }, Position = new Vector3(0f, size.Y * 0.5f + 0.17f, 0f) });

        float wx = size.X * 0.5f - 0.12f, wz = size.Z * 0.31f;
        foreach ((string name, float x, float z, bool steer) in new[]
        {
            ("WheelFL", wx, wz, true), ("WheelFR", -wx, wz, true), ("WheelRL", wx, -wz, false), ("WheelRR", -wx, -wz, false),
        })
        {
            v.AddChild(new VehicleWheel3D
            {
                Name = name,
                Position = new Vector3(x, 0.42f, z),
                WheelRadius = 0.3f,
                WheelRestLength = 0.15f,
                WheelFrictionSlip = 2.6f,
                WheelRollInfluence = 0.05f,
                SuspensionTravel = 0.2f,
                SuspensionStiffness = 50f,
                SuspensionMaxForce = v.Mass * 7f,
                DampingCompression = 1.2f,
                DampingRelaxation = 1.4f,
                UseAsSteering = steer,
                // Fusca: tração traseira (motor atrás). Os outros também, como os carros da época.
                UseAsTraction = !steer,
            });
        }

        // Motorista do lado esquerdo (+X olhando para a frente +Z), como no Brasil.
        // Na van a cabine fica lá na frente (o baú é atrás) e o banco é mais alto.
        var seatPos = van ? new Vector3(0.42f, 0.62f, 0.95f) : new Vector3(0.36f, 0.32f, -0.15f);
        var seat = new Marker3D { Name = "SeatMarker", Position = seatPos, Rotation = new Vector3(0f, Mathf.Pi, 0f) };
        var exit = new Marker3D { Name = "ExitMarker", Position = new Vector3(size.X * 0.5f + 0.8f, 0f, seatPos.Z), Rotation = new Vector3(0f, Mathf.Pi * 0.5f, 0f) };
        v.AddChild(seat);
        v.AddChild(exit);
        // Olhos uns 30 cm abaixo do teto, com um painel simples na frente (volante, painel e forro do teto).
        float eyeY = size.Y - 0.3f;
        v.EyeOffsetY = eyeY - (seatPos.Y + 1.6f);
        BuildSimpleCabin(v, new Vector3(seatPos.X, eyeY, seatPos.Z), size);
        var door = new DriverSeatInteractable
        {
            Name = "DriverDoor",
            Van = v,
            SeatMarker = seat,
            ExitMarker = exit,
            CollisionLayer = 4,
            CollisionMask = 0,
            Monitoring = false,
            Position = new Vector3(0f, size.Y * 0.5f, 0f),
        };
        door.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = size + new Vector3(0.5f, 0.2f, 0.2f) } });
        v.AddChild(door);

        var lights = new Node3D { Name = "Headlights" };
        v.AddChild(lights);
        foreach (float x in new[] { -0.55f, 0.55f })
        {
            lights.AddChild(new SpotLight3D
            {
                Position = new Vector3(x, 0.7f, size.Z * 0.5f + 0.1f),
                Rotation = new Vector3(0f, Mathf.Pi, 0f),
                LightColor = new Color(1f, 0.94f, 0.78f),
                LightEnergy = 5f,
                SpotRange = 28f,
                SpotAngle = 28f,
                ShadowEnabled = false,
            });
        }
        return v;
    }

    /// <summary>Monta o veículo (ainda fora da árvore). Posicione e adicione depois.</summary>
    public static VanController Build(VehicleKind kind)
    {
        Spec s = Get(kind);
        var v = new VanController
        {
            Name = s.NetId,
            NetId = s.NetId,
            Kind = kind == VehicleKind.Moto ? "moto" : "truck",
            DisplayName = s.Name,
            Mass = s.Mass,
            CollisionLayer = 16, // Vehicle
            CollisionMask = 1 | 2 | 8,
            CenterOfMassMode = RigidBody3D.CenterOfMassModeEnum.Custom,
            CenterOfMass = new Vector3(0f, kind == VehicleKind.Moto ? 0.05f : 0.3f, 0f),
            MaxEngineForce = s.Engine,
            MaxReverseForce = s.Engine * 0.45f,
            BrakeForce = s.Brake,
            HandbrakeForce = s.Brake * 2f,
            ParkedBrake = s.Brake * 0.7f,
            CoastBrake = s.Brake * 0.07f,
            MaxSpeedKmh = s.MaxKmh,
            MaxSteerDegrees = kind == VehicleKind.Moto ? 30f : 28f,
        };
        if (kind == VehicleKind.Moto)
            v.AngularDamp = 3f; // segura a moto em pé

        float bodyY = s.Body.Y * 0.5f + s.WheelRadius * 0.6f;
        v.AddChild(new CollisionShape3D { Name = "BodyShape", Shape = new BoxShape3D { Size = s.Body }, Position = new Vector3(0f, bodyY, 0f) });

        string path = AssetProps.Kenney + s.Model;
        if (kind == VehicleKind.Moto && MeshyVehicles.HasProp("moto"))
        {
            // v0.10: moto de entregas 125 cc do Meshy (frente = +Z), no lugar da moto da Kenney.
            var meshy = new Node3D { Name = "Model" };
            v.AddChild(meshy);
            MeshyVehicles.BuildProp(meshy, "moto", new Vector3(0.8f, 1.08f, 2.0f), 1);
        }
        else if (ResourceLoader.Exists(path) && GD.Load<PackedScene>(path)?.Instantiate() is Node3D model)
        {
            model.Name = "Model";
            model.Scale = Vector3.One * s.ModelScale;
            model.Rotation = new Vector3(0f, s.ModelYaw, 0f);
            v.AddChild(model);
        }

        // Rodas invisíveis (o modelo já tem as dele). Na moto ficam bem juntas: ela não tomba.
        foreach ((string name, float x, float z, bool steer) in new[]
        {
            ("WheelFL", s.WheelX, s.WheelZ, true), ("WheelFR", -s.WheelX, s.WheelZ, true),
            ("WheelRL", s.WheelX, -s.WheelZ, false), ("WheelRR", -s.WheelX, -s.WheelZ, false),
        })
        {
            v.AddChild(new VehicleWheel3D
            {
                Name = name,
                Position = new Vector3(x, s.WheelRadius + 0.12f, z),
                WheelRadius = s.WheelRadius,
                WheelRestLength = 0.15f,
                WheelFrictionSlip = kind == VehicleKind.Moto ? 3.2f : 2.4f,
                WheelRollInfluence = kind == VehicleKind.Moto ? 0.02f : 0.05f,
                SuspensionTravel = 0.2f,
                SuspensionStiffness = kind == VehicleKind.Moto ? 55f : 55f,
                SuspensionMaxForce = s.Mass * 7f,
                DampingCompression = 1.2f,
                DampingRelaxation = 1.4f,
                UseAsSteering = steer,
                UseAsTraction = !steer || kind != VehicleKind.Moto,
            });
        }

        // Caminhões: olhos dentro da cabine (antes a câmera ficava acima do teto). A moto fica como está.
        if (kind != VehicleKind.Moto)
            v.EyeOffsetY = 1.72f - (s.Seat.Y + 1.6f);
        var seat = new Marker3D { Name = "SeatMarker", Position = s.Seat, Rotation = new Vector3(0f, Mathf.Pi, 0f) };
        var exit = new Marker3D { Name = "ExitMarker", Position = s.Exit, Rotation = new Vector3(0f, Mathf.Pi * 0.5f, 0f) };
        v.AddChild(seat);
        v.AddChild(exit);
        var door = new DriverSeatInteractable
        {
            Name = "DriverDoor",
            Van = v,
            SeatMarker = seat,
            ExitMarker = exit,
            CollisionLayer = 4,
            CollisionMask = 0,
            Monitoring = false,
            Position = new Vector3(0f, bodyY, 0f),
        };
        door.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = s.Body + new Vector3(0.5f, 0.2f, 0.2f) } });
        v.AddChild(door);

        var lights = new Node3D { Name = "Headlights" };
        v.AddChild(lights);
        foreach (float x in kind == VehicleKind.Moto ? new[] { 0f } : new[] { -0.75f, 0.75f })
        {
            lights.AddChild(new SpotLight3D
            {
                Position = s.Lights + new Vector3(x, 0f, 0f),
                Rotation = new Vector3(0f, Mathf.Pi, 0f),
                LightColor = new Color(1f, 0.94f, 0.78f),
                LightEnergy = 6f,
                SpotRange = 30f,
                SpotAngle = 28f,
                ShadowEnabled = false,
            });
        }
        if (kind == VehicleKind.Moto)
            MotoTopCase.Attach(v);
        return v;
    }
}
