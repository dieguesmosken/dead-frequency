/**
 * Dead Frequency — servidor multiplayer (Cloudflare Workers + Durable Objects).
 *
 * Cada sala é um Durable Object identificado pelo código de 5 letras.
 * O servidor é a autoridade das REGRAS: turnos, pedidos, pagamentos, dinheiro do grupo,
 * quem é dono de cada objeto (van/caixas) e quem é o anfitrião.
 * A física roda nos clientes: o dono de cada objeto simula e envia o estado; o servidor
 * só aceita estado de quem é dono e repassa para os outros.
 *
 * HTTP:
 *   GET  /                      → status
 *   GET  /api/rooms             → salas públicas abertas { rooms: [...] }
 *   POST /api/rooms             → cria sala  { code }  (corpo: level, autoProfession, isPublic)
 *   GET  /api/rooms/:code       → info da sala { code, players, max }
 *   GET  /api/rooms/:code/ws    → WebSocket (?name=Apelido)
 *
 * Admin (Authorization: Bearer ADMIN_TOKEN):
 *   GET  /admin                       → painel (HTML)
 *   GET  /api/admin/status            → totais + lista de salas
 *   GET  /api/admin/rooms/:code       → detalhes de uma sala
 *   POST /api/admin/rooms/:code/close → encerra a sala
 *   POST /api/admin/announce {msg}    → aviso para todas as salas
 */
import { DurableObject } from "cloudflare:workers";
import { ADMIN_HTML } from "./admin";

export interface Env {
  ROOMS: DurableObjectNamespace<GameRoom>;
  REGISTRY: DurableObjectNamespace<Registry>;
  /** Defina com: npx wrangler secret put ADMIN_TOKEN  (local: arquivo .dev.vars) */
  ADMIN_TOKEN?: string;
  /** Senha do modo desenvolvedor. */
  DEBUG_PASSWORD?: string;
  /** Segredo HMAC para pseudonimizar IPs no limite de tentativas. Configure com: npx wrangler secret put DEBUG_RATE_LIMIT_SECRET */
  DEBUG_RATE_LIMIT_SECRET?: string;
}

/** Momento em que esta instância do Worker atendeu a primeira requisição (Date.now() no escopo global vale 0). */
let SERVER_STARTED = 0;

const PROTOCOL_VERSION = 5;
/** Veículos da fase (VanController.NetId no Godot): a van e, desde a v0.9.3, a moto e os caminhões do pátio. */
const VEHICLES = ["van", "moto", "truck_yellow", "truck_red", "car_gou", "car_fusca", "car_opalinha"];
/** Carros da concessionária (v0.9.7): comprados uma vez por sala, com o dinheiro do grupo. */
const CARS = ["car_gou", "car_fusca", "car_opalinha"];
const MAX_PLAYERS = 8;
const CODE_ALPHABET = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // sem 0/O/1/I
const CODE_LENGTH = 5;
const MAX_MESSAGE_BYTES = 8 * 1024;
const EMPTY_ROOM_TTL_MS = 30 * 60 * 1000;
const DEBUG_AUTH_WINDOW_MS = 15 * 60 * 1000;
const DEBUG_AUTH_LOCK_MS = 15 * 60 * 1000;
const DEBUG_AUTH_MAX_ATTEMPTS = 5;
const NEXT_SHIFT_DELAY_MS = 4000;

/** Mesma ordem de DeliveryManager.CargoCatalog no Godot. */
const CARGO_CATALOG = [
  { name: "Fitas Magnéticas", payment: 150 },
  { name: "Monitor CRT", payment: 300 },
  { name: "Suprimentos Médicos", payment: 120 },
  { name: "Peças de Rádio", payment: 180 },
  { name: "Caixa de Válvulas", payment: 220 },
  { name: "Documentos Lacrados", payment: 90 },
  { name: "Bateria Automotiva", payment: 160 },
  { name: "Botijão de Gás", payment: 140 },
  { name: "Bolo de Casamento", payment: 260 },
  { name: "Aquário com Peixe", payment: 280 },
  { name: "Antena Parabólica", payment: 200 },
  { name: "Caixote de Tomates", payment: 110 },
  { name: "Panela de Feijoada", payment: 170 },
];

/** Preços da loja — mesmos de ItemCatalog.cs no Godot. */
const SHOP_PRICES: Record<string, number> = {
  radar: 400,
  nightvision: 350,
  gloves: 250,
  backpack: 200,
  repairkit: 150,
  tape: 50,
  energy: 60,
  // v0.9.5
  guarana: 15,
  cafe: 25,
  marmitex: 35,
  espetinho: 12,
  chinelos: 80,
  prego: 10,
  // v0.9.7 — Auto Center Dois Mil
  car_gou: 2000,
  car_fusca: 2000,
  car_opalinha: 2000,
};

const GAME_OVER_RESTART_MS = 8000;

/**
 * Profissões (v0.9) — mesmas de Scripts/Core/Professions.cs e LocalServer.Professions.
 * pay = valor por unidade; maxUnits = unidades por pagamento; cooldownMs = intervalo mínimo entre pagamentos.
 * Taxista: o cliente manda o valor da corrida, limitado a [pay, maxAmount].
 */
const PROFESSIONS: Record<string, { pay: number; maxUnits: number; maxAmount?: number; cooldownMs: number; levels: string[] }> = {
  entregador: { pay: 0, maxUnits: 0, cooldownMs: 0, levels: ["city", "farm"] },
  taxista: { pay: 30, maxUnits: 1, maxAmount: 400, cooldownMs: 8000, levels: ["city", "farm"] },
  gari: { pay: 25, maxUnits: 3, cooldownMs: 1500, levels: ["city", "farm"] },
  repositor: { pay: 30, maxUnits: 1, cooldownMs: 2500, levels: ["city"] },
  frentista: { pay: 45, maxUnits: 1, cooldownMs: 5000, levels: ["city"] },
  locutor: { pay: 20, maxUnits: 1, cooldownMs: 9000, levels: ["city"] },
};
const PROFESSION_ORDER = ["entregador", "taxista", "gari", "repositor", "frentista", "locutor"];

