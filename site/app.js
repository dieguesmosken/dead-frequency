// Dead Frequency — site. Sem dependências: lê data/site.json (gerado pelo site/build.py a partir do
// GameVersion.cs), a última versão no GitHub e as estatísticas públicas do servidor.
const REPO = "dieguesmosken/dead-frequency";
const SERVER = "https://dead-frequency.teuzin0dev.workers.dev";
const $ = (s, el = document) => el.querySelector(s);
const fmt = (n) => (typeof n === "number" ? n.toLocaleString("pt-BR") : "—");
const mb = (b) => (b ? `${(b / 1048576).toFixed(0)} MB` : "");
const esc = (s) => String(s).replace(/[&<>"]/g, (c) => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;" })[c]);

// ---------------- menu no celular
$(".menu-btn").addEventListener("click", (e) => {
  const nav = $(".nav nav");
  nav.classList.toggle("open");
  e.currentTarget.setAttribute("aria-expanded", nav.classList.contains("open"));
});
document.querySelectorAll(".nav nav a").forEach((a) => a.addEventListener("click", () => $(".nav nav").classList.remove("open")));

// ---------------- copiar (Pix)
document.querySelectorAll("[data-copy]").forEach((btn) =>
  btn.addEventListener("click", async () => {
    const text = $(btn.dataset.copy).textContent.trim();
    try {
      await navigator.clipboard.writeText(text);
      btn.textContent = "Copiado!";
    } catch {
      btn.textContent = "Selecione e copie";
    }
    setTimeout(() => (btn.textContent = "Copiar"), 1800);
  })
);

// ---------------- lightbox
const lb = { list: [], i: 0, el: $("#lightbox") };
function openLightbox(list, i) {
  lb.list = list;
  lb.i = i;
  showLightbox();
  lb.el.hidden = false;
}
function showLightbox() {
  const it = lb.list[lb.i];
  $("img", lb.el).src = it.src;
  $("img", lb.el).alt = it.caption;
  $("figcaption", lb.el).textContent = it.caption;
}
const step = (d) => { lb.i = (lb.i + d + lb.list.length) % lb.list.length; showLightbox(); };
$(".lb-close").onclick = () => (lb.el.hidden = true);
$(".lb-prev").onclick = () => step(-1);
$(".lb-next").onclick = () => step(1);
lb.el.addEventListener("click", (e) => { if (e.target === lb.el) lb.el.hidden = true; });
document.addEventListener("keydown", (e) => {
  if (lb.el.hidden) return;
  if (e.key === "Escape") lb.el.hidden = true;
  if (e.key === "ArrowLeft") step(-1);
  if (e.key === "ArrowRight") step(1);
});

function thumbs(container, list) {
  container.innerHTML = list
    .map((s, i) => `<figure data-i="${i}"><img loading="lazy" src="${esc(s.src)}" alt="${esc(s.caption)}"><figcaption>${esc(s.caption)}</figcaption></figure>`)
    .join("");
  container.querySelectorAll("figure").forEach((f) => f.addEventListener("click", () => openLightbox(list, +f.dataset.i)));
}

// ---------------- dados do site (changelog + fotos)
async function loadSite() {
  let data;
  try {
    data = await (await fetch("data/site.json", { cache: "no-cache" })).json();
  } catch {
    $("#changelog").innerHTML = '<p class="dim">Não foi possível carregar o histórico. Veja o <a href="https://github.com/dieguesmosken/dead-frequency/blob/main/CHANGELOG.md">CHANGELOG.md</a>.</p>';
    return;
  }
  $("#pix-code").textContent = data.pix || "";
  setVersion(data.version, null);
  thumbs($("#gallery"), data.gallery);

  $("#changelog").innerHTML = data.changelog
    .map((r, i) => `
      <details class="release" ${i < 2 ? "open" : ""}>
        <summary><span class="ver">v${esc(r.version)}</span><span class="title">${esc(r.title)}</span>${i === 0 ? '<span class="new">atual</span>' : ""}<span class="date">${esc(r.date)}</span></summary>
        <div class="body">
          <ul>${r.changes.map((c) => `<li>${esc(c)}</li>`).join("")}</ul>
          ${r.shots.length ? `<div class="shots" data-v="${i}"></div>` : ""}
        </div>
      </details>`)
    .join("");
  data.changelog.forEach((r, i) => {
    const box = document.querySelector(`.shots[data-v="${i}"]`);
    if (box) thumbs(box, r.shots);
  });
}

