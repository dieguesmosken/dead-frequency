// Scripts/Net/NetClient.cs
// Autoload (registrado no project.godot como "NetClient").
// Conexão com o servidor multiplayer na Cloudflare: cria/entra em salas e troca mensagens JSON
// por WebSocket. Não sabe nada do jogo — quem interpreta as mensagens é o NetSync da fase.
using System;
using System.Collections.Generic;
using System.Text.Json;
using Godot;

namespace DeadFrequency;

public class NetPlayerInfo
{
    public int Id;
    public string Name = "";
    public Color Color = Colors.White;
    public string Profession = Professions.Entregador;
}

public partial class NetClient : Node
{
    // V7 permanece como base compatível e protocolo do servidor local.
    public const int ProtocolVersion = 7;
    public const int PreferredProtocolVersion = 8;
    /// <summary>Servidor oficial do projeto (Cloudflare). Não aparece na tela; é o padrão.</summary>
    public const string OfficialServer = "https://dead-frequency.teuzin0dev.workers.dev";

    public enum ConnectionStatus
    {
        Offline,
        Connecting,
        Online,
    }

    public static NetClient Instance { get; private set; }

    /// <summary>Texto curto para a interface ("Conectando...", "Sala cheia" etc.).</summary>
    public event Action<string> StatusMessage;
    /// <summary>Entrou na sala (recebeu o welcome).</summary>
    public event Action Joined;
    /// <summary>Falhou ao criar/entrar ou caiu a conexão.</summary>
    public event Action<string> Disconnected;
    /// <summary>Toda mensagem recebida do servidor (válida só durante a chamada).</summary>
    public event Action<JsonElement> MessageReceived;

    public event Action<bool, string> DebugAuthResult;

    private ulong _debugAuthExpiresAt;
    public bool IsDebugAuthorized => _debugAuthExpiresAt > Time.GetTicksMsec();

    public ConnectionStatus Status { get; private set; } = ConnectionStatus.Offline;
    public bool IsOnline => Status == ConnectionStatus.Online;
    public string ServerUrl { get; private set; } = "";
    /// <summary>Endereço que os amigos usam (link do túnel quando você hospeda no PC).</summary>
    public string PublicServerUrl { get; set; } = "";
    public bool UsingOfficialServer => NormalizeServer(ServerUrl) == NormalizeServer(OfficialServer);
    public string RoomCode { get; private set; } = "";
    public string PlayerName { get; private set; } = "";
    public int MyId { get; private set; }
    public Color MyColor { get; private set; } = Colors.White;
    public int Seed { get; private set; }
    public int ActiveProtocolVersion { get; private set; } = ProtocolVersion;
    /// <summary>Fase da sala ("city", "farm"...), vinda do servidor.</summary>
    public string Level { get; private set; } = "city";
    public int HostId { get; private set; }
    /// <summary>A sala sorteia as profissões (configuração de quem criou).</summary>
    public bool AutoProfession { get; private set; }
    public bool IsHost => IsOnline && HostId == MyId;
    /// <summary>JSON bruto do welcome, relido pelo NetSync quando a fase carrega.</summary>
    public string WelcomeJson { get; private set; } = "";
    public IReadOnlyDictionary<int, NetPlayerInfo> Players => _players;
    public int PlayerCount => IsOnline ? _players.Count + 1 : 1;
    public float PingMs { get; private set; }

    private readonly Dictionary<int, NetPlayerInfo> _players = new();
    private WebSocketPeer _socket;
    private WebSocketPeer.State _lastState = WebSocketPeer.State.Closed;
    private HttpRequest _http;
    private double _pingTimer;
    private string _joinPassword = "";
    private string _connectFailureMessage = "";
    private int _createProtocol = PreferredProtocolVersion;
    private string _pendingCreateLevel = "city";
    private bool _pendingAutoProfession;
    private bool _pendingIsPublic = true;

    public override void _EnterTree()
    {
        Instance = this;
        ProcessMode = ProcessModeEnum.Always;
    }

    public override void _ExitTree()
    {
        if (Instance == this)
            Instance = null;
    }

    public override void _Ready()
    {
        _http = new HttpRequest { Timeout = 10 };
        AddChild(_http);
        _http.RequestCompleted += OnCreateRoomCompleted;
    }

    // ---------------- API pública ----------------

