// Scripts/Story/StoryChapters.cs
// Dados da campanha "Dead Frequency" (roteiro em docs/lore/01_LORE_E_CAPITULOS.md).
// Cada capítulo diz onde acontece, a meta que libera a estrada e as falas de abertura e de fim (pelo rádio).
// Capítulos em fases que ainda não existem (Bananal, Gruta, Serra) ficam com Ready = false: o jogo mostra
// "em construção" no lugar da viagem. Quando a fase ficar pronta, é só ligar o Ready e a ChapterTransitionZone.
using System.Collections.Generic;

namespace DeadFrequency;

public static class StoryChapters
{
    /// <summary>Uma fala: quem fala (aparece em cima) e o texto.</summary>
    public readonly record struct Line(string Speaker, string Text);

    /// <param name="Number">Número do capítulo (1 = Cidade).</param>
    /// <param name="Roman">Texto grande do banner ("CAPÍTULO II").</param>
    /// <param name="Title">Título do capítulo.</param>
    /// <param name="Level">Fase onde acontece (GameLevels: "city", "farm"; as futuras: "bananal", "gruta", "serra").</param>
    /// <param name="Job">Profissão que conta para a meta (Professions.*); null = qualquer profissão.</param>
    /// <param name="Goal">Tarefas pagas que liberam a estrada (0 = sem meta jogável ainda).</param>
    /// <param name="MaxBroken">Caixas quebradas permitidas (-1 = sem limite). Passou disso, a contagem recomeça.</param>
    /// <param name="Hint">Texto curto da meta no canto da tela.</param>
    /// <param name="Ready">A fase do capítulo já existe no jogo.</param>
    public readonly record struct Chapter(int Number, string Roman, string Title, string Level, string Job, int Goal, int MaxBroken,
        string Hint, bool Ready, Line[] Intro, Line[] Outro, Line[] TooManyBroken);

    private const string Waldir = "SEU WALDIR · 96.6", Lurdes = "DONA LURDES · DF ENTREGAS", Tonho = "SEU TONHO · AGRO-TUDÃO",
        Toninho = "TONINHO · 96.6", Katia = "KÁTIA · 96.6", Primo = "O PRIMO · CONSERTA-TUDO";

