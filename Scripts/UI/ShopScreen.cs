// Scripts/UI/ShopScreen.cs
// Loja do comerciante de rua. Solo: paga com a carteira. Online: o servidor desconta do dinheiro do grupo.
using System.Linq;
using Godot;

namespace DeadFrequency;

/// <summary>O que cada vendedor tem (v0.9.7): cada item só aparece em quem vende ele.</summary>
public enum ShopStock { Ze, Churrasco, Market, Dealer }

public partial class ShopScreen : ModalScreen
{
    /// <summary>Abas da loja: equipamento, consertos e comida/bebida.</summary>
    private enum Tab { Gear, Repair, Food, Cars, Sell }

    private static readonly string[] TabNames = { "Equipamentos", "Consertos", "Comida e bebida", "Carros", "Vender" };

    private static Tab TabOf(ItemDef def) => def.Car ? Tab.Cars : def.IsGear ? Tab.Gear : def.Food || def.Id == ItemCatalog.EnergyDrink ? Tab.Food : Tab.Repair;

    /// <summary>Barraca do Zé: equipamento e conserto. Churrasquinho: comida. Atacadinho: comida, bebida
    /// e o básico de conserto (fita e prego), mas nada de equipamento.</summary>
    private static bool Sells(ShopStock stock, ItemDef def) => def.Car ? stock == ShopStock.Dealer : stock switch
    {
        ShopStock.Dealer => false,
        ShopStock.Churrasco => def.Id is ItemCatalog.Skewer or ItemCatalog.Guarana,
        ShopStock.Market => TabOf(def) == Tab.Food && def.Id != ItemCatalog.Skewer || def.Id is ItemCatalog.DuctTape or ItemCatalog.Nail,
        _ => TabOf(def) != Tab.Food || def.Id == ItemCatalog.Guarana,
    };

    private ShopStock _stock;
    private Tab _tab;
    private bool _salePending;
    private HBoxContainer _tabs;
    private Label _tagline;

    [Export] public DeliveryManager Delivery { get; set; }

    public static ShopScreen Instance { get; private set; }

    private Label _title;
    private Label _money;
    private Label _feedback;
    private VBoxContainer _list;
    private AudioStreamPlayer _buySound;

    public override void _EnterTree() => Instance = this;

