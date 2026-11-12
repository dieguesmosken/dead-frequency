<p align="center">
  <img src="site/assets/shots/hero.jpg" alt="Dead Frequency — o depósito à noite" width="100%">
</p>

<h1 align="center">Dead Frequency</h1>

<p align="center">
  <b>Cidade simulada na zona morta · primeira pessoa · estilo PS1 · Godot 4.7 (.NET / C#)</b><br>
  <a href="https://github.com/dieguesmosken/dead-frequency/releases/latest"><img alt="Versão" src="https://img.shields.io/github/v/release/dieguesmosken/dead-frequency?label=vers%C3%A3o&color=2a9a4a"></a>
  <a href="https://github.com/dieguesmosken/dead-frequency/releases"><img alt="Downloads" src="https://img.shields.io/github/downloads/dieguesmosken/dead-frequency/total?label=downloads&color=2a9a4a"></a>
  <img alt="Godot 4.7" src="https://img.shields.io/badge/Godot-4.7%20.NET-478cbf">
  <a href="LICENSE"><img alt="Licença GPL-3.0" src="https://img.shields.io/badge/licen%C3%A7a-GPL--3.0-blue"></a>
  <img alt="Plataformas" src="https://img.shields.io/badge/Windows%20%7C%20Linux-grátis-4dff7a">
</p>

<p align="center">
  <a href="https://www.teuzin.com.br/dead-frequency/"><b>🌐 Site</b></a> ·
  <a href="https://github.com/dieguesmosken/dead-frequency/releases/latest/download/DeadFrequencyLauncher-windows.exe"><b>⬇ Baixar (Windows)</b></a> ·
  <a href="https://www.teuzin.com.br/dead-frequency/#servidor">📡 Status do servidor</a> ·
  <a href="https://www.teuzin.com.br/dead-frequency/#apoie">💚 Apoie (Pix)</a>
</p>

Escolha uma profissão — entregador, taxista, gari, repositor, frentista ou locutor da rádio 96.6 —,
ganhe um apartamento no Edifício Sintonia e ajude a cidade da zona morta a funcionar, sozinho ou com
os amigos online. Tem física nas caixas, ciclo de dia e noite, rádio com músicas originais, itens,
lojas paródia, personagens e bichos animados e multiplayer.

**Versão atual:** v0.9.6 (alpha) · veja o [CHANGELOG](CHANGELOG.md) (ou com fotos, no [site](https://www.teuzin.com.br/dead-frequency/#novidades)) · roteiro em [ROADMAP](ROADMAP.md)

## Fotos

| | | |
|---|---|---|
| ![Personagens animados](site/assets/shots/v0.9.4/personagens.jpg) | ![Vacas no sítio](site/assets/shots/v0.9.4/animal_Cow.jpg) | ![Celeiro](site/assets/shots/v0.9.4/farm_Barn.jpg) |
| ![Frentista](site/assets/shots/v0.9.3/posto_carro_bomba.jpg) | ![Frota do pátio](site/assets/shots/v0.9.3/frota.jpg) | ![Orelhões](site/assets/shots/v0.9.4/orelhao_0_cyan.jpg) |
| ![Mercado à noite](site/assets/shots/v0.9.0/mercado_frente_noite.jpg) | ![Rádio 96.6](site/assets/shots/v0.9.0/radio_estudio.jpg) | ![Launcher](site/assets/shots/v0.9.2/4_progresso.jpg) |

Mais fotos na [galeria do site](https://www.teuzin.com.br/dead-frequency/#galeria).

## Baixar e jogar

Baixe o **launcher** no [site](https://www.teuzin.com.br/dead-frequency/#baixar) ou em [Releases](https://github.com/dieguesmosken/dead-frequency/releases/latest)
(`DeadFrequencyLauncher-windows.exe` ou `-linux`) e abra: ele pergunta onde instalar, baixa o jogo
e instala as próximas atualizações (sozinho ou perguntando antes). Também dá para baixar o zip do jogo direto.

## Rodar pelo código

1. Instale o **Godot 4.7 .NET** e o **.NET SDK 10** (`winget install Microsoft.DotNet.SDK.10`).
2. Abra a pasta no Godot (Importar → `project.godot`).
3. Clique no martelo (**Build**) e depois em **Executar** (F5).

### Controles

| Tecla | Ação |
|---|---|
| WASD / Shift / Espaço | andar, correr, pular (na van: freio de mão) |
| E | pegar caixa, entrar na van, usar orelhão/PC/barraca, elevador, cama (salvar), TV, rádio |
| R | rádio 96.6 da caminhonete |
| Clique esquerdo | arremessar |
| F | lanterna |
| I ou Tab | inventário |
| N | visão noturna (com o item) |
| Esc / P | pausa |

## Profissões

| Profissão | Tarefa |
|---|---|
| Entregador | caixas do depósito até o feixe verde (paga pela integridade) |
| Taxista | passageiros com a van, corrida pela distância |
| Gari | sacos de lixo das calçadas para as caçambas |
| Repositor | caixas do estoque para as prateleiras do Atacadinho |
| Frentista | abastecer os carros que chegam no posto |
| Locutor da 96.6 | ler os comerciais quando a luz NO AR acender |

No online, quem cria a sala pode deixar o servidor escolher a profissão de cada um.

## Fases

- **Fase 1 · Cidade:** prédios, ônibus, pedestres, lojas, bancos, postos, bombeiros e delegacia.
- **Fase 2 · Rancho:** estradas de terra, sítios, lago com pesqueiro, capela e cemitério.

## Multiplayer

No menu **Multiplayer**:

- **Servidor oficial:** o servidor do projeto na Cloudflare (padrão).
- **Meu servidor:** o endereço de outro servidor ou o link de um amigo que hospeda.
- **Hospedar no meu PC:** o jogo vira o servidor e gera um link público grátis com o Cloudflare Tunnel.

O servidor fica em [`server/`](server/) (Cloudflare Workers + Durable Objects). Para publicar:

```bash
cd server
npm install
npx wrangler login
npm run deploy
```

O servidor também tem estatísticas públicas (só números somados) em `/api/stats`, mostradas no
[site](https://www.teuzin.com.br/dead-frequency/#servidor), e o painel `/admin` (pede o `ADMIN_TOKEN`).

## Site

O site fica em [`site/`](site/) e é publicado pelo GitHub Actions no GitHub Pages a cada push.
A central [Atualizações](https://www.teuzin.com.br/dead-frequency/updates.html) lê os GitHub Releases:
versões estáveis da `main`, pré-releases da `Internal`, notas, downloads e commits do comparativo entre tags.
A aba Próximas novidades carrega o `ROADMAP.md`. Para ocultar um Release do site, inclua
`SITE_VISIBILITY: PRIVATE` no corpo da publicação (GitHub Release); isso só oculta no site — o Release
continua público no GitHub se estiver publicado em um repositório público.
`python3 site/build.py` continua gerando `site/data/site.json` com o changelog do `GameVersion.cs` e as fotos de
`site/assets/shots/vX.Y.Z/` (legendas em `captions.json`). O texto dos releases sai de `site/release_notes.py`.

## Contribuindo

Fluxo de branches, padrão de commits, pull requests e como lançar versão: [CONTRIBUTING.md](CONTRIBUTING.md).
Achou uma falha de segurança? Veja o [SECURITY.md](SECURITY.md).

## Apoie

O jogo é grátis. Para ajudar com o servidor e as próximas versões: **Pix `contato@badwolf3d.com.br`**
(QR Code no [site](https://www.teuzin.com.br/dead-frequency/#apoie)).

## Licença

O código do Dead Frequency (jogo, launcher, servidor, site e os scripts que geram músicas, sons e texturas)
é software livre sob a **[GNU GPL v3.0](LICENSE)**: você pode usar, estudar, modificar e redistribuir, desde que
qualquer versão distribuída continue com o código aberto e sob a mesma licença.

- Os modelos 3D de terceiros em `Assets/Kenney`, `Assets/KayKit`, `Assets/Quaternius` e `Assets/GGBot` são **CC0** (domínio público),
  cada pasta com o seu `LICENSE.txt`. O Godot Engine é MIT.
- A GPL cobre o código, não o nome: "Dead Frequency" e a identidade visual do projeto são de Matheus (teuzin0dev).
  Versões modificadas devem usar outro nome para não confundir os jogadores.

## Créditos

- Criação: Matheus (teuzin0dev). Código, cidade procedural, servidor e áudio sintetizado feitos com o Claude (Anthropic).
- [Godot Engine](https://godotengine.org) (MIT).
- [Kenney](https://kenney.nl): Starter Kit Racing (CC0).
- [KayKit](https://kaylousberg.com), de Kay Lousberg: City Builder, Furniture, Halloween e Restaurant Bits (CC0).
- [Quaternius](https://quaternius.com): personagens animados, animais, prédios da fazenda, natureza e viaturas (CC0).
- [GGBot](https://ggbot.itch.io/psx-style-cars): táxi do pacote PSX Style Cars (CC0).
- Músicas, efeitos, letreiros e anúncios: gerados por código (`Audio/*.py.txt`, `Assets/Textures/Stores/*.py.txt`).
- Rádio 96.6: cinco músicas originais de bandas fictícias, geradas por `Audio/synth_radio.py.txt` (livres de direitos autorais).
- Siga [twitch.tv/teuzin0dev](https://twitch.tv/teuzin0dev) · [github.com/dieguesmosken](https://github.com/dieguesmosken) · [recadin.com.br](https://recadin.com.br)
- Lojas, bancos e marcas do jogo são **paródias** e não têm relação com empresas reais.
