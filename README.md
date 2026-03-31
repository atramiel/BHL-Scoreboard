# BHL Scoreboard

A full-featured scoreboard application built for **Bot Hockey League (BHL)** events. Runs on Windows and integrates with a Stream Deck+ for physical button control, a live phone scoreboard for spectators, Challonge bracket management, and SignalRGB lighting effects.

---

## Table of Contents

- [Features](#features)
- [Screenshots](#screenshots)
- [Prerequisites](#prerequisites)
- [Installation](#installation)
  - [1. Run the Scoreboard App](#1-run-the-scoreboard-app)
  - [2. Build the Stream Deck Plugin](#2-build-the-stream-deck-plugin)
  - [3. Install the Stream Deck Plugin](#3-install-the-stream-deck-plugin)
  - [4. Install the Stream Deck Profile](#4-install-the-stream-deck-profile)
- [Configuration](#configuration)
- [Stream Deck Controls](#stream-deck-controls)
- [Live Phone Scoreboard](#live-phone-scoreboard)
- [Challonge Integration](#challonge-integration)
- [LED Lighting Effects](#led-lighting-effects)
- [Sound Effects](#sound-effects)
- [Keyboard Shortcuts](#keyboard-shortcuts)

---

## Features

- **Real-time scoreboard** — clock, scores, and up to 2 simultaneous penalties per team
- **Stream Deck+ control** — score, penalties, clock, undo/redo, swap sides, and more from physical buttons and dials
- **Live phone scoreboard** — spectators connect to the scoreboard PC over Wi-Fi and see a live view on their phone; QR code shown on the between-game screen
- **Challonge integration** — pull open matches from a bracket, load team names directly from the Stream Deck, and automatically report final scores back to Challonge at game end
- **LED lighting effects** — trigger SignalRGB effects on goals, penalties, game over, sudden death, and clock warnings
- **Between-game screen** — countdown timer to next match, QR codes for bracket and league info, and next match display
- **Sound effects** — audio cues for goals, penalties, game start/end, and clock warnings
- **Timing modes** — Stop Time (clock pauses on goal) or Run Time (clock keeps running)
- **Undo / Redo** — full history of all scoring and penalty changes
- **Kiosk mode** — lock the display for unattended operation
- **Sudden death** — automatic overtime detection when the game ends in a tie

---

## Prerequisites

### Hardware

| Item | Required? | Notes |
|---|---|---|
| Windows 10 or later PC | Required | App is Windows-only |
| Elgato Stream Deck+ | Required for Stream Deck control | Profile is built for the Stream Deck+ (8 buttons + 4 dials + touch strip) |
| Display or projector | Recommended | To show the scoreboard to players and audience |
| SignalRGB-compatible LED hardware | Optional | For goal/penalty/game lighting effects |

### Software

| Software | Required? | Download |
|---|---|---|
| .NET 10 Runtime (Windows) | Required | https://dotnet.microsoft.com/en-us/download/dotnet/10.0 |
| Elgato Stream Deck software v6.4+ | Required for Stream Deck | https://www.elgato.com/downloads |
| Node.js v20+ | Required to build the plugin | https://nodejs.org |
| SignalRGB | Optional (LED effects) | https://signalrgb.com |

### Accounts

| Account | Required? | Notes |
|---|---|---|
| Challonge account + API key | Optional | Needed for bracket/match integration |

---

## Installation

### 1. Run the Scoreboard App

Download or clone this repository, then build the WPF app:

```bash
dotnet build Scoreboard.sln -c Release
```

The executable will be at `bin\Release\net10.0-windows\Scoreboard.exe` — run that directly.

Or open `Scoreboard.sln` in Visual Studio, switch the configuration to **Release** in the toolbar, and press **Ctrl+F5**.

> **First launch:** The app will prompt for UAC elevation once to add a Windows Firewall rule and a URL reservation for the phone scoreboard (port 5000). Accept both prompts and the setup is permanent — it won't ask again.

---

### 2. Build the Stream Deck Plugin

Open a terminal in the `com.codingrecluse.scoreboard.sdPlugin` folder:

```bash
cd com.codingrecluse.scoreboard.sdPlugin
npm install
npm run build
```

This compiles the TypeScript source and outputs `bin/plugin.js`.

---

### 3. Install the Stream Deck Plugin

There are two ways to install the plugin.

#### Option A — Junction (recommended for development)

This links the plugin folder directly into Stream Deck's plugins directory. Changes you make (after rebuilding) take effect immediately without re-copying.

Open **PowerShell** and run:

```powershell
$linkPath = "$env:APPDATA\Elgato\StreamDeck\Plugins\com.codingrecluse.scoreboard.sdPlugin"
$targetPath = "C:\Users\<you>\Documents\BHL-Scoreboard\com.codingrecluse.scoreboard.sdPlugin"
New-Item -ItemType Junction -Path $linkPath -Target $targetPath
```

Replace `<you>` with your Windows username.

#### Option B — Manual copy

Copy the entire `com.codingrecluse.scoreboard.sdPlugin` folder to:

```
%APPDATA%\Elgato\StreamDeck\Plugins\
```

So the final path looks like:

```
%APPDATA%\Elgato\StreamDeck\Plugins\com.codingrecluse.scoreboard.sdPlugin\
```

> **Note:** If you use Option B and update the plugin later, you will need to re-copy the folder.

After installing via either option, **restart the Stream Deck app** (quit and reopen from the system tray).

---

### 4. Install the Stream Deck Profile

The **Bot Hockey** profile for the Stream Deck+ is included at:

```
com.codingrecluse.scoreboard.sdPlugin\profiles\Bot Hockey.streamDeckProfile
```

Stream Deck may install this profile automatically when the plugin first loads. If it doesn't:

1. Open the **Stream Deck app**
2. Click the **gear icon** (Settings) in the top right
3. Go to the **Profiles** tab
4. Click **Import** and select `Bot Hockey.streamDeckProfile` from the `profiles/` folder

---

## Configuration

Open the configuration window from the **gear icon** in the bottom-right of the scoreboard.

| Setting | Description |
|---|---|
| Home / Visitor Team Name | Team names shown on the scoreboard |
| Home / Visitor Color | Background color behind each team's score |
| Game Length (minutes) | How long each game runs (default: 10) |
| Penalty Length (minutes) | How long each penalty lasts (default: 2) |
| Timing Mode | **Stop Time** (default) — clock pauses on goal. **Run Time** — clock keeps running on goal, only stops on manual pause |
| Sound | Enable or disable all sound effects |
| Kiosk Mode | Locks the window for unattended display |
| Bracket URL | Challonge tournament URL (used for QR code and match fetching) |
| Learn More URL | Custom URL shown as a QR code on the between-game screen |
| Challonge API Key | Your Challonge API key for match fetching and score reporting |
| SignalRGB Path | Path to your SignalRGB install (default: `%LOCALAPPDATA%\VortxEngine`) |
| LED Effects | Configure which SignalRGB effect triggers on each game event |
| Key Bindings | Customize keyboard shortcuts for all actions |

The **Phone Scoreboard URL** is shown at the bottom of the configuration window — this is what spectators open on their phones.

---

## Stream Deck Controls

The Bot Hockey profile maps the Stream Deck+ as follows:

### Buttons

| Button | Action |
|---|---|
| Score Home | Add 1 to home team score |
| Score Away | Add 1 to visitor team score |
| Penalty Home | Start a 2-minute penalty for home team |
| Penalty Away | Start a 2-minute penalty for visitor team |
| Play / Pause | Start or pause the game clock |
| Reset Game | Reset scores and clock |
| Undo | Undo the last action |
| Redo | Redo the last undone action |
| Swap Sides | Mirror the scoreboard (swap home/visitor sides) |
| Reset Clock | Reset the clock to the configured game length |
| Between Game | Show/hide the between-game screen |
| Match Slot 1–6 | Shows upcoming matches from Challonge; press to load teams |

### Dial (Encoder)

| Input | Action |
|---|---|
| Rotate | Adjust the next match countdown time |
| Press | Start the next match countdown |

---

## Live Phone Scoreboard

The app runs a small web server on **port 5000** that any device on the same Wi-Fi network can connect to.

1. The scoreboard PC's URL is shown in the **Configuration window** (e.g., `http://192.168.1.50:5000/`)
2. On the **between-game screen**, a QR code is shown — spectators can scan it to open the live scoreboard on their phone
3. The phone view updates in real time via WebSocket and shows:
   - Game status (Live / Paused / Game Over / Sudden Death)
   - Game clock (color changes as time runs low)
   - Both team names and scores
   - Active penalties for each team

> On first launch the app will prompt for UAC elevation to add the firewall rule and URL reservation automatically. After accepting, phones on the same Wi-Fi network can connect immediately.

---

## Challonge Integration

1. Set your **Bracket URL** and **Challonge API Key** in Configuration
2. The 6 **Match Slot** buttons on the Stream Deck will populate with upcoming open matches (e.g., "Player1 vs Player2")
3. Press a match slot button to load those team names into the scoreboard
4. Group-stage tournaments are supported

---

## LED Lighting Effects

Requires [SignalRGB](https://signalrgb.com) installed on the scoreboard PC.

Set the **SignalRGB path** in Configuration to point to your install, then configure which effect plays for each game event:

| Event | Default Effect |
|---|---|
| Game Running | Rainbow |
| Game Stopped | Solid Color |
| Penalty Added | Rainbow |
| Penalty Expired | Pipeline |
| Game Over | Bullet Hell |
| Sudden Death | Side To Side |
| Home Scores | Screen Ambience |
| Visitor Scores | Radar |
| Under 1 minute | Rgbarz |
| Under 30 seconds | Radar |
| Under 10 seconds | Neon Shift |

---

## Sound Effects

All sounds can be toggled in Configuration. Sound plays automatically for:

| Event | Sound |
|---|---|
| Goal scored | Score sound |
| Game start | Game start fanfare |
| Play / Pause toggled | Cartoon whistle |
| Penalty added | Penalty buzzer |
| Penalty expired | Chime |
| Clock hits 1:00 | Slow heartbeat |
| Clock hits 0:30 | Medium heartbeat |
| Sudden death | Sudden death sting |
| Game ends | Final buzzer |

---

## Keyboard Shortcuts

Default bindings (all rebindable in Configuration):

| Key | Action |
|---|---|
| Page Up | Score Home |
| Page Down | Score Away |
| Home | Penalty Home |
| End | Penalty Away |
| Space | Play / Pause |
| Backspace | Undo |
| Enter | Redo |
| Delete | Reset Game |
| Right Windows | Reset Clock |
| ` (Tilde) | Swap Sides |
| B | Between Game screen |
