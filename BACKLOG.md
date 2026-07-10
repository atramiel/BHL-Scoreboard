# Backlog

**Design rule:** All stats, profiles, and history are tracked at the **team** level (the Challonge participant). Refs never track individual goal scorers — the scoreboard only knows which side scored, and that's enough.

## Planned Features

### Goal Celebration Moments
Make every goal feel like an event, not just a number change.
- Brief full-screen "GOAL" flash/animation on the scoring team's side
- Per-team goal horn: `Resources/Sounds/Horns/<TeamName>.wav`, auto-matched by name when a Challonge match is selected, falling back to a default horn. Teams "pick" their horn by handing the operator an audio file. Later migrates to a field on team profiles.
- Synced LED lighting effect via the existing VortxEngine hooks
- Short enough that it never delays play resuming (~2–3 seconds)

### Final-Minute Drama Mode
When the game is close and the clock crosses 1:00, the scoreboard makes the room look up.
- Triggers **only in close games**: tied or 1-goal lead when the clock hits 1:00
- Clock turns red and pulses; background/accent colors shift for the final minute
- Low heartbeat/tension sound loop (respects the sound-enabled setting)
- If a goal makes it a 2+ goal game mid-minute, drama mode stands down; if the gap closes again, it comes back
- Escalates in the final 10 seconds, blending into the existing end-of-game countdown
- LED effect via the existing lighting hooks

### Live Stat Tracker Web App (separate from the relay)
A standalone web app running on a second laptop or tablet for a dedicated stats person — not the ref — to track detailed match stats.
- Track events live during a match: goals, hits, blocks, substitutions, etc.
- Replay mode: enter/correct stats after the fact while reviewing a recorded match
- Runs as a website on another device; independent of the existing phone-scoreboard relay
- This is where person-level stats live — the main scoreboard stays team-only per the design rule

### Championship Game Treatment
When the final is on the table, the scoreboard should dress for the occasion — but only when the operator says so.
- **Manual on/off toggle** — no automatic detection, since events run side brackets (4th–8th place) whose "finals" aren't the championship
- Selectable option (settings or a quick toggle) to mark the current game as the championship
- Gold-accented theme override for the duration of the game
- Extended intro moment before the start countdown
- Game over screen upgrades: confetti animation, trophy graphic, "CHAMPIONS" instead of "GAME OVER"
- Dedicated LED effect for the championship win

### Modernize the Settings Dialog
The configuration window has outgrown its simple grid — give it a proper redesign.
- Reorganize into grouped sections/tabs (Game Rules, Teams, Challonge, Display, Sound, Lighting, Advanced)
- Modern styling consistent with the scoreboard's visual language
- Room to grow: upcoming options (championship toggle, relay toggle, theme, horns) need a sane home

### Live Bracket View
Show the actual tournament picture between games, not just "next up."
- Render the live Challonge bracket (Challonge provides an embeddable bracket module/image per tournament)
- Refresh whenever the between-game window opens — same moment matches are fetched today
- Multiple simultaneous brackets (main + 4th–8th place) are handled by Attract Mode's screen rotation: each bracket is one panel in the cycle
- Falls back gracefully to the current between-game layout when no bracket is configured or the network is down

### Team Profiles & Career Stats
Every team in the league gets an identity the scoreboard knows — and teams enter it themselves.
- **Team registration website**: before the event, teams fill in their own profile on a simple site (could live alongside the Railway relay hosting)
- Profile fields: team name, driver names, bot names, special features, home town, logo/photo, goal horn
- Career stats accumulated automatically from the game history log: all-time record, goals for/against, overtime record, championships
- Pre-game "tale of the tape" display when a match is selected: both teams side by side — all-time records, drivers, bots, special features, home towns, and head-to-head record
- **Name link/merge admin**: match profiles to Challonge participant names, with a way to link or merge when a team registers under a slightly different name
- Teams without a profile get a bare auto-created one the first time they appear in a match
- Head-to-head detail on the tale of the tape: all-time record between the two teams, last meeting's score, current streak
- **Rivalries, curated on the website**: mark special rivalry matchups and write the story of *why* it's a rivalry; when those teams meet, the pre-game card flags it as a RIVALRY GAME and shows the story alongside the head-to-head numbers

