'use strict';
const http = require('http');
const { WebSocketServer, WebSocket } = require('ws');

const PORT = process.env.PORT || 3000;

const viewers = new Set();
let lastState = null;

// ---------------------------------------------------------------------------
// HTTP server — serves scoreboard HTML for all GET requests
// ---------------------------------------------------------------------------
const server = http.createServer((req, res) => {
    if (req.url === '/health') {
        res.writeHead(200, { 'Content-Type': 'text/plain' });
        res.end('ok');
        return;
    }
    res.writeHead(200, { 'Content-Type': 'text/html; charset=utf-8' });
    res.end(SCOREBOARD_HTML);
});

// ---------------------------------------------------------------------------
// WebSocket upgrade handler
//   /source  →  WPF app (publisher)
//   anything else  →  browser (viewer)
// ---------------------------------------------------------------------------
const wss = new WebSocketServer({ noServer: true });

server.on('upgrade', (req, socket, head) => {
    wss.handleUpgrade(req, socket, head, (ws) => {
        if (req.url === '/source') {
            // WPF app connects here and sends state JSON
            ws.on('message', (data) => {
                lastState = data.toString();
                for (const v of viewers) {
                    if (v.readyState === WebSocket.OPEN) {
                        v.send(lastState);
                    }
                }
            });
            ws.on('close', () => console.log('Source disconnected'));
            console.log('Source connected');
        } else {
            // Browser viewer
            viewers.add(ws);
            if (lastState) ws.send(lastState);
            ws.on('close', () => viewers.delete(ws));
            console.log(`Viewer connected (${viewers.size} total)`);
        }
    });
});

server.listen(PORT, () => console.log(`BHL Scoreboard Relay listening on :${PORT}`));

// ---------------------------------------------------------------------------
// Embedded scoreboard page — identical UX to the local LAN page
// ---------------------------------------------------------------------------
const SCOREBOARD_HTML = `<!DOCTYPE html>
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

      function setPenalty(boxId, timeId, time, active) {
        const box = $(boxId);
        $(timeId).textContent = active ? time : '—';
        box.classList.toggle('active', active);
      }
      setPenalty('hp1', 'hp1-time', s.homePenaltyOne,    s.activeHomePenaltyOne);
      setPenalty('hp2', 'hp2-time', s.homePenaltyTwo,    s.activeHomePenaltyTwo);
      setPenalty('vp1', 'vp1-time', s.visitorPenaltyOne, s.activeVisitorPenaltyOne);
      setPenalty('vp2', 'vp2-time', s.visitorPenaltyTwo, s.activeVisitorPenaltyTwo);
    }

    let ws, reconnectDelay = 1000;

    function connect() {
      const proto = location.protocol === 'https:' ? 'wss:' : 'ws:';
      ws = new WebSocket(proto + '//' + location.host + '/');

      ws.onopen = () => {
        $('status-bar').textContent = 'Connected';
        $('status-bar').className = 'connected';
        reconnectDelay = 1000;
      };

      ws.onmessage = e => {
        try { applyState(JSON.parse(e.data)); } catch {}
      };

      ws.onclose = ws.onerror = () => {
        $('status-bar').textContent = 'Reconnecting in ' + (reconnectDelay / 1000) + 's…';
        $('status-bar').className = '';
        setTimeout(connect, reconnectDelay);
        reconnectDelay = Math.min(reconnectDelay * 2, 15000);
      };
    }

    connect();
  </script>
</body>
</html>`;
