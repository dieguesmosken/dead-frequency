// Scripts/Items/PlayerInventory.cs
// Autoload (registrado no project.godot como "PlayerInventory").
// Mochila (itens guardados), equipamentos por slot e a carteira do modo solo.
// Salvo em user://inventory.json, então os itens continuam entre partidas (solo e online).
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Godot;

namespace DeadFrequency;

public partial class PlayerInventory : Node
{
    public const int BaseCapacity = 4;
    public const int BackpackCapacity = 10;
    private const string SavePath = "user://inventory.json";

    public static PlayerInventory Instance { get; private set; }

    /// <summary>Qualquer mudança (itens, equipamento ou dinheiro).</summary>
    public event Action Changed;

    public IReadOnlyList<string> Items => _items;
    public IReadOnlyDictionary<ItemSlot, string> Equipped => _equipped;
    public int Wallet { get; private set; }
    public int Capacity => IsEquipped(ItemCatalog.Backpack) ? BackpackCapacity : BaseCapacity;
    public bool IsFull => _items.Count >= Capacity;
    public int ItemCount => _items.Count;
    public int SelectedHotbarIndex { get; private set; } = -1;
    public string SelectedHotbarItemId =>
        SelectedHotbarIndex >= 0 && SelectedHotbarIndex < _items.Count ? _items[SelectedHotbarIndex] : null;

    private readonly List<string> _items = new();
    private readonly Dictionary<ItemSlot, string> _equipped = new();

    // O inventário local salvo em disco fica separado do inventário temporário autoritativo online.
    private bool _onlineSessionActive;
    private List<string> _offlineItems;
    private Dictionary<ItemSlot, string> _offlineEquipped;
    private int _offlineSelectedHotbarIndex = -1;

    public override void _EnterTree()
    {
        Instance = this;
        ProcessMode = ProcessModeEnum.Always;
    }

    public override void _ExitTree()
    {
        if (Instance == this)
            Instance = null;
    }

    public override void _Ready() => Load();

    public bool IsEquipped(string itemId) => _equipped.Values.Contains(itemId);

    public string HotbarItem(int index) => index >= 0 && index < 4 && index < _items.Count ? _items[index] : null;

    public bool SelectHotbar(int index)
    {
        if (index < 0 || index >= 4 || index >= _items.Count)
        {
            ClearHotbarSelection();
            return false;
        }
        if (SelectedHotbarIndex == index)
            return true;
        SelectedHotbarIndex = index;
        Changed?.Invoke();
        return true;
    }

    public void ClearHotbarSelection()
    {
        if (SelectedHotbarIndex < 0)
            return;
        SelectedHotbarIndex = -1;
        Changed?.Invoke();
    }

    public bool CanAddToBag(string itemId) =>
        ItemCatalog.Get(itemId) is { Car: false } && !IsFull;

    public bool TryTakeSelected(out string itemId)
    {
        itemId = SelectedHotbarItemId;
        if (itemId == null)
            return false;
        RemoveAt(SelectedHotbarIndex);
        return true;
    }

    public string UseSelected(PlayerController player)
    {
        int index = SelectedHotbarIndex;
        if (index < 0 || index >= _items.Count || player == null)
            return "";
        ItemDef def = ItemCatalog.Get(_items[index]);
        if (def == null)
            return "";
        if (def.IsGear)
        {
            SelectedHotbarIndex = -1;
            bool equipped = Equip(index);
            return equipped ? $"{def.Name} equipado." : "Não deu para equipar.";
        }
        string result = Use(index, player);
        if (!string.IsNullOrEmpty(result))
            SelectedHotbarIndex = -1;
        return result;
    }

    /// <summary>Preserva o inventário offline enquanto a sessão online usa o estado enviado pelo servidor.</summary>
    public void BeginOnlineSession()
    {
        if (_onlineSessionActive)
            return;
        _offlineItems = new List<string>(_items);
        _offlineEquipped = new Dictionary<ItemSlot, string>(_equipped);
        _offlineSelectedHotbarIndex = SelectedHotbarIndex;
        _onlineSessionActive = true;
    }