    public void CreateRoom(string serverUrl, string playerName, string level = "city", bool autoProfession = false, bool isPublic = true)
    {
        if (Status != ConnectionStatus.Offline)
            Leave();

        ServerUrl = NormalizeServer(serverUrl);
        PlayerName = playerName;
        Status = ConnectionStatus.Connecting;
        StatusMessage?.Invoke("Criando sala...");

        _pendingCreateLevel = level;
        _pendingAutoProfession = autoProfession;
        _pendingIsPublic = isPublic;
        _createProtocol = PreferredProtocolVersion;
        SendCreateRoomRequest();
    }

    /// <summary>Protocolo 0 detecta a versão da sala; 7/8 conecta diretamente.</summary>
    public void JoinRoom(string serverUrl, string code, string playerName, string roomPassword = "", int protocol = 0)
    {
        if (Status != ConnectionStatus.Offline)
            Leave();

        ServerUrl = NormalizeServer(serverUrl);
        PlayerName = playerName;
        PublicServerUrl = "";
        RoomCode = code.Trim().ToUpperInvariant();
        _joinPassword = roomPassword.Trim();
        _connectFailureMessage = "";
        Status = ConnectionStatus.Connecting;
        StatusMessage?.Invoke($"Entrando na sala {RoomCode}...");

        if (protocol is 7 or 8)
            ConnectToRoom(protocol);
        else
            ProbeRoomProtocol();
    }

    private void ProbeRoomProtocol()
    {
        var probe = new HttpRequest { Timeout = 2 };
        AddChild(probe);
        probe.RequestCompleted += (result, responseCode, headers, body) =>
        {
            probe.QueueFree();
            int protocol = ProtocolVersion;
            if (result == (long)HttpRequest.Result.Success && responseCode == 200)
            {
                try
                {
                    using JsonDocument doc = JsonDocument.Parse(body);
                    if (doc.RootElement.TryGetProperty("protocol", out JsonElement p)
                        && p.TryGetInt32(out int detected) && detected is 7 or 8)
                        protocol = detected;
                }
                catch (JsonException)
                {
                    // Worker antigo ou servidor local: mantém V7.
                }
            }
            // Se a descoberta não existir (Worker antigo/servidor local), tenta V7.
            ConnectToRoom(protocol);
        };

        Error err = probe.Request($"{HttpBase(ServerUrl)}/api/rooms/{RoomCode}?protocol={PreferredProtocolVersion}");
        if (err != Error.Ok)
        {
            probe.QueueFree();
            ConnectToRoom(ProtocolVersion);
        }
    }

    private void ConnectToRoom(int protocol)
    {
        ActiveProtocolVersion = protocol is 7 or 8 ? protocol : ProtocolVersion;
        string url = $"{WsBase(ServerUrl)}/api/rooms/{RoomCode}/ws?protocol={ActiveProtocolVersion}&name={Uri.EscapeDataString(PlayerName)}";
        _socket = new WebSocketPeer();
        _socket.InboundBufferSize = 1 << 20;
        Error err = _socket.ConnectToUrl(url);
        if (err != Error.Ok)
        {
            Fail($"Falha ao conectar ({err}).");
            return;
        }
        _lastState = WebSocketPeer.State.Connecting;
    }

    public void Leave()
    {
        // Fecha e esquece o socket na hora: o _Process não vê mais esse fechamento,
        // então não precisa de flag (a flag antiga ficava presa e engolia o "kick" do servidor).
        if (_socket != null && _socket.GetReadyState() is WebSocketPeer.State.Open or WebSocketPeer.State.Connecting)
            _socket.Close(1000, "leave");
        ResetSession();
    }

    public void Send(object payload)
    {
        if (_socket == null || _socket.GetReadyState() != WebSocketPeer.State.Open)
            return;
        _socket.SendText(JsonSerializer.Serialize(payload));
    }

    /// <summary>Solicita ao servidor acesso temporário às ferramentas de desenvolvedor.</summary>
    public void RequestDebugAuth(string password)
    {
        if (!IsOnline)
        {
            DebugAuthResult?.Invoke(false, "Entre em uma sala online para autenticar.");
            return;
        }
        Send(new { t = "debug_auth", password = password ?? "" });
    }

    public void RequestDebugAddMoney(int amount)
    {
        if (!IsOnline || !IsDebugAuthorized) return;
        Send(new { t = "debug_add_money", amount });
    }

    public readonly record struct RoomListing(string Code, string Name, int Players, int Max, string Level, string Host, int Shift, bool AutoProfession, bool Permanent, bool HasPassword, int Protocol);