function setVersion(v, date) {
  if (!v) return;
  $("#hero-version").textContent = "v" + v;
  // Topo do site: a última versão publicada (no lugar do "96.6").
  document.querySelectorAll(".brand-version").forEach((e) => (e.textContent = "v" + v));
  $("#dl-version").textContent = "v" + v;
  if (date) $("#dl-date").textContent = "· publicada em " + new Date(date).toLocaleDateString("pt-BR");
}

// ---------------- última versão no GitHub (tamanhos e links dos zips)
async function loadRelease() {
  try {
    const rel = await (await fetch(`https://api.github.com/repos/${REPO}/releases/latest`)).json();
    if (!rel.tag_name) return;
    setVersion(rel.tag_name.replace(/^v/, ""), rel.published_at);
    for (const a of rel.assets || []) {
      document.querySelectorAll(`[data-asset="${a.name}"] .size`).forEach((s) => (s.textContent = mb(a.size)));
      const zip = a.name.endsWith("-windows.zip") ? "#zip-win" : a.name.endsWith("-linux.zip") ? "#zip-linux" : null;
      if (zip) {
        $(zip).href = a.browser_download_url;
        $(".size", $(zip)).textContent = mb(a.size);
      }
    }
  } catch {
    // sem internet / limite da API: os links "latest/download" continuam funcionando
  }
}

// ---------------- servidor
async function loadServer() {
  const led = $("#srv-led");
  try {
    const s = await (await fetch(`${SERVER}/api/stats`, { cache: "no-store" })).json();
    if (!s.ok) throw new Error("sem stats");
    led.className = "led on";
    $("#srv-state").textContent = s.onlinePlayers > 0 ? `Online · ${s.onlinePlayers} jogando agora` : "Online · ninguém jogando agora (chama a galera!)";
    $("#srv-sub").textContent = `Protocolo ${s.protocol} · até ${s.maxPlayers} jogadores por sala`;
    $("#st-online").textContent = fmt(s.onlinePlayers);
    $("#st-rooms").textContent = fmt(s.activeRooms);
    $("#st-public").textContent = fmt(s.publicRooms);
    $("#st-peak").textContent = fmt(s.peakPlayers);
    $("#st-created").textContent = fmt(s.roomsCreated);
    $("#st-joins").textContent = fmt(s.joins);
    $("#st-deliv").textContent = fmt(s.deliveries);
    $("#st-money").textContent = "$ " + fmt(s.moneyEarned);
    $("#st-shifts").textContent = fmt(s.shiftsCompleted);
    $("#st-lost").textContent = fmt(s.lostCargo);
    if (s.since) $("#srv-since").textContent = `Contando desde ${new Date(s.since).toLocaleDateString("pt-BR")}.`;
    const rooms = [];
    if (s.cityRooms) rooms.push(`<span class="room">🏙 ${s.cityRooms} na cidade</span>`);
    if (s.farmRooms) rooms.push(`<span class="room">🌾 ${s.farmRooms} no sítio</span>`);
    $("#srv-rooms").innerHTML = rooms.join("");
  } catch {
    // Servidor antigo (sem /api/stats) ou fora do ar: tenta pelo menos o /health.
    try {
      const h = await (await fetch(`${SERVER}/health`, { cache: "no-store" })).json();
      led.className = "led on";
      $("#srv-state").textContent = "Online";
      $("#srv-sub").textContent = `Protocolo ${h.protocol} · estatísticas indisponíveis nesta versão do servidor`;
    } catch {
      led.className = "led off";
      $("#srv-state").textContent = "Servidor oficial fora do ar no momento";
      $("#srv-sub").textContent = "Dá para jogar sozinho ou hospedar a partida no seu PC (menu Multiplayer).";
    }
  }
}

loadSite();
loadRelease();
loadServer();
setInterval(loadServer, 30000);
