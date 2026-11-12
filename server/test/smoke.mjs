// Teste rápido contra `npm run dev` (ou passe a URL: node test/smoke.mjs https://seu-worker.workers.dev)
import WebSocket from "ws";

const base = (process.argv[2] ?? "http://127.0.0.1:8787").replace(/\/$/, "");
const wsBase = base.replace(/^http/, "ws");
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));
const assert = (cond, label) => {
  if (!cond) throw new Error("FALHOU: " + label);
  console.log("ok -", label);
};
async function waitFor(client, type, predicate = () => true, timeout = 3000) {
  const deadline = Date.now() + timeout;
  while (Date.now() < deadline) {
    const message = client.inbox.find((m) => m.t === type && predicate(m));
    if (message) return message;
    await sleep(25);
  }
  return undefined;
}

function connect(code, name, prof = "entregador", items = [], roomPassword = "", protocol = 7) {
  return new Promise((resolve, reject) => {
    const ws = new WebSocket(`${wsBase}/api/rooms/${code}/ws?protocol=${protocol}&name=${encodeURIComponent(name)}`);
    const inbox = [];
    ws.on("message", (data) => inbox.push(JSON.parse(data.toString())));
    ws.on("open", () => {
      ws.send(JSON.stringify({ t: "hello", version: protocol, prof, items, roomPassword }));
      resolve({ ws, inbox, send: (m) => ws.send(JSON.stringify(m)), find: (t) => inbox.find((m) => m.t === t), all: (t) => inbox.filter((m) => m.t === t) });
    });
    ws.on("error", reject);
  });
}

const health = await (await fetch(base + "/")).json();
assert(health.ok, "health");
assert(Array.isArray(health.supportedProtocols) && health.supportedProtocols.includes(7) && health.supportedProtocols.includes(8), "health anuncia protocolos habilitados");
const pub = await (await fetch(base + "/api/stats")).json();
assert(pub.ok && typeof pub.onlinePlayers === "number" && pub.protocol === health.protocol, "stats públicas");
assert(!JSON.stringify(pub).includes("code") && !("rooms" in pub), "stats sem códigos de sala");

const permanentList = await (await fetch(base + "/api/rooms")).json();
assert(permanentList.rooms.some((r) => r.code === "SERVA" && r.permanent && r.players === 0), "sala permanente de cidade aparece vazia");
assert(permanentList.rooms.some((r) => r.code === "SERVB" && r.level === "farm"), "sala permanente do sitio aparece na lista");
assert(!permanentList.rooms.some((r) => r.code === "ADMNA" || r.code === "ADMNB"), "salas administrativas protegidas ficam ocultas");

const { code } = await (await fetch(base + "/api/rooms", { method: "POST" })).json();
assert(/^[A-Z2-9]{5}$/.test(code), "sala criada " + code);

const info = await (await fetch(`${base}/api/rooms/${code}`)).json();
assert(info.players === 0, "info da sala");
assert((await fetch(`${base}/api/rooms/ZZZZZ`)).status === 404, "sala inexistente = 404");

// Conexão ainda não autenticada não pode gastar o dinheiro compartilhado.
const preHello = new WebSocket(`${wsBase}/api/rooms/${code}/ws?name=Intruso`);
const preHelloInbox = [];
preHello.on("message", (data) => preHelloInbox.push(JSON.parse(data.toString())));
await new Promise((resolve, reject) => {
  preHello.once("open", resolve);
  preHello.once("error", reject);
});
preHello.send(JSON.stringify({ t: "buy", item: "tape", req: 1 }));
await sleep(150);
assert(preHelloInbox.some((m) => m.t === "buyFail"), "compra bloqueada antes do hello");
preHello.close();

const a = await connect(code, "Teuzin", "entregador", ["tape", "cafe", "guarana"]);
await sleep(300);
const welcomeA = a.find("welcome");
assert(welcomeA && welcomeA.code === code && welcomeA.jobs.length >= 3, "welcome A com pedidos");
assert(Array.isArray(welcomeA.items) && welcomeA.items.length === 0, "inventário local enviado no hello é ignorado");
a.send({ t: "inventory", items: ["guarana", "tape"], eq: "" });
assert(await waitFor(a, "itemsState", (m) => m.items.length === 0), "servidor rejeita criação de itens pelo cliente");
a.send({ t: "inventory", items: [], eq: "backpack" });
assert(await waitFor(a, "itemsState", (m) => m.items.length === 0 && m.equipment === ""), "servidor rejeita equipamento inventado");
a.send({ t: "sell", item: "guarana", req: 1 });
assert(await waitFor(a, "sellFail", (m) => m.req === 1), "item inventado não pode ser vendido");
assert(welcomeA.level === "city", "sala sem fase = cidade");