    /// <summary>Lista salas V7 e V8; Workers antigos ignoram a query e suas salas V7 são deduplicadas.</summary>
    public void ListRooms(string serverUrl, Action<List<RoomListing>, string> callback)
    {
        string baseUrl = HttpBase(NormalizeServer(serverUrl));
        var rooms = new Dictionary<string, RoomListing>(StringComparer.OrdinalIgnoreCase);
        int completed = 0;
        int successful = 0;
        string lastError = "Servidor fora do ar.";

        void FinishOne()
        {
            completed++;
            if (completed < 2)
                return;
            callback(successful == 0 ? null : new List<RoomListing>(rooms.Values), successful == 0 ? lastError : null);
        }

        void FetchProtocol(int requestedProtocol)
        {
            var http = new HttpRequest { Timeout = 10 };
            AddChild(http);
            http.RequestCompleted += (result, code, headers, body) =>
            {
                http.QueueFree();
                if (result == (long)HttpRequest.Result.Success && code == 200)
                {
                    try
                    {
                        using JsonDocument doc = JsonDocument.Parse(body);
                        foreach (JsonElement r in doc.RootElement.GetProperty("rooms").EnumerateArray())
                        {
                            int protocol = r.TryGetProperty("protocol", out JsonElement rp)
                                && rp.TryGetInt32(out int parsed) && parsed is 7 or 8 ? parsed : ProtocolVersion;
                            var room = new RoomListing(
                                r.GetProperty("code").GetString() ?? "",
                                r.TryGetProperty("name", out JsonElement nm) ? nm.GetString() ?? "" : "",
                                r.GetProperty("players").GetInt32(),
                                r.TryGetProperty("max", out JsonElement mx) ? mx.GetInt32() : 8,
                                r.TryGetProperty("level", out JsonElement lv) ? lv.GetString() ?? "city" : "city",
                                r.TryGetProperty("host", out JsonElement h) && h.ValueKind == JsonValueKind.String ? h.GetString() : "",
                                r.TryGetProperty("shift", out JsonElement sh) ? sh.GetInt32() : 1,
                                r.TryGetProperty("autoProfession", out JsonElement ap) && ap.ValueKind == JsonValueKind.True,
                                r.TryGetProperty("permanent", out JsonElement permanent) && permanent.ValueKind == JsonValueKind.True,
                                r.TryGetProperty("hasPassword", out JsonElement password) && password.ValueKind == JsonValueKind.True,
                                protocol);
                            rooms[$"{protocol}:{room.Code}"] = room;
                        }
                        successful++;
                    }
                    catch (Exception)
                    {
                        lastError = "Resposta inválida do servidor.";
                    }
                }
                else if (result != (long)HttpRequest.Result.Success)
                    lastError = "Servidor fora do ar.";
                else if (!(requestedProtocol == PreferredProtocolVersion && code == 400))
                    lastError = $"O servidor não tem lista de salas ({code}).";
                FinishOne();
            };

            Error err = http.Request($"{baseUrl}/api/rooms?protocol={requestedProtocol}");
            if (err != Error.Ok)
            {
                http.QueueFree();
                lastError = $"Não foi possível contatar o servidor ({err}).";
                FinishOne();
            }
        }

        FetchProtocol(PreferredProtocolVersion);
        FetchProtocol(ProtocolVersion);
    }

    /// <summary>Texto pronto para mandar a um amigo.</summary>
    public string InviteText()
    {
        if (UsingOfficialServer)
            return $"Bora entregar comigo no Dead Frequency! Código da sala: {RoomCode}";
        string server = string.IsNullOrEmpty(PublicServerUrl) ? ServerUrl : PublicServerUrl;
        return $"Bora entregar comigo no Dead Frequency! Código da sala: {RoomCode}\nServidor: {server}";
    }

    // ---------------- Loop ----------------

