# Otimização, IK e multiplayer

## Inverse Kinematics (IK) nos personagens

Objetivo: mãos que seguram de verdade a caixa, o saco de lixo e o volante, e NPCs que viram a cabeça
para olhar o jogador.

### Nós permitidos (Godot 4.7)

| Nó | Uso no jogo |
|---|---|
| `TwoBoneIK3D` | Braço (ombro → cotovelo → mão) indo até a alça da caixa ou o volante. Perna em degrau, se precisar. |
| `LookAtModifier3D` | Cabeça/pescoço do NPC olhando o jogador que passa (combina com os balões do `SidewalkTalk`). |
| `FABRIK3D` | Cadeias com mais de 2 ossos (coluna, rabo do caramelo). Usar só se o `TwoBoneIK3D` não der conta. |

**Nunca usar `SkeletonIK3D`**: é o nó antigo, está obsoleto na Godot 4 e não combina com os modificadores novos.

### Como encaixar no `CharacterModel`

- O `CharacterModel` já acha o `Skeleton3D` do modelo Quaternius (`FindChild("Skeleton3D")`). Os modificadores
  entram como **filhos desse `Skeleton3D`**, criados em código como o resto do personagem.
- O alvo é um `Node3D`/`Marker3D` (ex.: um marcador na alça do `CargoItem`, outro no aro do volante da van).
- Ligue o IK só quando houver alvo (`Active = true` ao pegar a caixa, `false` ao soltar) e use `Influence`
  para entrar suave (tween de 0 a 1 em ~0,15 s), sem "estalo".
- Os nomes dos ossos (ombro, antebraço, mão, cabeça) vêm do modelo Quaternius: confira no `Skeleton3D`
  antes de escrever. Não chute nomes.
- NPCs longe do jogador ficam sem IK (desliga o modificador além de ~20 m): é custo por osso, por quadro.
- No online o IK é só visual e local: cada cliente calcula para os bonecos que vê. Nada vai pela rede.

### O que já existe (v0.10.1)

- `Scripts/Characters/CharacterModel.IK.cs`: um `TwoBoneIK3D` para os braços e outro para as pernas (2 settings cada),
  um `LookAtModifier3D` na cabeça e entrada/saída suave pela `Influence`. API: `SetHandTargets`, `SetFootTargets`,
  `SetClimbing(bool, fase)`, `FootGrounding`, `LookTarget`.
- `Scripts/Characters/FootPlacer.cs`: `SkeletonModifier3D` antes do IK das pernas. Raio para o chão embaixo de cada pé
  (camada World), pé na altura do degrau e quadril abaixado quando um pé fica mais baixo. Ligado no boneco do jogador
  (terceira pessoa) e nos jogadores remotos.
- Ciclista: pose parada + IK, mãos nas manoplas e pés nos pedais que giram com o pedivela (`Cyclist`).
- Escada de marinheiro da caixa d'água: `PlayerController.StartClimb` (W sobe, S desce, E solta); o boneco põe mãos e pés
  nos degraus. No online vai só o estado `st=3` (servidor e `LocalServer` aceitam), o IK é calculado em cada cliente.
- NPCs que conversam (`Chatty`) viram a cabeça para o jogador a até ~5 m.
- Achados ao montar: os modelos do Meshy têm o esqueleto em escala 0,01 (o IK funciona mesmo assim); o eixo "frente"
  do osso `Head` é **+Y**; a animação de sentar (`Chair_Sit_Idle`) atrapalha o IK dos braços, então a ciclista usa a
  pose parada com o quadril no selim. `TwoBoneIK3D` precisa de nó de polo para dobrar certo.

## Otimização de cenário

Regra geral: **coisa que se repete vira `MultiMeshInstance3D`**.

- Bananeiras, capim, árvores, cercas, postes e pedras: um `MultiMesh` por tipo de malha e por pedaço de mapa.
  O `GrassTufts` e o NatureKit já fazem isso: siga o mesmo padrão.
- Divida em pedaços (chunks) para o motor descartar o que está fora da câmera. Um `MultiMesh` gigante cobrindo
  a fase inteira é desenhado inteiro sempre.
- Peças pequenas somem na neblina: use `VisibilityRangeEnd` (o `CityGenerator.OptimizeVisibility` já faz isso
  na cidade). Com neblina densa ninguém vê além de ~80 m mesmo.
- `OccluderInstance3D` nas áreas fechadas (bananal denso, quarteirões de prédio, gruta): o que está atrás da
  parede não é desenhado. Precisa ligar *Occlusion Culling* em Project Settings → Rendering.
- Luzes: poucas `OmniLight3D` com sombra. Longe, apaga (também já é feito no `OptimizeVisibility`).
- A otimização para PC fraco (streaming por chunks) está sendo feita na branch `hotfix/pc-fraco`. Fases novas
  devem nascer compatíveis com ela: nada de guardar referência a nó de chunk que pode ser descarregado.

## Multiplayer

### Como é hoje (manter)

- Servidor em `server/`: Cloudflare Worker + um Durable Object por sala (código de 5 letras).
- O servidor manda nas regras (pagamento, preços, profissões); a física roda no dono de cada objeto.
- Protocolos: o servidor aceita os protocolos de `SUPPORTED_PROTOCOLS` em `server/src/index.ts`.
  O cliente (`NetClient`) tenta primeiro o preferido (`PreferredProtocolVersion`) e cai para o legado
  (`ProtocolVersion`) se o servidor não aceitar. Salas de protocolos diferentes ficam separadas.
- Hospedar no próprio PC: `LocalServer` + `TunnelService` (link público via Cloudflare Tunnel).

### O que o Modo História muda no online

**Nada.** A história é offline. `StoryManager` e `ChapterTransitionZone` não fazem nada com
`NetClient.Instance.IsOnline` ligado, e o servidor não precisa saber de capítulos. As fases novas podem
ganhar modo online depois, mas isso é outro projeto.

### Colyseus (só pós-v1.0)

- Ideia para testar no futuro: servidor Node.js persistente com Colyseus na Oracle Cloud Free Tier.
- Se acontecer: cliente pelo pacote NuGet `Colyseus` no `DeadFrequency.csproj` (com tipos gerados pelo
  `schema-codegen`), **nunca** por plugin GDExtension.
- Até lá, nenhuma mudança no `server/` por causa disso.