### Hall of Fame Screen
The league's living trophy room, browsable on the big screen.
- Past tournament champions, event by event — auto-recorded when a championship-toggled game ends, plus **editable on the league website** so the deep pre-app history gets seeded in and corrections are easy
- Records that stand: biggest blowout, longest win streak, most championships, most OT wins, fastest goal (once the stat tracker exists)
- Legendary bots/teams get a featured card
- **"What is Bot Hockey?" + league history screens**: a screen or two telling the story of bot hockey and the league, maintained on the website, aimed at first-time spectators
- Hall of Fame and the history/explainer screens are all panels in Attract Mode's rotation, and manually openable any time
- Data comes from the game history log + team profiles + website edits, so it builds itself over time

### Event Stats & Awards Screen
The night's story in numbers, ready any time and starring at the end.
- Live event superlatives pulled from the game history log: top-scoring team, biggest blowout, most OT thrillers, longest game, fastest goal (once the stat tracker feeds in)
- Awards/podium view at event end: champion, runner-up, third place from Challonge final standings, plus the night's superlatives as "awards" (Most Goals, Cardiac Kings for OT wins, etc.)
- **Custom awards entered on the website** day-of: Best Bot, Best Driver, judges' picks, and the new trophies being added — shown alongside the auto-computed awards in the ceremony view
- Doubles as an Attract Mode panel during the event, and a dedicated closing-ceremony screen at the end

### King of the Rink Mode
Winner-stays-on mode for casual nights and post-tournament hangs. Just for fun — **completely off the books**.
- Toggleable mode, fully separate from tournament play — no Challonge, no game history log, no career stats
- Winner keeps the rink; a crown icon and defense counter appear next to their name ("👑 Hammers — 5 straight")
- Challenger's name entered quickly (or picked from known team profiles)
- Dethroning moment gets a celebration: crown transfers with a flourish, LED effect
- Optional on-screen challenger queue so people know who's up next

### Mystery Rule Game
Party mode: the rules change every 2 minutes, all game long.
- **Every 2 minutes of game time**, a new random modifier spins in — a brief big-screen roulette moment with sound/LED so the whole room catches the change
- The active rule stays displayed prominently during play so nobody forgets what's in effect
- Two kinds of modifiers: app-enforced (goals worth double, 3-minute penalties, sudden death from current score) and honor-system (displayed big, ref-enforced — e.g. reverse driving only)
- Modifier list is editable/toggleable so house rules can evolve
- Off the books like King of the Rink — casual mode only

### Attract Mode
When no game is running, the big screen works the room like an arcade cabinet.
- **Triggered by the ref with the existing Between Game button** (keyboard `B` or Stream Deck) — the between-game screen becomes the rotation
- Rotates through panels on a timer (~15–20 seconds each): live bracket(s) — main and 4th–8th place, Hall of Fame, "What is Bot Hockey?" / league history, event stats so far, upcoming matches, and the QR codes (phone scoreboard, bracket, league info)
- Pressing Between Game again (or starting a match) exits back to normal operation, same as today
- Panel set and order configurable — turn off what doesn't apply to a given event

### Discord Auto-Posting
The league Discord finds out the moment it happens.
- Post via a Discord webhook (one URL in settings — no bot account needed)
- Final scores posted automatically at game end: teams, score, OT flag
- "Next up" announcements when a match is selected
- Optional hype pings: sudden death starting, championship game starting
- End-of-night recap: champion, podium, and the night's superlatives from the awards screen
- Each post type individually toggleable, and nothing posts in off-the-books modes (King of the Rink, Mystery Rule Game)

### Schedule Pace Tracker
Every tournament runs late — this makes it visible before it snowballs.
- Setup is just three numbers per event: **target start time, target end time, planned number of matches** (every event is different, but these always exist)
- From matches completed vs. clock time elapsed, show drift on the between-game screen: "on pace" / "running ~12 min behind" / "ahead — room for a longer intermission"
- Estimate improves as the day goes, using the actual average match turnaround so far
- Ref-facing by default; optional public "estimated next match" time in the attract rotation for spectators wandering off for food

