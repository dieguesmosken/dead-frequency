// Scripts/Audio/RadioStation.cs
// Rádio Frequência 96.6 FM: a programação é a mesma para todo mundo porque sai do relógio (unix time).
// Cada bloco = vinheta + música + intervalo (um comercial, evento, comunicado ou vinheta falada).
// Dois jogadores (ou a van e o escritório) ouvem o mesmo trecho.
// Músicas originais geradas por código (Audio/synth_radio.py.txt), sem direitos autorais de terceiros.
// Vozes dos intervalos: Audio/Radio/Ads/<id>.ogg (Gemini TTS; roteiros em docs/radio/anuncios_966.csv).
// Intervalo cujo áudio não existe é pulado.
using System.Collections.Generic;
using Godot;

namespace DeadFrequency;

public static class RadioStation
{
    public const string Frequency = "96.6";
    public const string Name = "Rádio Frequência 96.6 FM";
    public const string JinglePath = "res://Audio/Radio/radio_jingle.ogg";
    public const float JingleLength = 2.6f;
    public const string AdsFolder = "res://Audio/Radio/Ads/";

    public readonly record struct Track(string Title, string Artist, string Path, float Length);

    /// <summary>Um trecho da programação. Kind: 0 = vinheta, 1 = música, 2 = intervalo.</summary>
    public readonly record struct Segment(int Kind, string Path, float Length, int Track, string Id);

    public static readonly Track[] Playlist =
    {
        new("Frequência Morta", "Os Sintonizados", "res://Audio/Radio/radio_01_frequencia_morta.ogg", 99.0f),
        new("Bossa da Madrugada", "Trio Estática", "res://Audio/Radio/radio_02_bossa_da_madrugada.ogg", 86.8f),
        new("Asfalto Molhado", "Lo-Fi do Viaduto", "res://Audio/Radio/radio_03_asfalto_molhado.ogg", 94.4f),
        new("Forró do Entregador", "Zé da Van e Banda", "res://Audio/Radio/radio_04_forro_do_entregador.ogg", 88.1f),
        new("Neon na Rodovia", "Cartucho 16", "res://Audio/Radio/radio_05_neon_na_rodovia.ogg", 93.8f),
    };

    /// <summary>Ordem dos intervalos (mistura comercial, evento, comunicado e vinheta). Ids = docs/radio/anuncios_966.csv.</summary>
    public static readonly string[] Spots =
    {
        "vh_966_classica", "ad_agro_tudao", "co_sem_sinal", "ad_la_de_aco", "ev_festa_sao_frequencio",
        "ad_conserta_tudo", "vh_966_entregador", "ad_atacadinho", "co_obras_avenida", "ad_petrobraz",
        "ev_feira_praca", "ad_shello", "vh_966_frequencia", "ad_droga_arraia", "co_neblina_serra",
        "ad_pao_de_sal", "ev_torneio_pesca", "ad_ipiranguinha", "vh_966_madrugada", "ad_acai_atacadista",
        "co_coleta_lixo", "ad_rabibs", "ev_quermesse", "ad_sorte_morta", "co_onibus_966",
        "ad_auto_center", "ev_inauguracao_agro", "ad_shopping_marconi", "co_estacionamento", "ad_taxi_sintonia",
        "ev_show_966", "ad_lambari_feliz", "co_vacina_animais", "co_entregador_seguro",
    };

    private static List<Segment> _program;
    private static float _programLength;

    /// <summary>A programação inteira (montada uma vez; os intervalos sem áudio ficam de fora).</summary>
    public static IReadOnlyList<Segment> Program
    {
        get
        {
            if (_program == null)
                Build();
            return _program;
        }
    }

    public static float CycleLength
    {
        get
        {
            if (_program == null)
                Build();
            return _programLength;
        }
    }

    private static void Build()
    {
        var spots = new List<(string Id, string Path, float Length)>();
        foreach (string id in Spots)
        {
            string path = AdsFolder + id + ".ogg";
            if (!ResourceLoader.Exists(path) || GD.Load<AudioStream>(path) is not AudioStream s)
                continue;
            spots.Add((id, path, (float)s.GetLength() + 0.4f)); // um respiro antes da vinheta seguinte
        }
        _program = new List<Segment>();
        int blocks = Mathf.Max(Playlist.Length, spots.Count);
        for (int k = 0; k < blocks; k++)
        {
            int t = k % Playlist.Length;
            _program.Add(new Segment(0, JinglePath, JingleLength, t, null));
            _program.Add(new Segment(1, Playlist[t].Path, Playlist[t].Length, t, null));
            if (spots.Count > 0)
            {
                var sp = spots[k % spots.Count];
                _program.Add(new Segment(2, sp.Path, sp.Length, t, sp.Id));
            }
        }
        _programLength = 0f;
        foreach (Segment s in _program)
            _programLength += s.Length;
    }

    /// <summary>O que está no ar agora: índice do trecho na programação e quantos segundos já tocaram.</summary>
    public static (int Index, Segment Segment, float Offset) Now()
    {
        IReadOnlyList<Segment> program = Program;
        double pos = Time.GetUnixTimeFromSystem() % CycleLength;
        for (int i = 0; i < program.Count; i++)
        {
            if (pos < program[i].Length)
                return (i, program[i], (float)pos);
            pos -= program[i].Length;
        }
        return (0, program[0], 0f);
    }

    public static string NowPlayingText()
    {
        (int _, Segment seg, float _) = Now();
        if (seg.Kind == 2)
            return $"{Frequency} FM · Intervalo comercial";
        Track t = Playlist[seg.Track];
        return $"{Frequency} FM · {t.Title} — {t.Artist}";
    }
}
