using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace Scoreboard.Services;

/// <summary>
/// Lightweight HTTP + WebSocket server using HttpListener (no Kestrel / ASP.NET conflicts with WPF).
/// Phones on the same LAN connect to http://&lt;pc-ip&gt;:5000/
/// State is pushed to all clients via WebSocket on every game update.
/// </summary>
public class WebBroadcastService : IDisposable
{
    public const int Port = 5000;
    public const string FirewallRuleName = "Scoreboard Web";

    /// <summary>True if the firewall rule is missing and could not be added automatically.</summary>
    public bool NeedsFirewallSetup { get; private set; }

    private HttpListener? _listener;
    private readonly CancellationTokenSource _cts = new();
    private readonly ConcurrentDictionary<string, WebSocket> _clients = new();
    private readonly SemaphoreSlim _broadcastLock = new(1, 1);
    private string _lastStateJson = "{}";
    private bool _disposed;

    public WebBroadcastService()
    {
        EnsureFirewallRule();
        EnsureUrlReservation();

        // Try LAN-accessible first, fall back to localhost-only
        foreach (var prefix in new[] { $"http://+:{Port}/", $"http://localhost:{Port}/" })
        {
            try
            {
                var listener = new HttpListener();
                listener.Prefixes.Add(prefix);
                listener.Start();
                _listener = listener;
                break;
            }
            catch { }
        }

        if (_listener == null) return; // Web server unavailable — app still runs fine

        _ = AcceptLoopAsync(_cts.Token);
    }

    private void EnsureFirewallRule()
    {
        // Check if the rule already exists
        var check = RunNetsh($"advfirewall firewall show rule name=\"{FirewallRuleName}\"");
        if (check.Contains(FirewallRuleName)) return; // Already set up

        // Try to add it (succeeds if app is running as admin)
        var add = RunNetsh($"advfirewall firewall add rule name=\"{FirewallRuleName}\" dir=in action=allow protocol=TCP localport={Port}");
        if (!add.Contains("Ok."))
            NeedsFirewallSetup = true; // Couldn't add — caller should warn the user
    }

    private void EnsureUrlReservation()
    {
        var url = $"http://+:{Port}/";

        // Already reserved — nothing to do
        var check = RunNetsh($"http show urlacl url={url}");
        if (check.Contains($"+:{Port}")) return;

        // Try to add the reservation via a UAC-elevated netsh process
        try
        {
            var p = Process.Start(new ProcessStartInfo("netsh", $"http add urlacl url={url} user=Everyone")
            {
                UseShellExecute = true,
                Verb = "runas"
            });
            p?.WaitForExit();
        }
        catch { }
        // Whether it succeeded or not, the binding attempt below will determine what's available
    }

    private static string RunNetsh(string args)
    {
        try
        {
            var p = Process.Start(new ProcessStartInfo("netsh", args)
            {
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                RedirectStandardOutput = true,
                UseShellExecute = false
            });
            var output = p?.StandardOutput.ReadToEnd() ?? "";
            p?.WaitForExit();
            return output;
        }
        catch { return ""; }
    }

