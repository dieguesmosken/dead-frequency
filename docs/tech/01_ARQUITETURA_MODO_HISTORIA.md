# Arquitetura do Modo História

> Status: **planejado**. Nada disto está no jogo ainda. Os modelos de código estão em `docs/templates/`
> como `.cs.txt`. Ao implementar, copie para `Scripts/Story/` e renomeie para `.cs`.

## Visão geral

```
 Profissões (TaxiJob, GariJob, ...)      DeliveryManager
            │ ProfessionManager.Credited        │ DeliveryCompleted
            ▼                                   ▼
        ┌───────────────── JobEvents (static) ─────────────────┐
        │  TaskDone(job, what, payment)                        │
        │  CargoBroken(cargoName)                              │
        └───────────────────────┬──────────────────────────────┘
                                │ só escuta
                                ▼
                    StoryManager (autoload, offline)
                    ├─ capítulo atual, concluídos, contadores
                    ├─ regra de cada capítulo → CanAdvance
                    └─ save em SaveData.Story (user://save.tzn)
                                │ consulta
                                ▼
           ChapterTransitionZone (Area3D na estrada)
           van entra → piloto automático → banner + rádio → carrega a próxima cena em segundo plano
```

Três peças, cada uma com uma responsabilidade:

| Peça | Arquivo final | Responsabilidade |
|---|---|---|
| `JobEvents` | `Scripts/Jobs/JobEvents.cs` | Avisar "uma tarefa aconteceu". Não sabe que a história existe. |
| `StoryManager` | `Scripts/Story/StoryManager.cs` | Contar o progresso e decidir quando o capítulo pode avançar. |
| `ChapterTransitionZone` | `Scripts/Story/ChapterTransitionZone.cs` | Fazer a viagem entre cenas (banner, rádio, carregamento). |

## 1. `JobEvents`: o único ponto de contato com as profissões

Uma classe estática com eventos C#. As profissões continuam sem saber nada de história; só ganham
**uma linha** que dispara o evento no lugar onde o pagamento já acontece:

- `ProfessionManager.Credited(...)` → `JobEvents.RaiseTaskDone(Current, what, payment);`
- `DeliveryManager`, ao emitir `DeliveryCompleted` → `JobEvents.RaiseTaskDone(Professions.Entregador, cargoName, payment);`
- `DeliveryManager.JobCargoDestroyed` → `JobEvents.RaiseCargoBroken(job.CargoName);`

Regras:

- Eventos estáticos seguram referência a quem assina. Quem assina **tem que** desassinar em `_ExitTree()`.
- Online o pagamento chega pelo servidor e passa pelo mesmo `Credited`, então o evento também dispara.
  O `StoryManager` ignora quando `NetClient.Instance.IsOnline` (história é offline).
- Nenhuma regra de jogo dentro de `JobEvents`: só repassa o que aconteceu.

Modelo: [`templates/JobEvents.cs.txt`](../templates/JobEvents.cs.txt).

## 2. `StoryManager`: progresso e regras de capítulo

- Autoload (`project.godot` → `[autoload] StoryManager="*res://Scripts/Story/StoryManager.cs"`), com `Instance`.
- Guarda: `CurrentChapter`, `CanAdvance`, `CompletedChapters` e contadores por profissão do capítulo atual.
- Cada capítulo tem uma regra simples de avanço (ex.: "5 tarefas de entregador no rancho"), definida num
  dicionário `ChapterGoals`. Quando os capítulos forem decididos (ver `lore/01_LORE_E_CAPITULOS.md`),
  a regra pode virar um `Resource` `.tres` (`[GlobalClass] ChapterData`), como manda o `CLAUDE.md`.
- Avisa a UI com `event Action<int> ChapterReady` e `event Action<int> ChapterChanged` ("signal up").
- Não toca em cena nem em nó de fase. Quem troca de cena é a `ChapterTransitionZone`.

### Save

O progresso vai junto do save atual, em `user://save.tzn`, sem arquivo novo:

```csharp
// Scripts/Core/SaveManager.cs — dentro de SaveData
public StorySave Story { get; set; } = new();

public class StorySave
{
    public int Chapter { get; set; } = 1;
    public bool CanAdvance { get; set; }
    public List<int> Completed { get; set; } = new();
    public Dictionary<string, int> Progress { get; set; } = new();
}
```

- `System.Text.Json` lida com isso direto: nada de `Godot.Collections.Dictionary`.
- Save antigo sem o campo `Story` carrega com o valor padrão (capítulo 1), então não precisa subir `SaveData.Version`.
- `StoryManager.ToSave()` / `FromSave(StorySave)` fazem a cópia; o `SaveManager` chama na hora de salvar (cama) e de carregar.

Modelo: [`templates/StoryManager.cs.txt`](../templates/StoryManager.cs.txt).

## 3. `ChapterTransitionZone`: o "loading disfarçado" na estrada

Uma `Area3D` no fim da estrada de cada fase. Quando a van do jogador entra e o capítulo está liberado:

1. Liga o piloto automático da van (segue reto em velocidade constante; o jogador não perde o controle da câmera).
2. Mostra o banner retrô ("CAPÍTULO II — Asfalto e Barro") no estilo `UiStyle` e mantém a 96.6 tocando.
3. Começa a carregar a próxima cena em segundo plano (`ResourceLoader.LoadThreadedRequest`).
4. Quando carregou **e** o banner ficou na tela o tempo mínimo, avisa o `StoryManager` e troca de cena.

Detalhes que o modelo já resolve:

- Detecta a van por tipo (`body is VanController`), não por grupo. A área usa máscara de colisão só na
  camada 5 (Vehicle).
- Só dispara se for o jogador local ao volante e se estiver offline.
- Se o carregamento falhar, desliga o piloto automático, esconde o banner e avisa no console, em vez de travar o jogo.

**A van da viagem já existe** (v0.9.9): `CityGenerator.TravelVan` (NetId `van_viagem`, modelo `CarType.Van80`),
parada no pátio da central. A zona pode aceitar só ela (`body == City.TravelVan`) quando a história exigir a van.

**Falta no jogo hoje:** a `VanController` não tem piloto automático. Quando for implementar, criar
`public void SetAutoDrive(bool on, float speedMs)` na `VanController` (acelera até a velocidade e
mantém o volante reto). A zona chama esse método direto, sem `Call("...")` por string.

Modelo: [`templates/ChapterTransitionZone.cs.txt`](../templates/ChapterTransitionZone.cs.txt).

## Fases isoladas

- Cada fase nova é uma cena própria em `Scenes/Levels/` (`Bananal.tscn`, `Gruta.tscn`, `Serra.tscn`) com
  um `PlayerController` e uma van, para dar F6 e testar sozinha.
- Enquanto a fase não estiver pronta, nenhuma `ChapterTransitionZone` aponta para ela.
- Scripts específicos de fase ficam em `Scripts/World/Phases/` (ex.: `CaveGenerator.cs`, `RadioBeacon.cs`).

## Ordem sugerida de implementação

1. `JobEvents` + as 3 linhas nos pontos de pagamento (não muda nada visível no jogo).
2. `StoryManager` com o save, mostrando o capítulo no debug F3.
3. Piloto automático na `VanController`.
4. `ChapterTransitionZone` ligando Cidade → Rancho (as duas cenas que já existem).
5. Só depois: Bananal, Gruta e Serra.
