# Dead Frequency — roadmap da cidade simulada

Atualizado em 12/11/2026 (v0.9.8 lançada). A partir da v0.9 o jogo deixa de ser só "entregas" e vira uma
**cidade simulada na zona morta**: cada jogador escolhe uma profissão, ganha um apartamento, ouve a
rádio 96.6 e ajuda a cidade a funcionar. Ordem sugerida: de cima para baixo; cada fase deixa o jogo jogável.

## Onde estamos (v0.9)

| Área | O que já tem |
|---|---|
| **Profissões** | Entregador (turnos de caixas), Taxista, Gari, Repositor, Frentista, Locutor da 96.6. Escolha no menu, troca na pausa; no online o criador da sala pode deixar o servidor sortear. Pagamento conferido pelo servidor (valor e intervalo mínimo). |
| **Rádio 96.6** | Estação com estúdio e torre, 5 músicas originais sincronizadas pelo relógio (todo mundo ouve o mesmo trecho), rádio na caminhonete (R), no escritório, mercado, saguão e apartamentos. Planejado: comerciais das lojas, anúncios de eventos e comunicados da cidade com áudio pt-BR pré-gerado por TTS e sincronizado entre jogadores. |
| **Moradia** | Edifício Sintonia: um apartamento por jogador (elevador no saguão), cama que salva o jogo, TV com canais, rádio. |
| **Lugares com interior** | Depósito e escritório, Supermercado Atacadinho (corredores, seções, caixas), rádio, saguão e apartamentos. |
| **Cidade** | Bancos, lotéricas, padarias, mercados, postos, lava-jato, caixa d'água, bombeiros, delegacia, ônibus 966, pedestres que desviam, outdoors iluminados, orelhões. Fase 2: rancho. |
| **Online** | Servidor oficial (Cloudflare), servidor próprio ou hospedar no PC com link público, lista de salas públicas. |
| **Gente e bichos** | Personagens animados (Quaternius) para jogadores e NPCs, bichos no sítio, caramelos. |
| **Distribuição** | Release automático no GitHub (Windows e Linux) com SHA256SUMS e atestado de build, launcher que atualiza sozinho, site em www.teuzin.com.br/dead-frequency. Builds da Internal (`vX.Y.Z.N-pre`) também trazem o APK do Android. |

---

## Planejamento de conteúdo (v0.9.5+)

### v0.9.5 — Calçada viva ✅
Conecta os itens com a "gente de verdade" da v0.9.4.

| Ideia | Como funciona no jogo |
|---|---|
| **O "trato" no caramelo** | Caramelos andam pela cidade e moram na varanda dos sítios. Use um **Espetinho** perto de um: ele segue você por 3 minutos, late de vez em quando e, no sítio, espanta os **pernilongos** da noite. |
| **Diálogos de calçada** | Pedestres, comerciantes e caixas soltam balões curtos estilo PS1 quando você passa perto: frases do dia a dia e a eterna briga do biscoito × bolacha (comentam a caixa que você carrega). |
| **Passageiros exigentes** (taxista) | Cada passageiro tem um jeito, que aparece no painel: *executivo* (roupa social: quer a Rádio 96.6 ligada, sem passar de 60 km/h e sem batida — paga bônus ou desconta), *apressado* (bônus se chegar rápido) e *tranquilo*. |
| **Guaraná Pinguim** | Lata verde: recupera o fôlego e acelera a recuperação por um tempo. |
| **Café Marreta** | Garrafa térmica: corta a leseira (sono da madrugada) na hora e segura por alguns minutos. |
| **Marmitex Tia da Quentinha** | Enche todo o fôlego, mas dá o "efeito pós-almoço": 10 s mais lento. |
| **Chinelos Legítimas** | Calçado: anda mais rápido com o "ploc-ploc" nos passos. Correndo na lama do sítio (ou de vez em quando no asfalto) a tira pode arrebentar; o **Prego** conserta. |
| **Fôlego e sono** | Correr gasta fôlego (barra no HUD). Entre meia-noite e 5h bate a leseira: a tela escurece nas bordas e você anda mais devagar. |
| **Botijão do Gás do Zé** | O botijão que já existia agora vaza: arremessado ou caindo forte da caçamba, solta um chiado com gás saindo e perde integridade até parar. |
| **Churrasquinho do Duvidoso** | Barraca de calçada: depois das 18h acende a chapa, solta fumaça e junta gente em volta. Vende espetinho, guaraná, café e marmitex. |

### v0.9.6 — Oficina (bugs e feedback dos testes) ✅
Tudo que apareceu jogando a v0.9.5. Vem antes de qualquer conteúdo novo.

| # | Tipo | O que corrigir |
|---|---|---|
| 1 | Bug | **Fusca com os vidros fora do lugar** (a estufa não acompanha a carroceria arredondada). |
| 2 | Bug | **Sirene da viatura duplicada** (o giroflex do modelo + o que o jogo desenhava por cima): deixar uma só. |
| 3 | Bug | **Jogador dirigindo some para os outros** no online: mostrar o boneco sentado no banco do veículo. |
| 4 | Feedback | **Balanço do carro dá vertigem**: câmera estabilizada (o horizonte não gira com a carroceria), suspensão mais firme e menos rolagem nas curvas; opção nas Opções. |
| 5 | Feedback | **Volante gira** quando você vira. |
| 6 | Bug | **Van sem chão na cabine** e **sem lugar para passageiro**: chão de verdade e banco do carona para outro jogador entrar (online). |
| 7 | Feedback | **Personagem virou mulher sozinho**: escolher homem ou mulher e personalizar (cor da pele, do cabelo, da camisa e da calça) no menu, visível para todos no online. |
| 8 | Bug | **Parede invisível no fundo do sítio**: muralha/serra de verdade no limite do mapa, como na cidade. |
| 9 | Feedback | **Modelo do botijão** melhor (corpo, alça e válvula). |
| 10 | Visual | **Ícone novo** do jogo e do launcher, e a etiqueta amarela **ALPHA** ao lado do número da versão (menu, launcher, site e ícone). |

