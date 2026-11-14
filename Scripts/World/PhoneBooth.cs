// Scripts/World/PhoneBooth.cs
// Orelhão. Mire e aperte E: às vezes a central dá uma dica de entrega, às vezes... é só a frequência morta.
// O visual é montado em código (concha aberta para a rua = +Z local).
// Operadoras paródia (v0.9.4): "tchau" (concha ciano, às vezes roxa, logo laranja), "morto" (roxa, laranja
// ou verde) e a rara "Telefóbica" (concha verde-limão, poste azul) — essas estão todas desativadas.
using Godot;

namespace DeadFrequency;

public enum PhoneBrand
{
    Tchau,
    Morto,
    /// <summary>Paródia da antiga operadora; orelhões raros e desativados.</summary>
    Telefobica,
}

public partial class PhoneBooth : Area3D, IInteractable
{
    public PhoneBrand Brand { get; set; } = PhoneBrand.Tchau;
    /// <summary>Cor da concha: cyan/purple (tchau), purple/orange/green (morto), lime (Telefóbica).</summary>
    public string ShellColor { get; set; } = "cyan";
    public bool Disabled => Brand == PhoneBrand.Telefobica;
    public StandardMaterial3D PhoneMaterial { get; set; }
    public StandardMaterial3D PoleMaterial { get; set; }

    private static readonly string[] StrangeCalls =
    {
        "Ninguém atende. Ao fundo, uma rádio toca uma música que não existe mais.",
        "Linha cruzada: duas pessoas discutem o preço do pão de sal.",
        "Uma voz chiada: \"não corra com o monitor CRT na caçamba...\"",
        "Tom de discagem. Depois, a sua própria voz dizendo \"alô?\".",
        "\"Bom dia, você ganhou um carnê das Casas Baixa!\" (tu-tu-tu)",
        "Só estática. Por um segundo, parece alguém lendo números ao contrário.",
        "Dona Cida: \"Minha barraca tem óculos de visão noturna, viu? Passa lá.\"",
        "Zé: \"Com o radar de pulso você nunca mais se perde, entregador.\"",
        "\"Correrios informa: sua encomenda está a caminho desde 1994.\"",
        "Uma gravação: \"Esta linha foi desativada pela frequência 96.6.\"",
    };

    private static readonly string[] TchauCalls =
    {
        "Gravação da tchau: \"Seus créditos acabaram antes de a ligação começar. tchau!\"",
        "tchau informa: o cartão telefônico de 20 unidades virou 2. Obrigado pela preferência.",
        "\"Você ligou para a tchau. Para continuar, insira mais fichas... ou desista.\"",
        "A ligação completa e alguém do outro lado só diz: \"tchau.\" (tu-tu-tu)",
    };

    private static readonly string[] MortoCalls =
    {
        "morto: \"A ligação caiu. Como sempre. Sinal: nenhum.\"",
        "\"Bem-vindo à morto, conectando você ao além desde 1998.\"",
        "morto informa: \"Área de cobertura: o cemitério da Rua Estática.\"",
        "Um silêncio total. Até o chiado da linha está morto.",
    };

    private static readonly string[] DeadLineCalls =
    {
        "Nada. O fone está mudo. Um adesivo diz: \"LINHA DESATIVADA — a Telefóbica agradece os 30 anos\".",
        "O gancho está solto e o teclado não acende. Este orelhão não funciona há muito tempo.",
        "Silêncio. Alguém escreveu no vidro do visor: \"saudades da ficha\".",
        "Você aperta as teclas e nada acontece. Só um besouro mora aqui agora.",
    };

    private AudioStreamPlayer3D _sound;
    private double _cooldown;
    private static readonly RandomNumberGenerator Rng = new();