    private async Task AcceptLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && _listener != null)
        {
            try
            {
                var ctx = await _listener.GetContextAsync();
                _ = HandleContextAsync(ctx, ct);
            }
            catch (ObjectDisposedException) { break; }
            catch (HttpListenerException) { break; }
            catch { }
        }
    }

    private async Task HandleContextAsync(HttpListenerContext ctx, CancellationToken ct)
    {
        if (ctx.Request.IsWebSocketRequest)
        {
            WebSocketContext? wsCtx = null;
            try { wsCtx = await ctx.AcceptWebSocketAsync(null); }
            catch { ctx.Response.StatusCode = 500; ctx.Response.Close(); return; }

            var ws = wsCtx.WebSocket;
            var id = Guid.NewGuid().ToString();
            _clients[id] = ws;

            // Send current state immediately on connect
            try { await SendToAsync(ws, _lastStateJson); } catch { }

            // Keep alive until client disconnects
            var buf = new byte[64];
            while (ws.State == WebSocketState.Open && !ct.IsCancellationRequested)
            {
                try
                {
                    var result = await ws.ReceiveAsync(buf, ct);
                    if (result.MessageType == WebSocketMessageType.Close) break;
                }
                catch { break; }
            }

            _clients.TryRemove(id, out _);
            try
            {
                if (ws.State == WebSocketState.Open)
                    await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, null, CancellationToken.None);
            }
            catch { }
            ws.Dispose();
        }
        else
        {
            // Serve HTML page for any non-WS GET request
            var html = Encoding.UTF8.GetBytes(ScoreboardHtml);
            ctx.Response.ContentType = "text/html; charset=utf-8";
            ctx.Response.ContentLength64 = html.Length;
            try
            {
                await ctx.Response.OutputStream.WriteAsync(html, ct);
                ctx.Response.Close();
            }
            catch { }
        }
    }

    public void BroadcastState(
        string homeTeam, string visitorTeam,
        int homeScore, int visitorScore,
        string clock, bool isRunning, bool gameDone, bool isSuddenDeath,
        string homePenaltyOne, string homePenaltyTwo,
        string visitorPenaltyOne, string visitorPenaltyTwo,
        bool activeHomePenaltyOne, bool activeHomePenaltyTwo,
        bool activeVisitorPenaltyOne, bool activeVisitorPenaltyTwo)
    {
        _lastStateJson = JsonSerializer.Serialize(new
        {
            homeTeam,
            visitorTeam,
            homeScore,
            visitorScore,
            clock,
            isRunning,
            gameDone,
            isSuddenDeath,
            homePenaltyOne,
            homePenaltyTwo,
            visitorPenaltyOne,
            visitorPenaltyTwo,
            activeHomePenaltyOne,
            activeHomePenaltyTwo,
            activeVisitorPenaltyOne,
            activeVisitorPenaltyTwo
        });

        _ = BroadcastAsync(_lastStateJson);
    }

    private async Task BroadcastAsync(string json)
    {
        if (_clients.IsEmpty) return;

        await _broadcastLock.WaitAsync();
        try
        {
            var dead = new List<string>();
            foreach (var (id, ws) in _clients)
            {
                if (ws.State != WebSocketState.Open) { dead.Add(id); continue; }
                try { await SendToAsync(ws, json); }
                catch { dead.Add(id); }
            }
            foreach (var id in dead) _clients.TryRemove(id, out _);
        }
        finally { _broadcastLock.Release(); }
    }

    private static async Task SendToAsync(WebSocket ws, string json)
    {
        var bytes = Encoding.UTF8.GetBytes(json);
        await ws.SendAsync(bytes, WebSocketMessageType.Text, endOfMessage: true, CancellationToken.None);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _cts.Cancel();
        foreach (var ws in _clients.Values)
            try { ws.Dispose(); } catch { }
        _clients.Clear();
        try { _listener?.Stop(); } catch { }
        try { _listener?.Close(); } catch { }
    }

    // ---------------------------------------------------------------------------
    // Self-contained scoreboard HTML — no external dependencies
    // ---------------------------------------------------------------------------
    private const string ScoreboardHtml = """
        <!DOCTYPE html>
        <html lang="en">
        <head>
        <meta charset="UTF-8"/>
        <meta name="viewport" content="width=device-width, initial-scale=1.0, maximum-scale=1.0"/>
        <title>BHL Scoreboard</title>
        <style>
          *, *::before, *::after { box-sizing: border-box; margin: 0; padding: 0; }

          body {
            background: #0a0a0a;
            color: #fff;
            font-family: 'Segoe UI', Arial, sans-serif;
            display: flex;
            flex-direction: column;
            align-items: center;
            justify-content: center;
            min-height: 100dvh;
            padding: 12px;
            user-select: none;
          }

          #status-bar {
            font-size: 0.75rem;
            color: #555;
            margin-bottom: 10px;
            letter-spacing: 0.05em;
          }
          #status-bar.connected { color: #4caf50; }

          #game-status {
            font-size: 1rem;
            font-weight: 700;
            letter-spacing: 0.15em;
            text-transform: uppercase;
            height: 1.4em;
            margin-bottom: 8px;
          }
          #game-status.running   { color: #4caf50; }
          #game-status.paused    { color: #ff9800; }
          #game-status.done      { color: #f44336; }
          #game-status.sudden    { color: #ff5722; animation: pulse 1s ease-in-out infinite; }

          @keyframes pulse {
            0%,100% { opacity: 1; } 50% { opacity: 0.4; }
          }

          #clock {
            font-size: clamp(3.5rem, 18vw, 7rem);
            font-weight: 800;
            font-variant-numeric: tabular-nums;
            letter-spacing: 0.05em;
            line-height: 1;
            margin-bottom: 18px;
          }
          #clock.warning  { color: #ff9800; }
          #clock.critical { color: #f44336; }

          .teams {
            display: flex;
            gap: 16px;
            width: 100%;
            max-width: 520px;
          }

          .team-card {
            flex: 1;
            background: #161616;
            border: 1px solid #2a2a2a;
            border-radius: 12px;
            padding: 16px 12px 12px;
            display: flex;
            flex-direction: column;
            align-items: center;
            gap: 6px;
            transition: border-color 0.2s;
          }
          .team-card.highlight {
            border-color: #ffeb3b;
            box-shadow: 0 0 18px rgba(255,235,59,0.25);
          }

          .team-name {
            font-size: clamp(0.85rem, 4vw, 1.1rem);
            font-weight: 700;
            letter-spacing: 0.08em;
            text-transform: uppercase;
            text-align: center;
            color: #ccc;
          }

          .team-score {
            font-size: clamp(4rem, 20vw, 6.5rem);
            font-weight: 900;
            font-variant-numeric: tabular-nums;
            line-height: 1;
          }

          .penalties {
            display: flex;
            gap: 6px;
            width: 100%;
            margin-top: 4px;
          }

          .penalty-box {
            flex: 1;
            background: #1f1f1f;
            border: 1px solid #333;
            border-radius: 6px;
            padding: 4px 6px;
            text-align: center;
            font-size: clamp(0.7rem, 3vw, 0.85rem);
            font-variant-numeric: tabular-nums;
          }
          .penalty-box.active {
            background: #3a1a00;
            border-color: #ff6f00;
            color: #ffb74d;
          }
          .penalty-label {
            font-size: 0.6rem;
            letter-spacing: 0.08em;
            color: #666;
            text-transform: uppercase;
          }
          .penalty-box.active .penalty-label { color: #ff8f00; }
        </style>
        </head>
        <body>
          <div id="status-bar">Connecting…</div>
          <div id="game-status"></div>
          <div id="clock">--:--</div>

          <div class="teams">
            <div class="team-card" id="home-card">
              <div class="team-name" id="home-name">HOME</div>
              <div class="team-score" id="home-score">0</div>
              <div class="penalties">
                <div class="penalty-box" id="hp1">
                  <div class="penalty-label">PEN 1</div>
                  <div id="hp1-time">—</div>
                </div>
                <div class="penalty-box" id="hp2">
                  <div class="penalty-label">PEN 2</div>
                  <div id="hp2-time">—</div>
                </div>
              </div>
            </div>

            <div class="team-card" id="visitor-card">
              <div class="team-name" id="visitor-name">VISITOR</div>
              <div class="team-score" id="visitor-score">0</div>
              <div class="penalties">
                <div class="penalty-box" id="vp1">
                  <div class="penalty-label">PEN 1</div>
                  <div id="vp1-time">—</div>
                </div>
                <div class="penalty-box" id="vp2">
                  <div class="penalty-label">PEN 2</div>
                  <div id="vp2-time">—</div>
                </div>
              </div>
            </div>
          </div>

          <script>
            const $ = id => document.getElementById(id);

            function applyState(s) {
              $('home-name').textContent    = s.homeTeam;
              $('visitor-name').textContent = s.visitorTeam;
              $('home-score').textContent    = s.homeScore;
              $('visitor-score').textContent = s.visitorScore;

              const clock = $('clock');
              clock.textContent = s.clock;
              const [m, sec] = s.clock.split(':').map(Number);
              const totalSec = m * 60 + sec;
              clock.className = totalSec <= 10 ? 'critical' : totalSec <= 30 ? 'warning' : '';

              const gs = $('game-status');
              if (s.gameDone) {
                gs.textContent = 'GAME OVER';
                gs.className = 'done';
              } else if (s.isSuddenDeath) {
                gs.textContent = 'SUDDEN DEATH';
                gs.className = 'sudden';
              } else if (s.isRunning) {
                gs.textContent = 'LIVE';
                gs.className = 'running';
              } else {
                gs.textContent = 'PAUSED';
                gs.className = 'paused';
              }

              setPenalty('hp1', 'hp1-time', s.activeHomePenaltyOne,    s.homePenaltyOne);
              setPenalty('hp2', 'hp2-time', s.activeHomePenaltyTwo,    s.homePenaltyTwo);
              setPenalty('vp1', 'vp1-time', s.activeVisitorPenaltyOne, s.visitorPenaltyOne);
              setPenalty('vp2', 'vp2-time', s.activeVisitorPenaltyTwo, s.visitorPenaltyTwo);
            }

            function setPenalty(boxId, timeId, active, time) {
              const box = $(boxId);
              $(timeId).textContent = active ? time : '—';
              box.classList.toggle('active', active);
            }

            let ws, reconnectDelay = 1000;

            function connect() {
              const host = location.host;
              ws = new WebSocket(`ws://${host}/`);

              ws.onopen = () => {
                $('status-bar').textContent = 'Connected';
                $('status-bar').className = 'connected';
                reconnectDelay = 1000;
              };

              ws.onmessage = e => {
                try { applyState(JSON.parse(e.data)); } catch {}
              };

              ws.onclose = ws.onerror = () => {
                $('status-bar').textContent = `Reconnecting in ${reconnectDelay / 1000}s…`;
                $('status-bar').className = '';
                setTimeout(connect, reconnectDelay);
                reconnectDelay = Math.min(reconnectDelay * 2, 15000);
              };
            }

            connect();
          </script>
        </body>
        </html>
        """;
}
