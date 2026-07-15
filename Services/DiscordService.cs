using Scoreboard.Models;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace Scoreboard.Services;

/// <summary>
/// Posts hype moments and results to a Discord channel via an incoming webhook.
/// Best-effort only — a missed post has no record-keeping consequence (unlike
/// Challonge/league-site results), so there's no retry queue, just a silent
/// swallow on failure.
/// </summary>
public static class DiscordService
{
    private static readonly HttpClient _http = new();

    public static bool IsConfigured(GameSettings settings) => !string.IsNullOrWhiteSpace(settings.DiscordWebhookUrl);

    public static Task PostFinalScoreAsync(
        GameSettings settings,
        string homeTeam, int homeScore, string visitorTeam, int visitorScore,
        bool overtime, bool championship)
    {
        if (!settings.DiscordPostFinalScores || !IsConfigured(settings)) return Task.CompletedTask;

        var homeWon = homeScore > visitorScore;
        var winner = homeWon ? homeTeam : visitorTeam;
        var loser = homeWon ? visitorTeam : homeTeam;
        var winnerScore = Math.Max(homeScore, visitorScore);
        var loserScore = Math.Min(homeScore, visitorScore);
        var suffix = overtime ? " (OT)" : "";
        var title = championship
            ? $"🏆 CHAMPIONSHIP: {winner} def. {loser} {winnerScore}-{loserScore}{suffix}"
            : $"🏒 FINAL: {winner} def. {loser} {winnerScore}-{loserScore}{suffix}";
        var color = championship ? 0xE8B34C : 0x2A4D8F; // gold vs blue, matches the league site palette

        return PostEmbedAsync(settings.DiscordWebhookUrl!, title, color, thumbnailUrl: LeagueSiteService.GetLogoUrl(winner));
    }

    public static Task PostNextUpAsync(GameSettings settings, string homeTeam, string visitorTeam)
    {
        if (!settings.DiscordPostNextUp || !IsConfigured(settings)) return Task.CompletedTask;
        return PostEmbedAsync(settings.DiscordWebhookUrl!, $"⏭ Next Up: {homeTeam} vs {visitorTeam} — Load In", 0x4A5670);
    }

    public static Task PostSuddenDeathAsync(GameSettings settings, string homeTeam, string visitorTeam)
    {
        if (!settings.DiscordPostHypePings || !IsConfigured(settings)) return Task.CompletedTask;
        return PostEmbedAsync(settings.DiscordWebhookUrl!, $"🚨 SUDDEN DEATH! {homeTeam} vs {visitorTeam} — first goal wins!", 0xD43F5A);
    }

    /// <summary>Pings just the two teams up next, using role IDs set on the website (admin.html "Team Discord Role"). A team with no mapped role is simply not tagged.</summary>
    public static async Task PostOnDeckAsync(GameSettings settings, string homeTeam, string visitorTeam)
    {
        if (!settings.DiscordPostOnDeck || !IsConfigured(settings)) return;

        var roles = await LeagueSiteService.FetchDiscordRoleIdsAsync(settings);
        var roleIds = new List<string>();
        if (roles.TryGetValue(homeTeam, out var homeRoleId)) roleIds.Add(homeRoleId);
        if (roles.TryGetValue(visitorTeam, out var visitorRoleId)) roleIds.Add(visitorRoleId);

        var content = roleIds.Count > 0 ? string.Join(" ", roleIds.Select(id => $"<@&{id}>")) : null;
        await PostEmbedAsync(settings.DiscordWebhookUrl!, $"🔜 On Deck: {homeTeam} vs {visitorTeam}", 0x4A5670,
            content: content, allowedMentionRoleIds: roleIds);
    }

    /// <summary>Manual end-of-night recap — resultLines are pre-formatted, e.g. "Team A 5–3 Team B".</summary>
    public static Task PostRecapAsync(GameSettings settings, IReadOnlyList<string> resultLines)
    {
        if (!IsConfigured(settings)) return Task.CompletedTask;
        var description = resultLines.Count > 0 ? string.Join("\n", resultLines) : "No games recorded today.";
        return PostEmbedAsync(settings.DiscordWebhookUrl!, "📋 Tonight's Results", 0x4A5670, description: description);
    }

    private static async Task PostEmbedAsync(string webhookUrl, string title, int color, string? thumbnailUrl = null, string? description = null, string? content = null, IReadOnlyList<string>? allowedMentionRoleIds = null)
    {
        try
        {
            var embed = new Dictionary<string, object?> { ["title"] = title, ["color"] = color };
            if (!string.IsNullOrWhiteSpace(description)) embed["description"] = description;
            if (!string.IsNullOrWhiteSpace(thumbnailUrl)) embed["thumbnail"] = new { url = thumbnailUrl };

            var payload = new Dictionary<string, object?> { ["embeds"] = new[] { embed } };
            if (!string.IsNullOrWhiteSpace(content)) payload["content"] = content;
            // Explicit allow-list: only these specific role IDs can be pinged, never @everyone/@here.
            if (allowedMentionRoleIds is { Count: > 0 }) payload["allowed_mentions"] = new { roles = allowedMentionRoleIds };

            var body = JsonSerializer.Serialize(payload);
            await _http.PostAsync(webhookUrl, new StringContent(body, Encoding.UTF8, "application/json"));
        }
        catch { /* best-effort */ }
    }
}
