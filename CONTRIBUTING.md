# Como trabalhar no Dead Frequency

Guia do fluxo do projeto: branches, commits, pull requests e lançamento de versões.

## Branches

| Branch | Para quê | Vai para |
|---|---|---|
| `main` | O que os jogadores recebem. **Só muda por pull request.** Cada merge com versão nova publica a release. | — |
| `Internal` | Integração e testes (pré-alpha). Cada PR mesclado gera uma pré-release `vX.Y.Z.N-pre` (ex.: `v0.9.9.1-pre`). | `main` (PR) |
| `feature/nome` | Recurso novo — `feature/menus-codigo`, `feature/shopping` | `Internal` |
| `fix/nome` | Correção sem pressa | `Internal` |
| `hotfix/nome` | Correção urgente de algo que já está na release | `main`, depois `Internal` |
| `release/vX.Y.Z` | Fechar uma versão: notas, fotos, ajustes finais | `main` |
| `exp/nome` | Experimento que pode ser descartado | `Internal` (se der certo) |
| `site/nome`, `docs/nome` | Só site ou documentação | `Internal` |

Regras no GitHub (Settings → Rules):
- `main`: sem push direto, sem force push, sem apagar; merge só por PR com a **Verificação de PR** passando.
- `Internal` e `hotfix/*`: não podem ser apagadas (force push liberado para reassinar commits).
- Tags `v*` e `internal-v*`: não podem ser apagadas nem movidas.

## Commits

Mensagem curta no imperativo, com prefixo:

| Prefixo | Quando |
|---|---|
| `feat:` | recurso novo |
| `fix:` | correção |
| `perf:` | otimização |
| `net:` | multiplayer (cliente ou servidor) |
| `site:` | site |
| `docs:` | documentação |
| `test:` | testes |
| `chore:` | CI, configuração, dependências |
| `vX.Y.Z — Título` | commit de lançamento de versão |

Commits assinados (SSH) aparecem como **Verified** no GitHub.

## Pull requests

1. Crie a branch a partir da `Internal` (ou da `main`, se for `hotfix/`).
2. Abra o PR — o modelo já traz o checklist (testar solo e online, deploy do servidor, protocolo).
3. A **Verificação de PR** compila o C#, confere a versão/changelog, gera o site e roda typecheck + smoke test do servidor.
4. Com tudo verde, faça o merge (ou ligue o *auto-merge*).

## Multiplayer e protocolo

- Servidor em `server/` (Cloudflare Worker + Durable Objects). Depois de mexer nele: `cd server && npm run deploy`.
- Mudou o formato das mensagens? Suba o protocolo e **mantenha o anterior** em `SUPPORTED_PROTOCOLS`:
  quem está com o jogo antigo continua jogando nas salas da versão dele (salas isoladas por protocolo).
  Detalhes em `server/README.md`.

## Lançar uma versão

1. Na `release/vX.Y.Z` (ou na `Internal`): suba `GameVersion.Current`, escreva a entrada no topo de `GameVersion.Changelog`
   e a mesma seção no `CHANGELOG.md`.
2. Fotos da versão em `site/assets/shots/vX.Y.Z/` com `captions.json`, e `python3 site/build.py`.
3. PR para a `main`. No merge, o CI gera os zips, os launchers, o `SHA256SUMS` e o atestado de build, e publica a release.
4. Se mexeu no servidor: `npm run deploy`.

## Segurança

Achou uma falha no servidor ou no jogo? Veja o [SECURITY.md](SECURITY.md) — não abra issue pública.
