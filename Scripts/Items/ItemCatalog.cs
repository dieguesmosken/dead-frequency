// Scripts/Items/ItemCatalog.cs
// Todos os itens do jogo. Os preços precisam bater com SHOP_PRICES em server/src/index.ts
// (o servidor confere o preço antes de descontar o dinheiro do grupo).
using System.Collections.Generic;
using Godot;

namespace DeadFrequency;

public enum ItemSlot
{
    None,   // consumível: fica na mochila até usar
    Head,   // cabeça
    Hands,  // mãos
    Back,   // costas
    Wrist,  // pulso
    Feet,   // pés (v0.9.5)
}

public sealed class ItemDef
{
    public string Id = "";
    public string Name = "";
    public string Description = "";
    public int Price;
    public ItemSlot Slot;
    public Color Color = Colors.White;
    /// <summary>Não aparece nas lojas (ex.: chinelo arrebentado).</summary>
    public bool Hidden;
    /// <summary>Comida e bebida (o Churrasquinho do Duvidoso só vende estes).</summary>
    public bool Food;
    /// <summary>Carro da concessionária (v0.9.7): não vai para a mochila, fica no pátio (CarGarage).</summary>
    public bool Car;

    public bool IsGear => Slot != ItemSlot.None;
}

public static class ItemCatalog
{
    public const string Radar = "radar";
    public const string NightVision = "nightvision";
    public const string Gloves = "gloves";
    public const string Backpack = "backpack";
    public const string RepairKit = "repairkit";
    public const string DuctTape = "tape";
    public const string EnergyDrink = "energy";
    // v0.9.5 — Calçada viva
    public const string Guarana = "guarana";
    public const string Coffee = "cafe";
    public const string Marmitex = "marmitex";
    public const string Skewer = "espetinho";
    public const string Slippers = "chinelos";
    public const string SlippersBroken = "chinelos_quebrados";
    public const string Nail = "prego";
    // v0.9.9 — Lã de Aço 1001 Utilidades
    public const string SteelWool = "palha_aco";
    public const string CarGou = "car_gou";
    public const string CarFusca = "car_fusca";
    public const string CarOpalinha = "car_opalinha";

