Custom goal horns
=================

Drop one .wav file per team into this folder, named exactly after the team
(case doesn't matter):

    ScottBot.wav
    Team EVAC.wav
    Magic Smoke.wav

When that team scores, their horn plays instead of the default goal sound.
Teams without a file here get the default (gameScore.wav).

Notes:
- .wav only (the app's sound player doesn't do .mp3)
- The name must match the team name shown on the scoreboard — which is the
  Challonge participant name when a match is selected
- Keep clips short (2-3 seconds); the game doesn't wait for the horn