    public override void _Ready()
    {
        CollisionLayer = 4; // Interactables
        CollisionMask = 0;
        Monitoring = false;
        AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(1.0f, 1.2f, 1.0f) }, Position = new Vector3(0f, 1.5f, -0.05f) });

        AddToGroup(MinimapView.MarkerGroup);
        SetMeta("map_kind", "phone");
        SetMeta("map_color", Disabled ? new Color(0.5f, 0.55f, 0.3f) : BrandColor(Brand));

        var body = new StaticBody3D { CollisionLayer = 1, CollisionMask = 0 };
        body.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(0.15f, 2.4f, 0.15f) }, Position = new Vector3(0f, 1.2f, -0.65f) });
        AddChild(body);

        PhoneMaterial ??= new StandardMaterial3D { AlbedoColor = new Color(0.2f, 0.21f, 0.23f) };
        PoleMaterial ??= new StandardMaterial3D { AlbedoColor = new Color(0.1f, 0.1f, 0.12f) };
        if (Disabled)
            PoleMaterial = new StandardMaterial3D { AlbedoColor = new Color(0.12f, 0.3f, 0.7f), Roughness = 0.5f }; // poste azul

        // Poste atrás da concha, com o braço que a segura.
        AddChild(new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = 0.05f, BottomRadius = 0.05f, Height = ShellCenter.Y + 0.25f, RadialSegments = 8 },
            Position = new Vector3(0f, (ShellCenter.Y + 0.25f) * 0.5f, ShellCenter.Z - ShellAxes.Z - 0.08f),
            MaterialOverride = PoleMaterial,
        });
        AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(0.14f, 0.32f, 0.12f) }, Position = new Vector3(0f, ShellCenter.Y, ShellCenter.Z - ShellAxes.Z - 0.03f), MaterialOverride = PoleMaterial });

        // Concha oca de verdade (casca de ovo aberta na frente e embaixo), com o logo uma vez só, nas costas,
        // pintado na textura. Por dentro é cinza claro, como os orelhões de rua.
        AddChild(new MeshInstance3D { Mesh = ShellMesh(true), Position = ShellCenter, MaterialOverride = OuterMaterial(Brand, ShellColor) });
        AddChild(new MeshInstance3D { Mesh = ShellMesh(false), Position = ShellCenter, MaterialOverride = InnerMaterial() });

        // Telefone na parede de dentro, no meio da concha: gancho, fio e o teclado com números e visor.
        float back = ShellCenter.Z - ShellAxes.Z * 0.8f;
        float py = ShellCenter.Y - 0.04f;
        AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(0.26f, 0.38f, 0.12f) }, Position = new Vector3(0f, py, back + 0.06f), MaterialOverride = PhoneMaterial });
        AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(0.07f, 0.26f, 0.08f) }, Position = new Vector3(-0.17f, py + 0.02f, back + 0.12f), MaterialOverride = PoleMaterial });
        if (ResourceLoader.Exists("res://Assets/Textures/phone_keypad.png"))
        {
            _keypad ??= new StandardMaterial3D { AlbedoTexture = GD.Load<Texture2D>("res://Assets/Textures/phone_keypad.png"), EmissionEnabled = true, EmissionTexture = GD.Load<Texture2D>("res://Assets/Textures/phone_keypad.png"), EmissionEnergyMultiplier = 0.25f };
            // Desativado: teclado apagado e encardido.
            _deadKeypad ??= new StandardMaterial3D { AlbedoTexture = GD.Load<Texture2D>("res://Assets/Textures/phone_keypad.png"), AlbedoColor = new Color(0.45f, 0.42f, 0.36f) };
            AddChild(new MeshInstance3D { Mesh = new QuadMesh { Size = new Vector2(0.16f, 0.24f) }, Position = new Vector3(0.03f, py + 0.02f, back + 0.122f), MaterialOverride = Disabled ? _deadKeypad : _keypad });
        }
        if (Disabled)
        {
            // Fio arrancado: o fone pendurado mais baixo, torto.
            AddChild(new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 0.006f, BottomRadius = 0.006f, Height = 0.6f }, Position = new Vector3(-0.17f, py - 0.38f, back + 0.12f), MaterialOverride = PhoneMaterial });
        }
        else
        {
            AddChild(new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 0.006f, BottomRadius = 0.006f, Height = 0.3f }, Position = new Vector3(-0.17f, py - 0.24f, back + 0.12f), MaterialOverride = PoleMaterial });
        }

        _sound = new AudioStreamPlayer3D { Bus = AudioManager.SfxBus, UnitSize = 4f, Position = new Vector3(0f, 1.6f, 0f) };
        if (ResourceLoader.Exists("res://Audio/SFX/phone_call.wav"))
            _sound.Stream = GD.Load<AudioStream>("res://Audio/SFX/phone_call.wav");
        AddChild(_sound);
    }

    private static readonly Vector3 ShellCenter = new(0f, 1.58f, -0.05f);
    /// <summary>Semi-eixos da casca (largura, altura, profundidade).</summary>
    private static readonly Vector3 ShellAxes = new(0.5f, 0.56f, 0.52f);
    private static ArrayMesh _outerMesh, _innerMesh;
    private static readonly System.Collections.Generic.Dictionary<string, StandardMaterial3D> Outer = new();
    private static StandardMaterial3D _inner;
    private static StandardMaterial3D _keypad, _deadKeypad;

    /// <summary>
    /// Casca de elipsoide aberta na frente e embaixo. A malha é feita em volta do eixo das "costas"
    /// (oposto à abertura), então a borda sai lisa. kind: 0 = fora, 1 = dentro, 2 = borda (espessura).
    /// UV: u = volta em torno do poste (0,5 nas costas, -Z), v = 0 no topo.
    /// </summary>
    private static ArrayMesh ShellMesh(bool outer) => outer ? (_outerMesh ??= BuildShell(true)) : (_innerMesh ??= BuildShell(false));

    private const float Inset = 0.94f;
    private static readonly Vector3 OpenDir = new Vector3(0f, -0.55f, 1f).Normalized();
    private const float RimAngle = 1.75f; // ~100° a partir das costas

    private static Vector3 ShellDir(float alpha, float beta)
    {
        Vector3 a = -OpenDir;
        Vector3 u = a.Cross(Vector3.Right).Normalized();
        Vector3 v = a.Cross(u).Normalized();
        return a * Mathf.Cos(alpha) + (u * Mathf.Cos(beta) + v * Mathf.Sin(beta)) * Mathf.Sin(alpha);
    }

    private static Vector2 ShellUV(Vector3 d) =>
        new(0.5f - Mathf.Atan2(d.X, -d.Z) / Mathf.Tau, Mathf.Acos(Mathf.Clamp(d.Y, -1f, 1f)) / Mathf.Pi);

    private static ArrayMesh BuildShell(bool outer)
    {
        const int rings = 18, seg = 56;
        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);
        float scale = outer ? 1f : Inset;
        void V(int r, int k)
        {
            Vector3 d = ShellDir(RimAngle * r / rings, Mathf.Tau * k / seg);
            Vector3 p = d * ShellAxes * scale;
            Vector3 n = new Vector3(d.X / ShellAxes.X, d.Y / ShellAxes.Y, d.Z / ShellAxes.Z).Normalized();
            st.SetNormal(outer ? n : -n);
            st.SetUV(ShellUV(d));
            st.AddVertex(p);
        }
        for (int r = 0; r < rings; r++)
        {
            for (int k = 0; k < seg; k++)
            {
                V(r, k); V(r + 1, k); V(r, k + 1);
                V(r, k + 1); V(r + 1, k); V(r + 1, k + 1);
            }
        }
        if (outer)
        {
            // Borda: liga a casca de fora com a de dentro (dá espessura à concha).
            for (int k = 0; k < seg; k++)
            {
                Vector3 d0 = ShellDir(RimAngle, Mathf.Tau * k / seg), d1 = ShellDir(RimAngle, Mathf.Tau * (k + 1) / seg);
                Vector3 o0 = d0 * ShellAxes, o1 = d1 * ShellAxes, i0 = o0 * Inset, i1 = o1 * Inset;
                Vector2 uv0 = ShellUV(d0), uv1 = ShellUV(d1);
                foreach ((Vector3 p, Vector2 uv) in new[] { (o0, uv0), (i0, uv0), (o1, uv1), (o1, uv1), (i0, uv0), (i1, uv1) })
                {
                    st.SetNormal(OpenDir);
                    st.SetUV(uv);
                    st.AddVertex(p);
                }
            }
        }
        return st.Commit();
    }

    private static StandardMaterial3D OuterMaterial(PhoneBrand brand, string color)
    {
        // Nome do arquivo sem acento (phone_shell_telefobica_lime.png); BrandName tem "Telefóbica" com acento.
        string key = $"{brand.ToString().ToLowerInvariant()}_{color}";
        if (Outer.TryGetValue(key, out StandardMaterial3D m))
            return m;
        string path = $"res://Assets/Textures/phone_shell_{key}.png";
        m = new StandardMaterial3D { AlbedoColor = Colors.White, Roughness = 0.35f, CullMode = BaseMaterial3D.CullModeEnum.Disabled };
        if (ResourceLoader.Exists(path))
            m.AlbedoTexture = GD.Load<Texture2D>(path);
        else
            m.AlbedoColor = BrandColor(brand);
        Outer[key] = m;
        return m;
    }

    private static StandardMaterial3D InnerMaterial() =>
        _inner ??= new StandardMaterial3D { AlbedoColor = new Color(0.72f, 0.72f, 0.7f), Roughness = 0.8f, CullMode = BaseMaterial3D.CullModeEnum.Disabled };

    public override void _Process(double delta)
    {
        if (_cooldown > 0)
            _cooldown -= delta;
    }

    /// <summary>Cor da marca (minimapa e mensagens).</summary>
    public static Color BrandColor(PhoneBrand brand) => brand switch
    {
        PhoneBrand.Morto => new Color(0.42f, 0.12f, 0.58f),
        PhoneBrand.Telefobica => new Color(0.78f, 0.87f, 0.16f),
        _ => new Color(0.16f, 0.73f, 0.84f),
    };

    public static string BrandName(PhoneBrand brand) => brand switch
    {
        PhoneBrand.Morto => "morto",
        PhoneBrand.Telefobica => "Telefóbica",
        _ => "tchau",
    };

    /// <summary>
    /// Sorteio estável de operadora e cor (pelo número dado). Telefóbica ~7%; tchau ~55% (roxo raro); morto ~38%.
    /// </summary>
    public static (PhoneBrand brand, string color) Pick(int hash)
    {
        hash = Mathf.Abs(hash);
        int roll = hash % 100;
        int sub = (hash / 100) % 100;
        if (roll < 7)
            return (PhoneBrand.Telefobica, "lime");
        if (roll < 62)
            return (PhoneBrand.Tchau, sub < 12 ? "purple" : "cyan");
        return (PhoneBrand.Morto, sub < 40 ? "purple" : sub < 70 ? "orange" : "green");
    }

    public string GetInteractionPrompt(PlayerController player) =>
        _cooldown > 0 ? "Orelhão (ocupado...)" : Disabled ? "[E] Tentar o orelhão da Telefóbica (desativado)" : $"[E] Usar o orelhão da {BrandName(Brand)}";

    public bool CanInteract(PlayerController player) => _cooldown <= 0;

    public void Interact(PlayerController player)
    {
        _cooldown = 6.0;
        if (Disabled)
        {
            GameHud.Toast(this, "ORELHÃO TELEFÓBICA: " + DeadLineCalls[Rng.RandiRange(0, DeadLineCalls.Length - 1)], new Color(0.8f, 0.9f, 0.4f), 5.0);
            return;
        }
        _sound.Play();
        string message = Rng.Randf() < 0.45f ? DeliveryTip(player) : null;
        if (message == null)
        {
            // Metade das ligações "normais" tem a cara da operadora.
            string[] pool = Rng.Randf() < 0.5f ? (Brand == PhoneBrand.Morto ? MortoCalls : TchauCalls) : StrangeCalls;
            message = pool[Rng.RandiRange(0, pool.Length - 1)];
        }
        // Mostra depois do "tu... tu..." da ligação.
        GetTree().CreateTimer(1.6).Timeout += () =>
        {
            if (IsInsideTree())
                GameHud.Toast(this, $"ORELHÃO {BrandName(Brand).ToUpperInvariant()}: " + message,
                    Brand == PhoneBrand.Morto ? new Color(0.85f, 0.6f, 1f) : new Color(0.5f, 0.9f, 1f), 6.0);
        };
    }

    /// <summary>A central informa a entrega pendente mais próxima de quem ligou.</summary>
    private string DeliveryTip(PlayerController player)
    {
        if (GetTree().GetFirstNodeInGroup("delivery_manager") is not DeliveryManager delivery || delivery.Jobs.Count == 0)
            return null;
        DeliveryJob best = null;
        float bestDistance = float.MaxValue;
        foreach (DeliveryJob job in delivery.Jobs)
        {
            float d = player.GlobalPosition.DistanceTo(job.Point.Position);
            if (d < bestDistance)
            {
                bestDistance = d;
                best = job;
            }
        }
        return best == null ? null : $"Central: entrega de {best.CargoName} na {best.Point.Address} ({bestDistance:0} m daqui).";
    }
}
