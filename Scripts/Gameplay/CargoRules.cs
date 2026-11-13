// Scripts/Gameplay/CargoRules.cs
// Que cargas podem sair em cada fase (v0.9.9). Mesma regra de cargoPool() em server/src/index.ts
// e usada pelo LocalServer e pelo DeliveryManager offline. Índices = DeliveryManager.CargoCatalog.
//   Cidade: todas menos o saco de ração.
//   Sítio : todas, e o saco de ração do Agro-Tudão pesa 3x no sorteio.
namespace DeadFrequency;

public static class CargoRules
{
    /// <summary>Índice do saco de ração em CargoCatalog / CARGO_CATALOG.</summary>
    public const int FeedSack = 13;
    public const int CatalogSize = 14;

    /// <summary>Sorteio ponderado: cada entrada é um índice do catálogo.</summary>
    public static int[] Pool(string level)
    {
        var pool = new System.Collections.Generic.List<int>();
        for (int i = 0; i < CatalogSize; i++)
        {
            if (i == FeedSack)
            {
                if (level == "farm")
                    pool.AddRange(new[] { i, i, i });
                continue;
            }
            pool.Add(i);
        }
        return pool.ToArray();
    }
}