    protected override void BuildUi()
    {
        PanelContainer panel = CenteredPanel(new Vector2(820, 0));
        var stack = new VBoxContainer();
        stack.AddThemeConstantOverride("separation", 10);
        panel.AddChild(stack);

        _title = UiStyle.MakeLabel("BARRACA", 28);
        stack.AddChild(_title);
        _tagline = UiStyle.MakeLabel("", 15, color: UiStyle.PhosphorDim);
        stack.AddChild(_tagline);
        _tabs = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        _tabs.AddThemeConstantOverride("separation", 8);
        stack.AddChild(_tabs);
        // Lista rola quando passa da tela (antes os itens ficavam cortados embaixo).
        var scroll = new ScrollContainer { CustomMinimumSize = new Vector2(0, 430), HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        stack.AddChild(scroll);
        _list = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _list.AddThemeConstantOverride("separation", 6);
        scroll.AddChild(_list);

        _feedback = UiStyle.MakeLabel("", 16, color: UiStyle.Warning);
        stack.AddChild(_feedback);
        _money = UiStyle.MakeLabel("", 22);
        stack.AddChild(_money);
        Button close = UiStyle.MakeButton("Fechar", Close, 220, UiSound.Back);
        close.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
        stack.AddChild(close);

        _buySound = new AudioStreamPlayer { Bus = AudioManager.UiBus };
        if (ResourceLoader.Exists("res://Audio/SFX/buy.wav"))
            _buySound.Stream = GD.Load<AudioStream>("res://Audio/SFX/buy.wav");
        AddChild(_buySound);

        if (PlayerInventory.Instance != null)
            PlayerInventory.Instance.Changed += Refresh;
    }

    public override void _ExitTree()
    {
        if (PlayerInventory.Instance != null)
            PlayerInventory.Instance.Changed -= Refresh;
        if (Instance == this)
            Instance = null;
        base._ExitTree();
    }

    public void OpenFor(string vendorName, ShopStock stock = ShopStock.Ze)
    {
        _stock = stock;
        _salePending = false;
        _tab = stock switch { ShopStock.Ze => Tab.Gear, ShopStock.Dealer => Tab.Cars, _ => Tab.Food };
        _title.Text = vendorName.ToUpperInvariant();
        _tagline.Text = stock switch
        {
            ShopStock.Churrasco => "\"Procedência? Confia.\"",
            ShopStock.Market => "\"Leve 3, pague 3.\"",
            ShopStock.Dealer => Online ? "\"Carro comprado com o dinheiro do grupo é da sala toda.\"" : "\"Dono único, só ia na missa.\"",
            _ => "\"Tudo testado, tudo funcionando. Quase tudo.\"",
        };
        _feedback.Text = "";
        Open();
    }

    protected override void OnOpened() => Refresh();

    public override void _Process(double delta)
    {
        // Online o dinheiro do grupo pode mudar enquanto a loja está aberta.
        if (IsOpen)
            _money.Text = (Online ? "Dinheiro do grupo: $ " : "Carteira: $ ") + (Delivery?.Money ?? 0);
    }

    private void Refresh()
    {
        if (_list == null)
            return;
        foreach (Node child in _list.GetChildren())
            child.QueueFree();
        foreach (Node child in _tabs.GetChildren())
            child.QueueFree();

        // Abas: só as que este vendedor tem; com uma só, nem mostra.
        var tabs = ItemCatalog.All.Where(d => !d.Hidden && Sells(_stock, d)).Select(TabOf).Distinct().OrderBy(t => t).ToList();
        if (_stock == ShopStock.Ze)
            tabs.Add(Tab.Sell);
        if (!tabs.Contains(_tab) && tabs.Count > 0)
            _tab = tabs[0];
        if (tabs.Count > 1)
        {
            foreach (Tab t in tabs)
            {
                Tab tab = t;
                Button b = UiStyle.MakeButton(t == _tab ? $"[ {TabNames[(int)t]} ]" : TabNames[(int)t], () => { _tab = tab; Refresh(); }, 220);
                b.Disabled = t == _tab;
                _tabs.AddChild(b);
            }
        }

        if (_tab == Tab.Sell)
        {
            RefreshSellList();
            return;
        }

        foreach (ItemDef def in ItemCatalog.All)
        {
            if (def.Hidden || !Sells(_stock, def) || TabOf(def) != _tab)
                continue;
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 12);

            var info = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            Label name = UiStyle.MakeLabel(def.Name + (def.IsGear ? $"  [{ItemCatalog.SlotName(def.Slot)}]" : ""), 18, HorizontalAlignment.Left, def.Color);
            Label desc = UiStyle.MakeLabel(def.Description, 13, HorizontalAlignment.Left, UiStyle.PhosphorDim);
            desc.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            desc.CustomMinimumSize = new Vector2(520, 0);
            info.AddChild(name);
            info.AddChild(desc);
            row.AddChild(info);

            bool owned = def.Car ? CarGarage.Owns(def.Id) : def.IsGear && (PlayerInventory.Instance?.IsEquipped(def.Id) == true || (PlayerInventory.Instance?.Items.Contains(def.Id) ?? false));
            string id = def.Id;
            Button buy = UiStyle.MakeButton(owned ? "Já tem" : $"$ {def.Price}", () => Buy(id), 120);
            buy.Disabled = owned;
            row.AddChild(buy);
            _list.AddChild(row);
        }
    }

