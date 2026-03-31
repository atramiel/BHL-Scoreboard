using Scoreboard.Models;
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace Scoreboard.Services;

public static class ChallongeService
{
    private static readonly HttpClient _http = new();

    public static async Task<List<PendingMatch>> FetchOpenMatchesAsync(string bracketUrl, string apiKey)
    {
        var slug = ExtractSlug(bracketUrl);
        if (slug == null) return [];

        try
        {
            var baseUrl = $"https://api.challonge.com/v1/tournaments/{slug}";
            var participants = await FetchParticipantsAsync(baseUrl, apiKey);
            return await FetchMatchesAsync(baseUrl, apiKey, participants);
        }
        catch
        {
            return [];
        }
    }

    private static string? ExtractSlug(string url)
    {
        try
        {
            var uri = new Uri(url);
            var path = uri.AbsolutePath.Trim('/');
            var parts = uri.Host.Split('.');
            if (parts.Length > 2)
                return $"{parts[0]}-{path}";
            return path;
        }
        catch { return null; }
    }

    private static async Task<Dictionary<long, string>> FetchParticipantsAsync(string baseUrl, string apiKey)
    {
        var json = await _http.GetStringAsync($"{baseUrl}/participants.json?api_key={apiKey}");
        using var doc = JsonDocument.Parse(json);

        var result = new Dictionary<long, string>();
        foreach (var item in doc.RootElement.EnumerateArray())
        {
            // The API wraps each participant: [{"participant":{...}}, ...]
            var p = item.TryGetProperty("participant", out var inner) ? inner : item;
            if (!p.TryGetProperty("id", out var idEl)) continue;

            var id = idEl.GetInt64();
            var name = GetFirstNonEmpty(p, "name", "display_name", "challonge_username") ?? $"#{id}";
            result[id] = name;

            // Also index by any group_player_ids so group-stage tournaments work
            if (p.TryGetProperty("group_player_ids", out var gpi) && gpi.ValueKind == JsonValueKind.Array)
                foreach (var gid in gpi.EnumerateArray())
                    if (gid.ValueKind == JsonValueKind.Number)
                        result[gid.GetInt64()] = name;
        }
        return result;
    }

    private static string? GetFirstNonEmpty(JsonElement el, params string[] keys)
    {
        foreach (var key in keys)
            if (el.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String
                && v.GetString() is { Length: > 0 } s)
                return s;
        return null;
    }

    private static async Task<List<PendingMatch>> FetchMatchesAsync(
        string baseUrl, string apiKey, Dictionary<long, string> participants)
    {
        var json = await _http.GetStringAsync($"{baseUrl}/matches.json?api_key={apiKey}&state=open");
        using var doc = JsonDocument.Parse(json);

        var result = new List<PendingMatch>();
        foreach (var item in doc.RootElement.EnumerateArray())
        {
            var m = item.TryGetProperty("match", out var inner) ? inner : item;
            if (!m.TryGetProperty("player1_id", out var p1El) || p1El.ValueKind == JsonValueKind.Null) continue;
            if (!m.TryGetProperty("player2_id", out var p2El) || p2El.ValueKind == JsonValueKind.Null) continue;

            var p1Id = p1El.GetInt64();
            var p2Id = p2El.GetInt64();
            participants.TryGetValue(p1Id, out var p1Name);
            participants.TryGetValue(p2Id, out var p2Name);

            var order = m.TryGetProperty("suggested_play_order", out var orderEl)
                        && orderEl.ValueKind == JsonValueKind.Number
                ? orderEl.GetInt32() : int.MaxValue;

            result.Add(new PendingMatch
            {
                MatchId = m.GetProperty("id").GetInt32(),
                Player1Id = p1Id,
                Player2Id = p2Id,
                Player1Name = p1Name ?? "?",
                Player2Name = p2Name ?? "?",
                SuggestedOrder = order
            });
        }

        return [.. result.OrderBy(m => m.SuggestedOrder).Take(6)];
    }

    public static async Task ReportResultAsync(
        string bracketUrl, string apiKey,
        int matchId, long winnerId,
        int player1Score, int player2Score)
    {
        var slug = ExtractSlug(bracketUrl);
        if (slug == null) return;

        var body = JsonSerializer.Serialize(new
        {
            api_key = apiKey,
            match = new
            {
                scores_csv = $"{player1Score}-{player2Score}",
                winner_id = winnerId
            }
        });

        try
        {
            var url = $"https://api.challonge.com/v1/tournaments/{slug}/matches/{matchId}.json";
            await _http.PutAsync(url, new StringContent(body, Encoding.UTF8, "application/json"));
        }
        catch { }
    }
}
