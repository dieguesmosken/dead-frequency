// Scripts/Core/SaveManager.cs
// Save do jogo em user://save.tzn (JSON via System.Text.Json + FileAccess, como manda o CLAUDE.md).
// Salva na cama do apartamento: profissão, turno, entregas, ganhos por profissão, dinheiro e tempo de jogo.
// A carteira e o inventário continuam também em user://inventory.json (PlayerInventory).
using System.Collections.Generic;
using System.Text.Json;
using Godot;

namespace DeadFrequency;

public class SaveData
{
    public int Version { get; set; } = 1;
    public string GameVersion { get; set; } = "";
    public string SavedAt { get; set; } = "";
    public string Level { get; set; } = "city";
    public string Profession { get; set; } = Professions.Entregador;
    public int WorldSeed { get; set; } = 1996;
    public int WorldGeneratorVersion { get; set; }
    public int Shift { get; set; } = 1;
    public int TotalDelivered { get; set; }
    public int Wallet { get; set; }
    public int Saves { get; set; }
    public double PlayMinutes { get; set; }
    public Dictionary<string, int> EarnedByJob { get; set; } = new();
}

public static class SaveManager
{
    public const string FilePath = "user://save.tzn";
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    private static SaveData _cache;
    /// <summary>Ganhos desta sessão ainda não salvos (somados ao save na próxima vez que dormir).</summary>
    private static readonly Dictionary<string, int> PendingEarned = new();
    private static double _sessionStartMs = Time.GetTicksMsec();

    public static bool Exists => FileAccess.FileExists(FilePath);

    public static SaveData Load()
    {
        if (_cache != null)
            return _cache;
        if (!FileAccess.FileExists(FilePath))
            return null;
        using FileAccess f = FileAccess.Open(FilePath, FileAccess.ModeFlags.Read);
        if (f == null)
            return null;
        try
        {
            _cache = JsonSerializer.Deserialize<SaveData>(f.GetAsText());
        }
        catch (JsonException e)
        {
            GD.PushWarning($"SaveManager: save corrompido ({e.Message}).");
            _cache = null;
        }
        return _cache;
    }

    public static void AddEarned(string job, int amount)
    {
        PendingEarned.TryGetValue(job, out int v);
        PendingEarned[job] = v + amount;
    }

    /// <summary>Grava o estado atual. Retorna o save gravado (ou null se falhou).</summary>
    public static SaveData Save(DeliveryManager delivery)
    {
        SaveData data = Load() ?? new SaveData();
        data.GameVersion = DeadFrequency.GameVersion.Current;
        data.SavedAt = Time.GetDatetimeStringFromSystem(false, true);
        data.Level = Professions.CurrentLevel;
        data.Profession = Professions.Current;
        if (delivery?.City?.WorldData is WorldGenerationData worldData)
        {
            data.WorldSeed = worldData.Seed;
            data.WorldGeneratorVersion = worldData.GeneratorVersion;
        }
        if (delivery != null && !delivery.NetworkMode)
        {
            data.Shift = delivery.Shift;
            data.TotalDelivered = delivery.TotalDelivered;
        }
        data.Wallet = PlayerInventory.Instance?.Wallet ?? data.Wallet;
        data.Saves++;
        double now = Time.GetTicksMsec();
        data.PlayMinutes += (now - _sessionStartMs) / 60000.0;
        _sessionStartMs = now;
        foreach ((string job, int amount) in PendingEarned)
        {
            data.EarnedByJob.TryGetValue(job, out int v);
            data.EarnedByJob[job] = v + amount;
        }
        PendingEarned.Clear();

        using FileAccess f = FileAccess.Open(FilePath, FileAccess.ModeFlags.Write);
        if (f == null)
        {
            GD.PushWarning($"SaveManager: não foi possível gravar ({FileAccess.GetOpenError()}).");
            return null;
        }
        f.StoreString(JsonSerializer.Serialize(data, Options));
        _cache = data;
        return data;
    }

    /// <summary>Resumo de uma linha para o menu ("Turno 4 · 12 entregas · salvo em ...").</summary>
    public static string Summary()
    {
        SaveData d = Load();
        if (d == null)
            return "";
        return $"Save: {Professions.Get(d.Profession).Name} · turno {d.Shift} · {d.TotalDelivered} entregas · {d.SavedAt}";
    }
}
