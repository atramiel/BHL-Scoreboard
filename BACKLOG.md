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

### Live Standings Panel
Built: pivoted away from an initial WebView2-embedded Challonge bracket design once it became clear BHL runs Swiss tournaments, which Challonge renders as a standings table, not a bracket tree — full writeup kept below under Shipped-in-progress notes. `ChallongeService.FetchStandingsAsync` tallies win/loss/tie per team from completed matches (works for any tournament type) and `BetweenGameViewModel` turns it into a normal Attract Mode panel (rank, team, logo, record) that fits the existing half-width carousel exactly like Trophy Case — no embedded browser, no new layout. Config: main tournament reuses the existing Bracket URL/API Key; a new "Other Tournaments" Settings field (`Name = URL` per line) adds standings for extra brackets like a 4th–8th place tournament.

### Discord Auto-Posting
Built: `Services/DiscordService.cs` posts to a Discord webhook (`DiscordWebhookUrl` in Settings) — no OAuth, fire-and-forget, no retry queue (a missed hype post has no record-keeping stakes). **Final score** posts for every game, exhibition included — Discord is hype/community, not a permanent record, so it isn't gated on a selected match (unlike Challonge/league-site reporting). **Next Up** posts the moment a match is selected. **Sudden Death** hype pings are match-linked only, keeping "come watch this" urgency for real bracket games. Championship finals get a gold-colored embed instead of blue; final-score embeds use the winning team's public logo as a thumbnail (reusing the same Supabase-hosted URLs the league site already uses). A manual **Post Recap to Discord** button (Settings) lists today's completed games, since there's no "the event is over" signal in the app to trigger it automatically. Post types toggleable individually (Final Scores / Next Up / Hype Pings). Tagging specific teams ("on deck" pings) deliberately deferred — see Schedule Pace Tracker below.

### Bug: Final-10-Seconds Countdown Said "GAME OVER" Even Going Into Overtime
Root cause: the countdown's completion text was fixed at construction time — 10 seconds before the clock actually reached zero — so it had no way to know a tie was coming. Fixed: the tie check now runs live at the exact moment the countdown hits zero (via a callback), so it correctly shows "OVERTIME" for a tied finish and "GAME OVER" otherwise — including when a goal during the final 10 seconds itself creates or breaks the tie.

### Live Stat Tracker (v3)
Built: `website/stats.html` — a full-screen, tablet-kiosk companion page for one or more stats keepers, separate from the ref-facing scoreboard (no site nav, no pinch-zoom, a Fullscreen button). Writes are gated by a **shared stats passcode** (`stats_key` in `league_secrets`, checked via `is_stats_key()`) rather than the league admin key, so multiple people can log stats without holding the key that can delete teams/events — the admin key still works too if used instead. The "Now on the Scoreboard" panel detects whatever match the app is currently running over the public relay feed and, since the relay has no event name, prompts for it explicitly (datalist of known events, not auto-guessed) before creating the session and jumping into lineup setup — one team at a time, 3 bots + drivers each. During play: on-ice bots show 6-at-a-glance with each bot's own identifying color (set per-bot on the team page) as a name badge; **tapping an on-ice bot logs a hit immediately, tapping a bench bot starts a sub** (no separate buttons/modals for either); goals still auto-pop a scorer/assist/own-goal picker off the relay's live score feed, with a manual fallback button. Every event is stamped with the **game clock** (e.g. "9:40"), not wall-clock time. A one-level **Undo Last** button reverses the most recent tap (including un-doing a sub). A **Delete All Stats** button on the sessions screen clears every session/event in one go, for testing. Scores and goal-detection are tracked **by team name, not relay position** — the app's mid-game Side Swap flips which physical "home"/"visitor" slot a team's score lives in (it swaps `HomeTeam`/`HomeScore` with `VisitorTeam`/`VisitorScore` together), so the tracker keys everything off the actual team name to survive a swap without misattributing a goal. Nothing is trusted automatically — post-game review confirms/edits/deletes each event before "Validate All," and now also has an **Add Event** form (team, event type, bot, related bot, driver, and a manually-typed game clock) for backfilling a goal/hit/assist missed live while reviewing footage afterward — the originally-specced replay/backfill mode, which existed as a design intent but hadn't actually been built until now. Two Supabase tables (`stat_sessions`, `game_events`), migrations 011–013 run; no app or relay changes. Not yet: public surfacing of validated stats anywhere on the site.

---

## Up Next (rough priority order)

### 7. Awards Ceremony Screen (re-scoped 2026-07-14 — simplified to just the closing podium)
A one-time closing ceremony after the championship game. No separate ongoing "live superlatives" panel — Alex confirmed that's too unreliable to compute same-day, so this is purely the closing podium.

**Trigger**: the championship is always played last, so by the time it ends, this is genuinely the last thing that happens. Shows automatically ~30 seconds after the championship buzzer/confetti screen — reusing the app's existing "visible, dial-adjustable, cancelable" countdown language (same as the between-game "Next Match In" timer) rather than a silent hard cut, so the operator isn't stuck mid-photo-op if they need a few more seconds. This 30-second ceremony-launch countdown is unrelated to the Pace Tracker's break-timer below — two different countdowns for two different purposes.