### v0.9.7 — Mãos ocupadas (itens e online) ✅
Já feito nesta build (feedback de 08/11):
- ✅ Texturas da cidade: fachadas (reboco, tijolo, pastilha, chapisco), calçada, pedra portuguesa, asfalto.
- ✅ Mochila virou bag quadrada de entregador, presa às costas dos bonecos novos.
- ✅ Ponto de ônibus e frente da concessionária sem carro estacionado.
- ✅ Placas e nomes dos carros no lugar; Fusca com grade, logo e retrovisores certos.
- ✅ Lojas com abas e estoque por vendedor (Zé, Churrasquinho, Atacadinho); pausa sempre por cima.
- ✅ Grama: textura nos gramados e no sítio + capim pixel art balançando (some além de 45 m).
- ✅ Otimização: peças pequenas somem na neblina, luz de poste apaga longe.
- ✅ Auto Center Dois Mil (ao lado do depósito): Gou, Fuska e Opalinha a $ 2000; online o carro é da sala.
- ✅ Menu inicial: manter o menu atual; o redesign foi retirado desta versão.
- ⏭ Sons novos (passos e ambiente) adiados; fontes CC0 definidas em `docs/ASSETS_E_SONS.md` (Pixabay descartado).

| Ideia | Como funciona no jogo |
|---|---|
| ✅ **Barra rápida 1–4** | Os 4 primeiros espaços do inventário viram atalhos (teclas 1–4): selecionar mostra o item na mão; clique esquerdo usa ou equipa. A mochila aumenta o inventário, a barra continua de 1 a 4. |
| ✅ **Largar e entregar itens** | **G** larga o item no chão (qualquer um pega). Mirando em outro jogador com o item selecionado: **[E] Entregar item**. |
| ✅ **Ver o que os outros seguram** | O servidor repassa o item selecionado na mão / a caixa que cada jogador está carregando. |
| ✅ **Vender para o Zé** | Na barraca do Zé, aba **Vender**: ele recompra itens da mochila por 50% do preço, arredondado para baixo; o servidor valida a transação. |
| ✅ **Chat da sala (T)** | Mensagens de texto na sala, com limite de 3 mensagens a cada 5 s no servidor. O painel admin mostra totais por dia UTC/sala (retenção de 30 dias), sem guardar o conteúdo. |
| ✅ **Salas permanentes do admin** | No painel admin: criar salas fixas, públicas ou com senha; senhas são geradas na criação e mostradas uma vez. Já existem `ADMNA`/`ADMNB` (privadas) e `SERVA`/`SERVB` (públicas); salas permanentes não expiram nem podem ser encerradas. Protocolo 6 valida a senha na entrada. |

### v0.9.8 — Rua de verdade ✅
| Ideia | Como funciona no jogo |
|---|---|
| ✅ **Céu procedural** | Nuvens em movimento, estrelas e cores sincronizadas ao ciclo de dia e noite. |
| ✅ **Meio-fio pintado** | Amarelo onde é proibido parar (bombeiros, delegacia, entradas de loja, postos, paradas de ônibus e concessionária) e branco intercalado onde pode estacionar. Os carros gerados só estacionam no branco. |
| ✅ **Placas de proibido estacionar** | Nos trechos amarelos; na delegacia e nos bombeiros com a plaquinha embaixo "POLÍCIA" / "BOMBEIROS". |
| ✅ **Bocas de lobo e bueiros** | Bocas de lobo com grade junto ao meio-fio e tampas de bueiro na rua. Perto delas dá para ouvir a água correndo embaixo. Sem rede de túneis. |
| ✅ **Ciclovia e ciclista** | Faixa de ciclovia pintada e um ciclista NPC que roda pela cidade como o ônibus (posição pelo relógio, igual para todos) e toca o sininho quando você está na frente. |
| ✅ **Polícia** | Faixa identificada na fachada da delegacia, ícone no minimapa e policiais NPC fardados (só figurantes por enquanto). |

Ideias para depois (pesquisa de 12/11 sobre as regras do CTB):
- **Meio-fio amarelo "de morador"**: em bairros residenciais, trechos pintados de amarelo por conta própria, sem placa (é irregular na vida real e não proíbe nada). Toque de realismo para quando houver multas.
- **Multas do Art. 181 do CTB** (quando a carreira tiver economia): estacionar a menos de 5 m da esquina, em frente a guia rebaixada, sobre a calçada/faixa/ciclovia, em frente a hidrante ou no ponto de ônibus, ou a mais de 50 cm da guia.
- **Meio-fio e calçada com mais detalhe**: rachaduras com normal map e vaga azul de idoso/PCD pintada no asfalto.

