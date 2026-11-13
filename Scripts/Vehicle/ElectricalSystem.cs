// Scripts/Vehicle/ElectricalSystem.cs
// Parte elétrica da caminhonete (painel diegético, Fase 3): bateria, alternador, quatro circuitos com
// fusível físico e a interferência eletromagnética da carga (CargoData.EmInterference).
// Esqueceu o farol ligado com o motor desligado? A bateria arria. Batida forte ou carga "nervosa" na
// caçamba pode queimar um fusível. O VanController chama Tick() e aplica o resultado nos faróis e no rádio.
using System;
using Godot;

namespace DeadFrequency;

/// <summary>Circuitos com fusível na caixa do painel.</summary>
public enum Circuit { Lights, Radio, Panel, Cabin }

public partial class ElectricalSystem : Node
{
    public static readonly string[] CircuitNames = { "FARÓIS", "RÁDIO", "PAINEL", "LUZ INT." };

    /// <summary>Carga da bateria, de 0 a 1.</summary>
    public float Charge { get; private set; } = 0.9f;
    public bool LightsSwitch { get; set; } = true;
    public bool CabinSwitch { get; set; }
    /// <summary>Fusíveis reserva guardados na tampa da caixa.</summary>
    public int Spares { get; private set; } = 2;
    /// <summary>Interferência da carga na caçamba, de 0 a 1 (chiado no rádio, ponteiros tremendo).</summary>
    public float Interference { get; private set; }
    public bool Charging { get; private set; }
    public bool BatteryLow => Charge < 0.2f;
    public bool BatteryDead => Charge < 0.03f;
    public bool AnyFuseBlown => !(_fuseOk[0] && _fuseOk[1] && _fuseOk[2] && _fuseOk[3]);
    /// <summary>Tensão mostrada no painel.</summary>
    public float Voltage => Charging ? 13.4f + Charge * 0.8f : 10.6f + Charge * 2.0f;

    public event Action<Circuit> FuseBlown;
    public event Action<Circuit> FuseReplaced;

    private readonly bool[] _fuseOk = { true, true, true, true };
    /// <summary>Gambiarra: palha de aço no lugar do fusível. Funciona... até dar curto.</summary>
    private readonly bool[] _jumper = new bool[4];
    private float _scan;
    private readonly RandomNumberGenerator _rng = new();

    public override void _Ready() => _rng.Randomize();

    public bool FuseOk(Circuit c) => _fuseOk[(int)c];
    public bool IsJumper(Circuit c) => _jumper[(int)c];
    public bool Powered(Circuit c) => _fuseOk[(int)c] && !BatteryDead;

    /// <summary>Brilho dos faróis (0 a 1): com o motor desligado e a bateria fraca eles amarelam.</summary>
    public float LightLevel => !LightsSwitch || !Powered(Circuit.Lights) ? 0f : Charging ? 1f : Mathf.Clamp(Charge / 0.3f, 0.2f, 1f);

    public void Restore(float charge) => Charge = Mathf.Clamp(charge, 0f, 1f);

    public void Tick(float dt, bool engineRunning, bool cranking, bool radioOn, Vector3 bedCenter)
    {
        Charging = engineRunning;
        if (engineRunning)
        {
            Charge = Mathf.Min(1f, Charge + dt / 150f);
        }
        else
        {
            float drain = 0f;
            if (LightsSwitch && Powered(Circuit.Lights)) drain += 1f / 720f;   // farol esquecido: ~12 min
            if (radioOn && Powered(Circuit.Radio)) drain += 1f / 2400f;
            if (CabinSwitch && Powered(Circuit.Cabin)) drain += 1f / 3600f;
            if (cranking) drain += 1f / 60f;
            Charge = Mathf.Max(0f, Charge - drain * dt);
        }

        _scan -= dt;
        if (_scan > 0f)
            return;
        _scan = 0.5f;
        float sum = 0f;
        if (IsInsideTree())
        {
            foreach (Node n in GetTree().GetNodesInGroup(CargoItem.GroupName))
            {
                if (n is not CargoItem cargo || cargo.Data == null || cargo.Integrity <= 0f || cargo.Data.EmInterference <= 0f)
                    continue;
                float d = cargo.GlobalPosition.DistanceTo(bedCenter);
                if (d < 3.2f)
                    sum += cargo.Data.EmInterference * Mathf.Clamp(1.4f - d / 3.2f, 0f, 1f);
            }
        }
        Interference = Mathf.MoveToward(Interference, Mathf.Clamp(sum, 0f, 1f), 0.25f);
        // Carga muito "nervosa" com o rádio ligado: de vez em quando o fusível do rádio não aguenta.
        if (Interference > 0.45f && radioOn && Powered(Circuit.Radio) && _rng.Randf() < (Interference - 0.45f) * 0.004f)
            Blow(Circuit.Radio);
    }

    /// <summary>Batida: quanto mais forte, maior a chance de queimar um fusível.</summary>
    public void OnCrash(float change)
    {
        float chance = change > 9f ? 0.3f : change > 6f ? 0.08f : 0f;
        if (chance > 0f && _rng.Randf() < chance)
            Blow((Circuit)_rng.RandiRange(0, 3));
    }

    public void Blow(Circuit c)
    {
        int i = (int)c;
        if (!_fuseOk[i])
            return;
        _fuseOk[i] = false;
        if (_jumper[i])
        {
            // A gambiarra não queima: dá curto e leva um pedaço da bateria junto.
            _jumper[i] = false;
            Charge = Mathf.Max(0.05f, Charge - 0.15f);
        }
        FuseBlown?.Invoke(c);
    }

    /// <summary>Troca o fusível queimado: reserva da caixa, fusível da mochila ou gambiarra com palha de aço.</summary>
    public string Replace(Circuit c)
    {
        int i = (int)c;
        string name = CircuitNames[i];
        if (_fuseOk[i])
            return $"Fusível {name}: está bom.";
        PlayerInventory inv = PlayerInventory.Instance;
        if (Spares > 0)
        {
            Spares--;
            _fuseOk[i] = true;
            FuseReplaced?.Invoke(c);
            return $"Fusível {name} trocado. Reservas na caixa: {Spares}.";
        }
        if (inv != null && inv.TryRemoveItem(ItemCatalog.Fuse))
        {
            _fuseOk[i] = true;
            FuseReplaced?.Invoke(c);
            return $"Fusível {name} trocado (da mochila).";
        }
        if (inv != null && inv.TryRemoveItem(ItemCatalog.SteelWool))
        {
            _fuseOk[i] = true;
            _jumper[i] = true;
            FuseReplaced?.Invoke(c);
            return $"Gambiarra: palha de aço no lugar do fusível ({name}). Funciona... por enquanto.";
        }
        return "Sem fusível reserva. Vende no Conserta-Tudo do Primo, na Lã de Aço e nos postos.";
    }
}