    public override void _Process(double delta)
    {
        if (_socket == null)
            return;

        _socket.Poll();
        WebSocketPeer.State state = _socket.GetReadyState();

        if (state == WebSocketPeer.State.Open)
        {
            if (_lastState != WebSocketPeer.State.Open)
                Send(new
                {
                    t = "hello",
                    version = ActiveProtocolVersion,
                    name = PlayerName,
                    prof = Professions.Current,
                    roomPassword = _joinPassword,
                });

            while (_socket.GetAvailablePacketCount() > 0)
                HandleText(_socket.GetPacket().GetStringFromUtf8());

            _pingTimer -= delta;
            if (_pingTimer <= 0 && IsOnline)
            {
                _pingTimer = 3.0;
                Send(new { t = "ping", ts = Time.GetTicksMsec() });
            }
        }
        else if (state == WebSocketPeer.State.Closed && _lastState != WebSocketPeer.State.Closed)
        {
            int code = _socket.GetCloseCode();
            string reason = _socket.GetCloseReason();
            _socket = null;
            {
                string message = code == 4001 ? "Sala encerrada pelo administrador."
                    : Status == ConnectionStatus.Online
                    ? "Conexão com o servidor perdida."
                    : !string.IsNullOrEmpty(_connectFailureMessage) ? _connectFailureMessage
                    : code == -1 ? "Não foi possível conectar. Confira o endereço do servidor e o código." : $"Conexão recusada ({code} {reason}).";
                Fail(message);
            }
        }

        _lastState = state;
    }

    private void HandleText(string text)
    {
        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(text);
        }
        catch (JsonException)
        {
            return;
        }