const PLAYER_COLORS = ["#8cff9e", "#ffb347", "#6ec6ff", "#ff6b6b", "#d58cff", "#fff27a", "#7affea", "#ff9ad5"];

const CORS: Record<string, string> = {
  "Access-Control-Allow-Origin": "*",
  "Access-Control-Allow-Methods": "GET, POST, OPTIONS",
  "Access-Control-Allow-Headers": "Content-Type",
};

function json(data: unknown, status = 200): Response {
  return new Response(JSON.stringify(data), { status, headers: { "Content-Type": "application/json", ...CORS } });
}

function randomCode(): string {
  const bytes = crypto.getRandomValues(new Uint8Array(CODE_LENGTH));
  return Array.from(bytes, (b) => CODE_ALPHABET[b % CODE_ALPHABET.length]).join("");
}

function normalizeCode(raw: string): string | null {
  const code = raw.toUpperCase().trim();
  if (code.length !== CODE_LENGTH) return null;
  for (const ch of code) if (!CODE_ALPHABET.includes(ch)) return null;
  return code;
}

// ------------------------------------------------------------------ Worker

export default {
  async fetch(request: Request, env: Env): Promise<Response> {
    if (!SERVER_STARTED) SERVER_STARTED = Date.now();
    const url = new URL(request.url);
    if (request.method === "OPTIONS") return new Response(null, { headers: CORS });

    if (url.pathname === "/" || url.pathname === "/health") {
      return json({ ok: true, game: "dead-frequency", protocol: PROTOCOL_VERSION });
    }

    if (url.pathname === "/admin" || url.pathname === "/admin/") {
      return new Response(ADMIN_HTML, {
        headers: { "Content-Type": "text/html; charset=utf-8", "Cache-Control": "no-store", "X-Frame-Options": "DENY" },
      });
    }

    if (url.pathname.startsWith("/api/admin/")) {
      return handleAdmin(request, url, env);
    }

    // Estatísticas públicas para o site (só números agregados: nada de códigos, nomes ou IPs).
    if (url.pathname === "/api/stats" && request.method === "GET") {
      const registry = env.REGISTRY.get(env.REGISTRY.idFromName("global"));
      const stats = await registry.publicStats();
      return new Response(JSON.stringify({ ok: true, protocol: PROTOCOL_VERSION, maxPlayers: MAX_PLAYERS, ...stats, now: Date.now() }), {
        headers: { "Content-Type": "application/json", "Cache-Control": "public, max-age=15", ...CORS },
      });
    }

    if (url.pathname === "/api/rooms" && request.method === "GET") {
      const registry = env.REGISTRY.get(env.REGISTRY.idFromName("global"));
      return json({ rooms: await registry.publicRooms() });
    }

    if (url.pathname === "/api/rooms" && request.method === "POST") {
      let level = "city";
      let autoProfession = false;
      let isPublic = true;
      try {
        const body = (await request.json()) as { level?: unknown; autoProfession?: unknown; isPublic?: unknown };
        if (typeof body.level === "string" && LEVELS.includes(body.level)) level = body.level;
        autoProfession = body.autoProfession === true;
        isPublic = body.isPublic !== false;
      } catch {
        // corpo vazio ou inválido: cidade
      }
      for (let attempt = 0; attempt < 6; attempt++) {
        const code = randomCode();
        const stub = env.ROOMS.get(env.ROOMS.idFromName(code));
        const res = await stub.fetch("https://room/init", { method: "POST", body: JSON.stringify({ code, level, autoProfession, isPublic }) });
        if (res.status === 200) return json({ code });
        if (res.status !== 409) return json({ error: "Falha ao criar sala" }, 500);
      }
      return json({ error: "Não foi possível gerar um código livre" }, 503);
    }

    const match = url.pathname.match(/^\/api\/rooms\/([A-Za-z0-9]+)(\/ws)?$/);
    if (match) {
      const code = normalizeCode(match[1]);
      if (!code) return json({ error: "Código inválido" }, 400);
      const stub = env.ROOMS.get(env.ROOMS.idFromName(code));
      if (match[2]) {
        if (request.headers.get("Upgrade")?.toLowerCase() !== "websocket") {
          return json({ error: "Esperado WebSocket" }, 426);
        }
        return stub.fetch(request);
      }
      return stub.fetch("https://room/info");
    }

    return json({ error: "Não encontrado" }, 404);
  },
} satisfies ExportedHandler<Env>;

// ------------------------------------------------------------------ Admin

function isAuthorized(request: Request, env: Env): boolean {
  const expected = env.ADMIN_TOKEN ?? "";
  const header = request.headers.get("Authorization") ?? "";
  const given = header.startsWith("Bearer ") ? header.slice(7) : "";
  if (!expected || given.length !== expected.length) return false;
  let diff = 0;
  for (let i = 0; i < expected.length; i++) diff |= expected.charCodeAt(i) ^ given.charCodeAt(i);
  return diff === 0;
}

async function handleAdmin(request: Request, url: URL, env: Env): Promise<Response> {
  if (!env.ADMIN_TOKEN) {
    return json({ error: "Painel desativado: defina o segredo ADMIN_TOKEN (npx wrangler secret put ADMIN_TOKEN)." }, 503);
  }
  if (!isAuthorized(request, env)) return json({ error: "Token inválido" }, 401);

  const registry = env.REGISTRY.get(env.REGISTRY.idFromName("global"));
  const path = url.pathname.replace(/^\/api\/admin/, "");

  if (path === "/status" && request.method === "GET") {
    const status = await registry.status();
    const colo = (request as Request & { cf?: { colo?: string } }).cf?.colo ?? "local";
    return json({ ...status, server: { protocol: PROTOCOL_VERSION, maxPlayers: MAX_PLAYERS, colo, isolateStartedAt: SERVER_STARTED, now: Date.now() } });
  }

  if (path === "/announce" && request.method === "POST") {
    const { msg } = (await request.json().catch(() => ({}))) as { msg?: string };
    const text = String(msg ?? "").trim().slice(0, 120);
    if (!text) return json({ error: "Mensagem vazia" }, 400);
    const codes = await registry.activeCodes();
    await Promise.all(codes.map((code) => env.ROOMS.get(env.ROOMS.idFromName(code)).adminAnnounce(text)));
    return json({ ok: true, rooms: codes.length });
  }

  const match = path.match(/^\/rooms\/([A-Za-z0-9]+)(\/close)?$/);
  if (match) {
    const code = normalizeCode(match[1]);
    if (!code) return json({ error: "Código inválido" }, 400);
    const room = env.ROOMS.get(env.ROOMS.idFromName(code));
    if (match[2] && request.method === "POST") {
      const closed = await room.adminClose();
      if (!closed) await registry.remove(code);
      return json({ ok: true, closed });
    }
    const detail = await room.adminDetail();
    return detail ? json(detail) : json({ error: "Sala não encontrada" }, 404);
  }

  return json({ error: "Não encontrado" }, 404);
}

