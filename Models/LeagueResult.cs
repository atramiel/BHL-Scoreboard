using System.Text.Json.Serialization;

namespace Scoreboard.Models;

/// <summary>
/// A finished game headed for the league website. Property names match the
/// record_game RPC parameters so the object serializes straight into the
/// request body (the admin key is added at send time, never persisted).
/// </summary>
public class LeagueResult
{
    [JsonPropertyName("p_event_name")]
    public string EventName { get; set; } = "";

    [JsonPropertyName("p_team1")]
    public string Team1 { get; set; } = "";          // winner first, by convention

    [JsonPropertyName("p_team2")]
    public string Team2 { get; set; } = "";

    [JsonPropertyName("p_score1")]
    public int Score1 { get; set; }

    [JsonPropertyName("p_score2")]
    public int Score2 { get; set; }

    [JsonPropertyName("p_overtime")]
    public bool Overtime { get; set; }

    [JsonPropertyName("p_championship")]
    public bool Championship { get; set; }

    [JsonPropertyName("p_challonge_match_id")]
    public long? ChallongeMatchId { get; set; }

    [JsonPropertyName("p_reported")]
    public bool ReportedToChallonge { get; set; }

    [JsonPropertyName("p_played_at")]
    public DateTimeOffset PlayedAt { get; set; }
}