// Sala do rancho (fase 2).
// Protocolo 8 usa outro Durable Object; a rota antiga continua apontando somente para V7.
assert((await fetch(base + "/api/rooms?protocol=6")).status === 400, "protocolo legado não habilitado");
const unsupportedCreate = await fetch(base + "/api/rooms", { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ protocol: 6 }) });
assert(unsupportedCreate.status === 400, "criação rejeita protocolo não suportado");
const { code: v8Code, protocol: v8Protocol } = await (await fetch(base + "/api/rooms", {
  method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ protocol: 8 }),
})).json();
assert(v8Protocol === 8, "criação declara protocolo 8");
const v8Info = await (await fetch(`${base}/api/rooms/${v8Code}?protocol=8`)).json();
assert(v8Info.protocol === 8, "info da sala identifica protocolo 8");
const v7Lookup = await fetch(`${base}/api/rooms/${v8Code}`);
const v7LookupInfo = v7Lookup.status === 200 ? await v7Lookup.json() : null;
assert(v7Lookup.status === 404 || v7LookupInfo?.protocol === 7, "rota padrão V7 não acessa sala V8");
const v8Client = await connect(v8Code, "ClienteV8", "entregador", [], "", 8);
await sleep(250);
assert(v8Client.find("welcome")?.code === v8Code && v8Client.find("welcome")?.protocol === 8, "cliente V8 entra na sala V8");
assert((await (await fetch(base + "/api/rooms?protocol=8")).json()).rooms.some((r) => r.code === v8Code), "lista pública filtra salas V8");
const mismatch = new WebSocket(`${wsBase}/api/rooms/${v8Code}/ws?protocol=8&name=VersaoErrada`);
const mismatchInbox = [];
mismatch.on("message", (data) => mismatchInbox.push(JSON.parse(data.toString())));
await new Promise((resolve, reject) => { mismatch.once("open", resolve); mismatch.once("error", reject); });
mismatch.send(JSON.stringify({ t: "hello", version: 7, prof: "entregador" }));
await sleep(150);
assert(mismatchInbox.some((m) => m.t === "error" && m.msg.includes("protocolo V8") && m.msg.includes("protocolo V7")), "hello V7 recebe mensagem clara ao tentar entrar em sala V8");
mismatch.close();
v8Client.ws.close();

const { code: farmCode } = await (await fetch(base + "/api/rooms", { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ level: "farm" }) })).json();
const farmer = await connect(farmCode, "Caipira");
await sleep(300);
assert(farmer.find("welcome")?.level === "farm", "sala criada com a fase rancho");
farmer.ws.close();
const { code: badCode } = await (await fetch(base + "/api/rooms", { method: "POST", body: JSON.stringify({ level: "lua" }) })).json();
const lost = await connect(badCode, "Perdido");
await sleep(300);
assert(lost.find("welcome")?.level === "city", "fase desconhecida vira cidade");
lost.ws.close();

const b = await connect(code, "Amigo<script>");
await sleep(300);
const welcomeB = b.find("welcome");
assert(welcomeB.players.length === 1 && welcomeB.seed === welcomeA.seed, "B vê A e mesma seed");
assert(welcomeB.name === "Amigoscript", "nome sanitizado");
assert(a.find("join")?.player.id === welcomeB.you, "A recebeu join de B");

const job = welcomeA.jobs[0];
a.send({ t: "claim", id: job.id });
assert(await waitFor(b, "owner", (m) => m.id === job.id && m.owner === welcomeA.you), "posse da caixa passou para A");

for (let i = 1; i <= 4; i++) a.send({ t: "chat", msg: `teste ${i}` });
assert(await waitFor(a, "chatFail"), "limite do chat bloqueia spam");
assert(await waitFor(b, "chat", (m) => m.msg === "teste 3"), "mensagens do chat chegam à sala");
assert(b.all("chat").filter((m) => m.msg?.startsWith("teste ")).length === 3, "servidor aceita no máximo 3 mensagens em 5 segundos");