// ------------------------------------------------------------------ Registro global

export interface RoomSummary {
  code: string;
  players: number;
  names: string[];
  hostName: string | null;
  shift: number;
  money: number;
  pendingJobs: number;
  createdAt: number;
  updatedAt: number;
  level?: string;
  /** Aparece na lista de salas abertas (padrão: sim). */
  isPublic?: boolean;
  autoProfession?: boolean;
}

interface DebugAuthRecord {
  attempts: number;
  windowStartedAt: number;
  lockedUntil: number;
  expiresAt: number;
}

interface DebugAuthAttemptResult {
  allowed: boolean;
  lockedUntil: number;
  attemptsRemaining: number;
}

interface GlobalStats {
  roomsCreated: number;
  joins: number;
  deliveries: number;
  lostCargo: number;
  moneyEarned: number;
  shiftsCompleted: number;
  peakPlayers: number;
  peakPlayersAt: number;
  since: number;
}

export type RegistryEvent = "created" | "join" | "leave" | "delivered" | "lost" | "shift" | "update";

/** Um único objeto ("global") que guarda o resumo de todas as salas e os totais do servidor. */
export class Registry extends DurableObject<Env> {
  private async stats(): Promise<GlobalStats> {
    return (
      (await this.ctx.storage.get<GlobalStats>("stats")) ?? {
        roomsCreated: 0, joins: 0, deliveries: 0, lostCargo: 0, moneyEarned: 0, shiftsCompleted: 0, peakPlayers: 0, peakPlayersAt: 0, since: Date.now(),
      }
    );
  }

  /**
   * Controla tentativas de debug por identificador HMAC, compartilhado entre salas e conexões.
   * O IP original nunca é persistido; somente a chave derivada é usada como identificador.
   */
  async attemptDebugAuth(clientId: string, passwordValid: boolean): Promise<DebugAuthAttemptResult> {
    const now = Date.now();
    const key = `debug-auth:${clientId}`;

    // Uma transação evita que duas conexões consumam a mesma tentativa simultaneamente.
    const result = await this.ctx.storage.transaction(async (txn) => {
      let record = await txn.get<DebugAuthRecord>(key);

      if (record && now < record.lockedUntil) {
        return { allowed: false, lockedUntil: record.lockedUntil, attemptsRemaining: 0, nextExpiry: record.expiresAt };
      }

      if (!record || now >= record.expiresAt || now - record.windowStartedAt >= DEBUG_AUTH_WINDOW_MS) {
        record = { attempts: 0, windowStartedAt: now, lockedUntil: 0, expiresAt: now + DEBUG_AUTH_WINDOW_MS };
      }

      if (passwordValid) {
        await txn.delete(key);
        return { allowed: true, lockedUntil: 0, attemptsRemaining: DEBUG_AUTH_MAX_ATTEMPTS, nextExpiry: 0 };
      }

      record.attempts++;
      if (record.attempts >= DEBUG_AUTH_MAX_ATTEMPTS) {
        record.lockedUntil = now + DEBUG_AUTH_LOCK_MS;
        record.expiresAt = record.lockedUntil;
      }
      await txn.put(key, record);
      return {
        allowed: false,
        lockedUntil: record.lockedUntil,
        attemptsRemaining: Math.max(0, DEBUG_AUTH_MAX_ATTEMPTS - record.attempts),
        nextExpiry: record.expiresAt,
      };
    });

    if (result.nextExpiry > 0) await this.ctx.storage.setAlarm(result.nextExpiry);
    return {
      allowed: result.allowed,
      lockedUntil: result.lockedUntil,
      attemptsRemaining: result.attemptsRemaining,
    };
  }

  /** Remove identificadores de autenticação expirados; mantém apenas hashes temporários. */
  async alarm(): Promise<void> {
    const records = await this.ctx.storage.list<DebugAuthRecord>({ prefix: "debug-auth:" });
    const now = Date.now();
    let nextExpiry = Number.POSITIVE_INFINITY;

    for (const [key, record] of records) {
      if (record.expiresAt <= now) await this.ctx.storage.delete(key);
      else nextExpiry = Math.min(nextExpiry, record.expiresAt);
    }

    if (Number.isFinite(nextExpiry)) await this.ctx.storage.setAlarm(nextExpiry);
  }

  async report(summary: RoomSummary, event: RegistryEvent, amount = 0): Promise<void> {
    const stats = await this.stats();
    if (event === "created") stats.roomsCreated++;
    if (event === "join") stats.joins++;
    if (event === "delivered") { stats.deliveries++; stats.moneyEarned += amount; }
    if (event === "lost") stats.lostCargo++;
    if (event === "shift") stats.shiftsCompleted++;

    await this.ctx.storage.put(`room:${summary.code}`, summary);
    const online = await this.onlinePlayers();
    if (online > stats.peakPlayers) { stats.peakPlayers = online; stats.peakPlayersAt = Date.now(); }
    await this.ctx.storage.put("stats", stats);
  }

  /** Salas abertas e públicas, para a lista do menu Multiplayer (sem nomes além do anfitrião). */
  async publicRooms(): Promise<unknown[]> {
    const rooms = await this.ctx.storage.list<RoomSummary>({ prefix: "room:" });
    const stale = Date.now() - 6 * 60 * 60 * 1000;
    return Array.from(rooms.values())
      .filter((r) => r.players > 0 && r.isPublic !== false && r.updatedAt > stale)
      .sort((a, b) => b.players - a.players || b.updatedAt - a.updatedAt)
      .slice(0, 30)
      .map((r) => ({
        code: r.code,
        players: r.players,
        max: MAX_PLAYERS,
        level: r.level ?? "city",
        host: r.hostName,
        shift: r.shift,
        autoProfession: r.autoProfession === true,
      }));
  }

