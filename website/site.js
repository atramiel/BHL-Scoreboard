// Shared Supabase client + helpers for all pages.
const sb = window.supabase.createClient(
  window.BHL_CONFIG.SUPABASE_URL,
  window.BHL_CONFIG.SUPABASE_ANON_KEY
);

// Consistent US date formatting regardless of browser locale, e.g. "Apr 4, 2026".
function fmtDate(value) {
  const d = typeof value === "string" && /^\d{4}-\d{2}-\d{2}$/.test(value)
    ? new Date(value + "T00:00:00") : new Date(value);
  return d.toLocaleDateString("en-US", { month: "short", day: "numeric", year: "numeric" });
}

// "Jun 15 – 17, 2007 · Fort Mason Center · San Francisco, CA" from an events row.
function eventMeta(ev) {
  if (!ev) return "";
  const parts = [];
  if (ev.event_date)
    parts.push(ev.end_date && ev.end_date !== ev.event_date
      ? `${fmtDate(ev.event_date)} – ${fmtDate(ev.end_date)}`
      : fmtDate(ev.event_date));
  if (ev.venue) parts.push(ev.venue);
  const cityState = [ev.city, ev.state].filter(Boolean).join(", ");
  if (cityState) parts.push(cityState);
  return parts.join(" · ");
}

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

// Alias resolver: maps any historical/sub-team name to the canonical team name.
// Usage: const canon = await loadAliasMap();  canon("EVAC A") -> "Team EVAC"
async function loadAliasMap() {
  const { data } = await sb.from("team_aliases").select("*");
  const m = new Map();
  for (const a of data ?? []) m.set(a.alias.toLowerCase().trim(), a.canonical);
  return (name) => m.get((name ?? "").toLowerCase().trim()) ?? name;
}

// Win-loss record for a team from games rows, resolving aliases when given.
function recordFor(games, name, canon = (x) => x) {
  const me = canon(name);
  let w = 0, l = 0;
  for (const g of games) {
    if (canon(g.team1_name) === me) (g.team1_score > g.team2_score ? w++ : l++);
    else if (canon(g.team2_name) === me) (g.team2_score > g.team1_score ? w++ : l++);
  }
  return { w, l };
}

// Score cell text — placeholder scores (goals not tracked) show as a dash.
function scoreText(g) {
  return g.scores_counted === false ? "—" : `${g.team1_score}–${g.team2_score}`;
}

// Matchup cell: the winner is always visible, even when goals weren't tracked.
// "Magic Smoke def. Hockeymaniacs" with the winner bolded.
function matchupHtml(g) {
  const t1Won = g.team1_score > g.team2_score;
  const winner = t1Won ? g.team1_name : g.team2_name;
  const loser = t1Won ? g.team2_name : g.team1_name;
  return `<strong>${esc(winner)}</strong> def. ${esc(loser)}`;
}
