// Scripts/Core/Professions.cs
// Profissões da cidade simulada (v0.9). O id vai para o servidor; pagamentos e intervalos mínimos
// são os mesmos de PROFESSIONS em server/src/index.ts e de LocalServer.Professions (não mude só aqui).
using Godot;

namespace DeadFrequency;

public static class Professions
{
    public const string Entregador = "entregador";
    public const string Taxista = "taxista";
    public const string Gari = "gari";
    public const string Repositor = "repositor";
    public const string Frentista = "frentista";
    public const string Locutor = "locutor";

    /// <param name="Pay">Pagamento por tarefa (taxista: corrida mínima; o valor final depende da distância).</param>
    /// <param name="Levels">Fases onde a profissão existe ("city", "farm").</param>
    public readonly record struct Def(string Id, string Name, string Task, string Where, int Pay, Color Color, string[] Levels);

    private static readonly string[] Both = { "city", "farm" };
    private static readonly string[] CityOnly = { "city" };

    public static readonly Def[] All =
    {
        new(Entregador, "Entregador", "Leve as caixas do depósito até o feixe verde. Paga pela integridade.", "Depósito Frequência", 0, new Color(0.55f, 1f, 0.62f), Both),
        new(Taxista, "Taxista", "Pegue o táxi amarelo no pátio da central, busque passageiros (feixe amarelo) e deixe no destino. Corrida pela distância.", "Qualquer rua", 30, new Color(1f, 0.85f, 0.2f), Both),
        new(Gari, "Gari", "Recolha os sacos de lixo das calçadas e jogue nas caçambas. R$ 25 por saco.", "Calçadas e caçambas", 25, new Color(1f, 0.55f, 0.15f), Both),
        new(Repositor, "Repositor", "Pegue caixas no estoque do Atacadinho e reponha as prateleiras vazias. R$ 30 por prateleira.", "Supermercado Atacadinho", 30, new Color(0.4f, 0.75f, 1f), CityOnly),
        new(Frentista, "Frentista", "Abasteça os carros que chegam no posto mais perto. R$ 45 por tanque cheio.", "Postos de gasolina", 45, new Color(0.3f, 0.9f, 0.4f), CityOnly),
        new(Locutor, "Locutor da 96.6", "Na rádio, leia os comerciais quando a luz NO AR acender. R$ 20 por comercial.", "Rádio Frequência 96.6", 20, new Color(0.85f, 0.5f, 1f), CityOnly),
    };

    public static Def Get(string id)
    {
        foreach (Def d in All)
            if (d.Id == id)
                return d;
        return All[0];
    }

    public static bool AvailableIn(string id, string level)
    {
        foreach (string l in Get(id).Levels)
            if (l == level)
                return true;
        return false;
    }

    /// <summary>Profissão escolhida para a partida atual (o servidor pode trocar no online).</summary>
    public static string Current { get; set; } = Entregador;

    public static string CurrentLevel { get; set; } = "city";
}
