# Documentação de design — leia antes de mexer no Modo História

Este é o ponto de entrada para quem vai implementar missões, capítulos, fases novas (Bananal, Gruta, Serra),
Inverse Kinematics ou otimização de cenário no **Dead Frequency**. Vale para gente e para IAs
(Claude Code, Copilot, ChatGPT). As regras gerais de código continuam no `CLAUDE.md` da raiz.

## O que é o jogo (e o que ele não é)

- Simulador de vida cotidiana e aventura leve, em primeira pessoa, com cara de PS1 e clima anos 90/2000.
- Um vale cercado por serra e mata preservada. Um minério da região bloqueia internet e celular, então a
  **Rádio 96.6 FM** é o elo da comunidade.
- Identidade brasileira sutil ("Brazilcore"): orelhão, lombada, bueiro, fio de poste, árvore com a base
  pintada de branco, barraca de pastel na praça.
- **Não é jogo de terror.** Nunca adicionar monstros, sustos, elementos sobrenaturais ou clima de horror.
  O perigo, quando existe, é do dia a dia: estrada ruim, neblina, carga que quebra, sinal de rádio fraco.

## Onde está cada coisa

| Arquivo | Assunto |
|---|---|
| [`lore/01_LORE_E_CAPITULOS.md`](lore/01_LORE_E_CAPITULOS.md) | O vale, a rádio, o tom e o esqueleto dos 7 capítulos |
| [`lore/02_CENARIOS_E_FASES.md`](lore/02_CENARIOS_E_FASES.md) | Fases 1 a 5: o que cada cenário tem e em que ordem fazer |
| [`tech/01_ARQUITETURA_MODO_HISTORIA.md`](tech/01_ARQUITETURA_MODO_HISTORIA.md) | `StoryManager`, `JobEvents`, `ChapterTransitionZone` e o save |
| [`tech/02_OTIMIZACAO_IK_E_MULTIPLAYER.md`](tech/02_OTIMIZACAO_IK_E_MULTIPLAYER.md) | IK nos personagens, MultiMesh/oclusão e o que muda (ou não) no multiplayer |
| [`ASSETS_E_SONS.md`](ASSETS_E_SONS.md) | De onde baixar assets e sons (só CC0/compatível) e onde publicamos os nossos |
| [`templates/`](templates/README.md) | Modelos de código C# prontos para adaptar (`.cs.txt`, não compilam) |

O planejamento com datas e versões fica no [`ROADMAP.md`](../ROADMAP.md), seção "Modo História e novas fases".

## Trava de escopo

1. **Agora:** detalhar a **Fase 1 (Cidade)** e reconstruir a **Fase 2 (Rancho)**.
2. As fases novas (`Bananal.tscn`, `Gruta.tscn`, `Serra.tscn`) nascem como **cenas isoladas**, testáveis
   sozinhas, sem mudar o fluxo do jogo. Só são ligadas ao resto pela `ChapterTransitionZone` quando estiverem prontas.
3. Ideias extras (carro de som, festa na praça, balsa, caramelo, lombadas) entram como polimento modular.
   Não podem tirar o foco das 6 profissões nem do Edifício Sintonia.
4. O Modo História é **offline**. O multiplayer continua sendo a cidade livre de hoje.

## Regras rápidas para quem for implementar

- Código de verdade vai em `Scripts/...`. Em `docs/` só documentação e modelos `.cs.txt`.
  O `DeadFrequency.csproj` ignora `docs/**`, mas não crie `.cs` aqui.
- Siga o `CLAUDE.md`: `public partial class`, `GetNode<T>()` no `_Ready()`, `collider is IInteractable` /
  `body is VanController` em vez de grupos por string, "call down, signal up".
- História escuta eventos das profissões (`JobEvents`). Nunca coloque lógica de capítulo dentro de `TaxiJob`,
  `GariJob`, `DeliveryManager` etc.
- Save de história vai junto do `user://save.tzn` (`SaveManager`, JSON com `System.Text.Json`).
- IK só com os nós `SkeletonModifier3D` (`TwoBoneIK3D`, `LookAtModifier3D`, `FABRIK3D`). Nunca `SkeletonIK3D`.
- Coisa repetida (bananeira, capim, poste, cerca) vai em `MultiMeshInstance3D`.
