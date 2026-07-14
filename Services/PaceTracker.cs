using Scoreboard.Models;

namespace Scoreboard.Services;

/// <summary>
/// Models how BHL actually runs a day: matches happen in round-sized batches
/// (whatever's open in that round), and a natural break follows once the next
/// available match needs a team that already played in the current batch.
/// Not persisted across app restarts — matches BHL's one-launch-per-event-day
/// usage, same as other same-day-only state elsewhere in the app.
/// </summary>
public class PaceTracker
{
    private readonly HashSet<string> _teamsPlayedThisBatch = new(StringComparer.OrdinalIgnoreCase);

    public int MatchesCompletedToday { get; private set; }

    public void RecordGameFinished(string homeTeam, string visitorTeam)
    {
        MatchesCompletedToday++;
        _teamsPlayedThisBatch.Add(homeTeam);
        _teamsPlayedThisBatch.Add(visitorTeam);
    }

    /// <summary>
    /// True the moment any upcoming match needs a team that already played in the
    /// current batch — a natural round boundary. Resets the batch when detected,
    /// so the next call starts tracking the new round fresh.
    /// </summary>
    public bool CheckRoundBoundary(IEnumerable<string> upcomingTeamNames)
    {
        if (_teamsPlayedThisBatch.Count == 0) return false;
        var boundary = upcomingTeamNames.Any(_teamsPlayedThisBatch.Contains);
        if (boundary) _teamsPlayedThisBatch.Clear();
        return boundary;
    }

    /// <summary>Positive = behind schedule, negative = ahead. Null if pace inputs aren't configured.</summary>
    public TimeSpan? ComputeDrift(GameSettings settings, DateTime now)
    {
        if (!TryGetPaceInputs(settings, out var start, out var lastStart, out var planned)) return null;

        // No real signal yet before the first match finishes — pre-match setup,
        // opening announcements, warmups, etc. shouldn't read as "falling behind"
        // just because time passed since the configured start.
        if (MatchesCompletedToday == 0) return null;

        var targetPacePerMatch = (lastStart - start) / planned;
        if (targetPacePerMatch <= TimeSpan.Zero) return null;

        var expectedElapsed = TimeSpan.FromTicks(targetPacePerMatch.Ticks * MatchesCompletedToday);
        var actualElapsed = now - start;
        return actualElapsed - expectedElapsed;
    }

    /// <summary>clamp(50min − drift, 20min, 75min) — behind schedule shrinks the break, ahead lengthens it.</summary>
    public TimeSpan? ComputeSuggestedBreak(GameSettings settings, DateTime now)
    {
        var drift = ComputeDrift(settings, now);
        if (drift == null) return null;

        var suggested = TimeSpan.FromMinutes(50) - drift.Value;
        if (suggested < TimeSpan.FromMinutes(20)) suggested = TimeSpan.FromMinutes(20);
        if (suggested > TimeSpan.FromMinutes(75)) suggested = TimeSpan.FromMinutes(75);
        return suggested;
    }

    public string GetPaceStatusText(GameSettings settings, DateTime now)
    {
        var drift = ComputeDrift(settings, now);
        if (drift == null) return "Pace: —";

        var minutes = (int)Math.Round(Math.Abs(drift.Value.TotalMinutes));
        if (minutes < 3) return "On Pace";
        return drift.Value > TimeSpan.Zero ? $"Behind ~{minutes}m" : $"Ahead ~{minutes}m";
    }

    private static bool TryGetPaceInputs(GameSettings settings, out DateTime start, out DateTime lastStart, out int planned)
    {
        start = default; lastStart = default; planned = 0;
        if (!TimeSpan.TryParse(settings.PaceEventStartTime, out var startTod)) return false;
        if (!TimeSpan.TryParse(settings.PaceLastGameStartTime, out var lastTod)) return false;
        if (settings.PaceTotalPlannedMatches <= 0) return false;

        var today = DateTime.Today;
        start = today + startTod;
        lastStart = today + lastTod;
        planned = settings.PaceTotalPlannedMatches;
        return true;
    }
}
