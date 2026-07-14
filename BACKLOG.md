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

### 7. Event Stats & Awards Ceremony Screen (scoped 2026-07-14 — full spec, re-added after Alex changed his mind on dropping it)
Two genuinely separate things bundled under one name: an **ongoing "live superlatives" panel** shown all night in Attract Mode, and a **one-time closing ceremony** after the championship game.

**Trigger for the ceremony — Alex's ask plus a refinement**: he wants it to appear automatically ~30 seconds after the championship buzzer/confetti screen. Straight auto-launch risks surprising the operator mid-photo-op or mid-speech, so the suggested refinement is to reuse the UX language the app already has for exactly this situation: the between-game screen's "Next Match In" countdown, which is visible, live-adjustable, and cancelable via the Stream Deck dial. Show a small "Awards Ceremony in 30s" indicator on the championship overlay itself, adjustable/cancelable the same way, defaulting to auto-launch if untouched — same trigger goal, but the operator isn't stuck if they need a few more seconds.

**Closing podium — what's actually knowable at that moment**:
- **1st and 2nd place** need no lookup at all — they're the winner and loser of the championship game that just ended, already sitting in `HomeTeam`/`VisitorTeam`/scores.
- **3rd place** is genuinely uncertain and needs verifying against the real Challonge API before assuming it works: participants carry a `final_rank` field (seen as `null` mid-bracket in earlier testing this session), which should populate once the bracket's last relevant matches complete — worth confirming against a real finished tournament rather than assuming, the same way the Swiss-vs-bracket assumption turned out wrong for Live Rankings. If it's not reliably available in time, fall back to letting the operator pick 3rd place manually in a quick prompt, or just omit it — 1st/2nd showing correctly matters far more than blocking on 3rd.
- **Custom trophies** (Best Bot, Best Driver, etc., from the existing `awards` table) almost certainly won't have same-night entries — that data entry has historically happened after the event, not during. Show them only if already entered for this `event_name`; the ceremony still works fine (just champion/runner-up) if the table's empty, which it usually will be on the night itself.

**Live superlatives (the other half — an ongoing panel, not part of the ceremony)**: reuses the exact same pattern already built for the website's Hall of Fame "League Records" section (biggest blowout, highest-scoring game, cardiac kings/OT wins) — except scoped to **today's games only**, computed from the same locally downloaded bundle already used for Today's Results and Live Rankings (no new data source). Rendered as one more Attract Mode panel in the existing weighted rotation, no new architecture needed.

**Architecture**: the ceremony screen is a new full-screen window following the same pattern as `BetweenGameWindow`/`StartingGameCountDownWindow` (Escape/dial-dismiss); the superlatives panel just slots into `BetweenGameViewModel`'s existing panel rotation like Live Rankings and Upcoming Matches already do.

### 8. Schedule Pace Tracker (scoped 2026-07-14 — full spec)
Three new Settings fields — target start time, target end time, planned match count — used to show schedule drift on the between-game screen ("On Pace" / "~12 min behind"), with the estimate sharpening as real turnaround times (game-end to next-game-start) come in during the event.

**Tracking**: hook into the same final-score convergence point already used for Discord/league posting to timestamp each game's end and increment a "matches completed today" counter; timestamp each match selection too, to get actual turnaround durations. A simple rolling average of the last several turnarounds projects the finish time: `now + (planned_count − completed_count) × avg_turnaround`, compared against target end to produce the drift readout.

**Display**: ref-facing by default (between-game screen, near the existing "Next Match In" countdown); optional public "estimated next match" time surfaced in the Attract rotation, same pattern as other panels.

**"On deck" Discord ping — doesn't actually need to wait for this feature**: the earlier plan tied this to the Pace Tracker's "who's up next" queue, but that queue already exists — `ChallongeService.FetchOpenMatchesAsync` (already used for match selection, and again for the Upcoming Matches Attract panel) already knows the next scheduled matchup without any pace-tracking math. The only real missing piece is a **per-team Discord mention** teams can self-manage (new field on their website profile, next to bot roster/motto) and a trigger moment (e.g. the instant a game ends, look at the next open match and ping those two teams). Worth doing independently of Pace Tracker if Alex wants it sooner — noting the decoupling here so it doesn't get blocked waiting on schedule math it doesn't actually need.

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
