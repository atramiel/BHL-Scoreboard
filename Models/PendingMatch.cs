namespace Scoreboard.Models;

public class PendingMatch
{
    public int MatchId { get; init; }
    public long Player1Id { get; init; }
    public long Player2Id { get; init; }
    public string Player1Name { get; init; } = "";
    public string Player2Name { get; init; } = "";
    public int SuggestedOrder { get; init; }
    // Which bracket this match came from — the main one (Settings.BracketUrl) or
    // one of the "Other Tournaments" entries. Reporting a result needs to go back
    // to the bracket the match actually belongs to, not always the main one.
    public string BracketUrl { get; init; } = "";
    public string Label => $"{Player1Name} vs {Player2Name}";
}