### Challonge Report Feedback (no more silent failures)
Today a failed score report vanishes without a trace — the app swallows the error.
- After every game-end report, show the result on screen: a small "✓ Reported to Challonge" confirmation or a loud "✗ Report failed — enter manually" warning
- Automatic retry (a couple of attempts with a short delay) before declaring failure
- **Manual resend only** after that — a "resend last result" action, with a persistent visible badge until the failure is resolved, so the ref always knows exactly what state the bracket is in
- Failure warning must be impossible to miss but must not block the game-over flow

### Game History Log & Website Sync
Every result recorded the moment a game ends — with the **league website as the master record** and the app pulling and pushing.
- Recorded per game: both team names, final score, OT flag, timestamp, game duration, Challonge match ID, whether the Challonge report succeeded, championship flag
- App writes each result to a local log first (durable, crash-safe, easy to back up), then pushes to the website when connected — nothing is ever lost offline
- **Pre-event download**: one button to pull the latest profiles, career stats, head-to-head records, rivalries, Hall of Fame, and history screens onto the laptop, so the entire event runs fine with no internet at the venue; results queue locally and push up afterward
- Local log doubles as the Challonge audit trail: if the bracket is ever wrong, the log settles it
- Feeds team career stats, tale of the tape, Hall of Fame, event awards, and milestone alerts
- Off-the-books modes (King of the Rink, Mystery Rule Game) don't write here

### Post-Game Score Edit & Resubmit (undo/Challonge desync fix)
Fixing a score after game end should fix it everywhere. Today the score reports the instant the game ends, and the app immediately forgets the match — a post-game correction never reaches Challonge.
- Keep the just-reported match remembered after the report instead of clearing it immediately
- **Edit the final result after game end**: adjust either team's score (undo, score keys, or a small edit dialog on the game-over screen), which also re-derives the winner
- Once the result differs from what was reported, show a "score changed — resubmit to Challonge" prompt; resubmitting overwrites the match result (the API supports updating a completed match)
- The corrected result also updates the local game history log
- The remembered match clears on the next reset/match selection, same as today

### Settings Backup & Export — Local and Cloud
One click to save the whole setup; one click to restore it.
- Export everything to a single local file: game settings, key bindings, LED effect mappings, Challonge credentials, relay URL, theme
- **Cloud backup too**: store the same settings bundle on the league website, so any laptop can pull the current setup down
- Import/restore from either source — new laptop or reinstall becomes a 10-second job
- Pre-event ritual: back up before every event (pairs with the pre-event offline download)

### Crash-Recovery Snapshot
If the app dies mid-game, the game doesn't.
- Continuously snapshot live game state to disk (every few seconds and on every scoring/penalty event): scores, clock, penalties, halftime state, sudden death, selected Challonge match
- On next launch after an unclean exit, offer: "Resume game? HOME 3–2 VISITOR, 4:12 left" — one click restores everything, including the match link so the score still reports to Challonge
- Decline and it starts fresh like today; snapshots are cleared on normal reset/game end
- Snapshot write must never stutter the UI or the clock

### Make Countdowns Brighter and Louder
The dark navy pulse countdown (game start / final 10 seconds) over-corrected from the old blue/red flash — it's now too subdued for a loud rink environment.
- Increase visual intensity: brighter colors or stronger pulse contrast so it reads from across the room
- Louder / more prominent countdown audio cues

### Toggle for Local vs Railway Relay URL
Quick switch in settings between local relay (development) and the Railway-hosted relay (event) without retyping URLs.

### Intermission Tracking / Visualization
Track and display intermission periods between games.
- Intermission timer or countdown visible on the main or between-game screen
- Possibly show on the phone scoreboard as well

### Pre-Game Speech Button and Screen
A dedicated button (Stream Deck or keyboard) and full-screen overlay for pre-game ceremony or announcements.
- Triggered before a game starts
- Full-screen display (team names, logos, or custom message)
- Dismissable by the operator when ready to begin
