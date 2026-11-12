# Rádio 96.6 — anúncios, eventos e comunicados (roteiros para TTS)

Roteiros da primeira leva de áudios da Rádio 96.6 (plano no `ROADMAP.md`, seção "Rádio 96.6 — publicidade
local e agenda de eventos"). Os mesmos textos estão em [`anuncios_966.csv`](anuncios_966.csv), um por linha,
para gerar tudo de uma vez no VoiceStudio (ou outro TTS local).

- **Tom:** rádio de cidade pequena dos anos 90, bem-humorado e acolhedor. Nada de terror, susto ou sobrenatural.
- **Marcas:** só as paródias que já existem no jogo (letreiros em `Assets/Textures/Stores/`). Nunca marca real.
- **Duração:** 10 a 30 segundos. Vinhetas de 3 a 6 s.
- **Arquivo final:** `Audio/Radio/Ads/<id>.ogg`, mono, 44,1 kHz, normalizado em -16 LUFS (mesmo volume das músicas).

## Locutores

As descrições servem para o "voice design" (criar voz por texto). Clonar voz só de quem autorizou por escrito
(o Matheus ou amigos que toparem); nunca de locutores, atores ou streamers reais.

| Id | Nome no jogo | Estilo | Descrição para o voice design |
|---|---|---|---|
| `waldir` | Seu Waldir | Rádio local tradicional | Homem de uns 60 anos, voz grave e calorosa, fala pausada de locutor AM do interior, sotaque paulista do interior, sorriso na voz. |
| `katia` | Kátia Varejão | Varejo animado | Mulher de uns 30 anos, voz brilhante e rápida, energia de locutora de loja e carro de som, empolgada sem gritar. |
| `prefeitura` | Voz da Prefeitura | Institucional | Voz neutra e clara, ritmo calmo, tom de aviso oficial, sem emoção exagerada. |
| `toninho` | Toninho Gozação | Humor | Homem de uns 40 anos, voz anasalada e brincalhona, exagera nas pausas para fazer graça. |

## Dicas de pronúncia

| Escrito | Como falar |
|---|---|
| 96.6 | "noventa e seis ponto seis" |
| Agro-Tudão | "a-gro-tu-DÃO" |
| Petrobraz | "pe-tro-BRÁS" |
| Shello | "XÉ-lo" |
| Rabib's | "ha-BIBS" |
| Muriçoca | "mu-ri-SSÓ-ca" |
| R$ 0,89 | "oitenta e nove centavos" (preço do litro em 1996) |

## Comerciais das lojas (16)

| Id | Locutor | Texto |
|---|---|---|
| `ad_agro_tudao` | waldir | Lá na Estrada do Rancho, logo depois da cooperativa, tem o Agro-Tudão! Ração pra galinha, pro porco e pro caramelo, semente, ferragem e o Repelente Espanta-Muriçoca. Agro-Tudão: do pintinho ao trator, tem! |
| `ad_conserta_tudo` | toninho | Quebrou? Rachou? Entortou? Leva no Conserta-Tudo do Primo, em frente à central de entregas! O Primo compra sua sucata e ainda vende a lanterna de longo alcance. Se não tem conserto... o Primo dá um jeito. |
| `ad_la_de_aco` | katia | Lã de Aço 1001 Utilidades, no Shopping Marconi! Fita, prego e a famosa palha de aço Brilhaço. A TV do apartamento tá com chuvisco? Enrola na antena e pronto! Lã de Aço: tem de tudo. Se não tiver... tem palha de aço! |
| `ad_atacadinho` | katia | Atenção, dona de casa! Supermercado Atacadinho com ofertas fresquinhas o dia inteiro: arroz, feijão, café e guaraná trincando de gelado. Atacadinho: leve três, pague três. Preço justo é isso aí! |
| `ad_petrobraz` | waldir | Posto Petrobraz: gasolina a oitenta e nove centavos o litro e frentista que confere o óleo sem você pedir. Abastece na Petrobraz e vai tranquilo pela estrada. |
| `ad_ipiranguinha` | katia | Encheu o tanque no Ipiranguinha? Ganhou um cafezinho na conveniência! Gasolina a oitenta e sete centavos. Ipiranguinha: o posto amarelinho do seu bairro. |
| `ad_shello` | toninho | Posto Shello. A gasolina é noventa e um centavos... mas o banheiro é limpinho. Shello: você paga um pouquinho mais, mas sai cheiroso. |
| `ad_droga_arraia` | waldir | Droga Arraia: remédio, curativo e protetor solar pra quem passa o dia na rua entregando encomenda. Farmacêutico de plantão até as dez da noite. Droga Arraia, cuidando da cidade. |
| `ad_pao_de_sal` | katia | Pão francês saindo agora, quentinho! Padaria Pão de Sal, no Shopping Marconi e no centro. Leva o pãozinho, a mortadela e o cafezinho pra começar o turno com o pé direito! |
| `ad_acai_atacadista` | toninho | Calor de rachar? Açaí Atacadista! Tigela de meio litro com granola e banana. Atacadista no nome... no tamanho... e na fila. Vale a pena! |
| `ad_rabibs` | katia | Esfiha aberta a cinquenta centavos! Só no Rabib's. Pediu, chegou quentinha. Rabib's: fome de esfiha? Rabib's resolve. |
| `ad_sorte_morta` | toninho | Lotérica Sorte Morta: o bilhete mais azarado da região! Mas quem não joga não ganha, né? Faça sua fezinha na Sorte Morta. Jogue com moderação. |
| `ad_auto_center` | waldir | Auto Center Dois Mil: seminovos, troca e financiamento. Fusca, Opalinha, Gou... tem carro pra todo bolso, e a revisão sai por nossa conta. Auto Center Dois Mil, o futuro chegou mais cedo! |
| `ad_shopping_marconi` | katia | Shopping Marconi: dois andares, oito lojas e a escada mais charmosa da cidade! Lã de Aço, Pão de Sal, Açaí Atacadista e muito mais. Shopping Marconi: tudo num só lugar, sintonizado com você! |
| `ad_taxi_sintonia` | waldir | Precisa de carona? Rádio Táxi Sintonia! O táxi amarelinho da cooperativa, número noventa e seis, roda a cidade dia e noite. Chame pela rádio ou acene na calçada. Rádio Táxi Sintonia: sua corrida na frequência certa. |
| `ad_lambari_feliz` | toninho | Pesqueiro Lambari Feliz, no rancho! Traga a vara, a minhoca e a paciência. Se não pegar peixe, pelo menos pega um ventinho. Lambari Feliz: o peixe é feliz, você também. |

## Eventos (6)

| Id | Locutor | Texto |
|---|---|---|
| `ev_festa_sao_frequencio` | waldir | Neste fim de semana tem a Festa de São Frequêncio, na capela do rancho! Quentão, pamonha, bingo e forró com Zé da Van e Banda. Traga a família. A renda vai para a reforma do telhado da capela. |
| `ev_feira_praca` | katia | Domingo é dia de feira na praça! Barraca de pastel, caldo de cana, verdura fresquinha do rancho e o churrasquinho do Duvidoso. Das sete ao meio-dia. Vem passear! |
| `ev_torneio_pesca` | toninho | Torneio de Pesca do Lambari Feliz! Ganha quem pegar o maior peixe. Vale lambari, tilápia e até bota velha... mas bota não conta ponto. Inscrição na cooperativa. |
| `ev_quermesse` | waldir | A quermesse da Escola do Vale vai ter barraca de prenda, correio elegante e pescaria de brinquedo para a criançada. Sábado, a partir das seis da tarde. Compareça! |
| `ev_inauguracao_agro` | katia | Inaugurou! O Agro-Tudão abriu as portas na Estrada do Rancho! Na primeira semana, todo saco de ração sai com um sorriso do Seu Tonho de brinde. Passa lá! |
| `ev_show_966` | waldir | A Rádio 96.6 apresenta: Show da Frequência, ao vivo no estúdio, com Os Sintonizados e Trio Estática. Sexta-feira, às oito da noite. Sintonize ou venha assistir da calçada! |

## Comunicados da cidade (8)

| Id | Locutor | Texto |
|---|---|---|
| `co_obras_avenida` | prefeitura | A Prefeitura informa: a Avenida Tesla terá obras de recapeamento nesta semana. Use a Rua das Antenas como desvio e respeite a sinalização. Agradecemos a compreensão. |
| `co_neblina_serra` | prefeitura | Atenção, motoristas: neblina densa na Estrada da Serra durante a madrugada. Reduza a velocidade, acenda o farol baixo e mantenha distância do veículo da frente. |
| `co_sem_sinal` | waldir | Lembrete aos novos moradores: o minério da serra bloqueia celular e internet aqui no vale. Para recados, use o orelhão ou a Rádio 96.6. A gente leva a sua mensagem! |
| `co_coleta_lixo` | prefeitura | A coleta de lixo passa todos os dias. Deixe os sacos na calçada e não jogue sucata na caçamba: sucata vai para o Conserta-Tudo do Primo, que ainda paga por ela. |
| `co_onibus_966` | prefeitura | A linha de ônibus 966 roda a cidade o dia inteiro, dando a volta sem parar. Aguarde no ponto com a placa azul de ônibus e dê sinal para o motorista. |
| `co_estacionamento` | prefeitura | Meio-fio pintado de amarelo é proibido estacionar. Não pare na frente de portões, do Corpo de Bombeiros nem da Delegacia. Respeitar a sinalização é respeitar o vizinho. |
| `co_vacina_animais` | waldir | Atenção, criadores do rancho: campanha de vacinação dos animais na cooperativa, nesta quinta e sexta. Traga a caderneta. E não esquece o caramelo, que ele também é da família! |
| `co_entregador_seguro` | waldir | Recado da central de entregas: caixa frágil é para ir devagar na lombada. Encomenda inteira é cliente feliz... e entregador com o bolso cheio. Bom turno a todos! |

## Vinhetas da rádio (4)

| Id | Locutor | Texto |
|---|---|---|
| `vh_966_classica` | waldir | Rádio 96.6 FM. A voz do vale. |
| `vh_966_madrugada` | waldir | Madrugada na 96.6. Se você está acordado, a gente também está. |
| `vh_966_entregador` | katia | Noventa e seis ponto seis: a trilha sonora de quem trabalha na rua! |
| `vh_966_frequencia` | toninho | Você está na frequência certa. A errada é aquela ali do lado... que não pega aqui. |

## Depois de gerar

1. Ouvir tudo e regerar o que tiver pronúncia ou pausa estranha.
2. Normalizar em -16 LUFS e salvar em `Audio/Radio/Ads/` com o nome do `id`.
3. Anotar em `docs/ASSETS_E_SONS.md`: ferramenta, modelo de voz e licença do modelo (precisa permitir
   distribuir comercialmente os áudios junto com o jogo).
4. Avisar o Claude para ligar os áudios na grade da 96.6 (intervalo comercial a cada duas ou três músicas,
   sincronizado pelo relógio, como as músicas), escolhendo os anúncios pelo contexto (rancho ou cidade, eventos
   da semana, loja inaugurada).
