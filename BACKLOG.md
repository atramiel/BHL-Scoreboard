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

---

## Up Next (rough priority order)

### 7. Live Bracket View
- Render the Challonge bracket (embeddable module/image); refresh when the between-game window opens
- Multiple brackets (main + 4th–8th) are separate Attract Mode panels
- Graceful fallback when offline/unconfigured

### 8. Live Stat Tracker (bumped up 2026-07-13 — full spec, rewritten 2026-07-13)
A touch-first companion web app for a dedicated stats keeper (iPad/phone) that records who actually scored, assisted, and hit — the ref-facing scoreboard only ever knows team totals, per the design rule at the top of this file. This is a **website feature, not an app feature**: no new C# code, no WPF changes, and no changes to the phone-scoreboard relay. It rides entirely on infrastructure that already exists.

**Why no relay/app changes are needed**
- `relay/server.js` already treats any WebSocket connection other than `/source` as a generic viewer and rebroadcasts the same score JSON to all of them — that's literally how the phone scoreboard works today. The stats page connects to that same public relay URL as one more viewer and watches `homeScore`/`visitorScore` for a change. Zero relay edits, zero risk to the live phone scoreboard, and the app team (Alex's WPF code) is untouched.
- Same Supabase project (`sspfdcadkjcoqngnonqr`), just two new tables — no new project, no new credentials.
- Reuses each team's existing `bot_roster` (name + photo) for the tap-to-select UI — teams already maintain this, nothing new for them to fill in.

**Data model** (two new tables)
- `stat_sessions`: id, event_name, home_team, visitor_team, status (`live` / `unconfirmed` / `validated`), created_at
- `game_events`: id, session_id, team_name, event_type (`goal` / `assist` / `hit` / `own_goal` / `sub` / `lineup_start`), bot_name, related_bot_name (assist target / hit target), driver_name, occurred_at, **validated boolean default false**
- Bot identity is team+bot name text, matching how `bot_roster` already works — no new bot ID scheme.

**Flow — one continuous build, not phased** (the relay realization above means live auto-detect adds no real risk or infrastructure over a manual button, so there's no reason to ship them as separate slices)

1. **Pre-game setup — one team at a time, not side by side**: stats keeper picks the event and the two teams (or a session is pre-created from the game the scoreboard is about to run). The screen then shows only Team A: full-roster photo tiles, tap the 3 bots "on the ice," confirm which driver is running each one this game (drivers rotate game to game — a per-game snapshot, not an edit to the team's profile). Only after Team A's lineup is confirmed does the screen advance to Team B and repeat the same single-team flow. Two teams are in the game at once, but lineup entry is always one team, full-screen, at a time — never a split/side-by-side picker.
2. **Live tracking**:
   - The page holds an open WebSocket to the public relay (same URL the phone scoreboard uses) and watches for `homeScore`/`visitorScore` to change independently. Both teams are being watched simultaneously throughout the game, but each individual goal event only ever prompts for **the one team whose score just went up** — big tap-to-select photo tiles of *that* scoring team's 3 on-ice bots. The other team's roster never appears on that prompt.
   - A **manual "Log Goal" button** always sits on screen too, as the fallback if the relay connection drops or lags — same picker, just operator-triggered instead of auto-triggered. Whichever path fires, the recording UI is identical.
   - Optional second tap for an assist, from the same team's remaining on-ice bots.
   - **Own Goal toggle**: flips the picker to the *conceding* team's on-ice bots instead — the goal still counts for the scoring team on the main board, but credit attaches to a bot on the other side.
   - If the stats keeper doesn't respond to a prompt, the goal still counts at the team level with no attribution — this layer never blocks or slows down the actual game.
   - **Substitutions**: swap an on-ice bot for a benched one any time; updates who's selectable for the rest of the game.
   - **Hits**: a standing "Log a Hit" button during play — pick the hitting bot (and optionally which bot got hit) in two taps.
3. **Post-game review & validation** — nothing recorded live is trusted automatically:
   - Every event starts `validated = false`; the session itself starts `unconfirmed`. Doesn't have to be reviewed by the same person who tapped it in live — could be a different reviewer re-watching footage.
   - A review screen lists every event in the session (photo, bot, team, type) with per-event confirm / edit (fix a mis-tap) / delete (clear error), plus a "Validate All" bulk action once it looks right.
   - Sessions show their status plainly (Unconfirmed / Validated) wherever they're listed.
   - Anywhere these stats eventually surface publicly (team pages, etc.), only validated sessions/events show — raw unconfirmed taps stay internal.
   - Replay/backfill mode: reopen a finished session afterward to add a missed assist or fix a mis-tap, same review screen.

**Design constraints**
- Big touch targets, minimal typing, works one-handed on a tablet.
- Best-effort and non-blocking — a dropped relay connection or a missed tap never affects the real scoreboard or Challonge/league reporting; the manual button and the "counts with no attribution" fallback exist specifically so this layer can never stall the game.

### 9. Discord Auto-Posting
- One webhook URL in settings; each post type toggleable
- Final scores at game end; "next up" on match select; optional hype pings (sudden death, championship); end-of-night recap
- Nothing posts in off-the-books modes

### 10. Event Stats & Awards Ceremony Screen
- Live superlatives during the night from the game log (top-scoring team, biggest blowout, OT thrillers)
- Closing podium view: Challonge standings + custom trophies entered on the website (Best Bot, Best Driver, new trophies)
- Attract Mode panel + dedicated ceremony screen

### 11. Schedule Pace Tracker
- Three inputs per event: target start, target end, planned match count
- Between-game screen shows drift ("on pace" / "~12 min behind"), estimate improves from actual turnaround times
- Ref-facing by default; optional public "estimated next match" in the attract rotation

### 12. Pre-Game Speech Button and Screen
Full-screen ceremony overlay (team names/logos or custom message), triggered before a game, dismissed by the operator.

### 13. Intermission Tracking / Visualization
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