a.send({ t: "tick", p: [1, 0, 2], ry: 0.5, hp: 0, st: 0, held: job.id, objs: [{ id: job.id, p: [1, 1, 1], q: [0, 0, 0, 1], integ: 0.5 }, { id: "van", p: [9, 9, 9], q: [0, 0, 0, 1] }] });
await sleep(200);
const tick = b.find("tick");
assert(tick && tick.objs.length === 1 && tick.objs[0].id === job.id, "tick repassado e van (sem posse) filtrada");

b.send({ t: "claim", id: job.id });
assert(await waitFor(b, "claimFail", (m) => m.id === job.id), "B não toma caixa que A está segurando");

b.send({ t: "deliver", cargoId: job.id, integ: 1 });
a.send({ t: "deliver", cargoId: job.id, integ: 0.5 });
const validDelivery = await waitFor(b, "delivered");
const delivered = b.all("delivered");
assert(validDelivery && delivered.length === 1 && validDelivery.by === welcomeA.you, "só o dono entrega");
assert(delivered[0].payment === Math.round([150, 300, 120, 180, 220, 90, 160, 140, 260, 280, 200, 110, 170][job.type] * 0.5), "pagamento proporcional à integridade");

// Os itens agora só entram no inventário após concessão validada pelo servidor.
a.send({ t: "buy", item: "cafe", req: 1 });
assert(await waitFor(a, "money", (m) => m.req === 1 && m.item === "cafe"), "compra de item aceita pelo servidor");
assert(await waitFor(a, "itemsState", (m) => m.items.includes("cafe")), "compra adiciona item ao inventário autoritativo");
a.send({ t: "buy", item: "cafe", req: 1 });
assert(await waitFor(a, "buyFail", (m) => m.req === 1), "requisição de compra repetida não cobra duas vezes");
a.send({ t: "buy", item: "guarana", req: 2 });
assert(await waitFor(a, "money", (m) => m.req === 2 && m.item === "guarana"), "segunda compra aceita");

a.send({ t: "tick", p: [1, 0, 2], ry: 0, hp: 0, st: 0, held: null, eq: "", objs: [] });
await sleep(100);
a.send({ t: "itemDrop", item: "cafe", p: [100, 0, 100], q: [0, 0, 0, 1] });
assert(await waitFor(a, "itemDropFail"), "descarte distante do jogador é recusado");
a.send({ t: "itemDrop", item: "cafe", p: [1, null, 2], q: [0, 0, 0, 1] });
assert(await waitFor(a, "itemDropFail"), "coordenada não numérica é recusada");
a.send({ t: "itemDrop", item: "cafe", p: [1, 0, 2], q: [0, 0, 0, 0] });
assert(await waitFor(a, "itemDropFail"), "rotação inválida é recusada");
a.send({ t: "itemDrop", item: "cafe", p: [1, 0, 2], q: [0, 0, 0, 1] });
const droppedItem = await waitFor(b, "itemSpawn", (m) => m.item === "cafe");
const dropInventory = await waitFor(a, "itemsState", (m) => !m.items.includes("cafe"));
assert(droppedItem?.id && dropInventory, "largar item remove da mochila e replica o objeto");
b.send({ t: "tick", p: [100, 0, 100], ry: 0, hp: 0, st: 0, held: null, eq: "", objs: [] });
b.send({ t: "itemPickup", id: droppedItem.id });
assert(await waitFor(b, "itemPickupFail"), "coleta de item distante é recusada");
b.send({ t: "tick", p: [1, 0, 2], ry: 0, hp: 0, st: 0, held: null, eq: "", objs: [] });
await sleep(100);
b.send({ t: "itemPickup", id: droppedItem.id });
const pickupInventory = await waitFor(b, "itemsState", (m) => m.items.includes("cafe"));
assert(pickupInventory && await waitFor(a, "itemRemove", (m) => m.id === droppedItem.id), "coleta próxima adiciona item e remove objeto do mundo");

a.send({ t: "itemTransfer", to: welcomeB.you, item: "guarana" });
const transferInventoryA = await waitFor(a, "itemsState", (m) => !m.items.includes("guarana"));
const transferInventoryB = await waitFor(b, "itemsState", (m) => m.items.includes("cafe") && m.items.includes("guarana"));
assert(transferInventoryA && transferInventoryB && await waitFor(b, "itemTransferred", (m) => m.from === welcomeA.you && m.to === welcomeB.you), "entregar item atualiza as duas mochilas e avisa a sala");