### v0.9.9 — Lojas de verdade ✅
| Ideia | Como funciona no jogo |
|---|---|
| ✅ **Prédios do tamanho da loja** | Fachadas comerciais aleatórias agora são lojas térreas independentes; o Atacadinho e os postos já ocupam quarteirões próprios. |
| ✅ **Shopping** | Shopping Marconi com oito balcões interativos, dois pisos e escada com rampa física walkable. |
| ✅ **Táxi amarelo** | O taxista ganha o próprio carro (modelo PSX da GGBot, CC0) no pátio da central, com o nome da cooperativa fictícia "Rádio Táxi Sintonia" e o adesivo do app paródia "66". Passageiro só embarca no táxi. |
| ✅ **Van de viagem** | Van anos 80 "Fórdi Viajante" (creme e marrom com faixas, janela bolha e bagageiro), estacionada no pátio da central: a van do Modo História. |
| ✅ **Menus novos por código** | Opções > Interface > Menu inicial: ORBITA966, CINEMA966 e TERMINAL966 ligam os menus novos (fundo com a própria cidade), VISUAL966 libera os três para escolher e CLASSICO966 volta ao menu clássico. |
| ✅ **Central e rádio** | Portão novo do lado esquerdo da central (feedback dos testes), grade de metal em cima dos muros da central e da rádio, e portãozinho de pedestre na lateral da rádio. |
| ✅ **Céu novo** | Céu 100% procedural (nuvens, sol, lua com relevo e estrelas) ligado ao ciclo de dia e noite. |
| ✅ **Agro-Tudão (casa de rações)** | Galpão na Estrada do Rancho, logo depois da cooperativa (Fase 2), com placa verde, pilhas de ração e o balcão do Seu Tonho. Carga nova **Saco de Ração 25 kg** (só no sítio, pesa 3x no sorteio): nasce na frente do galpão e vai para os sítios. A loja vende o **Repelente Espanta-Muriçoca** (5 min sem pernilongo), luva e conserto. Sorteio de carga por fase: `CargoRules.Pool` = `cargoPool()` no servidor. |
| ✅ **Conserta-Tudo do Primo** | Barraca em frente ao depósito. O gari acha sucata na rua (rádio velho, cano, roda) e vende ao Primo pelo mesmo valor do saco; o Primo vende a lanterna de longo alcance (dobro do alcance, basta estar na mochila), kit de reparo, fita e prego. Pilhas para a visão noturna ficaram de fora: ela não gasta bateria hoje. |
| ✅ **Lã de Aço 1001 Utilidades** | Balcão no Shopping Marconi (no lugar das Lojas Marcianas) com palha de aço Brilhaço, fita e prego. Na TV do apartamento, a palha de aço na antena acaba com o chuvisco. O repositor às vezes repõe fardos de palha de aço (carga leve). |

### v0.10 — Modo História inicial e Dead Frequency no celular (Android)
Escopo fechado em 13/11/2026: **Modo História inicial + versão Android inicial + carros anunciantes**. Quando
estas etapas terminarem, a v0.10 sai primeiro só na Internal; o público (`main`) vem depois, quando o Matheus liberar.
Planejado em 12/11/2026 (antes a v0.10 era a Carreira, que passou para a v0.11). Objetivo: jogar e testar no
celular, nos dois aparelhos do Matheus e em outros. Depende da otimização para PC fraco (`hotfix/pc-fraco`,
streaming por chunks) estar na Internal.