    private void RefreshSellList()
    {
        PlayerInventory inv = PlayerInventory.Instance;
        if (inv == null || inv.Items.Count == 0)
        {
            _list.AddChild(UiStyle.MakeLabel("A mochila está vazia.", 17, color: UiStyle.PhosphorDim));
            return;
        }

        bool hasSellableItem = false;
        foreach (string itemId in inv.Items)
        {
            ItemDef def = ItemCatalog.Get(itemId);
            if (def == null || def.Hidden || def.Car)
                continue;

            int price = def.Price / 2;
            if (price <= 0)
                continue;
            hasSellableItem = true;
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 12);
            Label name = UiStyle.MakeLabel(def.Name, 18, HorizontalAlignment.Left, def.Color);
            name.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            row.AddChild(name);
            Button sell = UiStyle.MakeButton($"+ $ {price}", () => Sell(itemId), 120);
            sell.Disabled = _salePending;
            row.AddChild(sell);
            _list.AddChild(row);
        }
        if (!hasSellableItem)
            _list.AddChild(UiStyle.MakeLabel("Não há itens disponíveis para venda.", 17, color: UiStyle.PhosphorDim));
    }

    private void Sell(string itemId)
    {
        PlayerInventory inv = PlayerInventory.Instance;
        ItemDef def = ItemCatalog.Get(itemId);
        if (_salePending || _stock != ShopStock.Ze || inv == null || Delivery == null
            || def == null || def.Hidden || def.Car || def.Price <= 0 || !inv.Items.Contains(itemId))
            return;

        int expectedPrice = def.Price / 2;
        if (Online)
        {
            if (NetSync.Instance == null)
            {
                _feedback.Text = "Não foi possível contatar o servidor.";
                return;
            }
            _salePending = true;
            _feedback.Text = "Vendendo...";
            Refresh();
            NetSync.Instance.RequestSale(itemId, (ok, price) => CompleteSale(itemId, ok, price));
            return;
        }

        if (!Delivery.TryCreditLocal(expectedPrice))
        {
            _feedback.Text = "Não foi possível receber o pagamento da venda.";
            Refresh();
            return;
        }
        if (!inv.TryRemoveItem(itemId))
        {
            if (!Delivery.TrySpendLocal(expectedPrice))
                GD.PushError("ShopScreen: não foi possível estornar uma venda local sem item.");
            _feedback.Text = "Esse item não está mais na mochila.";
            Refresh();
            return;
        }
        _feedback.Text = $"{def.Name} vendido por $ {expectedPrice}.";
        Refresh();
    }

    private void CompleteSale(string itemId, bool ok, int price)
    {
        _salePending = false;
        if (!IsInsideTree())
            return;
        if (!ok)
        {
            _feedback.Text = "O servidor recusou a venda.";
            Refresh();
            return;
        }
        if (price <= 0)
        {
            _feedback.Text = "O servidor retornou um valor de venda inválido.";
            Refresh();
            return;
        }
        // O servidor já removeu o item e enviou o inventário atualizado em "itemsState".
        _feedback.Text = $"{ItemCatalog.Get(itemId)?.Name ?? "Item"} vendido por $ {price}.";
        Refresh();
    }

    private bool HasSpaceFor(ItemDef def)
    {
        PlayerInventory inv = PlayerInventory.Instance;
        if (inv == null)
            return false;
        if (def.Car)
            return !CarGarage.Owns(def.Id);
        if (def.IsGear && !inv.Equipped.ContainsKey(def.Slot))
            return true;
        return !inv.IsFull;
    }

    private void Buy(string itemId)
    {
        ItemDef def = ItemCatalog.Get(itemId);
        if (def == null || Delivery == null)
            return;
        if (!HasSpaceFor(def))
        {
            _feedback.Text = def.Car ? "Esse carro já é seu." : "Mochila cheia. Descarte algo ou compre a mochila maior.";
            return;
        }
        if (Delivery.Money < def.Price)
        {
            _feedback.Text = "Dinheiro insuficiente.";
            return;
        }

        if (Online)
        {
            if (NetSync.Instance == null)
                return;
            _feedback.Text = "Comprando...";
            NetSync.Instance.RequestPurchase(itemId, ok => Delivered(def, ok));
            return;
        }

        if (!Delivery.TrySpendLocal(def.Price))
        {
            _feedback.Text = "Dinheiro insuficiente.";
            return;
        }
        Delivered(def, true);
    }

    private void Delivered(ItemDef def, bool ok)
    {
        if (!ok)
        {
            _feedback.Text = "O servidor recusou a compra (dinheiro do grupo insuficiente?).";
            return;
        }
        _buySound.Play();
        if (def.Car)
        {
            CarGarage.MarkBought(def.Id);
            _feedback.Text = $"{def.Name} é seu! Está no pátio da concessionária, chave na porta.";
            Refresh();
            return;
        }
        // No multiplayer o servidor já adicionou/equipou o item e enviou "itemsState".
        if (!Online)
            PlayerInventory.Instance?.TryAdd(def.Id);
        _feedback.Text = def.IsGear ? $"{def.Name} comprado e equipado (veja no inventário: I)." : $"{def.Name} foi para a mochila.";
        Refresh();
    }
}
