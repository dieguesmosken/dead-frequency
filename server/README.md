# Servidor multiplayer — Dead Frequency

Worker da Cloudflare com um **Durable Object por sala**. Cabe no plano gratuito.

## O que o servidor decide
- Código da sala (5 caracteres, sem 0/O/1/I) e até 8 jogadores por sala.
- Seed da cidade: todos geram exatamente a mesma cidade e os mesmos endereços.
- Turnos, pedidos, pagamento (base × integridade) e o dinheiro do grupo.
- **Posse dos objetos:** quem pega uma caixa ou senta na van vira o dono. Só o dono pode mandar
  o estado daquele objeto ou reportar a entrega. Ninguém toma a van de quem está dirigindo
  nem a caixa da mão de outro.
- Anfitrião (passa para o próximo se ele sair). Sala vazia é apagada depois de 30 minutos.

A física continua no Godot: o dono simula e os outros só acompanham o estado recebido.

## Rodar no seu PC (teste)
```bash
cd server
npm install
npm run dev          # sobe em http://localhost:8787
npm test             # em outro terminal: teste automático (26 checagens, inclui o painel)
```
No jogo: **Multiplayer → Servidor:** `http://localhost:8787` → **Criar sala**.
Para testar com 2 janelas, abra o jogo exportado duas vezes, ou rode pelo editor
(Depuração → Executar várias instâncias).

## Colocar no ar (grátis)
```bash
cd server
npx wrangler login   # abre o navegador para entrar na sua conta Cloudflare
npm run deploy
```
O Wrangler mostra o endereço, algo como `https://dead-frequency.teuzin0dev.workers.dev`.
Coloque esse endereço no campo **Servidor** do jogo. Seus amigos usam o mesmo endereço
e o código da sala.

## Painel de admin
Página com jogadores online, salas, entregas, dinheiro, detalhes de cada sala (quem está dirigindo,
quem segura cada caixa), aviso para todas as salas, botão para encerrar uma sala e a aba **Uso e limites**
(mensagens/conexões WebSocket recebidas, bytes, tipos de mensagem e estimativa parcial de requisições dos Durable Objects).

1. Crie o token (uma senha longa qualquer):
   ```bash
   npx wrangler secret put ADMIN_TOKEN
   ```
2. Faça o deploy de novo (`npm run deploy`) e abra **https://dead-frequency.teuzin0dev.workers.dev/admin**.
3. Digite o mesmo token. Ele fica salvo só no seu navegador.

No teste local, copie `.dev.vars.example` para `.dev.vars` e use o token `teste-local`
em http://localhost:8787/admin.

## Limites do plano gratuito

- **Workers Free:** 100.000 requisições de entrada por dia. Cada abertura de WebSocket conta como uma requisição do Worker; as mensagens WebSocket encaminhadas não contam como novas requisições do Worker.
- **Durable Objects Free:** 100.000 requisições por dia. Para a cobrança de compute, 20 mensagens WebSocket recebidas equivalem a 1 requisição; mensagens enviadas pelo servidor não são cobradas como requisições. O upgrade inicial da conexão é uma requisição.
- O painel **Uso e limites** registra mensagens e conexões WebSocket e calcula a parcela estimada desse tráfego no consumo de requisições dos Durable Objects, incluindo o RPC usado para gravar cada lote de métricas. A estimativa não inclui outros RPCs, requisições internas HTTP, alarmes nem o total oficial de requisições do Worker; consulte o Analytics da Cloudflare para os números oficiais.
- Os contadores são persistidos em lotes de 100 mensagens ou 10 conexões para evitar gravar cada pacote individualmente. Hibernação ou reinicialização antes da gravação pode perder o lote ainda não persistido.
- O cliente mantém 10 Hz durante mudanças de estado, mas agora omite ticks sem alteração relevante e envia um heartbeat de 1 segundo quando está parado. O painel administrativo atualiza automaticamente a cada 60 segundos para evitar polling excessivo.

## Compatibilidade entre protocolos

- O protocolo 7 continua sendo o padrão para clientes antigos: omitir `protocol` mantém o roteamento e os IDs históricos dos Durable Objects.
- O protocolo 8 usa salas isoladas com IDs `v8:<codigo>`; o mesmo código pode existir em versões diferentes sem compartilhar jogadores, inventário ou estado de mundo.
- Criar sala: envie `protocol: 8` no JSON de `POST /api/rooms`. Sem esse campo, a sala é criada no protocolo 7.
- Listar, consultar ou conectar: use `?protocol=8` em `GET /api/rooms`, `/api/rooms/:code` e `/api/rooms/:code/ws`. O valor também deve corresponder ao `version` enviado em `hello`; divergências são recusadas.
- A lista de versões habilitadas fica em `SUPPORTED_PROTOCOLS`. Protocolos 6 e anteriores permanecem desabilitados.
- As salas permanentes e os códigos já existentes continuam no namespace V7. O Registry mantém as chaves históricas do V7 e grava novas versões em chaves próprias.
- **Estado inicial:** V8 está isolado por versão, mas usa o mesmo contrato de mensagens e as mesmas validações do V7. Antes de introduzir mudanças incompatíveis no wire format do V8, implemente um adaptador explícito e testes de conversão. Não basta adicionar um número à lista de protocolos.

## Protocolo (JSON por WebSocket)
| Cliente → servidor | Para quê |
|---|---|
| `hello {version}` | entrar de fato na sala |
| `tick {p, ry, hp, st, held, objs[]}` | posição do jogador + objetos que ele controla |
| `claim {id}` / `release {id}` | pedir/soltar posse (`van` ou id da caixa) |
| `deliver {cargoId, integ}` / `lost {cargoId}` | entrega concluída / caixa destruída |
| `chat {msg}` · `ping {ts}` | |

| Servidor → cliente | |
|---|---|
| `welcome` | estado completo: seed, jogadores, pedidos, donos, objetos |
| `join` · `leave` · `host` | entrada/saída/anfitrião |
| `tick` | estado de outro jogador (só objetos que ele realmente controla) |
| `owner` · `claimFail` | mudança de posse / pedido negado |
| `shift` · `delivered` · `lost` · `shiftDone` | turnos e entregas |

**Importante:** a lista `CARGO_CATALOG` em `src/index.ts` precisa ter a mesma ordem de
`DeliveryManager.CargoCatalog` no Godot.

## Limitação conhecida
O servidor confia no cliente sobre a posição e a integridade da caixa entregue (não roda física).
Para jogar com amigos isso basta; contra trapaça séria seria preciso validar posições no servidor.
