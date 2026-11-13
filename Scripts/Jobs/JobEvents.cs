// Scripts/Jobs/JobEvents.cs
// Ponte entre as profissões e quem quiser saber delas (Modo História, conquistas, estatísticas).
// As profissões só disparam; não sabem quem escuta. Quem assina TEM que desassinar em _ExitTree().
// Ver docs/tech/01_ARQUITETURA_MODO_HISTORIA.md.
using System;

namespace DeadFrequency;

public static class JobEvents
{
    /// <summary>Uma tarefa foi paga. job = id de Professions (ex.: "taxista"), what = descrição, payment = valor.</summary>
    public static event Action<string, string, int> TaskDone;

    /// <summary>Uma caixa do turno quebrou (entregador).</summary>
    public static event Action<string> CargoBroken;

    /// <summary>Chamado em ProfessionManager.Credited e no DeliveryManager (onde emite DeliveryCompleted).</summary>
    public static void RaiseTaskDone(string job, string what, int payment) => TaskDone?.Invoke(job, what, payment);

    /// <summary>Chamado no DeliveryManager quando uma caixa do turno quebra.</summary>
    public static void RaiseCargoBroken(string cargoName) => CargoBroken?.Invoke(cargoName);
}
