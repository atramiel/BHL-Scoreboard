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

### Brighter, Louder Countdowns
Built: smooth color journey (midnight → electric blue → violet → teal) with a glowing 240pt number that pops each tick; final three seconds pulse red; per-second beeps with rising pitch and a blast at zero. Team logos also pulled from the league bundle and shown beside team names and on the goal flash.

### Championship Game Treatment
Built: 🏆 Championship checkbox in Settings (instant, self-clears on reset). Gold glow vignette during the game; "🏆 CHAMPIONS 🏆" in gold with continuous confetti rain at the buzzer; result posts to the league site with championship=true. Not done: extended intro and dedicated LED effect — fold into pre-game speech / LED config later.

### Bug: Today's Results Still Stale Right After a Game
Root cause: the bundle refresh added after posting a result runs in the background — if the between-game screen opened within a few seconds (the normal case), it lost the race and read the old bundle. Fixed: opening the between-game screen now refreshes the bundle itself before building the attract panels, instead of depending on a refresh triggered by the previous event.

### Attract Mode (v1)
Built: QR codes moved to a permanent bottom bar (always visible); a carousel of panels flows left-to-right above them (each panel enters left, shifts right next tick, cycles off), refreshed every 15 s. Weighted-random rotation: the three original panels (🏆 Trophy Case, Today's Results, "What is Bot Hockey?") appear 4x as often as the rest (🔥 Rivalry — one per curated rivalry with the story; Team Spotlight — one team at a time, its bio plus its own bot roster with per-bot photos; the separate aggregated Bots panel was folded into Spotlight). League Records panel was tried and dropped (felt dry next to the others). All data from the offline bundle; the app now auto-refreshes that bundle right after each successful league-site post so Today's Results reflects the game that just finished, not just whatever was there at the last manual Download League Data. Not yet: live bracket panel (see #7), panel configurability.

### Countdown Says GO! / GAME OVER Instead of Vanishing at Zero
Fixed: both countdown windows (game start, final 10 seconds) now hold on "GO!" / "GAME OVER" for a beat before closing, with a pop-in animation, instead of disappearing right as the number hits zero.

### Bug: Leftover Side-Swap Corrupted the NEXT Game's Challonge Report (critical)
Root cause: `IsReverse` (which the Challonge report un-swap math depends on) was never reset between games. If a ref swapped sides once (e.g. at halftime) and didn't swap back before the game ended, `IsReverse` stayed `true` into the next game — even though team names/scores had already been freshly assigned for the new match. The un-swap formula then compensated for a swap that didn't happen in the new game, silently reporting the wrong team's score to Challonge. Fixed: `IsReverse` now resets to `false` on game reset and on selecting a new Challonge match. Also fixed two adjacent bugs found in `SwapSides()` while tracing this: penalty key bindings and LED score-effects weren't actually swapping (a duplicate assignment was clobbering the correct one right after it was set).

### Halftime On/Off Toggle + Stops Flashing at Game End
Built: a "Halftime" checkbox in Settings turns the warning flash and "HALF NOW" reminder off entirely for games that don't need one. Also fixed: if halftime is never taken, the "HALF NOW" flash (which runs for the rest of the game until acknowledged) now always stops the instant the game ends, instead of potentially flashing behind the game-over/championship overlay.

### Live Stat Tracker (v3)
Built: `website/stats.html` — a full-screen, tablet-kiosk companion page for one or more stats keepers, separate from the ref-facing scoreboard (no site nav, no pinch-zoom, a Fullscreen button). Writes are gated by a **shared stats passcode** (`stats_key` in `league_secrets`, checked via `is_stats_key()`) rather than the league admin key, so multiple people can log stats without holding the key that can delete teams/events — the admin key still works too if used instead. The "Now on the Scoreboard" panel detects whatever match the app is currently running over the public relay feed and, since the relay has no event name, prompts for it explicitly (datalist of known events, not auto-guessed) before creating the session and jumping into lineup setup — one team at a time, 3 bots + drivers each. During play: on-ice bots show 6-at-a-glance with each bot's own identifying color (set per-bot on the team page) as a name badge; **tapping an on-ice bot logs a hit immediately, tapping a bench bot starts a sub** (no separate buttons/modals for either); goals still auto-pop a scorer/assist/own-goal picker off the relay's live score feed, with a manual fallback button. Every event is stamped with the **game clock** (e.g. "9:40"), not wall-clock time. A one-level **Undo Last** button reverses the most recent tap (including un-doing a sub). A **Delete All Stats** button on the sessions screen clears every session/event in one go, for testing. Scores and goal-detection are tracked **by team name, not relay position** — the app's mid-game Side Swap flips which physical "home"/"visitor" slot a team's score lives in (it swaps `HomeTeam`/`HomeScore` with `VisitorTeam`/`VisitorScore` together), so the tracker keys everything off the actual team name to survive a swap without misattributing a goal. Nothing is trusted automatically — post-game review confirms/edits/deletes each event before "Validate All," and now also has an **Add Event** form (team, event type, bot, related bot, driver, and a manually-typed game clock) for backfilling a goal/hit/assist missed live while reviewing footage afterward — the originally-specced replay/backfill mode, which existed as a design intent but hadn't actually been built until now. Two Supabase tables (`stat_sessions`, `game_events`), migrations 011–013 run; no app or relay changes. Not yet: public surfacing of validated stats anywhere on the site.

---

## Up Next (rough priority order)

### 7. Live Bracket View (scoped 2026-07-13 — full spec)
Shows the actual live Challonge bracket tree on the between-game screen, as its own panel(s) in the existing Attract Mode rotation — not a redraw of the bracket in the app's own graphics, but the real Challonge page rendered live.

**Confirmed embed mechanism**: Challonge has a public embed, no API key needed — `https://challonge.com/{slug}/module` as an iframe (confirmed via Challonge's own bracket module docs). This only works for **user-hosted tournaments**; Challonge's docs explicitly say it does **not** support organization-hosted tournaments. Since `ChallongeService.ExtractSlug()` already special-cases org-subdomain URLs (`org-slug` format for the API), BHL's bracket may well be org-hosted — **action item before building: check whether the real BHL bracket URL is a personal challonge.com/{slug} link or an org subdomain**. If org-hosted, the fallback is loading the full public tournament page (`https://{org}.challonge.com/{slug}`) instead of `/module` — uglier chrome (nav, comments), but confirmed to work for any hosting type.

**New dependency**: this needs an embedded browser control — WPF has no built-in HTML renderer. `Microsoft.Web.WebView2` is the standard choice, but it'd be the **first browser-engine dependency in an app that has to survive a live event without crashing** (per the standing rule: never commit without Alex testing first, and this is exactly the kind of change that needs real stress-testing before an event, not just a quick check). Needs the WebView2 Evergreen Runtime on the event laptop — verify it's present (usually is, ships with Windows 11 and Edge) before relying on it.

**Layout — brackets need full width, not a half-width column**: existing Attract Mode panels (Trophy Case, Rivalry, Team Spotlight, etc.) are text/photo content that rotates through two side-by-side half-width slots (`CurrentLeft`/`CurrentRight` in `BetweenGameViewModel`). A bracket tree is wide (many rounds horizontally) and won't read at half width — it needs its own **full-width single-panel rotation turn**, not squeezed alongside another panel. This is a real layout change, not just a new `AttractPanel` entry.

**Resource-conscious design**: one bracket panel per bracket (main + each configured secondary bracket) in the weighted rotation, but backed by a **single persistent `WebView2` control**, not one instance per bracket — spinning up N full browser environments simultaneously is unnecessary weight on a kiosk laptop that already runs the relay, Stream Deck bridge, and everything else for hours. Bind the control's `Source` to whichever bracket is currently the active rotation panel; WebView2 auto-navigates on `Source` change, which naturally refreshes the bracket **every time it rotates into view** — that alone satisfies "refresh on between-game open" and then some, no manual reload code needed.

**Multiple brackets**: the main bracket reuses the existing Challonge Settings URL (no new field). Secondary brackets (e.g. "4th–8th Place") are **display-only** — Challonge's module URL needs no API key for public viewing — so add a lightweight new Settings field: a simple line-based list (`Name = URL`, one per line), consistent with this app's existing preference for plain text config over building new list-editor UI.

**Graceful fallback** (both cases must never affect the real game):
- No bracket URL configured → that panel simply isn't added to the rotation weights (same pattern as panels today when the league bundle is missing).
- WebView2 navigation fails (offline, blocked, runtime missing, org-hosting mismatch) → catch `NavigationCompleted.IsSuccess`; on failure, mark that specific bracket "unavailable for the session" and exclude it from further rotation rather than retrying every ~15s cycle and risking a stuck/blank panel.

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

### Date-Aware Team Aliases (name reuse)
Team names get reused: the 2024–25 "Team Exile" became Team Blueshift, and a *different* team (formerly Hock Stuff) now competes as Team Exile. Resolved for now (2026-07-12) by baking the modern name into the old game rows — the cosmetic cost is those rows display "Team Blueshift" instead of the day-of name. If preserving day-of names becomes important, or a name gets reused again:
- Add `valid_from` / `valid_until` (nullable) to `team_aliases`; admin UI for the date range
- Stats pages resolve each game's team names using the game's `played_at` against the alias date range

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

---

## Shipped

- **League website** (bhl-scoreboard.vercel.app, July 2026): team self-registration via private edit links (auto-copied to clipboard), profiles with logo/photos/established/motto, per-bot roster cards (photo, weight, weapon, driver, built), global rankings, events with dates/venues/locations, Hall of Fame trophy case + live league records, custom + legacy awards, rivalries with stories, team aliases with chain resolution (renames + sub-teams like EVAC Maroon/Gold), uncounted-score handling (W/L without fake goals), bulk game import, admin page for everything
- **Historical data imported**: 13 RoboGames podiums (2007–2023, legacy era), Spartan Bell Slugfest 2024, Nexus Knockout 1–3, OC Maker Faire 2024, BHL at Open Sauce 2025, Norcal Robotics Expo 2025
- **Redesigned game start/end visuals, overtime fixes, background themes, halftime flow** (v1.6.x, earlier)