        using (doc)
        {
            JsonElement msg = doc.RootElement;
            string type = msg.TryGetProperty("t", out JsonElement t) ? t.GetString() ?? "" : "";

            switch (type)
            {
                case "welcome":
                    OnWelcome(msg, text);
                    break;
                case "join":
                    JsonElement p = msg.GetProperty("player");
                    AddPlayer(p);
                    break;
                case "leave":
                    _players.Remove(msg.GetProperty("id").GetInt32());
                    break;
                case "host":
                    HostId = msg.GetProperty("id").GetInt32();
                    break;
                case "debug_auth_result":
                    {
                        bool success =
                            msg.TryGetProperty("ok", out JsonElement okElement) &&
                            okElement.ValueKind == JsonValueKind.True;

                        string message =
                            msg.TryGetProperty("msg", out JsonElement messageElement)
                                ? messageElement.GetString() ?? ""
                                : "";

                        _debugAuthExpiresAt = success ? Time.GetTicksMsec() + 30UL * 60UL * 1000UL : 0;
                        DebugAuthResult?.Invoke(success, message);
                        break;
                    }
                case "pong":
                    PingMs = (float)(Time.GetTicksMsec() - msg.GetProperty("ts").GetUInt64());
                    break;
                case "error":
                    string errorMessage = msg.GetProperty("msg").GetString() ?? "Falha ao entrar na sala.";
                    if (Status == ConnectionStatus.Connecting)
                        _connectFailureMessage = errorMessage;
                    StatusMessage?.Invoke(errorMessage);
                    break;
                case "kick":
                    // Servidor mandou sair (ex.: admin encerrou a sala). Avisa e desconecta já.
                    MessageReceived?.Invoke(msg);
                    _socket?.Close(1000, "kicked");
                    Fail(msg.TryGetProperty("msg", out JsonElement reasonEl) ? reasonEl.GetString() : "Você foi desconectado.");
                    return;
                
            }

            MessageReceived?.Invoke(msg);
        }
    }

    private void OnWelcome(JsonElement msg, string raw)
    {
        if (msg.TryGetProperty("protocol", out JsonElement protocolElement)
            && protocolElement.TryGetInt32(out int serverProtocol)
            && serverProtocol != ActiveProtocolVersion)
        {
            _socket?.Close(1002, "protocol mismatch");
            Fail($"Protocolo incompatível: cliente V{ActiveProtocolVersion}, servidor V{serverProtocol}.");
            return;
        }

        MyId = msg.GetProperty("you").GetInt32();
        MyColor = ParseColor(msg.GetProperty("color").GetString());
        PlayerName = msg.GetProperty("name").GetString() ?? PlayerName;
        RoomCode = msg.GetProperty("code").GetString() ?? RoomCode;
        Seed = msg.GetProperty("seed").GetInt32();
        // Servidores antigos não mandam a fase: cidade.
        Level = msg.TryGetProperty("level", out JsonElement levelEl) && levelEl.ValueKind == JsonValueKind.String ? levelEl.GetString() : "city";
        HostId = msg.GetProperty("host").GetInt32();
        AutoProfession = msg.TryGetProperty("autoProfession", out JsonElement autoEl) && autoEl.ValueKind == JsonValueKind.True;
        if (msg.TryGetProperty("prof", out JsonElement profEl) && profEl.ValueKind == JsonValueKind.String)
            Professions.Current = profEl.GetString();
        Professions.CurrentLevel = Level;
        WelcomeJson = raw;

        _players.Clear();
        foreach (JsonElement p in msg.GetProperty("players").EnumerateArray())
            AddPlayer(p);

        Status = ConnectionStatus.Online;
        _joinPassword = "";
        _connectFailureMessage = "";
        StatusMessage?.Invoke($"Na sala {RoomCode}");
        Joined?.Invoke();
    }

    private void AddPlayer(JsonElement p)
    {
        var info = new NetPlayerInfo
        {
            Id = p.GetProperty("id").GetInt32(),
            Name = p.GetProperty("name").GetString() ?? "?",
            Color = ParseColor(p.GetProperty("color").GetString()),
            Profession = p.TryGetProperty("prof", out JsonElement prof) && prof.ValueKind == JsonValueKind.String ? prof.GetString() : Professions.Entregador,
        };
        _players[info.Id] = info;
    }

    // ---------------- Criar sala (HTTP) ----------------

    private void SendCreateRoomRequest()
    {
        Error err = _http.Request(
            $"{HttpBase(ServerUrl)}/api/rooms",
            new[] { "Content-Type: application/json" },
            HttpClient.Method.Post,
            JsonSerializer.Serialize(new { level = _pendingCreateLevel, autoProfession = _pendingAutoProfession, isPublic = _pendingIsPublic, protocol = _createProtocol }));
        if (err != Error.Ok)
            Fail($"Não foi possível contatar o servidor ({err}).");
    }

    private void OnCreateRoomCompleted(long result, long responseCode, string[] headers, byte[] body)
    {
        if (Status != ConnectionStatus.Connecting)
            return;

        if (result != (long)HttpRequest.Result.Success || responseCode != 200)
        {
            if (result == (long)HttpRequest.Result.Success && responseCode == 400 && _createProtocol == PreferredProtocolVersion)
            {
                _createProtocol = ProtocolVersion;
                SendCreateRoomRequest();
                return;
            }
            Fail(result != (long)HttpRequest.Result.Success
                ? "Servidor fora do ar ou endereço errado."
                : $"O servidor recusou ({responseCode}).");
            return;
        }

        try
        {
            using JsonDocument doc = JsonDocument.Parse(body);
            string code = doc.RootElement.GetProperty("code").GetString();
            int protocol = doc.RootElement.TryGetProperty("protocol", out JsonElement p)
                && p.TryGetInt32(out int parsed) && parsed is 7 or 8 ? parsed : ProtocolVersion;
            JoinRoom(ServerUrl, code, PlayerName, protocol: protocol);
        }
        catch (Exception)
        {
            Fail("Resposta inválida do servidor.");
        }
    }

    // ---------------- Utilitários ----------------

    private void Fail(string message)
    {
        ResetSession();
        StatusMessage?.Invoke(message);
        Disconnected?.Invoke(message);
    }

    private void ResetSession()
    {
        _debugAuthExpiresAt = 0;
        _joinPassword = "";
        _connectFailureMessage = "";
        ActiveProtocolVersion = ProtocolVersion;
        _socket = null;
        _lastState = WebSocketPeer.State.Closed;
        Status = ConnectionStatus.Offline;
        _players.Clear();
        MyId = 0;
        HostId = 0;
        WelcomeJson = "";
    }

    public static string NormalizeServer(string url)
    {
        url = (url ?? "").Trim().TrimEnd('/');
        if (!url.Contains("://"))
            url = (url.StartsWith("localhost") || url.StartsWith("127.") ? "http://" : "https://") + url;
        // No Windows "localhost" costuma resolver para IPv6 (::1), mas o wrangler dev só escuta em 127.0.0.1.
        url = url.Replace("://localhost", "://127.0.0.1");
        return url;
    }

    private static string HttpBase(string url) =>
        url.StartsWith("wss://") ? "https://" + url[6..] : url.StartsWith("ws://") ? "http://" + url[5..] : url;

    private static string WsBase(string url) =>
        url.StartsWith("https://") ? "wss://" + url[8..] : url.StartsWith("http://") ? "ws://" + url[7..] : url;

    public static Color ParseColor(string hex) =>
        string.IsNullOrEmpty(hex) ? Colors.White : Color.FromHtml(hex);
}