for (const j of welcomeA.jobs.slice(1)) {
  a.send({ t: "claim", id: j.id });
  await sleep(50);
  a.send({ t: "deliver", cargoId: j.id, integ: 1 });
}
assert(await waitFor(a, "shiftDone"), "turno concluído");
const shift = await waitFor(a, "shift", (m) => m.shift === 2, 6000);
assert(shift && shift.shift === 2 && shift.jobs.every((j) => j.status === "pending"), "novo turno via alarm");

b.send({ t: "claim", id: "van" });
assert(await waitFor(a, "owner", (m) => m.id === "van" && m.owner === welcomeB.you), "van passa para B");
b.send({ t: "tick", p: [0, 0, 0], ry: 0, hp: 0, st: 1, held: null, objs: [] });
assert(await waitFor(a, "tick", (m) => m.id === welcomeB.you && m.st === 1), "estado de motorista chega à sala");
a.send({ t: "claim", id: "van" });
assert(await waitFor(a, "claimFail", (m) => m.id === "van"), "não dá para tomar a van de quem está dirigindo");

// ---------------- loja e game over ----------------
const moneyBefore = a.all("delivered").at(-1)?.money ?? 0;
a.send({ t: "buy", item: "tape", req: 7 });
const bought = await waitFor(b, "money", (m) => m.req === 7);
assert(bought && bought.money === moneyBefore - 50 && bought.item === "tape", "compra desconta do dinheiro do grupo");
assert(await waitFor(a, "itemsState", (m) => m.items.includes("tape")), "compra concede item no servidor");
a.send({ t: "sell", item: "tape", req: 2 });
const firstSale = await waitFor(a, "sold", (m) => m.req === 2);
assert(firstSale?.price === 25 && firstSale?.by === welcomeA.you, "recompra paga metade do preço");
assert(await waitFor(a, "itemsState", (m) => !m.items.includes("tape")), "venda remove item no servidor");
a.send({ t: "sell", item: "tape", req: 2 });
assert(await waitFor(a, "sellFail", (m) => m.req === 2), "requisição de venda repetida não paga duas vezes");
a.send({ t: "buy", item: "radar", req: 8 });
a.send({ t: "buy", item: "nave-espacial", req: 9 });
await waitFor(a, "buyFail", (m) => m.req === 9);
const radarBought = await waitFor(a, "money", (m) => m.req === 8);
const radarRejected = await waitFor(a, "buyFail", (m) => m.req === 8);
const fails = a.all("buyFail").map((m) => m.req);
assert(fails.includes(9) && (firstSale.money >= 400 ? !!radarBought : !!radarRejected), "compra sem dinheiro / item inválido recusada");
a.send({ t: "tick", p: [0, 0, 0], ry: 0, hp: 0, st: 0, held: null, eq: "radar,gloves<x>", objs: [] });
await sleep(150);
assert(b.all("tick").at(-1)?.eq === (radarBought ? "radar" : ""), "equipamento remoto vem do estado validado pelo servidor");
const shift2 = a.all("shift").at(-1);
for (const j of shift2.jobs) {
  a.send({ t: "claim", id: j.id });
  await sleep(40);
  a.send({ t: "lost", cargoId: j.id });
}
const over = await waitFor(b, "gameOver", () => true, 3000);
if (!over || over.money !== 0) {
  throw new Error(`FALHOU: todas as caixas perdidas = game over e dinheiro zerado (${JSON.stringify({ jobs: shift2.jobs, lost: a.all("lost").map((m) => m.cargoId), shifts: a.all("shift").map((m) => m.shift) })})`);
}
assert(true, "todas as caixas perdidas = game over e dinheiro zerado");
await sleep(8800);
const restart = b.all("shift").at(-1);
assert(restart.shift === 1 && restart.money === 0, "sala recomeça no turno 1 depois do game over");

