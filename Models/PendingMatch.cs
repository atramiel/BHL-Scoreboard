namespace Scoreboard.Models;

public class PendingMatch
{
    public int MatchId { get; init; }
    public string Player1Name { get; init; } = "";
    public string Player2Name { get; init; } = "";
    public int SuggestedOrder { get; init; }
    public string Label => $"{Player1Name} vs {Player2Name}";
}
