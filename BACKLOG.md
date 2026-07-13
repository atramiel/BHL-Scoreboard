# Backlog

**Design rule:** All stats, profiles, and history are tracked at the **team** level (the Challonge participant). Refs never track individual goal scorers — the scoreboard only knows which side scored, and that's enough. Person-level stats belong to the separate stat-tracker app with a dedicated stats person.

**Master record:** The league website (Supabase + static site in `/website`) is the source of truth for teams, profiles, history, and results. The app pulls before events and pushes after games.

---

## In Testing (built, awaiting event-style testing — do not ship untested)

### Challonge Report Feedback
Game-over screen shows ✓ Reported / ✗ Failed with automatic retry and a manual Resubmit button. No more silent failures.

### Post-Game Score Edit & Resubmit
After game end, undo/score keys correct the result silently (no celebration), a desync warning appears, and Resubmit overwrites the Challonge result. Match link clears on reset/new match.

### Bug: Game Length Change Needed a Reset
Fixed: saving Settings applies the new game length immediately when no game is underway; a game in progress keeps its clock until reset.

### App → Website Result Push + Offline Bundle
Built: match-linked results post to the league site via `record_game` after the Challonge attempt (winner first, OT flag, match id); failures queue to `leagueQueue.json` and auto-flush on the next post or app launch; "Download League Data" in Settings saves `leagueBundle.json` (teams, games, champions, rivalries, aliases, events) for offline use. Status badge on the game-over screen. Settings: Event Name, League Site URL, Public Key, Admin Key. Known limits: post-game score edits don't update the site (Challonge only); championship flag always false until the championship toggle exists.

### Goal Celebration Moments
Built: full-screen "GOAL!" flash in the scoring team's color with their name, ~2.5 s fade, display-only (never blocks play, hidden for post-game corrections). Per-team horns: drop `<TeamName>.wav` into `Resources/Sounds/Horns/` (README in the folder); falls back to the default goal sound. LED goal effects were already wired.

### Final-Minute Drama Mode
Built: pulsing red screen edge when the game is tied or within one goal under 1:00. Stands down when the lead grows to 2+, returns when it closes; never during sudden death or after game over. Existing final-minute clock colors, heartbeats, and LED pulses continue underneath.

### Bug: Stream Deck next-match countdown was static
Fixed: the between-game countdown now re-sends state to the plugin every tick, so the Stream Deck display counts down live.

---

## Up Next (rough priority order)

### 4. Brighter, Louder Countdowns
The dark navy pulse over-corrected — too subdued for a loud rink. More visual intensity and more prominent audio.

### 5. Championship Game Treatment
- **Manual toggle** (side brackets have "finals" too — no auto-detection)
- Gold theme override, extended intro, confetti + trophy + "CHAMPIONS" at game end, dedicated LED effect

### 6. Attract Mode
- Triggered by the existing Between Game button — the between-game screen becomes a rotation (~15–20 s per panel)
- Panels: live bracket(s), Hall of Fame, "What is Bot Hockey?"/league history (from the website), event stats so far, upcoming matches, QR codes
- Panel set and order configurable

### 7. Live Bracket View
- Render the Challonge bracket (embeddable module/image); refresh when the between-game window opens
- Multiple brackets (main + 4th–8th) are separate Attract Mode panels
- Graceful fallback when offline/unconfigured

### 8. Discord Auto-Posting
- One webhook URL in settings; each post type toggleable
- Final scores at game end; "next up" on match select; optional hype pings (sudden death, championship); end-of-night recap
- Nothing posts in off-the-books modes

### 9. Event Stats & Awards Ceremony Screen
- Live superlatives during the night from the game log (top-scoring team, biggest blowout, OT thrillers)
- Closing podium view: Challonge standings + custom trophies entered on the website (Best Bot, Best Driver, new trophies)
- Attract Mode panel + dedicated ceremony screen

