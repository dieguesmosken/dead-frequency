// Scripts/Story/StoryChapters.cs
// Dados dos capítulos do Modo História. Títulos, resumos e metas definitivas são decisão do Matheus
// (docs/lore/01_LORE_E_CAPITULOS.md): o que não foi decidido fica marcado como PROVISÓRIO e sem nome inventado.
// Quando os capítulos forem fechados, isto pode virar Resource .tres ([GlobalClass] ChapterData).
namespace DeadFrequency;

public static class StoryChapters
{
    /// <param name="Number">Número do capítulo (1 = Cidade).</param>
    /// <param name="Roman">Texto do banner ("CAPÍTULO II").</param>
    /// <param name="Title">Subtítulo do banner; vazio enquanto não for decidido.</param>
    /// <param name="Level">Fase onde o capítulo acontece (GameLevels: "city", "farm").</param>
    /// <param name="Job">Profissão que conta para a meta (Professions.*); null = qualquer profissão.</param>
    /// <param name="Goal">Quantas tarefas pagas liberam a estrada para o próximo capítulo (0 = fim da campanha por enquanto).</param>
    /// <param name="MaxBroken">Caixas quebradas permitidas no capítulo (-1 = sem limite).</param>
    public readonly record struct Chapter(int Number, string Roman, string Title, string Level, string Job, int Goal, int MaxBroken, string Hint);

    public static readonly Chapter[] All =
    {
        // PROVISÓRIO: meta de exemplo do modelo de arquitetura (5 entregas), até o Matheus decidir o capítulo I.
        new(1, "CAPÍTULO I", "", "city", Professions.Entregador, 5, -1,
            "Faça 5 entregas na cidade. Depois pegue a estrada do rancho (saída leste)."),
        // Título já usado no planejamento (docs/tech). Meta ainda a definir: por enquanto o capítulo só chega ao rancho.
        new(2, "CAPÍTULO II", "Asfalto e Barro", "farm", null, 0, -1,
            "Você chegou ao rancho. Os próximos capítulos ainda estão sendo escritos."),
    };

    public static Chapter Get(int number)
    {
        foreach (Chapter c in All)
            if (c.Number == number)
                return c;
        return All[^1];
    }

    public static bool Exists(int number) => number >= 1 && number <= All.Length;
}