    public static readonly Chapter[] All =
    {
        new(1, "CAPÍTULO I", "Primeiro Turno", "city", Professions.Entregador, 5, 2,
            "Faça 5 entregas na cidade (no máximo 2 caixas quebradas).", true,
            new Line[]
            {
                new(Lurdes, "Então você é o novato. Bem-vindo à DF Entregas: aqui a encomenda chega inteira."),
                new(Lurdes, "As caixas estão no pátio, os destinos no painel da caminhonete. Cinco entregas e a gente conversa."),
                new(Waldir, "Bom diaaa, vale! Noventa e seis ponto seis... com um chiadinho hoje, né? Já já melhora."),
            },
            new Line[]
            {
                new(Lurdes, "Cinco entregas e quase tudo inteiro. Gostei. Amanhã tem mais."),
                new(Waldir, "Interrompemos a programação para um pedido: o chiado da 96.6 piorou de novo."),
                new(Waldir, "As válvulas da Repetidora Landell, lá na serra, estão no fim. O Seu Tonho, no rancho, guarda peça da época."),
                new(Waldir, "Alguém da DF Entregas leva essas válvulas velhas até o Agro-Tudão? Estrada do rancho, saída leste da cidade."),
                new(Lurdes, "Ouviu, né? Pega a caminhonete e vai. Com cuidado: é vidro."),
            },
            new Line[] { new(Lurdes, "Caixa quebrada demais. Zera a contagem e faz de novo, com calma.") }),

        new(2, "CAPÍTULO II", "Asfalto e Barro", "farm", null, 5, 1,
            "Ajude o rancho: 5 entregas ou tarefas no rancho (no máximo 1 caixa quebrada).", true,
            new Line[]
            {
                new(Tonho, "Ô, chegou a moçada da cidade! Deixa as válvulas aqui no balcão que eu vou procurar as peças."),
                new(Tonho, "Enquanto isso, me ajuda? Tem saco de ração e encomenda para os sítios, e a estrada aqui é de barro."),
                new(Toninho, "Notícia do rancho: o novato da DF Entregas chegou. Os buracos da estrada mandam lembranças."),
            },
            new Line[]
            {
                new(Tonho, "Achei! Mas falta o principal: o rádio de campanha que a gente usou para testar a repetidora em setenta e oito."),
                new(Tonho, "Ficou no barracão da estrada velha... que o bananal engoliu. Lá dentro todo mundo se perde."),
                new(Waldir, "Dica da 96.6: no bananal, siga o chiado. Quanto mais limpo o rádio, mais perto da antena."),
                new(Waldir, "Esta história continua no capítulo III, Sinal no Bananal. Por enquanto, o rancho é todo seu."),
            },
            new Line[] { new(Tonho, "Eita, quebrou de novo? Começa a contagem outra vez, devagar nos buracos.") }),

        new(3, "CAPÍTULO III", "Sinal no Bananal", "bananal", null, 0, -1,
            "Ache o barracão no bananal seguindo o chiado do rádio.", false,
            new Line[] { new(Waldir, "No bananal, siga o chiado. Quanto mais limpo, mais perto.") },
            new Line[] { new(Tonho, "O rádio de campanha! E ainda pega um fiapo do sinal... que vem pela gruta.") },
            new Line[0]),

        new(4, "CAPÍTULO IV", "A Gruta da Estática", "gruta", null, 0, -1,
            "Siga o cabo da repetidora pela gruta e religue a emenda.", false,
            new Line[] { new(Waldir, "Leve lanterna. Aquele zumbido na gruta é o minério, não tem nada lá além de morcego.") },
            new Line[] { new(Waldir, "Emenda religada! O sinal voltou... falta só a válvula nova.") },
            new Line[0]),

        new(5, "CAPÍTULO V", "Neblina na Serra", "serra", null, 0, -1,
            "Leve as válvulas novas até a Repetidora Landell sem quebrar.", false,
            new Line[] { new(Primo, "Válvula nova, feita na unha. Se quebrar, eu choro. Vai devagar na serra.") },
            new Line[] { new(Waldir, "Válvulas no lugar! Agora a gente precisa sair do ar uma noite para a troca.") },
            new Line[0]),

        new(6, "CAPÍTULO VI", "Fora do Ar", "city", Professions.Locutor, 0, -1,
            "A 96.6 está fora do ar: leia os avisos no estúdio e entregue os recados.", false,
            new Line[] { new(Waldir, "Esta noite a voz do vale é a sua. O rádio de campanha está no estúdio.") },
            new Line[] { new(Waldir, "Voltamos! Noventa e seis ponto seis, limpinho do jeito que tem que ser.") },
            new Line[0]),

        new(7, "CAPÍTULO VII", "Show da Frequência", "city", Professions.Entregador, 0, -1,
            "Leve o equipamento do Show da Frequência até a praça.", false,
            new Line[] { new(Katia, "Sexta é dia de Show da Frequência na praça! Falta só o equipamento chegar!") },
            new Line[] { new(Waldir, "Obrigado, vale. A 96.6 continua no ar... e você continua na estrada.") },
            new Line[0]),
    };

    public static Chapter Get(int number)
    {
        foreach (Chapter c in All)
            if (c.Number == number)
                return c;
        return All[^1];
    }

    public static bool Exists(int number) => number >= 1 && number <= All.Length;

    public static bool IsLast(int number) => number >= All.Length;

    /// <summary>A fase do capítulo existe no jogo (cidade e rancho, hoje).</summary>
    public static bool LevelExists(string level) => level is "city" or "farm";

    public static IEnumerable<Chapter> Upcoming(int after)
    {
        foreach (Chapter c in All)
            if (c.Number > after)
                yield return c;
    }
}
