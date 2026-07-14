using Scoreboard.Models;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace Scoreboard.Services;

public record StandingRow(string Team, int Wins, int Losses, int Ties);

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

    /// <summary>
    /// Win/loss/tie record per participant, computed from completed matches —
    /// works for any tournament type (Swiss, elimination, round robin), since
    /// it's just tallying results rather than reading a bracket-shaped structure.
    /// </summary>
    public static async Task<List<StandingRow>> FetchStandingsAsync(string bracketUrl, string apiKey)
    {
        var slug = ExtractSlug(bracketUrl);
        if (slug == null) return [];

        try
        {
            var baseUrl = $"https://api.challonge.com/v1/tournaments/{slug}";
            var participants = await FetchParticipantsAsync(baseUrl, apiKey);

            var record = new Dictionary<string, (int Wins, int Losses, int Ties)>();
            foreach (var name in participants.Values.Distinct())
                record[name] = (0, 0, 0);

            var json = await _http.GetStringAsync($"{baseUrl}/matches.json?api_key={apiKey}&state=complete");
            using var doc = JsonDocument.Parse(json);

            foreach (var item in doc.RootElement.EnumerateArray())
            {
                var m = item.TryGetProperty("match", out var inner) ? inner : item;
                if (!m.TryGetProperty("player1_id", out var p1El) || p1El.ValueKind == JsonValueKind.Null) continue;
                if (!m.TryGetProperty("player2_id", out var p2El) || p2El.ValueKind == JsonValueKind.Null) continue;

                var p1Id = p1El.GetInt64();
                var p2Id = p2El.GetInt64();
                if (!participants.TryGetValue(p1Id, out var p1Name)) continue;
                if (!participants.TryGetValue(p2Id, out var p2Name)) continue;

                var hasWinner = m.TryGetProperty("winner_id", out var winEl) && winEl.ValueKind == JsonValueKind.Number;
                if (!hasWinner)
                {
                    // Completed with no winner recorded — a tie/draw (Swiss allows these).
                    var (w1, l1, t1) = record[p1Name]; record[p1Name] = (w1, l1, t1 + 1);
                    var (w2, l2, t2) = record[p2Name]; record[p2Name] = (w2, l2, t2 + 1);
                    continue;
                }

                var winnerId = winEl.GetInt64();
                var winnerName = winnerId == p1Id ? p1Name : p2Name;
                var loserName = winnerId == p1Id ? p2Name : p1Name;
                var (ww, wl, wt) = record[winnerName]; record[winnerName] = (ww + 1, wl, wt);
                var (lw, ll, lt) = record[loserName]; record[loserName] = (lw, ll + 1, lt);
            }

            return [.. record
                .Select(kv => new StandingRow(kv.Key, kv.Value.Wins, kv.Value.Losses, kv.Value.Ties))
                .OrderByDescending(r => r.Wins)
                .ThenBy(r => r.Losses)
                .ThenBy(r => r.Team)];
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

    /// <summary>
    /// Reports a match result, retrying briefly on failure.
    /// Returns true only when Challonge confirmed the update.
    /// </summary>
    public static async Task<bool> ReportResultAsync(
        string bracketUrl, string apiKey,
        int matchId, long winnerId,
        int player1Score, int player2Score)
    {
        var slug = ExtractSlug(bracketUrl);
        if (slug == null) return false;

        var body = JsonSerializer.Serialize(new
        {
            api_key = apiKey,
            match = new
            {
                scores_csv = $"{player1Score}-{player2Score}",
                winner_id = winnerId
            }
        });

        var url = $"https://api.challonge.com/v1/tournaments/{slug}/matches/{matchId}.json";
        for (var attempt = 0; attempt < 3; attempt++)
        {
            if (attempt > 0) await Task.Delay(1500);
            try
            {
                var response = await _http.PutAsync(url, new StringContent(body, Encoding.UTF8, "application/json"));
                if (response.IsSuccessStatusCode) return true;
                // 4xx means the request itself is wrong (bad key, closed match) — retrying won't help
                if ((int)response.StatusCode < 500) return false;
            }
            catch { /* network error — retry */ }
        }
        return false;
    }
}