| Ideia | Como funciona no jogo |
|---|---|
| 🚧 **Exportar para Android** | Preset "Android" no `export_presets.cfg` (C#/.NET, arm64, sem gradle) e workflow `android-test.yml`, que gera um APK debug como artefato (botão Run workflow ou push em `android/**` / `feat/android-*`). No celular o renderizador é o Compatibility, com texturas ETC2/ASTC. Falta: chave de release (segredo do Matheus), APK no prerelease da Internal e testar nos aparelhos. |
| ✅ **Controles de toque** | `Scripts/UI/TouchControls.cs` (autoload, desenhado em código): joystick que nasce onde o dedo encosta, arrastar à direita mexe a câmera, botões USAR, PULAR/FREIO, ITEM/JOGAR, SOLTAR, CORRER e, no canto de cima, PAUSA, MOCHILA, LUZ, VISÃO e RÁDIO/ESTAÇÃO na van. Com tela aberta só aparece o VOLTAR. Tudo vira ação do Input Map. Opções > Controles > Toque: automático/sempre/nunca e tamanho dos botões. |
| 🚧 **Modo gráfico leve** | Primeira vez no celular (`GameSettings.ApplyMobileDefaults`): qualidade Baixa (sem sombras nem brilho), resolução 3D 70%, sem antisserrilhado, 30 fps; câmera enxerga até 160 m e a neblina fica 2,2x mais densa para esconder o corte. Começa em primeira pessoa. Falta medir nos aparelhos e ajustar o raio dos pedaços da cidade. |
| ✅ **Menos chamadas de desenho** | `StaticBatcher`: quando um quarteirão fica pronto, as caixinhas e cilindros estáticos viram poucas malhas (cores lisas viram cor de vértice; cada carro estacionado vira uma ou duas malhas). Medido na fase de teste em modo celular: avenida de 5.829 para 1.757 chamadas por quadro, central de 3.381 para 1.357, praça de 1.043 para 483. Colisões e aparência não mudam; `--no-batch` desliga para comparar. Ainda dá para juntar: letreiros (`Label3D`), pintura de guia, semáforos de pedestre e orelhões. |
| ✅ **Menus e HUD para tela pequena** | Tela cheia sempre (modo imersivo, sem barra de notificação nem de gestos). Interface 1,6x; menu inicial com layout próprio (`MainMenu.Mobile.cs`: botões grandes em duas colunas, telas que rolam com o dedo); HUD do jogo encolhido para ~1,1x; dicas de teclado escondidas; botão CÂMERA nos controles de toque. Para testar no PC: rodar com `-- --mobile`. |
| ✅ **Personagem do jogador novo** | Entregador e entregadora feitos no Meshy (low-poly ~4 mil triângulos, textura 512 px sem filtro) com esqueleto e 6 animações: parado, andar, correr, sentado (dirigir/carona), pulo e carregando caixa. As cores do menu Personagem continuam valendo: um shader pinta camiseta, calça, pele e cabelo por máscara, mantendo as sombras. Vale para você, para os outros jogadores no online, para o menu e para a mochila; NPCs e vendedores ganham modelos novos depois. |
| ✅ **Caminhonete e Fuska novos** | Modelos do Meshy (~5–6 mil triângulos, textura 512 px sem filtro). Caminhonete de entregas com calotas: só a carroceria muda, interior, volante animado, rádio e colisões da caçamba continuam. Fuska pintado por máscara (cada um com a sua cor: estacionados, concessionária e o comprado). **Placas** cinzas dos anos 90 ("SINTONIA · SP" + ABC-1234) diferentes em cada carro (`Plates`, pelo NetId ou pela ordem de geração); a da empresa é DFQ-1996. |
| ✅ **Moradores, barracas e viaturas novos** | Seis moradores do Meshy (senhor, rapaz, homem de escritório, moça, dona e senhora) com parado, andar, sentado e conversar gesticulando, pintados por máscara com as cores de cada pedestre (dá dezenas de combinações). Barracas dos vendedores com o toldo na cor de cada um, churrasquinho do Duvidoso completo (guarda-sol, churrasqueira, isopor) com brasa e fumaça, caminhão dos bombeiros e viatura da polícia novos. O caramelo agora é caramelo de verdade (pelagem caramelo e creme, orelha caída). |
| ✅ **Revisão visual da central** | Moto de entregas 125 cc nova (Meshy) com baú térmico do **DF Entregas** (logo nos três lados); paleteira nova; caixas do galpão com textura (fita, setas, taça) e etiquetas em imagem (FRÁGIL, ESTE LADO PARA CIMA, lojas) coladas na caixa; etiquetas em imagem também nas caixas das entregas; quadro de avisos, funcionário do mês e paisagem do vale como quadros de verdade; muro da central rebocado com barrado verde e rachaduras; galpão de telha metálica. Moradores: cabelo e lenço ficam como no modelo (a máscara pintava em pedaços). **Primeira pessoa nos carros:** olhos dentro da cabine (antes ficavam acima do teto), painel, volante e forro simples, e os vidros opacos somem para quem está dentro. |
| ✅ **Modo História inicial** | Menu Iniciar turno → Modo História (offline). Capítulo I na cidade com meta no HUD; meta cumprida libera a saída leste e a viagem no piloto automático até o rancho (capítulo II "Asfalto e Barro"). Metas e títulos definitivos: a decidir pelo Matheus (`Scripts/Story/StoryChapters.cs`). Detalhes na seção "Modo História e novas fases". |
| ✅ **Carro de som** | Kombi amarela com cornetas no teto rodando devagar (~15 km/h) pelo anel 2 da cidade, o tempo todo, anunciando as lojas e eventos paródia da cidade (os mesmos roteiros da 96.6). Posição pelo relógio, igual ao ônibus: online todo mundo vê no mesmo lugar. Por enquanto a fala aparece num balão em cima do carro. |
| ✅ **Carro do ovo (evento)** | Picape com bandejas de ovo e placa "OVO! 30 por R$ 5" que aparece alguns minutos a cada 15 (aviso na tela quando chega), dando a volta no anel do centro no sentido contrário ao ônibus. Também só com balão por enquanto. |
| ✅ **Dirigibilidade** | Carros bem mais difíceis de capotar: centro de massa mais baixo, estabilizador em código (barra estabilizadora "de jogo"), limite de rolagem/arfagem, menos esterço em alta velocidade e desvirar sozinho se ficar de lado parado. No teste de curva fechada a 94 km/h, Fusca e táxi capotavam; agora inclinam no máximo 7°. **Saída do carro:** o jogador procura um lugar livre com chão (porta, outro lado, atrás, frente, por cima) em vez de aparecer dentro do muro ou abaixo do chão. |
| ✅ **Conforto (enjoo)** | Opções > Jogo > Conforto: **Reduzir movimento** (liga tudo de uma vez), câmera estável no carro, vinheta nas bordas com o carro rápido e ponto fixo no meio da tela. O balanço da câmera já vinha desligado. Baseado nas Game Accessibility Guidelines e Xbox Accessibility Guidelines. |
| **Áudio dos carros anunciantes** | Gerar as vozes (VoiceStudio/TTS, mesmas vozes da 96.6) e colocar em `Audio/Street/<id>.ogg`: os ids dos anúncios (`ad_*`, `ev_*`) e do carro do ovo (`ovo_01`..`ovo_04`) estão em `Scripts/World/AnnouncerCar.cs`. O carro toca o áudio sozinho quando o arquivo existe. Depois: vender ovo de verdade (item novo + preço no servidor). |
| **Online entre PC e celular** | Mesmo servidor e protocolo (WebSocket): sem mudança no `server/`. Testar sala com PC e celular juntos. |
| 🚧 **Distribuição** | O prerelease da Internal (`vX.Y.Z.N-pre`) traz o `DeadFrequency-…-android.apk`. **Atualizador no próprio jogo** (`AppUpdater`): no celular o menu confere o GitHub, mostra "Nova versão", baixa o APK com progresso e abre o instalador do Android (se não abrir, usa o navegador). Quem tem build da Internal recebe os `-pre`; quem tem o público, só os públicos. O CI grava o build exato em `application/config/version` (`GameVersion.Build`). Instalar por cima precisa da chave do Matheus (segredos `ANDROID_KEYSTORE_*`); com chave de debug, desinstale antes. Link no site e Play Store ficam para depois. |

### v0.11 — Carreira
| Ideia | Como funciona no jogo |
|---|---|
| **Níveis de profissão** | Cada tarefa dá experiência na profissão. A cada nível: bônus em dinheiro por tarefa e itens de presente (ex.: luvas no nível 3 de entregador, café grátis para o taxista da madrugada). Salvo no save e conferido pelo servidor no online. |

### v0.11.x — Trânsito de verdade
| Ideia | Como funciona no jogo |
|---|---|
| **Trânsito procedural** | Carros andando pelas ruas, parando no semáforo e no PARE. |
| **Duno Mille (com escada)** | Hatch quadrado; com a escada de fibra no teto, ignora o limite de velocidade e anda mais rápido que a viatura. |
| **Maré-Azar 20V** | Sedã esportivo com temperatura escondida: acelerador no talo na estrada de terra → fumaça preta e motor morto (som pronto). |

### Infraestrutura (em paralelo)
| Tarefa | Detalhe |
|---|---|
| **Launcher menor** | Hoje o launcher leva o motor Godot inteiro dentro do .exe (~105 MB descompactado), por isso é maior que o zip do jogo (que é compactado). Dá para cortar para ~25–40 MB compilando um "template" do Godot só com o que o launcher usa (sem 3D e física) no CI. |
| **Atualização incremental** | Em vez de baixar o zip inteiro a cada versão: publicar um manifesto com o hash de cada arquivo e o launcher baixar só o que mudou. O maior arquivo é o `.pck` (todos os assets); separar em um `.pck` base (modelos, sons, texturas — muda pouco) e um de patch (código e cenas) deixa a maioria das atualizações com poucos MB. |
| **Arte gerada** | Texturas/sprites de muros e fachadas com o SpriteCook e um modelo de jogador novo com o Meshy, quando os conectores estiverem ligados na conversa (hoje não estão). |
| **Escalabilidade multiplayer (Cloudflare + Oracle Cloud)** | Medir e reduzir o tráfego WebSocket no Cloudflare Workers/Durable Objects. Manter registrada a possibilidade de migrar o servidor de salas para uma VM gratuita Oracle Cloud Always Free (ou operar em paralelo: Cloudflare para API/autenticação e Oracle para sessões WebSocket), após validar latência, disponibilidade, custos, segurança, persistência e um mecanismo de failover. Não migrar nem fazer deploy automaticamente; primeiro comparar métricas e testar uma implementação compatível com o protocolo. |

## Fase 1 — Polimento da v0.9 (curto prazo)
| Tarefa | Por quê | Esforço |
|---|---|---|
| Partida online com 3+ PCs, uma pessoa em cada profissão | Validar pagamentos, sorteio de profissões e apartamentos | 1 sessão |
| Mostrar a profissão de cada jogador no nome em cima da cabeça e no minimapa (a pausa já lista) | Saber quem faz o quê | pequeno |
| Sacos de lixo e passageiros visíveis para todos no online | Hoje cada um vê as próprias tarefas | médio |
| Visitar o apartamento de um amigo (campainha no saguão) | Social | pequeno |
| `MultiMeshInstance3D` nos objetos repetidos e `visibility_range` nos detalhes | FPS | médio |
| Chat (T) | Pedido comum | médio |

## Fase 2 — Economia da cidade
- **Carteira individual + caixa da cidade:** cada profissão paga o jogador; uma parte vai para a prefeitura (imposto) e libera melhorias para todos (postes novos, ônibus extra, reforma de prédios).
- **Contas do apartamento:** aluguel e luz por dia de jogo; atrasou, a TV e o rádio cortam.
- **Mercado de verdade:** comprar comida no Atacadinho (energia/fome), com estoque que o repositor abastece; se ninguém repor, as prateleiras esvaziam para todos.
- **Preço do combustível** varia com os postos atendidos pelo frentista; a caminhonete passa a gastar combustível (fase 3).
- **Ranking semanal** por profissão (Cloudflare D1).

## Fase 3 — Painel da caminhonete (Módulo 3 do plano original)

✅ **Primeira versão pronta (painel diegético):**

- **Mostradores no lugar do HUD:** velocímetro, conta-giros, gasolina, temperatura e luzes-espia (bateria, temperatura, reserva, fusível, farol) atrás do volante. Em primeira pessoa na caminhonete o "km/h" do HUD some; na câmera de trás ele continua.
- **Monitor CRT verde** (`DashMonitor` + `Shaders/Vehicle/crt_monitor.gdshader`) com três telas trocadas no botão TELA: entregas do turno (distância e integridade), sistema (bateria, tanque, motor, fusíveis, interferência) e rádio.
- **Botões físicos** (`DashboardButton`, mire e aperte E sentado): CHAVE, FAROL, LUZ (cabine), RÁDIO e TELA. A tecla da lanterna (e o botão LUZ do celular) vira o farol quando você está dirigindo.
- **Motor** (`EngineSystem`): partida, tanque de 50 L, temperatura (pé no fundo em alta ferve e perde força até esfriar). Sem gasolina dá para ir devagar "no cheiro"; **galão de gasolina** nas bombas dos postos e no Agro-Tudão (meio tanque).
- **Elétrica** (`ElectricalSystem`): bateria que arria se esquecer o farol ligado com o motor desligado (aí pega no tranco), alternador, quatro **fusíveis físicos** (faróis, rádio, painel, luz interna) na caixa do lado do carona, com 2 reservas; fusível avulso no Conserta-Tudo, Lã de Aço, Agro-Tudão e postos. Sem fusível, vale gambiarra com palha de aço (até dar curto).
- **Interferência da carga** (`EmInterference`): gerador, peças de rádio e fitas na caçamba fazem o rádio chiar, os ponteiros tremerem e o monitor chuviscar; com o rádio ligado podem queimar o fusível dele. Batida forte também queima fusível.
- Tanque e bateria da caminhonete da empresa ficam no save (`user://save.tzn`). No online cada jogador tem o seu estado (o servidor só conhece os preços: `fusivel`, `galao`).

**Falta:**

- **Rádio com dial:** sintonizar a 96.6 e estações piratas com chiado; a 96.6 interrompe para notícias da cidade (entregas feitas, apagões, eventos).
- Abastecer direto na bomba com a mangueira (hoje é galão) e levar o painel para os outros veículos (moto, caminhões, carros).
- Sincronizar tanque, bateria e fusíveis da caminhonete no online.
- Esconder também o painel de entregas do HUD quando o monitor estiver ligado (opção nas Opções).

## Fase 4 — Mais profissões e tarefas
| Profissão | Tarefa |
|---|---|
| **Motorista de ônibus** | Dirigir o 966 pela linha e parar nos pontos no horário |
| **Policial** | Multar quem corre perto da delegacia, achar a van "roubada" |
| **Bombeiro** | Apagar incêndios em prédios aleatórios com o caminhão |
| **Mecânico** | Consertar carros quebrados na rua e a caminhonete dos outros |
| **Padeiro** | Assar pães de madrugada e abastecer as padarias |
| **Carteiro (Correrios)** | Rotas de cartas a pé, caixa por caixa |
| **Lavador** | Lava-Jato Molhadinho: lavar carros que chegam |
| **DJ da 96.6** | Montar a programação (ordem das músicas) para todos os jogadores |

Cada profissão nova = um `JobBase` em `Scripts/Jobs/` + a regra em `PROFESSIONS` (servidor e LocalServer).

## Fase 5 — Mundo
- Mais interiores: banco (sacar/depositar), lotérica (raspadinha), padaria, farmácia 24h.
- Novos bairros: **morro** com ladeiras e vielas, **centro** com prédios altos, **porto** com contêineres.
- Trânsito de verdade: carros andando, semáforos, motoristas que respeitam o PARE.
- Eventos da noite: apagão num bairro, neblina densa, orelhões tocando sozinhos, chiado na 96.6 que leva a uma entrega secreta.
- Decoração do apartamento comprada nas lojas (sofá, pôster, planta).

## Fase 6 — Visual PS1 (Módulo 5 do plano original)
- ✅ **Filtro de tela PS1** (`Shaders/ps1_screen.gdshader`, numa camada abaixo da interface): pixels em múltiplo inteiro (360 ou 480 linhas), cores de 15 bits e dithering ordenado 4×4. HUD e menus continuam nítidos.
- ✅ **Tremor de vértices** (`Shaders/ps1_common.gdshaderinc`, global `ps1_jitter`) nos modelos do Meshy: jogador, moradores, caminhonete, Fuska, barracas e viaturas.
- ✅ **Opções > Gráficos > Visual PS1:** Desligado, Leve (padrão no PC) ou Completo. No celular começa desligado.
- Falta: tremor também nos prédios e no chão (precisa trocar os materiais padrão por shaders próprios sem abrir frestas) e textura "afim" (sem correção de perspectiva).
- Monitor CRT verde dentro da caminhonete mostrando o radar e a música da 96.6.

## Fase 7 — Lançamento
- Ícone, tela de carregamento e página no itch.io com trailer.
- Integração com a Twitch: o chat da live vota em eventos (apagão, chuva, entrega bônus) e pede música na 96.6.
- Mais músicas na rádio (aceitar faixas da comunidade com licença livre).


## Expansão da cidade (ideias adicionadas em 09/11/2026)

As fases abaixo são propostas de planejamento; a ordem pode mudar conforme testes, dependências e escopo das builds.

### v0.12 — Bairros e infraestrutura urbana
- **Área residencial simplificada:** bairro de casas térreas, sem prédios altos, inspirado em conjuntos habitacionais como CDHU ou em um condomínio simples. Avaliar como uma área separada/mapa para manter a cidade principal mais leve.
- **Praça e parque:** área de convivência com bancos, árvores, iluminação e eventos comunitários.
- **Playground:** parquinho próximo ao bairro residencial.
- **Prefeitura e Boulevard:** rua de paralelepípedo, mão única e prédio da prefeitura, formando uma área cívica/comercial característica.
- **Correios (Correrios):** reformar a agência com entrada clara, balcão de atendimento, caixas de correspondência e uma van amarela inspirada na Fiat Scudo estacionada na entrada.
- **Canteiros de obras permanentes:** construções inacabadas que fazem parte da paisagem e mudam conforme a cidade evolui.
- **Obras viárias:** cones, cavaletes, máquinas e desvios temporários ou permanentes, com sinalização coerente.
- **Sinalização ampliada:** mais placas de trânsito, orientação e advertência, integradas às regras de circulação.
- **Calçadas e meio-fio:** corrigir os encontros entre calçada, guia e rua; revisar pintura e sinalização de estacionamento para que sejam consistentes.
- **Postes e fiação:** postes com emaranhados de fios, ligações clandestinas ("gatos") e cabos de telecomunicação.
- **Infraestrutura de internet:** cabos ADSL antigos abandonados e fibra óptica chegando às casas.
- **Antenas e parabólicas:** modelos de 60 cm convencionais; antenas clandestinas de TV a cabo ("gato Net") com identidade visual fictícia; marcas fictícias inspiradas em operadoras populares (Céu, Escuro, Antena da Tchau e Antena da Morto); parabólica amarela Centauro de 150 cm; modelos rurais de tela/grade Centauro de 200 e 340 cm.

### v0.13 — Zona rural, água e pesca
- **Rios e lagos rurais:** corpos d'água distribuídos pelo mapa rural, com margens e vegetação próprias.
- **Pontos de pesca:** locais acessíveis nas margens, com áreas apropriadas para pescar.
- **Sistema de pesca:** varas, iscas, espera/ fisgada e diferentes espécies de peixes, com raridades e valores distintos.
- **Agricultura:** plantações e áreas cultivadas, ampliando as atividades do sítio e as entregas rurais.

### v0.14 — Comércio, indústria e energia
- **Rodoviária/terminal de ônibus:** terminal como origem e ponto de organização das linhas intermunicipais/urbanas.
- **Restaurantes:** estabelecimentos com identidade própria, salão, balcão e refeições.
- **Área industrial:** galpões, pátios de carga e circulação de veículos de serviço.
- **Usinas de energia:** instalações solares e eólicas como marcos visuais da região.
- **Data centers:** complexo técnico com geradores, refrigeração e infraestrutura de rede, como parte do cenário e de possíveis tarefas/eventos.

### Rádio 96.6 — publicidade local e agenda de eventos
A rádio deve anunciar os próprios estabelecimentos e acontecimentos do mundo de Dead Frequency, em vez de depender de comerciais genéricos.
- **Anúncios das lojas:** ofertas do Atacadinho, preços e promoções dos postos, novidades do Agro-Tudão, serviços do Conserta-Tudo do Primo, ofertas da Lã de Aço 1001 Utilidades, pratos de restaurantes, campanhas de farmácias e inaugurações.
- **Anúncios de eventos:** festas, shows, feiras, atividades na praça, eventos rurais, inaugurações e programações comunitárias.
- **Comunicados da cidade:** obras, ruas interditadas, desvios, avisos da prefeitura e informações úteis aos moradores.
- **Conteúdo contextual:** selecionar anúncios conforme promoções ativas, eventos próximos/em andamento, estabelecimentos recém-inaugurados e condições atuais do mundo.
- **Roteiros e vozes:** criar textos curtos em pt-BR e usar locutores distintos (rádio local tradicional, varejo animado, voz institucional e anúncios bem-humorados).
- **Áudio pré-gerado:** gerar os arquivos com TTS durante o desenvolvimento, revisar pronúncia/pausas/volume, normalizar e incluir os áudios no projeto. A execução do jogo não deve chamar APIs, exigir internet nem consumir créditos.
- **Programação e sincronização:** inserir comerciais e comunicados na grade da 96.6; sincronizar o momento/identificador do anúncio entre jogadores para que todos ouçam o mesmo conteúdo. Conteúdo pode variar de acordo com os eventos e estado do mundo.
- **Jingles:** criar vinhetas curtas para marcas e estabelecimentos fictícios.
- **Licenciamento:** confirmar que o plano e os termos do serviço TTS permitem distribuir comercialmente os áudios gerados junto com o jogo.

### Outras prioridades de mundo (a distribuir entre as fases)
- Farmácias e escolas como edifícios reconhecíveis, com fachadas e interiores quando fizer sentido para as atividades.
- Mais restaurantes e serviços de bairro, evitando concentrar todo o comércio em prédios genéricos.
- Revisar a malha viária e a sinalização sempre que novos bairros, obras ou rotas forem adicionados.


---

## Modo História e novas fases (planejado em 12/11/2026)

Documentação completa em [`docs/README_PARA_IAS.md`](docs/README_PARA_IAS.md). Tom: cotidiano e aventura leve, **sem terror**.

### Em andamento — Mundo e polimento (foco atual)
- [ ] **Fase 1 · Cidade (ambientação Brazilcore):** lombadas com física de suspensão, asfalto remendado/bueiros, postes com fios e cartazes, orelhões, lixeiras de grade, calçadas irregulares e árvores com a base pintada de branco.
- [ ] **Fase 2 · Rancho (reconstrução):** estradas de terra vermelha em circuito, sítios, *packing house* de banana/palmito, lago com pesqueiro e sinalização rústica.

### Próximas — Imersão e novos cenários (cenas isoladas)
- [ ] **IK nos personagens:** `TwoBoneIK3D` para mãos na caixa/saco/volante e `LookAtModifier3D` para NPCs olharem o jogador.
- [ ] **Otimização de renderização:** `MultiMeshInstance3D` para vegetação/postes e *occlusion culling*.
- [ ] **Fase 3 · O Bananal (`Bananal.tscn`):** navegação pelo sinal da 96.6 (radar sonoro).
- [ ] **Fase 4 · Gruta da Estática (`Gruta.tscn`):** caverna de calcário com salas modulares, lanterna e visão noturna.
- [ ] **Fase 5 · Estrada da Serra (`Serra.tscn`):** subida sinuosa com neblina e teste de integridade da carga.

### Modo História (offline)
- [x] Progressão desacoplada (`StoryManager` + `JobEvents`), salva no `save.tzn` (v0.10). Menu: Iniciar turno → **Modo História** (continuar / recomeçar). HUD com capítulo e meta (`StoryDirector`); linha no F3.
- [x] Piloto automático na `VanController` + `ChapterTransitionZone` (loading disfarçado na estrada, rádio ligado e banner retrô). Cidade → Rancho pela saída leste, com feixe verde e placa "ESTRADA DO RANCHO" quando a meta é cumprida.
- [x] **Campanha escrita** (13/11/2026, `docs/lore/01_LORE_E_CAPITULOS.md`): "Dead Frequency", 7 capítulos sobre salvar a Repetidora Landell, que leva o sinal da 96.6 ao vale. I Primeiro Turno (Cidade) · II Asfalto e Barro (Rancho) · III Sinal no Bananal · IV A Gruta da Estática · V Neblina na Serra · VI Fora do Ar (Cidade/Rádio) · VII Show da Frequência (praça). Personagens: Seu Waldir, Kátia, Toninho, Dona Lurdes (gerente da DF Entregas, nova), o Primo, Dona Cida, Seu Tonho, o Duvidoso.
- [x] Capítulos I e II jogáveis com falas pelo rádio (`StoryDialog`), metas e limite de caixas quebradas (passou do limite, a contagem recomeça).
- [ ] Capítulos III a V: dependem das fases Bananal, Gruta e Serra. VI e VII acontecem na cidade, mas precisam de tarefas novas (avisos no estúdio sem a rádio, recados de porta em porta, equipamento do show).

### Backlog opcional — vida urbana
- [ ] Praça da Matriz à noite com barracas de pastel/lanche e carro de som anunciando eventos.
- [ ] Festivais sazonais na praça (Festa da Colheita, Bon Odori) e travessia de balsa.

### Pós-v1.0
- [ ] Testar servidor dedicado com Colyseus (Oracle Cloud Free Tier), cliente via NuGet `Colyseus` + `schema-codegen`.

---

## Notas técnicas
- **Sorteios da cidade:** `_rng` (layout e endereços) e `_decoRng` (detalhes). Quarteirões especiais pulam o `_rng`; mexer neles muda a cidade: suba `PROTOCOL_VERSION` (servidor) e `NetClient.ProtocolVersion` juntos (o LocalServer usa o do NetClient).
- **Quarteirões especiais:** depósito (0,0), bombeiros (1,-1), delegacia (-1,1), caixa d'água (-1,-1), postos (2,1) com lava-jato, (-2,-2) e (-3,1), **rádio (1,2)**, **mercado (-2,2)**, **Edifício Sintonia (2,-2)**.
- **Veículos:** `VanController.NetId` ↔ `VEHICLES` no servidor ↔ `LocalServer.Vehicles` (van, moto, truck_yellow, truck_red). Moto e caminhões são montados pelo `VehicleFactory`.
- **Listas que precisam bater:** `CargoCatalog` ↔ `CARGO_CATALOG` ↔ `LocalServer.CargoPayments`; `ItemCatalog` ↔ `SHOP_PRICES`; `Professions` ↔ `PROFESSIONS` ↔ `LocalServer.ProfessionRules`; `GameLevels` ↔ `LEVELS`.
- **Rádio:** `RadioStation` calcula música e posição pelo `Time.GetUnixTimeFromSystem()` — sem tráfego de rede. Músicas em `Audio/Radio/*.ogg`, geradas por `Audio/synth_radio.py.txt`.
- **Apartamentos:** salas montadas 60 m abaixo do quarteirão; o elevador e a porta teletransportam (`PlayerController.Teleport`). Apartamento = `(MyId - 1) % 8 + 1`.
- **Save:** `SaveManager` grava `user://save.tzn` (JSON). Carteira e inventário continuam em `user://inventory.json`.
- **Versão nova:** suba `GameVersion.Current`, escreva a entrada em `GameVersion.Changelog`, regenere `CHANGELOG.md` e faça push na main: o GitHub Actions compila Windows/Linux + launcher e cria o release.
- Assets de terceiros em `Assets/<autor>/` com LICENSE.txt; só CC0 ou licenças que permitam uso comercial.
- **Quaternius (v0.9.4):** personagens (`CharacterModel`, paleta em faixas pele/olhos/cabelo/camisa/calça/sapato), `FarmAnimal`, `NatureKit`. Texturas de natureza reduzidas para 256 px e sem normal map. Ainda não usados: Downtown City MegaKit e Ultimate Textured Building Pack (prédios modulares), Universal Animation Library (esqueleto diferente; precisaria de retarget).
