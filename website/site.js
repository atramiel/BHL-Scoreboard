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

// Upload a file to the team-media bucket; returns its public URL.
async function uploadMedia(file, path) {
  const { error } = await sb.storage.from("team-media").upload(path, file);
  if (error) throw error;
  return sb.storage.from("team-media").getPublicUrl(path).data.publicUrl;
}

// Logo <img> (or lettered placeholder) for a team card.
function logoHtml(team, cls = "team-logo") {
  if (team.logo_url)
    return `<img class="${cls}" src="${esc(team.logo_url)}" alt="${esc(team.name)} logo">`;
  const letter = (team.name || "?").trim()[0]?.toUpperCase() ?? "?";
  return `<div class="${cls} placeholder">${esc(letter)}</div>`;
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