  /** Totais para a página pública de status. */
  async publicStats(): Promise<Record<string, number>> {
    const stats = await this.stats();
    const rooms = Array.from((await this.ctx.storage.list<RoomSummary>({ prefix: "room:" })).values());
    const stale = Date.now() - 6 * 60 * 60 * 1000;
    const live = rooms.filter((r) => r.players > 0 && r.updatedAt > stale);
    return {
      onlinePlayers: live.reduce((sum, r) => sum + r.players, 0),
      activeRooms: live.length,
      publicRooms: live.filter((r) => r.isPublic !== false).length,
      cityRooms: live.filter((r) => (r.level ?? "city") === "city").length,
      farmRooms: live.filter((r) => r.level === "farm").length,
      roomsCreated: stats.roomsCreated,
      joins: stats.joins,
      deliveries: stats.deliveries,
      lostCargo: stats.lostCargo,
      moneyEarned: stats.moneyEarned,
      shiftsCompleted: stats.shiftsCompleted,
      peakPlayers: stats.peakPlayers,
      peakPlayersAt: stats.peakPlayersAt,
      since: stats.since,
    };
  }

  async remove(code: string): Promise<void> {
    await this.ctx.storage.delete(`room:${code}`);
  }

  async activeCodes(): Promise<string[]> {
    const rooms = await this.ctx.storage.list<RoomSummary>({ prefix: "room:" });
    return Array.from(rooms.values()).filter((r) => r.players > 0).map((r) => r.code);
  }

  async status(): Promise<{ stats: GlobalStats; rooms: RoomSummary[]; onlinePlayers: number; activeRooms: number }> {
    const rooms = Array.from((await this.ctx.storage.list<RoomSummary>({ prefix: "room:" })).values())
      .sort((a, b) => b.players - a.players || b.updatedAt - a.updatedAt);
    return {
      stats: await this.stats(),
      rooms,
      onlinePlayers: rooms.reduce((sum, r) => sum + r.players, 0),
      activeRooms: rooms.filter((r) => r.players > 0).length,
    };
  }

  private async onlinePlayers(): Promise<number> {
    const rooms = await this.ctx.storage.list<RoomSummary>({ prefix: "room:" });
    let total = 0;
    for (const r of rooms.values()) total += r.players;
    return total;
  }
}

// ------------------------------------------------------------------ Sala

type JobStatus = "pending" | "delivered" | "lost";

interface Job {
  id: string; // ex.: c3_1  (turno 3, caixa 1)
  type: number; // índice em CARGO_CATALOG
  pick: number; // sorteio do destino (o cliente mapeia para um ponto da cidade)
  slot: number; // posição de spawn no depósito
  status: JobStatus;
}

/** Fases que existem no jogo (Scripts/Core/GameLevels.cs). */
const LEVELS = ["city", "farm"];

interface RoomState {
  code: string;
  /** Fase da sala: todos os jogadores carregam a mesma. Salas antigas não têm (= cidade). */
  level?: string;
  seed: number;
  createdAt: number;
  shift: number;
  money: number;
  jobs: Job[];
  nextPlayerId: number;
  hostId: number | null;
  owners: Record<string, number | null>; // objeto → id do jogador dono
  cars?: string[]; // carros da concessionária já comprados (v0.9.7)
  pendingShift: boolean;
  /** O servidor escolhe a profissão de cada jogador (equilibrando as vagas). */
  autoProfession?: boolean;
  /** Aparece na lista de salas abertas. Salas antigas não têm (= pública). */
  isPublic?: boolean;
  /** Todas as caixas do turno quebraram: a sala recomeça do turno 1. */
  gameOver?: boolean;
  emptySince: number | null;
}

interface Attachment {
  id: number;
  name: string;
  color: string;
  joined: boolean;
  prof?: string;
  debugAuthorizedUntil?: number;
  /** HMAC-SHA-256 do IP de origem; nunca contém o IP original. */
  debugClientId?: string;
}

interface PlayerSnapshot {
  p: number[];
  ry: number;
  hp: number;
  st: number;
  held: string | null;
  eq: string;
  lk: string;
}

interface ObjectSnapshot {
  id: string;
  p: number[];
  q: number[];
  v?: number[];
  w?: number[];
  integ?: number;
}

export class GameRoom extends DurableObject<Env> {
  private room: RoomState | null = null;
  /** Último estado de cada jogador/objeto (só em memória; reconstruído pelos próprios clientes). */
  private players = new Map<number, PlayerSnapshot>();
  private objects = new Map<string, ObjectSnapshot>();
  /** Último pagamento de profissão de cada jogador (limite de frequência). */
  private lastEarn = new Map<number, number>();

  constructor(ctx: DurableObjectState, env: Env) {
    super(ctx, env);
    ctx.blockConcurrencyWhile(async () => {
      this.room = (await ctx.storage.get<RoomState>("room")) ?? null;
    });
  }

  private async save(): Promise<void> {
    if (this.room) await this.ctx.storage.put("room", this.room);
  }

  // ---------------- Registro / admin ----------------

  private summary(excludeId?: number): RoomSummary {
    const room = this.room!;
    const joined = this.connected().map(attachmentOf).filter((a) => a.joined && a.id !== excludeId);
    return {
      code: room.code,
      players: joined.length,
      names: joined.map((a) => a.name),
      hostName: joined.find((a) => a.id === room.hostId)?.name ?? null,
      shift: room.shift,
      money: room.money,
      pendingJobs: room.jobs.filter((j) => j.status === "pending").length,
      createdAt: room.createdAt,
      updatedAt: Date.now(),
      level: room.level ?? "city",
      isPublic: room.isPublic !== false,
      autoProfession: room.autoProfession === true,
    };
  }