    public void EndOnlineSession()
    {
        if (!_onlineSessionActive)
            return;
        _items.Clear();
        _items.AddRange(_offlineItems ?? new List<string>());
        _equipped.Clear();
        if (_offlineEquipped != null)
            foreach (var pair in _offlineEquipped)
                _equipped[pair.Key] = pair.Value;
        SelectedHotbarIndex = _offlineSelectedHotbarIndex >= 0 && _offlineSelectedHotbarIndex < _items.Count
            ? _offlineSelectedHotbarIndex
            : -1;
        _offlineItems = null;
        _offlineEquipped = null;
        _offlineSelectedHotbarIndex = -1;
        _onlineSessionActive = false;
        Changed?.Invoke();
    }

    public void ApplyNetworkItems(IEnumerable<string> items) => ApplyNetworkState(items, EquipmentCsv());

    public void ApplyNetworkState(IEnumerable<string> items, string equipmentCsv)
    {
        string selectedItemId = SelectedHotbarItemId;
        _items.Clear();
        foreach (string id in items)
        {
            if (ItemCatalog.Get(id) == null || ItemCatalog.Get(id).Car)
                continue;
            _items.Add(id);
            if (_items.Count >= BackpackCapacity)
                break;
        }

        _equipped.Clear();
        if (!string.IsNullOrWhiteSpace(equipmentCsv))
        {
            foreach (string id in equipmentCsv.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                ItemDef def = ItemCatalog.Get(id);
                if (def == null || !def.IsGear || _equipped.ContainsKey(def.Slot))
                    continue;
                _equipped[def.Slot] = id;
            }
        }

        // Reaplica o limite depois de saber se a mochila veio equipada.
        int capacity = IsEquipped(ItemCatalog.Backpack) ? BackpackCapacity : BaseCapacity;
        if (_items.Count > capacity)
            _items.RemoveRange(capacity, _items.Count - capacity);
        SelectedHotbarIndex = string.IsNullOrEmpty(selectedItemId) ? -1 : _items.IndexOf(selectedItemId);
        if (SelectedHotbarIndex >= 4)
            SelectedHotbarIndex = -1;
        NotifyAndSave();
    }

    /// <summary>Lista curta para mandar pela rede ("radar,gloves").</summary>
    public string EquipmentCsv() => string.Join(",", _equipped.Values.OrderBy(v => v));

    public void SetWallet(int value)
    {
        Wallet = Math.Max(0, value);
        NotifyAndSave();
    }

    /// <summary>Adiciona um item. Equipamento vai direto para o corpo se o slot estiver livre.</summary>
    public bool TryAdd(string itemId)
    {
        ItemDef def = ItemCatalog.Get(itemId);
        if (def == null)
            return false;
        if (def.IsGear && !_equipped.ContainsKey(def.Slot))
        {
            _equipped[def.Slot] = itemId;
            NotifyAndSave();
            return true;
        }
        if (IsFull)
            return false;
        _items.Add(itemId);
        NotifyAndSave();
        return true;
    }

    /// <summary>Equipa o item da mochila; o que estava no slot volta para a mochila.</summary>
    public bool Equip(int index)
    {
        if (index < 0 || index >= _items.Count)
            return false;
        ItemDef def = ItemCatalog.Get(_items[index]);
        if (def == null || !def.IsGear)
            return false;
        _items.RemoveAt(index);
        if (_equipped.TryGetValue(def.Slot, out string previous))
            _items.Insert(index, previous);
        _equipped[def.Slot] = def.Id;
        NotifyAndSave();
        return true;
    }

    public bool Unequip(ItemSlot slot)
    {
        if (!_equipped.TryGetValue(slot, out string id))
            return false;
        // Tirar a mochila diminui o espaço: só se o que está guardado couber.
        int capacityAfter = id == ItemCatalog.Backpack ? BaseCapacity : Capacity;
        if (_items.Count + 1 > capacityAfter)
            return false;
        _equipped.Remove(slot);
        _items.Add(id);
        NotifyAndSave();
        return true;
    }

    public void RemoveAt(int index)
    {
        if (index < 0 || index >= _items.Count)
            return;
        _items.RemoveAt(index);
        if (SelectedHotbarIndex == index)
            SelectedHotbarIndex = -1;
        else if (SelectedHotbarIndex > index)
            SelectedHotbarIndex--;
        NotifyAndSave();
    }

    public bool TryRemoveItem(string itemId)
    {
        int index = _items.IndexOf(itemId);
        if (index < 0)
            return false;
        RemoveAt(index);
        return true;
    }