**Closing podium**:
- **1st and 2nd place** need no lookup — they're the winner and loser of the championship game that just ended, already sitting in `HomeTeam`/`VisitorTeam`/scores.
- **3rd place** — confirmed auto-detect from Challonge, since the championship-always-last rule means 3rd is already decided by then (either a completed 3rd-place match, or a standings tie with no head-to-head decider). Implementation: look for a completed match Challonge itself marks as the 3rd-place/consolation match; if there isn't one, fall back to `FetchStandingsAsync`-style win-tally to find who's tied for 3rd. **Still needs verifying against a real finished BHL bracket before trusting it live** — exactly the kind of assumption (like Swiss-vs-elimination earlier this session) that's worth confirming with real data rather than guessing from docs. If detection comes up empty for some reason, don't block the ceremony — just show 1st/2nd and skip the 3rd-place slot.
- **Custom trophies** (Best Bot, Best Driver, etc., from the existing `awards` table) — confirmed likely empty on the night itself, since that data entry has historically happened after the event. Shown only if already entered for this `event_name`; ceremony works fine with just champion/runner-up if not.

**Architecture**: a new full-screen window following the same pattern as `BetweenGameWindow`/`StartingGameCountDownWindow` (Escape/dial-dismiss).

### 8. Schedule Pace Tracker (re-scoped 2026-07-14 — full spec, redesigned around how BHL actually runs a day)
Not a generic countdown — this models BHL's actual rhythm: matches run in round-sized batches (2–4 at a time, whatever's open in that round), and once a newly-available match needs a team that already played in the current batch, that's a natural round boundary where a 45–60 min break usually happens.

**Inputs** (3 Settings fields, matching how Alex actually plans a day):
- Event start time
- Recommended start time for the **last** game of the day (the real scheduling anchor — already implicitly includes whatever buffer he wants before the venue closes, no separate buffer field needed)
- Total planned match count for the day, **across all brackets** — main bracket plus any consolation/secondary brackets (e.g. 4th–8th place) running the same day on the same physical scoreboard, confirmed this should all count toward one shared pace budget

**Round-boundary detection**: rather than depending on Challonge's internal round/bracket bookkeeping (which would need to work identically across Swiss, elimination, and whatever the secondary bracket uses), detect it the same way Alex described it from experience: track which teams have played since the last recommended break; the moment a newly selectable match includes a team already in that "played this batch" set, that's the boundary — recommend a break right then, and reset the tracking set.

**Pace math** — continuous drift readout, always visible on the between-game screen (not just at breaks):
- `TargetPacePerMatch = (RecommendedLastGameStart − EventStart) ÷ TotalPlannedMatchCount`
- `ExpectedElapsedByNow = MatchesCompletedToday × TargetPacePerMatch`
- `Drift = (Now − EventStart) − ExpectedElapsedByNow` — positive = behind schedule, negative = ahead. Shows as "On Pace" / "~12 min behind" per the original ask.
- Every completed game counts toward `MatchesCompletedToday` regardless of bracket or exhibition status — it's real clock time either way, using the same universal "a game just ended" hook already wired for Discord's final-score post.

**Break-length recommendation** (only at detected round boundaries, per Alex's explicit ask that this be smarter than a fixed number): auto-fills the existing "Next Match In" countdown — still adjustable via the dial exactly like today — with `clamp(50min − Drift, 20min, 75min)`. Running behind shrinks the suggested break to help claw back time; running ahead lengthens it since there's slack to spend. The 50-minute base splits Alex's stated 45–60 min range; the 20/75 min floor and ceiling keep it from ever suggesting something absurd in either direction.

**Gentle over-selection warning** (added 2026-07-14): the dial never stops the operator from setting a longer break than suggested — he's always got final say, same as every other override in this app (Halftime, Championship, etc.) — but if he dials the timer *above* the suggested value, show a small, calm, non-blocking note under the countdown, e.g. "That's 15 min more than suggested — you'd be running ~27 min behind pace after this break." Updates live as the dial turns, using the same styling language as other soft notices in the app (not red/alarming); disappears entirely once the value is back at or below the suggestion. Only fires on *more* time than suggested — dialing to less needs no warning, since that only helps the pace.

**Display**: ref-facing by default (between-game screen); optional public "estimated next match" time in Attract rotation, unchanged from the original ask. **Also surfaced on the Stream Deck, sharing the Halftime button** (added 2026-07-14, confirmed with Alex): halftime has no meaning between games anyway, so while no game is active that button's display shows pace instead — "Ahead" / "On Pace" / "Behind ~12m" — then reverts to its normal halftime warning/HALF NOW behavior the instant a match starts. Reuses the same TCP bridge state-sync (`TcpBridgeService.SendStateAsync`) that already carries halftime/countdown state to the Stream Deck today; no new physical button needed.

**"On deck" Discord ping — doesn't actually need to wait for this feature**: `ChallongeService.FetchOpenMatchesAsync` (already used for match selection and the Upcoming Matches Attract panel) already knows the next scheduled matchup without any pace-tracking math. The only real missing piece is a **per-team Discord mention** teams can self-manage (new field on their website profile, next to bot roster/motto) and a trigger moment (the instant a game ends, look at the next open match and ping those two teams). Worth doing independently of Pace Tracker if Alex wants it sooner.

### 9. Pre-Game Speech Button and Screen
Full-screen ceremony overlay (team names/logos or custom message), triggered before a game, dismissed by the operator.

### 10. Intermission Tracking / Visualization
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