    public static readonly IReadOnlyList<ItemDef> All = new[]
    {
        new ItemDef { Id = Radar, Name = "Radar de pulso", Price = 400, Slot = ItemSlot.Wrist, Color = new Color(0.3f, 0.9f, 0.45f),
            Description = "Mostra um minimapa com ruas, entregas, o depósito, a barraca e o ônibus." },
        new ItemDef { Id = NightVision, Name = "Óculos de visão noturna", Price = 350, Slot = ItemSlot.Head, Color = new Color(0.2f, 0.75f, 0.3f),
            Description = "Aperte N para ligar. Enxerga no escuro (tudo fica verde)." },
        new ItemDef { Id = Gloves, Name = "Luvas reforçadas", Price = 250, Slot = ItemSlot.Hands, Color = new Color(0.95f, 0.55f, 0.15f),
            Description = "Carrega até 60 kg, arremessa mais forte e a caixa na mão sofre 60% menos dano." },
        new ItemDef { Id = Backpack, Name = "Mochila de entregador", Price = 200, Slot = ItemSlot.Back, Color = new Color(0.85f, 0.2f, 0.15f),
            Description = "Aumenta o inventário de 4 para 10 espaços." },
        new ItemDef { Id = RepairKit, Name = "Kit de reparo", Price = 150, Slot = ItemSlot.None, Color = new Color(0.9f, 0.9f, 0.9f),
            Description = "Usar segurando uma caixa: recupera 40% da integridade." },
        new ItemDef { Id = DuctTape, Name = "Fita adesiva", Price = 50, Slot = ItemSlot.None, Color = new Color(0.6f, 0.6f, 0.65f),
            Description = "Usar segurando uma caixa: recupera 20% da integridade." },
        new ItemDef { Id = EnergyDrink, Name = "Energético Frequência", Price = 60, Slot = ItemSlot.None, Color = new Color(0.2f, 0.6f, 1f),
            Description = "Corre 35% mais rápido por 60 segundos." },
        new ItemDef { Id = Guarana, Name = "Guaraná Pinguim", Price = 15, Slot = ItemSlot.None, Color = new Color(0.2f, 0.75f, 0.25f), Food = true,
            Description = "Latinha verde gelada: recupera o fôlego na hora e ele volta mais rápido por 45 segundos." },
        new ItemDef { Id = Coffee, Name = "Café Marreta", Price = 25, Slot = ItemSlot.None, Color = new Color(0.45f, 0.25f, 0.12f), Food = true,
            Description = "Garrafa térmica de café extraforte: corta a leseira da madrugada na hora e segura o sono por 4 minutos." },
        new ItemDef { Id = Marmitex, Name = "Marmitex Tia da Quentinha", Price = 35, Slot = ItemSlot.None, Color = new Color(0.95f, 0.95f, 0.9f), Food = true,
            Description = "Prato feito no isopor amassado: enche todo o fôlego, mas dá 10 segundos de lerdeza pós-almoço." },
        new ItemDef { Id = Skewer, Name = "Espetinho do Duvidoso", Price = 12, Slot = ItemSlot.None, Color = new Color(0.7f, 0.35f, 0.15f), Food = true,
            Description = "Use perto de um caramelo e ele vira seu companheiro por 3 minutos. Sem cachorro por perto, você mesmo come (+fôlego)." },
        new ItemDef { Id = Slippers, Name = "Chinelos Legítimas", Price = 80, Slot = ItemSlot.Feet, Color = new Color(0.2f, 0.45f, 0.85f),
            Description = "Anda 12% mais rápido, com o ploc-ploc. Correndo na lama do sítio (ou às vezes no asfalto) a tira pode arrebentar: conserte com um prego." },
        new ItemDef { Id = SlippersBroken, Name = "Chinelo arrebentado", Price = 0, Slot = ItemSlot.Feet, Color = new Color(0.35f, 0.4f, 0.5f), Hidden = true,
            Description = "A tira soltou: você anda pisando torto (mais devagar). Use um Prego para consertar." },
        new ItemDef { Id = Nail, Name = "Prego", Price = 10, Slot = ItemSlot.None, Color = new Color(0.7f, 0.7f, 0.72f),
            Description = "Conserta a tira do chinelo arrebentado (precisa estar calçado)." },
        new ItemDef { Id = SteelWool, Name = "Palha de aço Brilhaço", Price = 8, Slot = ItemSlot.None, Color = new Color(0.78f, 0.74f, 0.66f),
            Description = "Da Lã de Aço 1001 Utilidades. Use perto da TV do apartamento: enrolada na antena, acaba com o chuvisco da imagem." },
        // v0.9.7 — Auto Center Dois Mil (preço de lançamento; depois sobe).
        new ItemDef { Id = CarGou, Name = "Gou 1.0 quadrado", Price = 2000, Slot = ItemSlot.None, Color = new Color(0.35f, 0.55f, 0.95f), Car = true,
            Description = "Folksvagem 1.0, econômico e valente. Fica no pátio da concessionária para todo mundo da sala dirigir." },
        new ItemDef { Id = CarFusca, Name = "Fuska 1300", Price = 2000, Slot = ItemSlot.None, Color = new Color(0.9f, 0.85f, 0.6f), Car = true,
            Description = "O besouro de sempre: motor atrás, barulhinho inconfundível. Fica no pátio da concessionária." },
        new ItemDef { Id = CarOpalinha, Name = "Opalinha 4.1", Price = 2000, Slot = ItemSlot.None, Color = new Color(0.75f, 0.2f, 0.2f), Car = true,
            Description = "Seis cilindros, banco de couro e muita estrada. Fica no pátio da concessionária." },
    };

    private static Dictionary<string, ItemDef> _byId;

    public static ItemDef Get(string id)
    {
        _byId ??= BuildIndex();
        return id != null && _byId.TryGetValue(id, out ItemDef def) ? def : null;
    }

    private static Dictionary<string, ItemDef> BuildIndex()
    {
        var index = new Dictionary<string, ItemDef>();
        foreach (ItemDef def in All)
            index[def.Id] = def;
        return index;
    }

    public static string SlotName(ItemSlot slot) => slot switch
    {
        ItemSlot.Head => "Cabeça",
        ItemSlot.Hands => "Mãos",
        ItemSlot.Back => "Costas",
        ItemSlot.Wrist => "Pulso",
        ItemSlot.Feet => "Pés",
        _ => "Consumível",
    };
}