  /** Avisa o registro global (sem travar a sala se ele demorar). */
  private report(event: RegistryEvent, amount = 0, exclude?: WebSocket): void {
    if (!this.room) return;
    const summary = this.summary(exclude ? attachmentOf(exclude).id : undefined);
    const registry = this.env.REGISTRY.get(this.env.REGISTRY.idFromName("global"));
    this.ctx.waitUntil(registry.report(summary, event, amount).catch(() => {}));
  }

  async adminDetail(): Promise<unknown> {
    const room = this.room;
    if (!room) return null;
    const players = this.connected().map(attachmentOf).filter((a) => a.joined).map((a) => ({
      id: a.id,
      name: a.name,
      color: a.color,
      prof: a.prof ?? "entregador",
      host: a.id === room.hostId,
      state: this.players.get(a.id) ?? null,
    }));
    return {
      code: room.code,
      seed: room.seed,
      level: room.level ?? "city",
      createdAt: room.createdAt,
      shift: room.shift,
      money: room.money,
      pendingShift: room.pendingShift,
      jobs: room.jobs.map((j) => ({ ...j, cargo: CARGO_CATALOG[j.type].name, owner: room.owners[j.id] ?? null })),
      vanOwner: room.owners.van ?? null,
      players,
    };
  }

  async adminAnnounce(text: string): Promise<void> {
    this.broadcast({ t: "chat", id: 0, name: "SERVIDOR", msg: text });
  }

  /** Encerra a sala: avisa, desconecta todos e apaga o estado. */
  async adminClose(): Promise<boolean> {
    if (!this.room) return false;
    const code = this.room.code;
    for (const socket of this.connected()) {
      try {
        socket.send(JSON.stringify({ t: "kick", msg: "Sala encerrada pelo administrador." }));
        socket.close(4001, "closed by admin");
      } catch {
        // já fechando
      }
    }
    this.room = null;
    this.players.clear();
    this.objects.clear();
    await this.ctx.storage.deleteAll();
    const registry = this.env.REGISTRY.get(this.env.REGISTRY.idFromName("global"));
    await registry.remove(code);
    return true;
  }

  // ---------------- HTTP interno ----------------

  async fetch(request: Request): Promise<Response> {
    const url = new URL(request.url);

    if (url.pathname === "/init") {
      if (this.room) return json({ error: "exists" }, 409);
      const { code, level, autoProfession, isPublic } = (await request.json()) as { code: string; level?: string; autoProfession?: boolean; isPublic?: boolean };
      this.room = {
        code,
        level: level && LEVELS.includes(level) ? level : "city",
        autoProfession: autoProfession === true,
        isPublic: isPublic !== false,
        seed: crypto.getRandomValues(new Uint32Array(1))[0] % 1_000_000,
        createdAt: Date.now(),
        shift: 0,
        money: 0,
        jobs: [],
        nextPlayerId: 1,
        hostId: null,
        owners: { van: null },
        pendingShift: false,
        emptySince: Date.now(),
      };
      this.newShift(false);
      await this.save();
      await this.ctx.storage.setAlarm(Date.now() + EMPTY_ROOM_TTL_MS);
      this.report("created");
      return json({ code });
    }

    if (!this.room) return json({ error: "Sala não encontrada" }, 404);

    if (url.pathname === "/info") {
      return json({ code: this.room.code, players: this.connected().length, max: MAX_PLAYERS, shift: this.room.shift });
    }

    if (url.pathname.endsWith("/ws")) {
      if (this.connected().length >= MAX_PLAYERS) return json({ error: "Sala cheia" }, 403);

      const pair = new WebSocketPair();
      const [client, server] = Object.values(pair);
      const id = this.room.nextPlayerId++;
      const name = sanitizeName(url.searchParams.get("name") ?? "") || `Entregador ${id}`;
      const sourceIp = request.headers.get("CF-Connecting-IP")?.trim() || "unknown";
      const debugClientId = this.env.DEBUG_RATE_LIMIT_SECRET
        ? await deriveDebugClientId(sourceIp, this.env.DEBUG_RATE_LIMIT_SECRET)
        : undefined;
      const attachment: Attachment = {
        id,
        name,
        color: PLAYER_COLORS[(id - 1) % PLAYER_COLORS.length],
        joined: false,
        debugClientId,
      };
      this.ctx.acceptWebSocket(server);
      server.serializeAttachment(attachment);
      this.room.emptySince = null;
      await this.save();
      return new Response(null, { status: 101, webSocket: client });
    }

    return json({ error: "Não encontrado" }, 404);
  }

  // ---------------- WebSocket ----------------

