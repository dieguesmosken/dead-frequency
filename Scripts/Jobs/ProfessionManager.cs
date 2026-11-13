// Scripts/Jobs/ProfessionManager.cs
// Cidade simulada (v0.9): cada jogador tem uma profissão. Este nó cria a tarefa da profissão atual,
// paga (solo: carteira; online: pede ao servidor com "earn", que confere e soma no dinheiro do grupo)
// e mostra o texto do painel no HUD. Criado pelo DeliveryManager ao carregar a fase.
using System;
using System.Text.Json;
using Godot;

namespace DeadFrequency;

public partial class ProfessionManager : Node3D
{
    public static ProfessionManager Instance { get; private set; }

    public CityGenerator City { get; set; }
    public PlayerController Player { get; set; }
    public DeliveryManager Delivery { get; set; }
    public VanController Van { get; private set; }

    public string Current => Professions.Current;
    public JobBase Job { get; private set; }
    public int SessionEarned { get; private set; }
    public int SessionTasks { get; private set; }

    /// <summary>Avisado quando a profissão muda (HUD, minimapa).</summary>
    public event Action<string> ProfessionChanged;

    private bool IsOnline => NetClient.Instance?.IsOnline == true;

    public override void _EnterTree() => Instance = this;

    public override void _ExitTree()
    {
        if (Instance == this)
            Instance = null;
        if (NetClient.Instance != null)
            NetClient.Instance.MessageReceived -= OnNetMessage;
    }

    public override void _Ready()
    {
        Van = GetTree().CurrentScene?.FindChild("Van", true, false) as VanController;
        Professions.CurrentLevel = City != null && City.Biome == WorldBiome.Farm ? "farm" : "city";
        if (NetClient.Instance != null)
            NetClient.Instance.MessageReceived += OnNetMessage;
        string wanted = Professions.Current;
        if (!IsOnline)
        {
            SaveData save = SaveManager.Load();
            if (save != null && !string.IsNullOrEmpty(save.Profession) && GameSettings.Instance?.Profession == save.Profession)
                wanted = save.Profession;
        }
        _placeOnSpawn = true;
        SetProfession(wanted, announce: true, fromServer: IsOnline);
        if (HomeGuide.Available)
            GetTree().CreateTimer(7.0).Timeout += () =>
            {
                if (IsInstanceValid(this))
                    GameHud.Toast(this, $"Você ganhou um apartamento: {HomeGuide.Address}. No inventário (I) dá para marcar a casa.", new Color(1f, 0.85f, 0.5f), 6.0);
            };
    }

    /// <summary>Na primeira profissão da partida o jogador nasce no local de trabalho dela.</summary>
    private bool _placeOnSpawn;

    /// <summary>Alvo da seta do topo da tela: muda com a profissão (rádio, mercado, posto, passageiro...).</summary>
    public bool TryGetTarget(out Vector3 position, out string label)
    {
        if (HomeGuide.Active && HomeGuide.Available)
        {
            position = HomeGuide.Position;
            label = "CASA";
            if (Player != null && IsInstanceValid(Player) && Player.GlobalPosition.DistanceTo(position) < 5f)
            {
                HomeGuide.Active = false;
                GameHud.Toast(this, "Você chegou em casa! O elevador fica no fundo do saguão.", new Color(1f, 0.85f, 0.5f), 4.0);
            }
            return true;
        }
        if (Job != null && IsInstanceValid(Job) && Job.TryGetTarget(out position, out label))
            return true;
        position = Vector3.Zero;
        label = "DEPÓSITO";
        return true;
    }

