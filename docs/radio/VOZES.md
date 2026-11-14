# Vozes dos personagens

Geradas no **Google AI Studio** (Playground → modelo **Gemini 3.8 Flash TTS**, "Single speaker"), uma fala por vez,
pelo plano Google AI Pro do Matheus. O campo **Style** recebe a descrição abaixo; o texto vai na caixa da fala.
Depois de baixar o .wav: corta o silêncio das pontas, normaliza em -16 LUFS, mono 44,1 kHz, .ogg (q5).
As falas do carro do ovo ganham o filtro de megafone (ver `tools/tts/gerar_audios.py`, `FILTRO_MEGAFONE`).
O AI Studio tem limite diário: quando aparece "Quota limit reached", para e continua no dia seguinte
(não trocar de plano nem ligar chave paga sem o Matheus autorizar).

| Personagem | Voz | Style (resumo) |
|---|---|---|
| Seu Waldir (96.6) | **Arlo** | Locutor veterano de rádio AM do interior de SP, anos 90, sotaque caipira leve, grave, caloroso, pausado. |
| Kátia (96.6) | **Cleo** | Locutora de comerciais de FM, animada, sorridente e rápida, ênfase nos preços e nomes das lojas. |
| Toninho (96.6) | **Mako** | Locutor de humor, moleque e debochado, pausinha antes da piada, final com malandragem. |
| Prefeitura | **Kira** | Comunicado oficial de cidade pequena, clara, formal e neutra, ritmo calmo. |
| Carro do ovo | **Enzo** | Vendedor no alto-falante, sotaque caipira, empolgado e cantado, voz alta e arrastada. |
| Dona Lurdes | **Enya** | Gerente da central de entregas, ~55 anos, seca e direta mas justa, fundo de humor. |
| Seu Tonho | **Algenib** | Dono do Agro-Tudão, ~70 anos, sotaque caipira forte puxando o R, voz rouca, bonachão. |

## Arquivos

- Comerciais, eventos, comunicados e vinhetas: `Audio/Radio/Ads/<id>.ogg` (ids em `anuncios_966.csv`).
  A 96.6 toca um por intervalo entre as músicas (`RadioStation.Spots`); o carro de som usa os mesmos.
- Carro do ovo: `Audio/Street/ovo_0N.ogg` (`carros_de_rua.csv`).
- Modo História: `Audio/Story/story_c<capítulo>_<intro|outro|broken>_<n>.ogg`, na ordem das falas de
  `Scripts/Story/StoryChapters.cs` (n começa em 1). Sem o arquivo, a caixa de diálogo só mostra o texto.

## Falta gerar (limite diário do AI Studio)

- `story_c2_intro_1`, `story_c2_intro_2`, `story_c2_outro_1`, `story_c2_outro_2`, `story_c2_broken_1` (Seu Tonho).
- `story_c2_intro_3` (Toninho).