  async webSocketMessage(ws: WebSocket, raw: string | ArrayBuffer): Promise<void> {
    if (!this.room) {
      // Sala apagada (admin/expirou) mas o socket ainda está vivo: manda sair.
      try {
        ws.send(JSON.stringify({ t: "kick", msg: "Esta sala não existe mais." }));
        ws.close(4001, "room closed");
      } catch {
        // já fechado
      }
      return;
    }
    const text = typeof raw === "string" ? raw : new TextDecoder().decode(raw);
    if (text.length > MAX_MESSAGE_BYTES) return;

    let msg: any;
    try {
      msg = JSON.parse(text);
    } catch {
      return;
    }
    const me = ws.deserializeAttachment() as Attachment;

    switch (msg.t) {
      case "debug_auth": {
        if (!me.joined) {
          ws.send(JSON.stringify({
            t: "debug_auth_result",
            ok: false,
            msg: "Entre na sala antes de autenticar."
          }));
          return;
        }

        const configuredPassword = this.env.DEBUG_PASSWORD;
        if (!configuredPassword || !this.env.DEBUG_RATE_LIMIT_SECRET || !me.debugClientId) {
          ws.send(JSON.stringify({
            t: "debug_auth_result",
            ok: false,
            msg: "Autenticação de debug não configurada corretamente no servidor."
          }));
          return;
        }

        const password = typeof msg.password === "string" ? msg.password : "";
        const passwordValid = constantTimeEquals(password, configuredPassword);
        const registry = this.env.REGISTRY.get(this.env.REGISTRY.idFromName("global"));
        const attempt = await registry.attemptDebugAuth(me.debugClientId, passwordValid);

        if (!attempt.allowed) {
          const locked = attempt.lockedUntil > Date.now();
          const retryAfter = locked ? Math.ceil((attempt.lockedUntil - Date.now()) / 1000) : 0;
          ws.send(JSON.stringify({
            t: "debug_auth_result",
            ok: false,
            msg: locked
              ? "Muitas tentativas. Tente novamente em 15 minutos."
              : "Senha incorreta.",
            attemptsRemaining: attempt.attemptsRemaining,
            retryAfter
          }));
          if (locked) ws.close(4003, "Too many debug auth attempts");
          return;
        }

        me.debugAuthorizedUntil = Date.now() + 30 * 60 * 1000;
        ws.serializeAttachment(me);
        ws.send(JSON.stringify({
          t: "debug_auth_result",
          ok: true,
          msg: "Modo desenvolvedor autorizado.",
          expiresIn: 1800
        }));
        return;
      }
      case "debug_add_money": {
        const amount = Number(msg.amount);
        if (!me.joined || !me.debugAuthorizedUntil || me.debugAuthorizedUntil <= Date.now()) {
          ws.send(JSON.stringify({ t: "debug_add_money_result", ok: false, msg: "Autorização de desenvolvedor expirada." })); return;
        }
        if (!Number.isInteger(amount) || amount < 1 || amount > 1_000_000) {
          ws.send(JSON.stringify({ t: "debug_add_money_result", ok: false, msg: "Valor inválido. Use de 1 a 1.000.000." })); return;
        }
        this.room.money += amount;
        await this.save();
        this.broadcast({ t: "money", money: this.room.money });
        ws.send(JSON.stringify({ t: "debug_add_money_result", ok: true, added: amount, money: this.room.money }));
        return;
      }
      case "hello":
        return this.onHello(ws, me, msg);
      case "tick":
        return this.onTick(ws, me, msg);
      case "claim":
        return this.onClaim(ws, me, String(msg.id ?? ""));
      case "release":
        return this.onRelease(me, String(msg.id ?? ""));
      case "deliver":
        return this.onDeliver(me, String(msg.cargoId ?? ""), Number(msg.integ));
      case "lost":
        return this.onLost(me, String(msg.cargoId ?? ""));
      case "buy":
        return this.onBuy(ws, me, String(msg.item ?? ""), Number(msg.req) || 0);
      case "earn":
        return this.onEarn(ws, me, msg);
      case "prof":
        return this.onProf(ws, me, String(msg.prof ?? ""));
      case "chat": {
        const chat = String(msg.msg ?? "").slice(0, 120).trim();
        if (chat) this.broadcast({ t: "chat", id: me.id, name: me.name, msg: chat });
        return;
      }
      case "ping":
        ws.send(JSON.stringify({ t: "pong", ts: msg.ts }));
        return;
    }
  }

  async webSocketClose(ws: WebSocket): Promise<void> {
    await this.onLeave(ws);
  }

  async webSocketError(ws: WebSocket): Promise<void> {
    await this.onLeave(ws);
  }

  private async onHello(ws: WebSocket, me: Attachment, msg: any): Promise<void> {
    const room = this.room!;
    if (Number(msg.version) !== PROTOCOL_VERSION) {
      ws.send(JSON.stringify({ t: "error", msg: "Versão do jogo diferente da do servidor. Atualize o jogo." }));
      ws.close(4000, "version");
      return;
    }
    if (me.joined) return;
    me.joined = true;
    me.prof = room.autoProfession ? this.assignProfession(me.id) : this.validProfession(String(msg.prof ?? ""));
    ws.serializeAttachment(me);

    if (room.hostId === null || !this.connected().some((s) => attachmentOf(s).id === room.hostId)) {
      room.hostId = me.id;
    }
    await this.save();

    const others = this.connected()
      .map(attachmentOf)
      .filter((a) => a.joined && a.id !== me.id)
      .map((a) => ({ id: a.id, name: a.name, color: a.color, prof: a.prof ?? "entregador", state: this.players.get(a.id) ?? null }));

    ws.send(
      JSON.stringify({
        t: "welcome",
        you: me.id,
        name: me.name,
        color: me.color,
        code: room.code,
        seed: room.seed,
        level: room.level ?? "city",
        prof: me.prof,
        autoProfession: room.autoProfession === true,
        host: room.hostId,
        players: others,
        shift: room.shift,
        money: room.money,
        jobs: room.jobs,
        owners: room.owners,
        cars: room.cars ?? [],
        objects: Array.from(this.objects.values()),
      }),
    );
    this.broadcast({ t: "join", player: { id: me.id, name: me.name, color: me.color, prof: me.prof } }, ws);
    this.report("join");
  }

  private onTick(ws: WebSocket, me: Attachment, msg: any): void {
    if (!me.joined) return;
    const room = this.room!;
    const snapshot: PlayerSnapshot = {
      p: vec(msg.p, 3),
      ry: num(msg.ry),
      hp: num(msg.hp),
      st: msg.st === 1 ? 1 : msg.st === 2 ? 2 : 0,
      held: typeof msg.held === "string" ? msg.held.slice(0, 24) : null,
      eq: typeof msg.eq === "string" ? msg.eq.replace(/[^a-z_,]/g, "").slice(0, 80) : "",
      lk: typeof msg.lk === "string" ? msg.lk.replace(/[^mw0-9,]/g, "").slice(0, 24) : "",
    };
    this.players.set(me.id, snapshot);

    // Só aceita estado de objetos que o jogador realmente controla.
    const objs: ObjectSnapshot[] = [];
    if (Array.isArray(msg.objs)) {
      for (const o of msg.objs.slice(0, 16)) {
        const id = String(o?.id ?? "");
        if (!id || room.owners[id] !== me.id) continue;
        const obj: ObjectSnapshot = { id, p: vec(o.p, 3), q: vec(o.q, 4), v: vec(o.v, 3), w: vec(o.w, 3) };
        if (typeof o.integ === "number") obj.integ = clamp01(o.integ);
        this.objects.set(id, obj);
        objs.push(obj);
      }
    }

    this.broadcast({ t: "tick", id: me.id, ...snapshot, objs }, ws);
  }