    /// <summary>Troca a profissão. Online, avisa o servidor (que pode recusar se a sala sorteia as profissões).</summary>
    public void SetProfession(string id, bool announce = true, bool fromServer = false)
    {
        if (!Professions.AvailableIn(id, Professions.CurrentLevel))
            id = Professions.Entregador;
        if (IsOnline && !fromServer)
        {
            NetClient.Instance.Send(new { t = "prof", prof = id });
            return; // aplica quando o servidor confirmar
        }

        if (Job != null && IsInstanceValid(Job))
            Job.QueueFree();
        Job = null;
        Professions.Current = id;
        if (GameSettings.Instance != null && !IsOnline)
        {
            GameSettings.Instance.Profession = id;
            GameSettings.Instance.Save();
        }

        Job = id switch
        {
            Professions.Taxista => new TaxiJob(),
            Professions.Gari => new GariJob(),
            Professions.Repositor => new RepositorJob(),
            Professions.Frentista => new FrentistaJob(),
            Professions.Locutor => new LocutorJob(),
            _ => null,
        };
        if (Job != null)
        {
            Job.Name = "Job_" + id;
            Job.Setup(this);
            AddChild(Job);
        }
        Delivery?.SetProfessionMode(id == Professions.Entregador);
        if (_placeOnSpawn)
        {
            _placeOnSpawn = false;
            if (Player != null && IsInstanceValid(Player))
            {
                // Prefere o ponto lógico preparado antes da geometria; mantém o Job como fallback
                // durante a migração de profissões que ainda não tenham spawn no WorldData.
                if (City?.UsesLogicalWorldData == true &&
                    City.WorldData != null &&
                    City.WorldData.TryGetProfessionSpawn(id, out WorldSpawnPoint logicalSpawn))
                {
                    Callable.From(() => Player.Teleport(logicalSpawn.Position, logicalSpawn.Yaw)).CallDeferred();
                }
                else if (Job != null && Job.TryGetSpawn(out Vector3 pos, out float yaw))
                {
                    Callable.From(() => Player.Teleport(pos, yaw)).CallDeferred();
                }
            }
        }
        ProfessionChanged?.Invoke(id);
        if (announce)
        {
            Professions.Def def = Professions.Get(id);
            GameHud.Toast(this, $"PROFISSÃO: {def.Name.ToUpperInvariant()} — {def.Task}", def.Color, 6.0);
        }
    }

    /// <summary>
    /// Paga uma tarefa. units = quantas unidades (ex.: sacos de lixo); amount só vale para o taxista (corrida).
    /// Online o servidor confere a profissão, o valor e o intervalo mínimo antes de pagar.
    /// </summary>
    public void Earn(int units, string what, int amount = 0)
    {
        Professions.Def def = Professions.Get(Current);
        int payment = Current == Professions.Taxista ? Mathf.Clamp(amount, 30, 400) : def.Pay * Mathf.Max(1, units);
        if (IsOnline)
        {
            NetClient.Instance.Send(new { t = "earn", job = Current, units = Mathf.Max(1, units), amount = payment, what });
            return;
        }
        PlayerInventory inv = PlayerInventory.Instance;
        inv?.SetWallet(inv.Wallet + payment);
        Credited(payment, what);
    }

    private void Credited(int payment, string what)
    {
        SessionEarned += payment;
        SessionTasks++;
        SaveManager.AddEarned(Current, payment);
        Professions.Def def = Professions.Get(Current);
        GameHud.Toast(this, $"{what}  +$ {payment}", def.Color, 3.5);
        PlayCash();
        JobEvents.RaiseTaskDone(Current, what, payment);
    }

    private void PlayCash()
    {
        if (!ResourceLoader.Exists("res://Audio/SFX/delivery_ok.wav"))
            return;
        var p = new AudioStreamPlayer { Stream = GD.Load<AudioStream>("res://Audio/SFX/delivery_ok.wav"), Bus = AudioManager.SfxBus, VolumeDb = -6f };
        AddChild(p);
        p.Play();
        p.Finished += p.QueueFree;
    }

    private void OnNetMessage(JsonElement m)
    {
        if (!IsInstanceValid(this))
            return;
        string type = m.TryGetProperty("t", out JsonElement t) ? t.GetString() : "";
        NetClient net = NetClient.Instance;
        switch (type)
        {
            case "earned":
            {
                int by = m.GetProperty("by").GetInt32();
                int payment = m.GetProperty("payment").GetInt32();
                Delivery?.ApplyNetMoney(m.GetProperty("money").GetInt32());
                string what = m.TryGetProperty("what", out JsonElement w) ? w.GetString() : "";
                if (by == net.MyId)
                    Credited(payment, what);
                break;
            }
            case "earnFail":
                GameHud.Toast(this, "O servidor recusou o pagamento (espere um pouco entre as tarefas).", UiStyle.Warning);
                break;
            case "prof":
            {
                int id = m.GetProperty("id").GetInt32();
                string prof = m.GetProperty("prof").GetString();
                if (id == net.MyId)
                    SetProfession(prof, announce: true, fromServer: true);
                else if (net.Players.TryGetValue(id, out NetPlayerInfo info))
                    info.Profession = prof;
                break;
            }
        }
    }

    public string PanelTitle()
    {
        Professions.Def def = Professions.Get(Current);
        return $"{def.Name.ToUpperInvariant()} · {def.Where}";
    }

    public string PanelText() => Job != null && IsInstanceValid(Job) ? Job.PanelText() : "";
}
