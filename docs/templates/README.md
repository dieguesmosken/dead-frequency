# Modelos de código (templates)

Bases em C# para o Modo História e as fases novas. Ficam com extensão **`.cs.txt`** de propósito:
assim o Godot e o `dotnet build` não compilam (o `DeadFrequency.csproj` também ignora `docs/**`).

Para usar: copie para a pasta indicada, troque a extensão para `.cs` e adapte.

| Modelo | Vai para | Explicação |
|---|---|---|
| `JobEvents.cs.txt` | `Scripts/Jobs/JobEvents.cs` | `tech/01_ARQUITETURA_MODO_HISTORIA.md`, seção 1 |
| `StoryManager.cs.txt` | `Scripts/Story/StoryManager.cs` (autoload) | `tech/01_ARQUITETURA_MODO_HISTORIA.md`, seção 2 |
| `ChapterTransitionZone.cs.txt` | `Scripts/Story/ChapterTransitionZone.cs` | `tech/01_ARQUITETURA_MODO_HISTORIA.md`, seção 3 |
| `RadioBeacon.cs.txt` | `Scripts/World/Phases/RadioBeacon.cs` | Fase 3 (Bananal) em `lore/02_CENARIOS_E_FASES.md` |
| `CaveGenerator.cs.txt` | `Scripts/World/Phases/CaveGenerator.cs` | Fase 4 (Gruta) em `lore/02_CENARIOS_E_FASES.md` |

Todos seguem as regras do `CLAUDE.md` (sem grupo por string, sem `Call("...")`, eventos para cima,
save em JSON). Onde dependem de algo que ainda não existe no jogo, há um comentário `// FALTA:`.