  private async onClaim(ws: WebSocket, me: Attachment, id: string): Promise<void> {
    const room = this.room!;
    if (!this.isKnownObject(id)) return;
    const owner = room.owners[id] ?? null;
    const ownerOnline = owner !== null && this.connected().some((s) => attachmentOf(s).id === owner);
    const ownerBusy = ownerOnline && owner !== me.id && this.isUsing(owner!, id);

    if (ownerBusy) {
      ws.send(JSON.stringify({ t: "claimFail", id, owner }));
      return;
    }
    if (owner !== me.id) {
      room.owners[id] = me.id;
      await this.save();
      this.broadcast({ t: "owner", id, owner: me.id });
    }
  }

  private async onRelease(me: Attachment, id: string): Promise<void> {
    const room = this.room!;
    // Caixas continuam com o último dono (ele segue simulando a física); só os veículos são liberados.
    if (!VEHICLES.includes(id) || room.owners[id] !== me.id) return;
    room.owners[id] = null;
    await this.save();
    this.broadcast({ t: "owner", id, owner: null });
  }

  private async onDeliver(me: Attachment, cargoId: string, integrity: number): Promise<void> {
    const room = this.room!;
    const job = room.jobs.find((j) => j.id === cargoId);
    if (!job || job.status !== "pending" || room.owners[cargoId] !== me.id) return;

    const integ = Number.isFinite(integrity) ? clamp01(integrity) : 0;
    const payment = Math.round(CARGO_CATALOG[job.type].payment * integ);
    job.status = "delivered";
    room.money += payment;
    delete room.owners[cargoId];
    this.objects.delete(cargoId);
    await this.save();

    this.broadcast({ t: "delivered", cargoId, by: me.id, name: me.name, payment, money: room.money });
    this.report("delivered", payment);
    await this.checkShiftDone();
  }

  /** Compra na barraca: confere o preço e desconta do dinheiro do grupo. */
  private async onBuy(ws: WebSocket, me: Attachment, item: string, req: number): Promise<void> {
    const room = this.room!;
    const price = SHOP_PRICES[item];
    const isCar = CARS.includes(item);
    if (price === undefined || room.money < price || (isCar && (room.cars ?? []).includes(item))) {
      ws.send(JSON.stringify({ t: "buyFail", item, req }));
      return;
    }
    room.money -= price;
    if (isCar) room.cars = [...(room.cars ?? []), item];
    await this.save();
    this.broadcast({ t: "money", money: room.money, by: me.id, name: me.name, item, req });
    this.report("update");
  }

  // ---------------- Profissões ----------------

  private validProfession(prof: string): string {
    const level = this.room?.level ?? "city";
    const def = PROFESSIONS[prof];
    return def && def.levels.includes(level) ? prof : "entregador";
  }

  /** Escolhe a profissão com menos gente (empate: a primeira da lista, começando por entregador). */
  private assignProfession(excludeId: number): string {
    const level = this.room?.level ?? "city";
    const counts = new Map<string, number>();
    for (const a of this.connected().map(attachmentOf)) {
      if (!a.joined || a.id === excludeId || !a.prof) continue;
      counts.set(a.prof, (counts.get(a.prof) ?? 0) + 1);
    }
    let best = "entregador";
    let bestCount = Infinity;
    for (const p of PROFESSION_ORDER) {
      if (!PROFESSIONS[p].levels.includes(level)) continue;
      const c = counts.get(p) ?? 0;
      if (c < bestCount) { best = p; bestCount = c; }
    }
    return best;
  }

  private async onProf(ws: WebSocket, me: Attachment, prof: string): Promise<void> {
    if (!me.joined) return;
    if (this.room!.autoProfession) {
      ws.send(JSON.stringify({ t: "prof", id: me.id, prof: me.prof ?? "entregador", locked: true }));
      return;
    }
    me.prof = this.validProfession(prof);
    ws.serializeAttachment(me);
    this.broadcast({ t: "prof", id: me.id, prof: me.prof });
  }

  /** Pagamento de tarefa de profissão: confere profissão, valor e intervalo mínimo. */
  private async onEarn(ws: WebSocket, me: Attachment, msg: any): Promise<void> {
    const room = this.room!;
    const job = String(msg.job ?? "");
    const def = PROFESSIONS[job];
    const now = Date.now();
    const last = this.lastEarn.get(me.id) ?? 0;
    if (!me.joined || !def || def.maxUnits === 0 || me.prof !== job || now - last < def.cooldownMs) {
      ws.send(JSON.stringify({ t: "earnFail", job }));
      return;
    }
    let payment: number;
    if (def.maxAmount) {
      const amount = Math.round(Number(msg.amount));
      payment = Number.isFinite(amount) ? Math.max(def.pay, Math.min(def.maxAmount, amount)) : def.pay;
    } else {
      const units = Math.floor(Number(msg.units));
      payment = def.pay * (Number.isFinite(units) ? Math.max(1, Math.min(def.maxUnits, units)) : 1);
    }
    this.lastEarn.set(me.id, now);
    room.money += payment;
    await this.save();
    const what = String(msg.what ?? "").slice(0, 60);
    this.broadcast({ t: "earned", by: me.id, name: me.name, job, payment, money: room.money, what });
    this.report("delivered", payment);
  }

  private entregadores(): number {
    return this.connected().map(attachmentOf).filter((a) => a.joined && (a.prof ?? "entregador") === "entregador").length;
  }

  private async onLost(me: Attachment, cargoId: string): Promise<void> {
    const room = this.room!;
    const job = room.jobs.find((j) => j.id === cargoId);
    if (!job || job.status !== "pending" || room.owners[cargoId] !== me.id) return;
    job.status = "lost";
    await this.save();
    this.broadcast({ t: "lost", cargoId, by: me.id, name: me.name });
    this.report("lost");
    await this.checkShiftDone();
  }

