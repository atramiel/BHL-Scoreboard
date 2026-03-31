# Changelog

All notable changes to this project are documented here.

---

## Unreleased

### Fixed
- Between-game screen QR codes now scale dynamically to fit any resolution via a Viewbox wrapper (designed at 1920×1080)
- QR code labels (BRACKET / LEARN MORE!) moved to below the codes
- Logo and QR code sizes reduced so all content is visible on 1080p displays without clipping

---

## [1.6.1] — 2026-03-31

### Added
- **Timing Mode setting** — choose between Stop Time (clock pauses on goal, default) and Run Time (clock keeps running on goal) in the Configuration window
- **Challonge score reporting** — final scores and winner are automatically pushed to Challonge when the game ends, including sudden death results
- **Auto phone scoreboard setup** — on first launch the app adds the required Windows HTTP URL reservation (`http://+:5000/`) via a UAC prompt, fixing the HTTP 400 error phones received when connecting over LAN
- **README** — full setup and feature documentation added to the repository
- **BACKLOG** — tracked backlog added to the repository

### Fixed
- Phone scoreboard (port 5000) was returning HTTP 400 to devices on the LAN due to a missing URL reservation; now handled automatically on first boot

### Changed
- Stream Deck plugin is now linked via a directory junction rather than copied, so rebuilds take effect without re-installing
- Stream Deck profile (Bot Hockey) included in the repository and configured for auto-install on plugin load

---

## [1.6.0] — earlier

### Added
- Live phone scoreboard via embedded WebSocket server (port 5000)
- Challonge bracket integration — fetch open matches and select them from Stream Deck buttons
- Between-game screen with next match countdown, QR codes, and next match display
- Stream Deck+ profile (Bot Hockey) with full button and dial mapping