// ---------------- profissões (v0.9) ----------------
{
  const { code: pc } = await (await fetch(base + "/api/rooms", { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ autoProfession: true }) })).json();
  const p1 = await connect(pc, "Um", "gari");
  await sleep(250);
  const p2 = await connect(pc, "Dois", "gari");
  await sleep(250);
  const w1 = p1.find("welcome"), w2 = p2.find("welcome");
  assert(w1.autoProfession === true && w1.prof === "entregador" && w2.prof === "taxista", "sala automática equilibra as profissões");
  assert(w2.players[0].prof === "entregador", "welcome mostra a profissão dos outros");
  p2.send({ t: "prof", prof: "gari" });
  assert(await waitFor(p2, "prof", (m) => m.prof === "taxista"), "sala automática não deixa trocar");
  p1.send({ t: "earn", job: "taxista", amount: 300 });
  assert(await waitFor(p1, "earnFail"), "não recebe por profissão que não é a sua");
  p2.send({ t: "earn", job: "taxista", amount: 99999, what: "corrida" });
  await sleep(150);
  const e1 = p1.all("earned").at(-1);
  assert(e1 && e1.payment === 400 && e1.by === w2.you, "corrida limitada ao máximo e avisada para a sala");
  p2.send({ t: "earn", job: "taxista", amount: 100 });
  await sleep(150);
  assert(p2.all("earnFail").length === 1, "intervalo mínimo entre pagamentos");
  const list = await (await fetch(base + "/api/rooms")).json();
  const listed = list.rooms.find((r) => r.code === pc);
  assert(listed && listed.players === 2 && listed.autoProfession === true, "lista de salas públicas mostra a sala aberta");
  const { code: hidden } = await (await fetch(base + "/api/rooms", { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ isPublic: false }) })).json();
  const h = await connect(hidden, "Escondido");
  await sleep(300);
  assert(!(await (await fetch(base + "/api/rooms")).json()).rooms.some((r) => r.code === hidden), "sala privada não aparece na lista");
  h.ws.close();
  p1.ws.close(); p2.ws.close();

  const { code: fc } = await (await fetch(base + "/api/rooms", { method: "POST" })).json();
  const g = await connect(fc, "Gari", "gari");
  const r = await connect(fc, "Repo", "locutor");
  await sleep(300);
  assert(g.find("welcome").prof === "gari" && r.find("welcome").prof === "locutor", "sala livre respeita a escolha");
  assert(g.find("welcome").jobs.length === 3, "turno conta só os entregadores");
  g.send({ t: "earn", job: "gari", units: 9 });
  assert((await waitFor(g, "earned"))?.payment === 75, "gari: no máximo 3 sacos por pagamento");
  r.send({ t: "prof", prof: "repositor" });
  assert(await waitFor(g, "prof", (m) => m.prof === "repositor" && m.id === r.find("welcome").you), "troca de profissão avisada");
  g.ws.close(); r.ws.close();
}

// ---------------- veículos do pátio (v0.9.3) ----------------
{
  const { code: vc } = await (await fetch(base + "/api/rooms", { method: "POST" })).json();
  const v1 = await connect(vc, "Motoqueiro");
  const v2 = await connect(vc, "Caminhoneiro");
  await sleep(300);
  v1.send({ t: "claim", id: "moto" });
  assert(await waitFor(v2, "owner", (m) => m.id === "moto" && m.owner === v1.find("welcome").you), "moto tem dono");
  v1.send({ t: "tick", p: [0, 0, 0], ry: 0, hp: 0, st: 1, held: null, objs: [{ id: "moto", p: [5, 0, 5], q: [0, 0, 0, 1] }] });
  assert(await waitFor(v2, "tick", (m) => m.objs?.some((o) => o.id === "moto")), "estado da moto repassado");
  v2.send({ t: "claim", id: "moto" });
  v2.send({ t: "claim", id: "truck_red" });
  v2.send({ t: "claim", id: "aviao" });
  await sleep(200);
  assert(v2.find("claimFail")?.id === "moto", "não toma a moto de quem está pilotando");
  assert(v1.all("owner").some((m) => m.id === "truck_red"), "caminhão emprestado");
  assert(!v1.all("owner").some((m) => m.id === "aviao"), "veículo desconhecido ignorado");
  v1.send({ t: "release", id: "moto" });
  await sleep(150);
  assert(v2.all("owner").some((m) => m.id === "moto" && m.owner === null), "moto liberada ao descer");
  v1.ws.close(); v2.ws.close();
}

