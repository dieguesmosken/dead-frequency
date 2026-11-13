# Assets e sons de terceiros: de onde vêm e para onde vão

Regra do projeto: só entra no repositório o que é **CC0** ou tem licença **compatível com a GPL v3**
do jogo, sempre com crédito (README → Créditos e um `LICENSE.txt` na pasta do asset).

## De onde baixar

| Fonte | Para quê | Licenças aceitas |
|---|---|---|
| [Freesound](https://freesound.org) | Sons: passos, ambiente de cidade e sítio, motores, portas | Só **CC0** (filtro "Creative Commons 0"). CC-BY só se o crédito for registrado. Nada de CC-BY-NC. |
| [OpenGameArt](https://opengameart.org) | Sons, músicas, texturas e modelos | CC0, CC-BY (com crédito) |
| [Kenney](https://kenney.nl) | Sons de interface e impacto, modelos | CC0 |
| [itch.io](https://itch.io) | Pacotes de assets (modelos PSX, sons) | Só os que dizem CC0 ou licença compatível na página |
| [Godot Shaders](https://godotshaders.com) | Shaders | O que estiver marcado CC0 ou MIT na página |

**Pixabay foi descartado:** a licença de lá não é CC0, não deixa escolher outra e proíbe redistribuir
os arquivos soltos, o que conflita com um repositório público.

Antes de usar um arquivo: abrir a página, confirmar a licença e anotar autor + link. Pacote sem licença
(sem arquivo nem página que diga) não entra.

## Gerados por nós com IA

| Asset | Ferramenta | Licença | Onde |
|---|---|---|---|
| Entregador e entregadora (modelo 3D, esqueleto e 6 animações) | [Meshy](https://www.meshy.ai) (plano pago do Matheus: os modelos gerados pertencem a ele) | Nossos, distribuídos com o jogo (GPL v3) | `Assets/Meshy/Characters/` |
| Caminhonete de entregas (com calotas) e Fuska | [Meshy](https://www.meshy.ai) (plano pago do Matheus) | Nossos, distribuídos com o jogo (GPL v3) | `Assets/Meshy/Vehicles/` |

Como refazer: desenho em T-pose (`meshy_text_to_image`, nano-banana-pro) → `meshy_image_to_3d` (smart topology
meshy-t2.5, ~4 mil triângulos, textura meshy-n1) → `meshy_rig` → `meshy_animate` com Idle, Casual Walk, Run 2,
Chair Sit Idle M/F, Regular Jump e Carry Heavy Object Walk. A máscara de cores (`*_mascara.png`: R camiseta,
G calça, B pele, A cabelo só na cabeça) e a textura de 512 px saem de um script Python a partir do GLB; no
`.import` da máscara, `process/fix_alpha_border` precisa ficar **false** (senão ele apaga os outros canais).

## Onde publicamos os nossos sons e o jogo

Os sons gerados por código no projeto (`Audio/*.py.txt`: trilha, efeitos e as músicas da Rádio 96.6)
são publicados, com a mesma liberdade do repositório, em:

- **Freesound**: um som por envio, licença CC0, link para o jogo na descrição.
- **OpenGameArt**: um pacote "Dead Frequency — sons e músicas", CC0.
- **itch.io**: o pacote de sons (CC0) e a **página do jogo**, no perfil teuzin0dev.

A página do jogo no itch.io aponta para os downloads do GitHub (releases) e para o site.