### 10. Schedule Pace Tracker
- Three inputs per event: target start, target end, planned match count
- Between-game screen shows drift ("on pace" / "~12 min behind"), estimate improves from actual turnaround times
- Ref-facing by default; optional public "estimated next match" in the attract rotation

### 11. Pre-Game Speech Button and Screen
Full-screen ceremony overlay (team names/logos or custom message), triggered before a game, dismissed by the operator.

### 12. Intermission Tracking / Visualization
Intermission timer/countdown on the main or between-game screen; possibly on the phone scoreboard.

---

## Fun Modes (off the books — no stats, no history, no Challonge/Discord)

### King of the Rink Mode
Winner stays on; crown icon + defense counter ("👑 Hammers — 5 straight"); quick challenger entry; dethroning celebration; optional challenger queue.

### Mystery Rule Game
Rules change **every 2 minutes of game time** via a big-screen roulette moment (sound/LED). Active rule stays displayed. App-enforced modifiers (double goals, 3-min penalties, sudden death from current score) + honor-system ones (displayed, ref-enforced). Editable modifier list.

---

## Reliability & Plumbing

### Crash-Recovery Snapshot
Snapshot live game state to disk every few seconds + on every event; on unclean exit offer "Resume game? HOME 3–2, 4:12 left" including the Challonge match link. Cleared on normal reset/end. Must never stutter the UI.

### Settings Backup & Export — Local and Cloud
One-file local export/import (settings, key bindings, LED mappings, credentials, theme) plus the same bundle stored on the league website so any laptop can pull it.

### Toggle for Local vs Railway Relay URL
One-click switch between development and event relay hosting.

### Integrate the League Site with the BHL WordPress Website
The official BHL website runs on WordPress; the league stats site (Vercel + Supabase) should eventually live there rather than as a separate destination. Options, cheapest first:
- **Subdomain**: point `league.` (or `stats.`) at the Vercel deployment and link it from the WordPress nav — zero code, keeps one canonical home
- **Embed**: iframe the roster/rankings/hall-of-fame pages into WordPress pages (the site is already self-contained; may want a "chromeless" query param that hides the nav)
- **Native**: a small WordPress plugin or theme snippet that reads the Supabase REST API directly (it's public-read) and renders teams/results in WordPress's own styling — most work, most seamless
Either way the Supabase database stays the single source of truth; nothing about the app integration changes.

### Design the Interface for 1080p TV
The scoreboard's primary display is a 1080p TV viewed from across a room. Do a sizing/layout pass with that as the design target: type scale, logo sizes, penalty timers, spacing — everything legible at couch-to-TV distance at 1920×1080. Consider a Viewbox-based layout (like the between-game screen) so it scales cleanly on other resolutions.

### Modernize the Settings Dialog
Grouped sections/tabs (Game Rules, Teams, Challonge, Website, Display, Sound, Lighting, Advanced); needs to absorb the championship toggle, relay toggle, horns, website credentials.

### Live Stat Tracker Web App
Separate web app on a second laptop/tablet for a dedicated stats person: goals, hits, blocks, substitutions — live or on replay. Independent of the phone-scoreboard relay. This is where person-level stats live.

---

## Shipped

- **League website** (bhl-scoreboard.vercel.app, July 2026): team self-registration via private edit links (auto-copied to clipboard), profiles with logo/photos/established/motto, per-bot roster cards (photo, weight, weapon, driver, built), global rankings, events with dates/venues/locations, Hall of Fame trophy case + live league records, custom + legacy awards, rivalries with stories, team aliases with chain resolution (renames + sub-teams like EVAC Maroon/Gold), uncounted-score handling (W/L without fake goals), bulk game import, admin page for everything
- **Historical data imported**: 13 RoboGames podiums (2007–2023, legacy era), Spartan Bell Slugfest 2024, Nexus Knockout 1–3, OC Maker Faire 2024, BHL at Open Sauce 2025, Norcal Robotics Expo 2025
- **Redesigned game start/end visuals, overtime fixes, background themes, halftime flow** (v1.6.x, earlier)