  private async onLeave(ws: WebSocket): Promise<void> {
    const room = this.room;
    if (!room) return;
    const me = attachmentOf(ws);
    this.players.delete(me.id);
    this.lastEarn.delete(me.id);

    // Libera a van se ele dirigia; caixas ficam sem dono até alguém pegar.
    let changed = false;
    for (const [id, owner] of Object.entries(room.owners)) {
      if (owner === me.id) {
        room.owners[id] = null;
        changed = true;
        this.broadcast({ t: "owner", id, owner: null }, ws);
      }
    }

    const remaining = this.connected().filter((s) => s !== ws && attachmentOf(s).joined);
    if (room.hostId === me.id) {
      room.hostId = remaining.length ? attachmentOf(remaining[0]).id : null;
      changed = true;
      if (room.hostId !== null) this.broadcast({ t: "host", id: room.hostId }, ws);
    }
    if (remaining.length === 0) {
      room.emptySince = Date.now();
      changed = true;
      await this.ctx.storage.setAlarm(Date.now() + EMPTY_ROOM_TTL_MS);
    }
    if (changed) await this.save();
    if (me.joined) {
      this.broadcast({ t: "leave", id: me.id }, ws);
      this.report("leave", 0, ws);
    }
  }

  // ---------------- Turnos ----------------

  private newShift(broadcast: boolean): void {
    const room = this.room!;
    room.shift += 1;
    room.pendingShift = false;
    // Só os entregadores contam para o tamanho do turno (os outros têm as tarefas da profissão).
    const count = Math.min(6, 2 + Math.max(1, this.entregadores()));
    const random = crypto.getRandomValues(new Uint32Array(count * 2));

    for (const job of room.jobs) delete room.owners[job.id];
    room.jobs = Array.from({ length: count }, (_, i) => ({
      id: `c${room.shift}_${i}`,
      type: random[i * 2] % CARGO_CATALOG.length,
      pick: random[i * 2 + 1] % 100000,
      slot: i,
      status: "pending" as JobStatus,
    }));
    for (const job of room.jobs) this.objects.delete(job.id);

    if (broadcast) this.broadcast({ t: "shift", shift: room.shift, jobs: room.jobs, money: room.money });
  }

  private async checkShiftDone(): Promise<void> {
    const room = this.room!;
    if (room.pendingShift || room.jobs.some((j) => j.status === "pending")) return;
    room.pendingShift = true;

    if (!room.jobs.some((j) => j.status === "delivered") && this.entregadores() > 0) {
      // Nenhuma entrega: game over. Zera o dinheiro e recomeça do turno 1.
      room.gameOver = true;
      room.money = 0;
      await this.save();
      this.broadcast({ t: "gameOver", shift: room.shift, money: 0 });
      await this.ctx.storage.setAlarm(Date.now() + GAME_OVER_RESTART_MS);
      return;
    }

    await this.save();
    const earned = room.jobs.length;
    this.broadcast({ t: "shiftDone", shift: room.shift, jobs: earned, money: room.money });
    await this.ctx.storage.setAlarm(Date.now() + NEXT_SHIFT_DELAY_MS);
  }

  async alarm(): Promise<void> {
    const room = this.room;
    if (!room) return;

    if (room.pendingShift) {
      if (room.gameOver) {
        room.gameOver = false;
        room.shift = 0;
      }
      this.newShift(true);
      await this.save();
      this.report("shift");
    }

    // Sala vazia há muito tempo: apaga tudo.
    if (this.connected().length === 0) {
      const since = room.emptySince ?? Date.now();
      if (Date.now() - since >= EMPTY_ROOM_TTL_MS - 1000) {
        const code = room.code;
        await this.ctx.storage.deleteAll();
        this.room = null;
        await this.env.REGISTRY.get(this.env.REGISTRY.idFromName("global")).remove(code);
        return;
      }
      await this.ctx.storage.setAlarm(since + EMPTY_ROOM_TTL_MS);
    }
  }

  // ---------------- Utilitários ----------------

  private connected(): WebSocket[] {
    return this.ctx.getWebSockets();
  }

  private broadcast(message: unknown, except?: WebSocket): void {
    const text = JSON.stringify(message);
    for (const socket of this.connected()) {
      if (socket === except || !attachmentOf(socket).joined) continue;
      try {
        socket.send(text);
      } catch {
        // socket fechando; o close handler cuida do resto
      }
    }
  }

  private isKnownObject(id: string): boolean {
    return VEHICLES.includes(id) || this.room!.jobs.some((j) => j.id === id && j.status === "pending");
  }

  /** O dono atual está segurando a caixa ou dirigindo a van neste momento? */
  private isUsing(playerId: number, objectId: string): boolean {
    const snap = this.players.get(playerId);
    if (!snap) return false;
    if (VEHICLES.includes(objectId)) return snap.st === 1;
    return snap.held === objectId;
  }
}

function attachmentOf(ws: WebSocket): Attachment {
  return ws.deserializeAttachment() as Attachment;
}

/** Gera um identificador estável por IP sem persistir ou expor o endereço original. */
async function deriveDebugClientId(ip: string, secret: string): Promise<string> {
  const encoder = new TextEncoder();
  const key = await crypto.subtle.importKey(
    "raw",
    encoder.encode(secret),
    { name: "HMAC", hash: "SHA-256" },
    false,
    ["sign"]
  );
  const signature = await crypto.subtle.sign(
    "HMAC",
    key,
    encoder.encode(`dead-frequency:debug-auth:v1:${ip}`)
  );
  return Array.from(new Uint8Array(signature), (byte) => byte.toString(16).padStart(2, "0")).join("");
}

/** Comparação sem saída antecipada para reduzir vazamento de tempo sobre a senha. */
function constantTimeEquals(a: string, b: string): boolean {
  if (a.length !== b.length) return false;
  let difference = 0;
  for (let i = 0; i < a.length; i++) difference |= a.charCodeAt(i) ^ b.charCodeAt(i);
  return difference === 0;
}

function sanitizeName(raw: string): string {
  return raw.replace(/[^\p{L}\p{N} _.\-]/gu, "").trim().slice(0, 16);
}

function num(value: unknown): number {
  const n = Number(value);
  return Number.isFinite(n) ? Math.round(n * 1000) / 1000 : 0;
}

function vec(value: unknown, length: number): number[] {
  const out = new Array<number>(length).fill(0);
  if (Array.isArray(value)) for (let i = 0; i < length; i++) out[i] = num(value[i]);
  return out;
}

function clamp01(n: number): number {
  return Math.min(1, Math.max(0, n));
}
