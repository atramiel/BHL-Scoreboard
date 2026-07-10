// Shared Supabase client + helpers for all pages.
const sb = window.supabase.createClient(
  window.BHL_CONFIG.SUPABASE_URL,
  window.BHL_CONFIG.SUPABASE_ANON_KEY
);

function qs(name) {
  return new URLSearchParams(location.search).get(name);
}

function esc(s) {
  const d = document.createElement("div");
  d.textContent = s ?? "";
  return d.innerHTML;
}

function showNotice(el, ok, msg) {
  el.textContent = msg;
  el.className = "notice " + (ok ? "ok" : "err");
}

// Win-loss record for a team name from the games table rows.
function recordFor(games, name) {
  let w = 0, l = 0;
  for (const g of games) {
    if (g.team1_name === name) (g.team1_score > g.team2_score ? w++ : l++);
    else if (g.team2_name === name) (g.team2_score > g.team1_score ? w++ : l++);
  }
  return { w, l };
}
