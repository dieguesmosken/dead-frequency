// Scripts/Vehicle/EngineSystem.cs
// Motor da caminhonete (painel diegético, Fase 3): chave, partida, combustível, temperatura e conta-giros.
// Não lê controles nem mexe na física: o VanController chama Tick() a cada passo ("call down") e usa
// PowerFactor na força do motor. Avisa quem está acima por eventos ("signal up").
// Nada aqui trava o jogo: sem bateria o motor pega "no tranco" e sem gasolina ele anda devagar, no cheiro.
using System;
using Godot;

namespace DeadFrequency;

public partial class EngineSystem : Node
{
    public const float TankLiters = 50f;
    /// <summary>Quanto um galão de gasolina enche (fração do tanque).</summary>
    public const float JerryCanAmount = 0.5f;

    [Export] public float IdleRpm { get; set; } = 850f;
    [Export] public float MaxRpm { get; set; } = 5000f;
    /// <summary>Fração do tanque gasta por segundo com o pé no fundo (tanque cheio ≈ 24 min assim).</summary>
    [Export] public float FuelPerSecondFullLoad { get; set; } = 0.0007f;
    [Export] public float FuelPerSecondIdle { get; set; } = 0.00005f;

    public bool Running { get; private set; }
    public bool Cranking => _crank > 0f;
    /// <summary>Tanque, de 0 a 1.</summary>
    public float Fuel { get; private set; } = 0.75f;
    public float FuelLiters => Fuel * TankLiters;
    public bool FuelReserve => Fuel < 0.15f;
    public bool OutOfFuel => Fuel <= 0f;
    /// <summary>Temperatura da água em °C (normal entre 80 e 100).</summary>
    public float Temperature { get; private set; } = 30f;
    public bool Overheated { get; private set; }
    public bool TemperatureWarning => Temperature > 106f;
    public float Rpm { get; private set; }
    /// <summary>0 = motor desligado, 1 = força total. Fervendo ou sem gasolina a força cai.</summary>
    public float PowerFactor => !Running ? 0f : (Overheated ? 0.45f : 1f) * (OutOfFuel ? 0.3f : 1f);

    public event Action Started;
    public event Action Stopped;
    /// <summary>Recado para o painel/HUD (ex.: "Motor fervendo").</summary>
    public event Action<string, bool> Notice;

    private float _crank;
    private bool _reserveWarned, _emptyWarned;

    /// <summary>Gira a chave. Com a bateria arriada a partida demora (pega no tranco), mas pega.</summary>
    public void Start(bool batteryWeak)
    {
        if (Running || Cranking)
            return;
        _crank = batteryWeak ? 3.2f : 0.8f;
        if (batteryWeak)
            Notice?.Invoke("Bateria arriada... pegando no tranco.", true);
    }

    public void Stop()
    {
        _crank = 0f;
        if (!Running)
            return;
        Running = false;
        Stopped?.Invoke();
    }

    /// <summary>Galão de gasolina. Retorna false se o tanque já está cheio.</summary>
    public bool Refuel(float amount)
    {
        if (Fuel >= 0.98f)
            return false;
        Fuel = Mathf.Clamp(Fuel + amount, 0f, 1f);
        _reserveWarned = Fuel < 0.15f;
        _emptyWarned = false;
        return true;
    }

    /// <summary>Estado vindo do save.</summary>
    public void Restore(float fuel) => Fuel = Mathf.Clamp(fuel, 0f, 1f);

    public void Tick(float dt, float throttle, float speedKmh)
    {
        float load = Mathf.Abs(throttle);
        if (Cranking)
        {
            _crank -= dt;
            if (_crank <= 0f)
            {
                _crank = 0f;
                Running = true;
                Started?.Invoke();
            }
        }

        if (Running)
        {
            Fuel = Mathf.Max(0f, Fuel - (FuelPerSecondIdle + FuelPerSecondFullLoad * load) * dt);
            if (FuelReserve && !_reserveWarned)
            {
                _reserveWarned = true;
                Notice?.Invoke("Entrou na reserva. Tem galão de gasolina nos postos.", true);
            }
            if (OutOfFuel && !_emptyWarned)
            {
                _emptyWarned = true;
                Notice?.Invoke("Acabou a gasolina! Dá para ir devagar, no cheiro, até um posto.", true);
            }
        }

        // Temperatura: sobe com o pé no fundo em alta; fervendo, a ventoinha segura até esfriar.
        float target = !Running ? 30f
            : Overheated ? 60f
            : 74f + 24f * load + 22f * Mathf.Clamp((speedKmh - 70f) / 25f, 0f, 1f);
        float tau = Running ? 35f : 90f;
        Temperature = Mathf.Lerp(Temperature, target, 1f - Mathf.Exp(-dt / tau));
        if (!Overheated && Temperature >= 114f)
        {
            Overheated = true;
            Notice?.Invoke("Motor fervendo! Alivia o pé até a temperatura baixar.", true);
        }
        else if (Overheated && Temperature < 96f)
        {
            Overheated = false;
            Notice?.Invoke("Temperatura normal de novo.", false);
        }

        // Conta-giros: quatro marchas de mentirinha pela velocidade.
        float targetRpm;
        if (Cranking)
            targetRpm = 260f + 120f * Mathf.Sin((float)Time.GetTicksMsec() * 0.03f);
        else if (!Running)
            targetRpm = 0f;
        else
        {
            float[] gears = { 0f, 22f, 45f, 70f, 105f };
            int g = 0;
            while (g < 3 && speedKmh > gears[g + 1])
                g++;
            float t = Mathf.Clamp((speedKmh - gears[g]) / (gears[g + 1] - gears[g]), 0f, 1f);
            targetRpm = speedKmh < 2f ? IdleRpm + load * 1400f : 1300f + t * 2500f + load * 600f;
        }
        Rpm = Mathf.Lerp(Rpm, Mathf.Min(targetRpm, MaxRpm), 1f - Mathf.Exp(-7f * dt));
    }
}
