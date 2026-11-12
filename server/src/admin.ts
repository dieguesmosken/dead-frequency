// Painel de admin servido em /admin. Página única, sem dependências externas.
// O token fica só no navegador (localStorage) e vai no cabeçalho Authorization.
export const ADMIN_HTML = /* html */ `<!doctype html>
<html lang="pt-BR">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<meta name="robots" content="noindex">
<title>Central · Dead Frequency</title>
<style>
  :root {
    --bg: #050806; --panel: #0a110c; --line: #1d3324; --dim: #4d8a5a; --fg: #8cff9e;
    --warn: #ffb347; --bad: #ff5a4f; --white: #e8ffe9;
    --mono: Consolas, "Lucida Console", "Courier New", monospace;
  }
  * { box-sizing: border-box; }
  html, body { margin: 0; background: var(--bg); color: var(--fg); font-family: var(--mono); }
  body::after { /* scanlines */
    content: ""; position: fixed; inset: 0; pointer-events: none;
    background: repeating-linear-gradient(0deg, rgba(0,0,0,.18) 0 1px, transparent 1px 3px);
  }
  main { max-width: 1180px; margin: 0 auto; padding: 20px 16px 60px; }
  header { display: flex; flex-wrap: wrap; gap: 12px; align-items: center; justify-content: space-between; margin-bottom: 18px; }
  h1 { margin: 0; font-size: 22px; letter-spacing: 2px; }
  h1 small { color: var(--dim); font-size: 13px; letter-spacing: 1px; display: block; margin-top: 2px; }
  h2 { font-size: 14px; color: var(--dim); letter-spacing: 2px; margin: 26px 0 10px; text-transform: uppercase; }
  .row { display: flex; flex-wrap: wrap; gap: 10px; align-items: center; }
  .dot { width: 10px; height: 10px; border-radius: 50%; background: var(--dim); display: inline-block; margin-right: 6px; }
  .dot.on { background: var(--fg); box-shadow: 0 0 8px var(--fg); animation: pulse 2s infinite; }
  .dot.off { background: var(--bad); }
  @keyframes pulse { 50% { opacity: .45; } }
  .muted { color: var(--dim); }
  button, input, select {
    font: inherit; color: var(--fg); background: #000; border: 1px solid var(--dim); padding: 8px 12px;
  }
  input { min-width: 0; }
  input:focus, button:focus-visible { outline: 1px solid var(--fg); }
  button { cursor: pointer; }
  button:hover { background: var(--fg); color: #000; }
  button.danger { border-color: var(--bad); color: var(--bad); }
  button.danger:hover { background: var(--bad); color: #000; }
  button:disabled { opacity: .4; cursor: default; }
  .cards { display: grid; grid-template-columns: repeat(auto-fill, minmax(170px, 1fr)); gap: 10px; }
  .card { background: var(--panel); border: 1px solid var(--line); padding: 12px 14px; }
  .card b { display: block; font-size: 28px; color: var(--white); font-weight: normal; margin-top: 4px; }
  .card span { color: var(--dim); font-size: 12px; text-transform: uppercase; letter-spacing: 1px; }
  .card em { color: var(--dim); font-size: 11px; font-style: normal; display: block; margin-top: 2px; }
  .panel { background: var(--panel); border: 1px solid var(--line); padding: 14px; }
  .table-wrap { overflow-x: auto; border: 1px solid var(--line); background: var(--panel); }
  table { width: 100%; border-collapse: collapse; font-size: 14px; }
  th, td { text-align: left; padding: 9px 10px; border-bottom: 1px solid var(--line); white-space: nowrap; }
  th { color: var(--dim); font-weight: normal; font-size: 12px; text-transform: uppercase; letter-spacing: 1px; }
  tr.empty td { color: var(--dim); }
  td.code { color: var(--white); letter-spacing: 3px; font-size: 16px; }
  td.names { white-space: normal; min-width: 160px; }
  .tag { border: 1px solid var(--line); padding: 1px 6px; font-size: 12px; margin-right: 4px; display: inline-block; }
  .tag.host { border-color: var(--warn); color: var(--warn); }
  .msg { padding: 10px 12px; border: 1px solid var(--line); margin-bottom: 14px; display: none; }
  .msg.show { display: block; }
  .msg.pre { white-space: pre-wrap; }
  .msg.err { border-color: var(--bad); color: var(--bad); }
  .msg.ok { border-color: var(--fg); }
  #login { max-width: 460px; margin: 12vh auto 0; }
  #login p { color: var(--dim); line-height: 1.5; font-size: 14px; }
  #login code { color: var(--white); }
  #detail { margin-top: 12px; display: none; }
  #detail.show { display: block; }
  .grid2 { display: grid; grid-template-columns: repeat(auto-fit, minmax(300px, 1fr)); gap: 12px; }
  .tabs { display:flex; gap:8px; flex-wrap:wrap; margin: 12px 0 18px; }
  .tab-btn[aria-selected="true"] { background: var(--fg); color:#000; border-color:var(--fg); }
  .tab-panel[hidden] { display:none; }
  .usage-note { line-height:1.55; white-space:normal; }
  .status-pending { color: var(--warn); }
  .status-delivered { color: var(--fg); }
  .status-lost { color: var(--bad); }
  footer { margin-top: 30px; color: var(--dim); font-size: 12px; }
  @media (max-width: 640px) { .card b { font-size: 22px; } h1 { font-size: 18px; } }
</style>
</head>
<body>
<main>
  <section id="login" hidden>
    <h1>CENTRAL DE RÁDIO<small>Dead Frequency · painel do servidor</small></h1>
    <p>Digite o token de admin. Ele é definido no servidor com<br><code>npx wrangler secret put ADMIN_TOKEN</code><br>(no teste local, no arquivo <code>server/.dev.vars</code>).</p>
    <form id="loginForm" class="row">
      <input id="tokenInput" type="password" placeholder="ADMIN_TOKEN" autocomplete="current-password" style="flex:1">
      <button type="submit">Entrar</button>
    </form>
    <div id="loginMsg" class="msg err" style="margin-top:12px"></div>
  </section>

  <section id="app" hidden>
    <header>
      <h1>CENTRAL DE RÁDIO<small>Dead Frequency · painel do servidor</small></h1>
      <div class="row">
        <span><span id="liveDot" class="dot"></span><span id="liveText" class="muted">conectando…</span></span>
        <label class="row muted" style="gap:6px"><input id="autoRefresh" type="checkbox" checked> auto (60 s)</label>
        <button id="refreshBtn">Atualizar</button>
        <button id="logoutBtn">Sair</button>
      </div>
    </header>

    <div id="appMsg" class="msg"></div>

    <nav class="tabs" aria-label="Seções do painel">
      <button type="button" class="tab-btn" data-tab="overview" aria-selected="true">Visão geral</button>
      <button type="button" class="tab-btn" data-tab="usage" aria-selected="false">Uso e limites</button>
    </nav>

    <section id="tab-overview" class="tab-panel">
    <div class="cards" id="cards"></div>

    <h2>Aviso para todas as salas</h2>
    <form id="announceForm" class="row panel">
      <input id="announceInput" maxlength="120" placeholder="Ex.: servidor reinicia em 5 minutos" style="flex:1; min-width:220px">
      <button type="submit">Enviar aviso</button>
    </form>

    <h2>Criar sala permanente</h2>
    <form id="permanentRoomForm" class="row panel">
      <input id="permanentRoomName" maxlength="32" placeholder="Nome da sala" required style="flex:1; min-width:180px">
      <select id="permanentRoomLevel">
        <option value="city">Cidade</option>
        <option value="farm">Sitio</option>
      </select>
      <label class="row muted" style="gap:6px"><input id="permanentRoomProtected" type="checkbox" checked> exigir senha</label>
      <button type="submit">Criar</button>
    </form>
    <div id="roomPasswords" class="msg pre"></div>
    <div id="createdRoomSecret" class="msg pre"></div>

    <h2>Salas <span id="roomCount" class="muted"></span></h2>
    <div class="table-wrap">
      <table>
        <thead><tr><th>Código</th><th>Jogadores</th><th>Turno</th><th>Pedidos</th><th>Dinheiro</th><th>Criada</th><th>Atividade</th><th></th></tr></thead>
        <tbody id="rooms"></tbody>
      </table>
    </div>

    <h2>Mensagens por dia e sala <span class="muted">(UTC · sem conteúdo)</span></h2>
    <div class="table-wrap">
      <table>
        <thead><tr><th>Dia</th><th>Sala</th><th>Mensagens</th></tr></thead>
        <tbody id="chatStats"></tbody>
      </table>
    </div>

    <section id="detail" class="panel"></section>

    <footer id="serverInfo"></footer>
    </section>

    <section id="tab-usage" class="tab-panel" hidden>
      <h2>Uso observado · WebSocket / Durable Objects</h2>
      <div class="cards" id="trafficCards"></div>
      <p id="trafficNote" class="panel usage-note muted">Aguardando métricas…</p>

      <h2>Mensagens recebidas por tipo · hoje (UTC)</h2>
      <div class="table-wrap">
        <table><thead><tr><th>Tipo</th><th>Mensagens</th></tr></thead><tbody id="trafficTypes"></tbody></table>
      </div>

      <h2>Histórico diário · últimos 30 dias (UTC)</h2>
      <div class="table-wrap">
        <table><thead><tr><th>Dia</th><th>Mensagens WS</th><th>Conexões</th><th>Equivalente DO*</th><th>Dados recebidos</th></tr></thead><tbody id="trafficDays"></tbody></table>
      </div>

      <h2>Limites de referência · plano Free</h2>
      <div class="grid2">
        <div class="panel usage-note">
          <strong>Workers · 100.000 requisições/dia</strong>
          <p>O código do jogo não consegue consultar o consumo total oficial da conta. Esse total inclui requisições HTTP e a abertura das conexões WebSocket. Consulte o painel de métricas da Cloudflare para o valor exato.</p>
          <a href="https://dash.cloudflare.com/" target="_blank" rel="noopener">Abrir painel Cloudflare ↗</a>
        </div>
        <div class="panel usage-note">
          <strong>Durable Objects · 100.000 requisições/dia</strong>
          <p>*Equivalente parcial estimado: conexões WebSocket + 1 requisição para cada 20 mensagens recebidas e 1 RPC por lote de métricas. Ainda não inclui outros RPCs, requisições HTTP internas nem alarmes.</p>
        </div>
      </div>
    </section>
</main>

<script>
(() => {
  const $ = (id) => document.getElementById(id);
  const store = {
    get() { try { return localStorage.getItem("df_admin_token") || ""; } catch { return ""; } },
    set(v) { try { v ? localStorage.setItem("df_admin_token", v) : localStorage.removeItem("df_admin_token"); } catch {} },
  };
  let token = store.get();
  let timer = null;
  let openRoom = null;

  const esc = (s) => String(s ?? "").replace(/[&<>"']/g, (c) => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[c]));
  const money = (n) => "$ " + Number(n || 0).toLocaleString("pt-BR");
  const ago = (t) => {
    if (!t) return "—";
    const s = Math.max(0, Math.round((Date.now() - t) / 1000));
    if (s < 60) return s + " s";
    if (s < 3600) return Math.round(s / 60) + " min";
    if (s < 86400) return Math.round(s / 3600) + " h";
    return Math.round(s / 86400) + " d";
  };

  async function api(path, options = {}) {
    const res = await fetch("/api/admin" + path, {
      ...options,
      headers: { "Authorization": "Bearer " + token, "Content-Type": "application/json", ...(options.headers || {}) },
    });
    const body = await res.json().catch(() => ({}));
    if (!res.ok) { const e = new Error(body.error || ("HTTP " + res.status)); e.status = res.status; throw e; }
    return body;
  }

  function show(el, text, kind) {
    el.textContent = text;
    el.className = "msg show " + (kind || "");
    if (kind === "ok") setTimeout(() => { el.className = "msg"; }, 3500);
  }

  function setLive(ok, text) {
    $("liveDot").className = "dot " + (ok ? "on" : "off");
    $("liveText").textContent = text;
  }

  function renderCards(data) {
    const s = data.stats;
    const cards = [
      ["Jogadores online", data.onlinePlayers, ""],
      ["Salas ativas", data.activeRooms, data.rooms.length + " no registro"],
      ["Salas criadas", s.roomsCreated, "desde " + new Date(s.since).toLocaleDateString("pt-BR")],
      ["Entregas", s.deliveries, s.lostCargo + " caixas perdidas"],
      ["Dinheiro ganho", money(s.moneyEarned), "somando todas as salas"],
      ["Turnos concluídos", s.shiftsCompleted, ""],
      ["Entradas em salas", s.joins, ""],
      ["Pico de jogadores", s.peakPlayers, s.peakPlayersAt ? "há " + ago(s.peakPlayersAt) : ""],
      ["Mensagens no chat", s.chatMessages || 0, "últimos 30 dias · sem conteúdo"],
    ];
    $("cards").innerHTML = cards.map(([label, value, note]) =>
      '<div class="card"><span>' + esc(label) + '</span><b>' + esc(value) + '</b>' + (note ? '<em>' + esc(note) + '</em>' : '') + '</div>').join("");
  }

  function renderRooms(rooms) {
    $("roomCount").textContent = "(" + rooms.length + ")";
    if (!rooms.length) {
      $("rooms").innerHTML = '<tr class="empty"><td colspan="8">Nenhuma sala ainda. Crie uma pelo jogo em Multiplayer → Criar sala.</td></tr>';
      return;
    }
    $("rooms").innerHTML = rooms.map((r) => {
      const names = r.names.length
        ? r.names.map((n) => '<span class="tag' + (n === r.hostName ? ' host' : '') + '">' + esc(n) + '</span>').join("")
        : '<span class="muted">' + (r.permanent ? "permanente · vazia" : "vazia · expira em ~30 min") + '</span>';
      const roomName = r.name ? '<br><span class="muted" style="letter-spacing:0;font-size:11px">' + esc(r.name) + '</span>' : "";
      const permanentTag = r.permanent ? '<span class="tag">PERMANENTE</span>' : "";
      const passwordTag = r.hasPassword ? '<span class="tag">SENHA</span>' : "";
      const protocol = Number(r.protocol || 7);
      const protocolTag = '<span class="tag">PROTOCOLO ' + protocol + '</span>';
      return '<tr class="' + (r.players ? "" : "empty") + '">' +
        '<td class="code">' + esc(r.code) + roomName + '<br>' + protocolTag + permanentTag + passwordTag + '</td>' +
        '<td class="names">' + r.players + ' · ' + names + '</td>' +
        '<td>' + r.shift + '</td>' +
        '<td>' + r.pendingJobs + ' pendentes</td>' +
        '<td>' + money(r.money) + '</td>' +
        '<td>há ' + ago(r.createdAt) + '</td>' +
        '<td>há ' + ago(r.updatedAt) + '</td>' +
        '<td><button data-detail="' + esc(r.code) + '" data-protocol="' + protocol + '">Detalhes</button> ' +
        (r.permanent ? '<button disabled title="Salas permanentes não expiram nem podem ser encerradas.">Permanente</button>' :
          '<button class="danger" data-close="' + esc(r.code) + '" data-protocol="' + protocol + '">Encerrar</button>') + '</td></tr>';
    }).join("");
  }

  function formatBytes(value) {
    const n = Number(value || 0);
    if (n < 1024) return n + " B";
    if (n < 1024 * 1024) return (n / 1024).toFixed(1) + " KB";
    return (n / (1024 * 1024)).toFixed(2) + " MB";
  }

  function renderTraffic(traffic, server) {
    const t = traffic?.today || { messages: 0, connections: 0, bytes: 0, byType: {}, estimatedDoRequests: 0 };
    const limit = Number(server?.limits?.durableObjectsFreeRequestsPerDay || 100000);
    const used = Number(t.estimatedDoRequests || 0);
    const percent = Math.min(100, used / limit * 100);
    const cards = [
      ["Mensagens WebSocket", Number(t.messages || 0).toLocaleString("pt-BR"), "recebidas hoje · UTC"],
      ["Conexões WebSocket", Number(t.connections || 0).toLocaleString("pt-BR"), "aberturas registradas hoje"],
      ["Equivalente DO estimado", used.toLocaleString("pt-BR"), percent.toFixed(2).replace(".", ",") + "% da referência Free"],
      ["Dados recebidos", formatBytes(t.bytes), "payloads WebSocket"],
    ];
    $("trafficCards").innerHTML = cards.map(([label, value, note]) =>
      '<div class="card"><span>' + esc(label) + '</span><b>' + esc(value) + '</b><em>' + esc(note) + '</em></div>').join("");

    const types = Object.entries(t.byType || {}).sort((a, b) => b[1] - a[1]);
    $("trafficTypes").innerHTML = types.length
      ? types.map(([type, count]) => "<tr><td>" + esc(type) + "</td><td>" + Number(count).toLocaleString("pt-BR") + "</td></tr>").join("")
      : '<tr class="empty"><td colspan="2">Nenhuma mensagem registrada hoje.</td></tr>';

    const days = traffic?.days || [];
    $("trafficDays").innerHTML = days.length
      ? days.map((d) => "<tr><td>" + esc(d.day) + "</td><td>" + Number(d.messages || 0).toLocaleString("pt-BR") +
        "</td><td>" + Number(d.connections || 0).toLocaleString("pt-BR") + "</td><td>" +
        Number(d.estimatedDoRequests || 0).toLocaleString("pt-BR") + "</td><td>" + esc(formatBytes(d.bytes)) + "</td></tr>").join("")
      : '<tr class="empty"><td colspan="5">Ainda não há histórico de tráfego.</td></tr>';

    $("trafficNote").textContent = "Contadores agregados em lotes (a cada 100 mensagens ou 10 conexões) e enviados ao registro global. O valor é aproximado: hibernação ou reinicialização antes da gravação pode perder o lote ainda não persistido. A equivalência soma conexões, mensagens recebidas na regra 20:1 e RPCs de gravação das métricas; outros RPCs e requisições internas não estão incluídos. Não substitui o Analytics oficial da Cloudflare.";
  }

  function renderChatStats(records) {
    if (!records || !records.length) {
      $("chatStats").innerHTML = '<tr class="empty"><td colspan="3">Nenhuma mensagem registrada nos últimos 30 dias.</td></tr>';
      return;
    }
    $("chatStats").innerHTML = records.map((r) =>
      "<tr><td>" + esc(r.day) + "</td><td class='code'>" + esc(r.room) + "</td><td>" + Number(r.messages || 0) + "</td></tr>"
    ).join("");
  }

  async function renderDetail(code, protocol = 7) {
    openRoom = { code, protocol };
    const box = $("detail");
    try {
      const d = await api("/rooms/" + encodeURIComponent(code) + "?protocol=" + protocol);
      const players = d.players.length ? d.players.map((p) => {
        const st = p.state;
        const where = st ? (st.st === 1 ? "dirigindo a van" : (st.held ? "segurando " + esc(st.held) : "a pé")) : "sem dados ainda";
        const pos = st ? st.p.map((v) => Math.round(v)).join(", ") : "—";
        return '<tr><td><span class="dot on" style="background:' + esc(p.color) + ';box-shadow:none;animation:none"></span>' + esc(p.name) +
          (p.host ? ' <span class="tag host">anfitrião</span>' : '') + '</td><td>' + where + '</td><td class="muted">' + pos + '</td></tr>';
      }).join("") : '<tr class="empty"><td colspan="3">Ninguém conectado.</td></tr>';

      const names = Object.fromEntries(d.players.map((p) => [p.id, p.name]));
      const jobs = d.jobs.map((j) =>
        '<tr><td>' + esc(j.id) + '</td><td>' + esc(j.cargo) + '</td><td class="status-' + j.status + '">' +
        ({ pending: "pendente", delivered: "entregue", lost: "perdida" }[j.status] || j.status) +
        '</td><td class="muted">' + (j.owner ? esc(names[j.owner] || ("#" + j.owner)) : "—") + '</td></tr>').join("");

      box.innerHTML =
        '<div class="row" style="justify-content:space-between"><h2 style="margin:0">Sala ' + esc(d.code) + '</h2>' +
        '<button id="closeDetail">Fechar detalhes</button></div>' +
        '<p class="muted">fase ' + (d.level === "farm" ? "rancho" : "cidade") + ' · seed ' + d.seed + ' · turno ' + d.shift + (d.pendingShift ? " (próximo em instantes)" : "") +
        ' · ' + money(d.money) + ' · van: ' + (d.vanOwner ? esc(names[d.vanOwner] || ("#" + d.vanOwner)) : "estacionada") +
        ' · criada ' + new Date(d.createdAt).toLocaleString("pt-BR") + '</p>' +
        '<div class="grid2"><div class="table-wrap"><table><thead><tr><th>Jogador</th><th>Situação</th><th>Posição</th></tr></thead><tbody>' + players + '</tbody></table></div>' +
        '<div class="table-wrap"><table><thead><tr><th>Pedido</th><th>Carga</th><th>Status</th><th>Com quem</th></tr></thead><tbody>' + jobs + '</tbody></table></div></div>';
      box.className = "panel show";
      $("closeDetail").onclick = () => { openRoom = null; box.className = "panel"; };
    } catch (e) {
      if (e.status === 404) { openRoom = null; box.className = "panel"; return; }
      show($("appMsg"), "Detalhes: " + e.message, "err");
    }
  }

  async function refresh() {
    try {
      const data = await api("/status");
      renderCards(data);
      renderRooms(data.rooms);
      renderChatStats(data.chatByDayRoom);
      renderTraffic(data.traffic, data.server);
      renderInitialPasswords(data.initialPasswords);
      const srv = data.server;
      $("serverInfo").textContent = "protocolo v" + srv.protocol + " · até " + srv.maxPlayers + " jogadores por sala · datacenter " + srv.colo +
        " · instância do worker ativa há " + ago(srv.isolateStartedAt) + " · atualizado " + new Date().toLocaleTimeString("pt-BR");
      setLive(true, "no ar");
      if (openRoom) renderDetail(openRoom.code, openRoom.protocol);
    } catch (e) {
      if (e.status === 401) { logout("Token inválido."); return; }
      setLive(false, e.status === 503 ? "painel desativado" : "sem resposta");
      show($("appMsg"), e.message, "err");
    }
  }

  function renderInitialPasswords(passwords) {
    const entries = Object.entries(passwords || {});
    if (!entries.length) return;
    const box = $("roomPasswords");
    const text = "Senhas iniciais das salas administrativas. Copie e guarde agora; elas nao serao exibidas novamente:\\n" +
      entries.map(([code, password]) => code + ": " + password).join("\\n");
    if (box.dataset.passwords === text) return;
    box.dataset.passwords = text;
    box.textContent = text;
    box.className = "msg show ok pre";
    const acknowledge = document.createElement("button");
    acknowledge.textContent = "Ja guardei as senhas";
    acknowledge.style.marginTop = "10px";
    acknowledge.onclick = async () => {
      try {
        await api("/initial-passwords/ack", { method: "POST" });
        box.className = "msg";
        box.dataset.passwords = "";
      } catch (e) {
        show($("appMsg"), "Nao foi possivel confirmar as senhas: " + e.message, "err");
      }
    };
    box.appendChild(document.createElement("br"));
    box.appendChild(acknowledge);
  }

  function showCreatedRoomSecret(code, password) {
    const box = $("createdRoomSecret");
    const text = "Sala permanente criada. Guarde esta senha, ela nao sera exibida novamente:\\n" + code + ": " + password;
    box.textContent = text;
    box.className = "msg show ok pre";
    const close = document.createElement("button");
    close.textContent = "Fechar";
    close.style.marginTop = "10px";
    close.onclick = () => { box.className = "msg"; };
    box.appendChild(document.createElement("br"));
    box.appendChild(close);
  }

  function schedule() {
    clearInterval(timer);
    if ($("autoRefresh").checked) timer = setInterval(refresh, 60000);
  }

  function login(value) {
    token = value.trim();
    store.set(token);
    $("login").hidden = true;
    $("app").hidden = false;
    refresh();
    schedule();
  }

  function logout(message) {
    clearInterval(timer);
    token = "";
    store.set("");
    $("app").hidden = true;
    $("login").hidden = false;
    if (message) show($("loginMsg"), message, "err");
    $("tokenInput").focus();
  }

  document.querySelectorAll("[data-tab]").forEach((button) => {
    button.onclick = () => {
      const selected = button.dataset.tab;
      document.querySelectorAll("[data-tab]").forEach((item) => item.setAttribute("aria-selected", String(item === button)));
      $("tab-overview").hidden = selected !== "overview";
      $("tab-usage").hidden = selected !== "usage";
    };
  });

  $("loginForm").onsubmit = (e) => { e.preventDefault(); if ($("tokenInput").value.trim()) login($("tokenInput").value); };
  $("logoutBtn").onclick = () => logout();
  $("refreshBtn").onclick = refresh;
  $("autoRefresh").onchange = schedule;

  $("rooms").onclick = async (e) => {
    const detail = e.target.closest("[data-detail]");
    const close = e.target.closest("[data-close]");
    if (detail) renderDetail(detail.dataset.detail, Number(detail.dataset.protocol || 7));
    if (close) {
      const code = close.dataset.close;
      const protocol = Number(close.dataset.protocol || 7);
      if (!confirm("Encerrar a sala " + code + " (protocolo " + protocol + ")? Todos os jogadores serão desconectados.")) return;
      close.disabled = true;
      try {
        await api("/rooms/" + encodeURIComponent(code) + "/close?protocol=" + protocol, { method: "POST" });
        show($("appMsg"), "Sala " + code + " encerrada.", "ok");
        if (openRoom && openRoom.code === code && openRoom.protocol === protocol) { openRoom = null; $("detail").className = "panel"; }
        refresh();
      } catch (err) { show($("appMsg"), err.message, "err"); close.disabled = false; }
    }
  };

  $("announceForm").onsubmit = async (e) => {
    e.preventDefault();
    const msg = $("announceInput").value.trim();
    if (!msg) return;
    try {
      const r = await api("/announce", { method: "POST", body: JSON.stringify({ msg }) });
      $("announceInput").value = "";
      show($("appMsg"), "Aviso enviado para " + r.rooms + " sala(s).", "ok");
    } catch (err) { show($("appMsg"), err.message, "err"); }
  };

  $("permanentRoomForm").onsubmit = async (e) => {
    e.preventDefault();
    const name = $("permanentRoomName").value.trim();
    if (!name) return;
    const button = $("permanentRoomForm").querySelector("button[type=submit]");
    button.disabled = true;
    try {
      const created = await api("/permanent-rooms", {
        method: "POST",
        body: JSON.stringify({
          name,
          level: $("permanentRoomLevel").value,
          passwordProtected: $("permanentRoomProtected").checked,
        }),
      });
      $("permanentRoomName").value = "";
      if (created.password) showCreatedRoomSecret(created.code, created.password);
      else show($("appMsg"), "Sala permanente publica criada: " + created.code, "ok");
      refresh();
    } catch (err) {
      show($("appMsg"), err.message, "err");
    } finally {
      button.disabled = false;
    }
  };

  if (token) login(token); else logout();
})();
</script>
</body>
</html>`;