// ---------------- admin ----------------
const token = process.env.ADMIN_TOKEN ?? "teste-local";
const admin = (path, init = {}) => fetch(base + "/api/admin" + path, { ...init, headers: { Authorization: "Bearer " + token, "Content-Type": "application/json" } });
const adminPage = await fetch(base + "/admin");
const adminHtml = await adminPage.text();
assert(adminPage.headers.get("content-type").includes("text/html"), "página /admin");
const adminScript = adminHtml.match(/<script>([\s\S]*?)<\/script>/)?.[1];
assert(adminScript && typeof new Function(adminScript) === "function", "script inline do admin tem sintaxe válida");
const unauthenticatedAdmin = await fetch(base + "/api/admin/status");
if (unauthenticatedAdmin.status === 503) {
  console.log("skip - painel admin desativado sem ADMIN_TOKEN");
} else {
  assert(unauthenticatedAdmin.status === 401, "admin sem token = 401");
  const status = await (await admin("/status")).json();
  const listed = status.rooms.find((r) => r.code === code);
  assert(listed && listed.players === 2 && status.stats.deliveries >= welcomeA.jobs.length, "status lista a sala, jogadores e entregas");
  assert(status.stats.chatMessages >= 3 && status.chatByDayRoom.some((r) => r.room === code && r.messages >= 3), "admin vê métricas agregadas de chat por dia e sala");
  assert(status.traffic && typeof status.traffic.today.messages === "number" && Array.isArray(status.traffic.days), "admin expõe métricas agregadas de tráfego WebSocket");
  assert(status.rooms.some((r) => r.code === "ADMNA" && r.permanent && r.hasPassword), "admin vê sala fixa protegida");
  const detail = await (await admin("/rooms/" + code)).json();
  assert(detail.players.length === 2 && detail.jobs.length > 0 && detail.vanOwner === welcomeB.you, "detalhes da sala (jogadores, pedidos, dono da van)");
  await (await admin("/announce", { method: "POST", body: JSON.stringify({ msg: "teste de aviso" }) })).json();
  await sleep(200);
  assert(a.all("chat").some((m) => m.name === "SERVIDOR" && m.msg === "teste de aviso"), "aviso chega nas salas");

  const permanentResponse = await admin("/permanent-rooms", {
    method: "POST",
    body: JSON.stringify({ name: "Teste Permanente", level: "city", passwordProtected: true }),
  });
  const permanentRoom = await permanentResponse.json();
  assert(permanentResponse.status === 201 && /^[A-Z2-9]{5}$/.test(permanentRoom.code) && permanentRoom.password.length === 10, "admin cria sala permanente com senha de uso unico");
  const rejected = await connect(permanentRoom.code, "SemSenha", "entregador", [], "ERRADA");
  assert(await waitFor(rejected, "error", (m) => m.msg === "Senha da sala incorreta."), "senha incorreta impede entrada");
  assert(!rejected.find("welcome"), "senha incorreta nao recebe welcome");
  const accepted = await connect(permanentRoom.code, "ComSenha", "entregador", [], permanentRoom.password);
  assert(await waitFor(accepted, "welcome"), "senha correta permite entrada");
  const protectedClose = await admin("/rooms/" + permanentRoom.code + "/close", { method: "POST" });
  assert(protectedClose.status === 409, "sala permanente nao pode ser encerrada");
  accepted.ws.close();
}

b.ws.close();
await sleep(400);
assert(a.find("leave")?.id === welcomeB.you, "saída avisada");
assert(a.all("owner").some((m) => m.id === "van" && m.owner === null), "van liberada quando o motorista sai");
if (unauthenticatedAdmin.status !== 503) {
  const trafficStatus = await (await admin("/status")).json();
  assert(trafficStatus.traffic.today.messages > 0 && trafficStatus.traffic.today.connections > 0, "tráfego WebSocket é persistido ao fechar a conexão");
}

if (unauthenticatedAdmin.status !== 503) {
  const closeRes = await (await admin("/rooms/" + code + "/close", { method: "POST" })).json();
  await sleep(300);
  assert(closeRes.closed && a.find("kick")?.msg.includes("encerrada"), "admin encerra a sala e manda kick");
  assert((await fetch(`${base}/api/rooms/${code}`)).status === 404, "sala encerrada some");
  const after = await (await admin("/status")).json();
  assert(!after.rooms.some((r) => r.code === code), "sala encerrada sai do registro");
}

console.log("\nTudo certo ✔");
process.exit(0);