    /// <summary>Usa um consumível. Retorna a mensagem para o HUD (vazia = não usou).</summary>
    public string Use(int index, PlayerController player)
    {
        if (index < 0 || index >= _items.Count || player == null)
            return "";
        string id = _items[index];
        string result = id switch
        {
            ItemCatalog.RepairKit => RepairHeld(player, 0.4f),
            ItemCatalog.DuctTape => RepairHeld(player, 0.2f),
            ItemCatalog.EnergyDrink => player.ApplySpeedBoost(60f) ? "Energia! Correndo mais rápido por 60 s." : "",
            ItemCatalog.Guarana => player.DrinkGuarana(),
            ItemCatalog.Coffee => player.DrinkCoffee(),
            ItemCatalog.Marmitex => player.EatMarmitex(),
            ItemCatalog.Skewer => player.UseSkewer(),
            ItemCatalog.Nail => FixSlippers(),
            _ => "",
        };
        if (!string.IsNullOrEmpty(result))
            RemoveAt(index);
        return result;
    }

    /// <summary>Prego: troca o chinelo arrebentado (calçado) pelo inteiro.</summary>
    private string FixSlippers()
    {
        if (!_equipped.TryGetValue(ItemSlot.Feet, out string feet) || feet != ItemCatalog.SlippersBroken)
            return "";
        _equipped[ItemSlot.Feet] = ItemCatalog.Slippers;
        NotifyAndSave();
        return "Tira do chinelo pregada. Ploc-ploc de novo!";
    }

    /// <summary>A tira arrebentou: o chinelo calçado vira o arrebentado.</summary>
    public bool BreakSlippers()
    {
        if (!_equipped.TryGetValue(ItemSlot.Feet, out string feet) || feet != ItemCatalog.Slippers)
            return false;
        _equipped[ItemSlot.Feet] = ItemCatalog.SlippersBroken;
        NotifyAndSave();
        return true;
    }

    private static string RepairHeld(PlayerController player, float amount)
    {
        CargoItem held = player.HeldItem;
        if (held == null)
            return "";
        held.Repair(amount);
        return $"{held.Data?.CargoName ?? "Caixa"} reparada: {held.Integrity:P0}";
    }

    // ---------------- Disco ----------------

    private sealed class SaveData
    {
        public List<string> Items { get; set; } = new();
        public Dictionary<string, string> Equipped { get; set; } = new();
        public int Wallet { get; set; }
        public List<string> Cars { get; set; } = new();
    }

    private readonly List<string> _cars = new();

    /// <summary>Carros comprados no modo solo (online quem guarda é a sala no servidor).</summary>
    public IReadOnlyList<string> Cars => _cars;

    public void AddCar(string id)
    {
        if (_cars.Contains(id))
            return;
        _cars.Add(id);
        NotifyAndSave();
    }

    private void NotifyAndSave()
    {
        Changed?.Invoke();
        Save();
    }

    public void Save()
    {
        // Não misturar itens comprados na sala multiplayer com o save local/offline.
        if (_onlineSessionActive)
            return;
        var data = new SaveData
        {
            Items = new List<string>(_items),
            Equipped = _equipped.ToDictionary(kv => kv.Key.ToString(), kv => kv.Value),
            Wallet = Wallet,
            Cars = new List<string>(_cars),
        };
        using FileAccess file = FileAccess.Open(SavePath, FileAccess.ModeFlags.Write);
        file?.StoreString(JsonSerializer.Serialize(data));
    }

    public void Load()
    {
        if (!FileAccess.FileExists(SavePath))
            return;
        try
        {
            using FileAccess file = FileAccess.Open(SavePath, FileAccess.ModeFlags.Read);
            SaveData data = JsonSerializer.Deserialize<SaveData>(file.GetAsText());
            if (data == null)
                return;
            _items.Clear();
            _items.AddRange(data.Items.Where(id => ItemCatalog.Get(id) != null));
            _equipped.Clear();
            foreach ((string slotName, string id) in data.Equipped)
            {
                if (Enum.TryParse(slotName, out ItemSlot slot) && ItemCatalog.Get(id)?.Slot == slot)
                    _equipped[slot] = id;
            }
            Wallet = Math.Max(0, data.Wallet);
            _cars.Clear();
            if (data.Cars != null)
                _cars.AddRange(data.Cars.Where(id => ItemCatalog.Get(id)?.Car == true));
        }
        catch (Exception e)
        {
            GD.PushWarning($"PlayerInventory: save inválido ({e.Message}).");
        }
        Changed?.Invoke();
    }
}
