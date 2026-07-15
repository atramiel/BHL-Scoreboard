using Scoreboard.Models;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Scoreboard.Services;

/// <summary>
/// Talks to the league website's Supabase backend: posts finished games via the
/// record_game RPC (queuing to disk when offline) and downloads a pre-event
/// bundle of league data for offline use at the venue.
/// </summary>
public static class LeagueSiteService
{
    private const string QueueFile = "leagueQueue.json";
    private const string BundleFile = "leagueBundle.json";

    private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(10) };
    private static readonly SemaphoreSlim _queueLock = new(1, 1);

    public static bool IsConfigured(GameSettings s) =>
        !string.IsNullOrWhiteSpace(s.SupabaseUrl)
        && !string.IsNullOrWhiteSpace(s.SupabaseAnonKey)
        && !string.IsNullOrWhiteSpace(s.LeagueAdminKey);

    private static string BaseUrl(GameSettings s)
    {
        var url = s.SupabaseUrl!.Trim().TrimEnd('/');
        if (!url.StartsWith("http")) url = "https://" + url;
        return url;
    }

    private static HttpRequestMessage NewRequest(GameSettings s, HttpMethod method, string path)
    {
        var request = new HttpRequestMessage(method, $"{BaseUrl(s)}{path}");
        request.Headers.Add("apikey", s.SupabaseAnonKey);
        request.Headers.Add("Authorization", $"Bearer {s.SupabaseAnonKey}");
        return request;
    }

    /// <summary>
    /// Posts one result. On failure the result is queued to disk and false is
    /// returned; queued results are retried by FlushQueueAsync.
    /// </summary>
    public static async Task<bool> PostResultAsync(GameSettings settings, LeagueResult result)
    {
        if (!IsConfigured(settings)) return false;

        if (await TrySendAsync(settings, result))
        {
            _ = FlushQueueAsync(settings); // good moment to drain anything from earlier dead spots
            return true;
        }

        await EnqueueAsync(result);
        return false;
    }

    /// <summary>The real reason the last failed send failed — status code and server response body.</summary>
    public static string? LastError { get; private set; }

    private static async Task<bool> TrySendAsync(GameSettings settings, LeagueResult result)
    {
        try
        {
            // Serialize the result, then attach the admin key for this send only
            var body = JsonSerializer.SerializeToNode(result)!.AsObject();
            body["p_admin_key"] = settings.LeagueAdminKey;

            var request = NewRequest(settings, HttpMethod.Post, "/rest/v1/rpc/record_game");
            request.Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");
            var response = await _http.SendAsync(request);
            if (response.IsSuccessStatusCode) return true;

            var responseBody = await response.Content.ReadAsStringAsync();
            LastError = $"HTTP {(int)response.StatusCode} {response.StatusCode}: {responseBody}";
            return false;
        }
        catch (Exception ex)
        {
            LastError = $"{ex.GetType().Name}: {ex.Message}";
            return false;
        }
    }

    /// <summary>Retries every queued result; keeps whatever still fails. Returns how many were sent.</summary>
    public static async Task<int> FlushQueueAsync(GameSettings settings)
    {
        if (!IsConfigured(settings)) return 0;

        await _queueLock.WaitAsync();
        try
        {
            var queue = LoadQueue();
            if (queue.Count == 0) return 0;

            var remaining = new List<LeagueResult>();
            var sent = 0;
            foreach (var result in queue)
            {
                if (await TrySendAsync(settings, result)) sent++;
                else remaining.Add(result);
            }
            SaveQueue(remaining);
            return sent;
        }
        finally { _queueLock.Release(); }
    }

    public static int QueuedCount()
    {
        try { return LoadQueue().Count; } catch { return 0; }
    }

    private static async Task EnqueueAsync(LeagueResult result)
    {
        await _queueLock.WaitAsync();
        try
        {
            var queue = LoadQueue();
            queue.Add(result);
            SaveQueue(queue);
        }
        finally { _queueLock.Release(); }
    }

    private static List<LeagueResult> LoadQueue()
    {
        try
        {
            if (!File.Exists(QueueFile)) return [];
            return JsonSerializer.Deserialize<List<LeagueResult>>(File.ReadAllText(QueueFile)) ?? [];
        }
        catch { return []; }
    }

    private static void SaveQueue(List<LeagueResult> queue)
    {
        try { File.WriteAllText(QueueFile, JsonSerializer.Serialize(queue)); } catch { }
    }

    // ---------- Team logos (resolved from the downloaded bundle, cached on disk) ----------

    private const string LogoCacheDir = "logoCache";
    private static Dictionary<string, string>? _logoUrlByName;

    /// <summary>Team name (or alias/sub-team name) → logo URL, from the offline bundle.</summary>
    private static Dictionary<string, string> LoadLogoMap()
    {
        if (_logoUrlByName != null) return _logoUrlByName;
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            if (File.Exists(BundleFile))
            {
                var root = JsonNode.Parse(File.ReadAllText(BundleFile))!;

                var logos = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var t in root["teams_public"]?.AsArray() ?? [])
                {
                    var name = t?["name"]?.GetValue<string>();
                    var url = t?["logo_url"]?.GetValue<string>();
                    if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(url))
                        logos[name.Trim()] = url;
                }

                var aliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var a in root["team_aliases"]?.AsArray() ?? [])
                {
                    var alias = a?["alias"]?.GetValue<string>();
                    var canonical = a?["canonical"]?.GetValue<string>();
                    if (!string.IsNullOrWhiteSpace(alias) && !string.IsNullOrWhiteSpace(canonical))
                        aliases[alias.Trim()] = canonical.Trim();
                }

                string Canon(string name)
                {
                    for (var hops = 0; hops < 5; hops++)
                        if (aliases.TryGetValue(name, out var next) && !next.Equals(name, StringComparison.OrdinalIgnoreCase))
                            name = next;
                        else break;
                    return name;
                }

                foreach (var (name, url) in logos) map[name] = url;
                foreach (var alias in aliases.Keys)
                    if (logos.TryGetValue(Canon(alias), out var url))
                        map[alias] = url;
            }
        }
        catch { }
        _logoUrlByName = map;
        return map;
    }

    /// <summary>
    /// Local file path for a team's logo, downloading and caching it on first
    /// use. Null when the team has no logo or nothing is downloadable.
    /// </summary>
    public static async Task<string?> GetLogoPathAsync(string teamName)
    {
        if (!LoadLogoMap().TryGetValue(teamName.Trim(), out var url)) return null;
        return await GetImagePathAsync(url);
    }

    /// <summary>The team's public (Supabase-hosted) logo URL, e.g. for a Discord embed thumbnail — not the local cache path.</summary>
    public static string? GetLogoUrl(string teamName) =>
        LoadLogoMap().TryGetValue(teamName.Trim(), out var url) ? url : null;

    /// <summary>
    /// Team name → Discord role ID, admin-managed on the website (admin.html
    /// "Team Discord Role"). A live authenticated lookup, like FetchAwardsAsync —
    /// discord_role_id is deliberately excluded from teams_public, so this can't
    /// be read from the offline bundle; it requires the admin key every time.
    /// </summary>
    public static async Task<Dictionary<string, string>> FetchDiscordRoleIdsAsync(GameSettings settings)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!IsConfigured(settings)) return map;
        try
        {
            var body = new JsonObject { ["p_admin_key"] = settings.LeagueAdminKey };
            var request = NewRequest(settings, HttpMethod.Post, "/rest/v1/rpc/admin_get_discord_roles");
            request.Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");
            var response = await _http.SendAsync(request);
            if (!response.IsSuccessStatusCode) return map;

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            foreach (var row in doc.RootElement.EnumerateArray())
            {
                var name = row.TryGetProperty("name", out var n) ? n.GetString() : null;
                var roleId = row.TryGetProperty("discord_role_id", out var r) ? r.GetString() : null;
                if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(roleId))
                    map[name.Trim()] = roleId.Trim();
            }
        }
        catch { }
        return map;
    }

    public record AwardEntry(string AwardName, string TeamName, string Notes);

    /// <summary>
    /// Custom trophies (Best Bot, Best Driver, etc.) already entered for this event —
    /// a live lookup, not from the offline bundle, since the Awards Ceremony wants
    /// whatever's true right now. Expect this to come back empty on the night
    /// itself: that data entry has historically happened after the event, and the
    /// ceremony works fine without it.
    /// </summary>
    public static async Task<List<AwardEntry>> FetchAwardsAsync(GameSettings settings, string eventName)
    {
        if (!IsConfigured(settings) || string.IsNullOrWhiteSpace(eventName)) return [];
        try
        {
            var request = NewRequest(settings, HttpMethod.Get,
                $"/rest/v1/awards?select=*&era=eq.BHL&event_name=eq.{Uri.EscapeDataString(eventName)}");
            var response = await _http.SendAsync(request);
            if (!response.IsSuccessStatusCode) return [];

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var result = new List<AwardEntry>();
            foreach (var a in doc.RootElement.EnumerateArray())
            {
                var name = a.TryGetProperty("award_name", out var n) ? n.GetString() ?? "" : "";
                var team = a.TryGetProperty("team_name", out var t) ? t.GetString() ?? "" : "";
                var notes = a.TryGetProperty("notes", out var no) ? no.GetString() ?? "" : "";
                if (name.Length > 0 && team.Length > 0) result.Add(new AwardEntry(name, team, notes));
            }
            return result;
        }
        catch { return []; }
    }

    /// <summary>
    /// Local file path for an arbitrary image URL (e.g. a bot photo), downloading
    /// and caching it on first use. Null when the URL is empty or unreachable.
    /// </summary>
    public static async Task<string?> GetImagePathAsync(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;
        try
        {
            var ext = Path.GetExtension(new Uri(url).LocalPath);
            if (string.IsNullOrEmpty(ext) || ext.Length > 5) ext = ".img";
            var hash = Convert.ToHexString(System.Security.Cryptography.SHA1.HashData(Encoding.UTF8.GetBytes(url)))[..16];
            Directory.CreateDirectory(LogoCacheDir);
            var file = Path.GetFullPath(Path.Combine(LogoCacheDir, hash + ext));

            if (!File.Exists(file))
                await File.WriteAllBytesAsync(file, await _http.GetByteArrayAsync(url));
            return file;
        }
        catch { return null; }
    }

    // ---------- Attract mode data (computed from the offline bundle) ----------

    public class AttractLine
    {
        public string Team { get; set; } = "";   // canonical team name, for logo lookup
        public string Text { get; set; } = "";
    }

    public class RivalryEntry
    {
        public string TeamA { get; set; } = "";
        public string TeamB { get; set; } = "";
        public string Story { get; set; } = "";
    }

    public class BotEntry
    {
        public string Name { get; set; } = "";
        public string Team { get; set; } = "";
        public string Weight { get; set; } = "";
        public string Weapon { get; set; } = "";
        public string Driver { get; set; } = "";
        public string PhotoUrl { get; set; } = "";
    }

    public class TeamSpotlightEntry
    {
        public string Team { get; set; } = "";
        public string Motto { get; set; } = "";
        public string HomeTown { get; set; } = "";
        public string Established { get; set; } = "";
        public string SpecialFeatures { get; set; } = "";
    }

    public class AttractData
    {
        public List<AttractLine> TrophyCase { get; } = [];
        public List<AttractLine> TodayResults { get; } = [];
        public List<RivalryEntry> Rivalries { get; } = [];
        public List<BotEntry> Bots { get; } = [];
        public List<TeamSpotlightEntry> TeamSpotlights { get; } = [];
        public string AboutTitle { get; set; } = "";
        public string AboutBody { get; set; } = "";
        public bool HasData =>
            TrophyCase.Count > 0 || Rivalries.Count > 0 || Bots.Count > 0
            || TeamSpotlights.Count > 0 || AboutBody.Length > 0;
    }

    /// <summary>
    /// League highlights for the between-game attract rotation: trophy case,
    /// all-time rankings, recent results, and the "What is Bot Hockey?" blurb.
    /// Empty (HasData == false) until the bundle has been downloaded.
    /// </summary>
    public static AttractData LoadAttractData()
    {
        var data = new AttractData();
        try
        {
            if (!File.Exists(BundleFile)) return data;
            var root = JsonNode.Parse(File.ReadAllText(BundleFile))!;

            var aliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var a in root["team_aliases"]?.AsArray() ?? [])
            {
                var alias = a?["alias"]?.GetValue<string>();
                var canonical = a?["canonical"]?.GetValue<string>();
                if (!string.IsNullOrWhiteSpace(alias) && !string.IsNullOrWhiteSpace(canonical))
                    aliases[alias.Trim()] = canonical.Trim();
            }
            string Canon(string name)
            {
                name = name.Trim();
                for (var hops = 0; hops < 5; hops++)
                    if (aliases.TryGetValue(name, out var next) && !next.Equals(name, StringComparison.OrdinalIgnoreCase))
                        name = next;
                    else break;
                return name;
            }

            // Trophy case: BHL-era podium counts per team
            var trophies = new Dictionary<string, int[]>(StringComparer.OrdinalIgnoreCase);
            foreach (var c in root["champions"]?.AsArray() ?? [])
            {
                if ((c?["era"]?.GetValue<string>() ?? "BHL") == "Legacy") continue;
                var name = Canon(c?["team_name"]?.GetValue<string>() ?? "");
                if (name.Length == 0) continue;
                var place = Math.Clamp(c?["place"]?.GetValue<int>() ?? 1, 1, 3);
                if (!trophies.TryGetValue(name, out var counts)) trophies[name] = counts = new int[3];
                counts[place - 1]++;
            }
            foreach (var (name, t) in trophies
                .OrderByDescending(kv => kv.Value[0]).ThenByDescending(kv => kv.Value[1]).ThenByDescending(kv => kv.Value[2])
                .Take(6))
            {
                // Compact counts ("3🥇 1🥈") — one emoji per trophy wraps badly for dynasties
                var parts = new List<string>();
                if (t[0] > 0) parts.Add($"{t[0]}🥇");
                if (t[1] > 0) parts.Add($"{t[1]}🥈");
                if (t[2] > 0) parts.Add($"{t[2]}🥉");
                data.TrophyCase.Add(new AttractLine
                {
                    Team = name,
                    Text = $"{name}   {string.Join(" ", parts)}",
                });
            }

            // Rankings + recent results from game history
            var record = new Dictionary<string, int[]>(StringComparer.OrdinalIgnoreCase); // [wins, losses]
            var games = (root["games"]?.AsArray() ?? [])
                .Select(g => new
                {
                    T1 = g?["team1_name"]?.GetValue<string>() ?? "",
                    T2 = g?["team2_name"]?.GetValue<string>() ?? "",
                    S1 = g?["team1_score"]?.GetValue<int>() ?? 0,
                    S2 = g?["team2_score"]?.GetValue<int>() ?? 0,
                    Counted = g?["scores_counted"]?.GetValue<bool>() ?? true,
                    PlayedAt = g?["played_at"]?.GetValue<string>() ?? "",
                })
                .Where(g => g.T1.Length > 0 && g.T2.Length > 0)
                .ToList();

            foreach (var g in games)
            {
                var winner = Canon(g.S1 > g.S2 ? g.T1 : g.T2);
                var loser = Canon(g.S1 > g.S2 ? g.T2 : g.T1);
                if (!record.TryGetValue(winner, out var w)) record[winner] = w = new int[2];
                if (!record.TryGetValue(loser, out var l)) record[loser] = l = new int[2];
                w[0]++; l[1]++;
            }
            // Today's results only — the attract screen is about the event in progress
            foreach (var g in games
                .Where(g => DateTimeOffset.TryParse(g.PlayedAt, out var at) && at.ToLocalTime().Date == DateTime.Today)
                .OrderByDescending(g => g.PlayedAt)
                .Take(7))
            {
                var t1Won = g.S1 > g.S2;
                var winner = Canon(t1Won ? g.T1 : g.T2); // canonical names so one screen shows one spelling
                var loser = Canon(t1Won ? g.T2 : g.T1);
                data.TodayResults.Add(new AttractLine
                {
                    Team = winner,
                    Text = g.Counted
                        ? $"{winner} {Math.Max(g.S1, g.S2)}–{Math.Min(g.S1, g.S2)} {loser}"
                        : $"{winner} def. {loser}",
                });
            }

            // "What is Bot Hockey?" blurb
            foreach (var s in root["site_content"]?.AsArray() ?? [])
            {
                if (s?["key"]?.GetValue<string>() != "what-is-bot-hockey") continue;
                data.AboutTitle = s?["title"]?.GetValue<string>() ?? "What is Bot Hockey?";
                var body = s?["body"]?.GetValue<string>() ?? "";
                data.AboutBody = body.Length > 450 ? body[..450].TrimEnd() + "…" : body;
            }

            // Rivalries — team vs. team plus the story of why
            foreach (var rv in root["rivalries"]?.AsArray() ?? [])
            {
                if (data.Rivalries.Count >= 6) break;
                var a = rv?["team_a"]?.GetValue<string>() ?? "";
                var b = rv?["team_b"]?.GetValue<string>() ?? "";
                if (a.Length == 0 || b.Length == 0) continue;
                var story = rv?["story"]?.GetValue<string>() ?? "";
                data.Rivalries.Add(new RivalryEntry
                {
                    TeamA = Canon(a),
                    TeamB = Canon(b),
                    Story = story.Length > 380 ? story[..380].TrimEnd() + "…" : story,
                });
            }

            // Team bios and bot rosters, straight from each team's profile
            foreach (var t in root["teams_public"]?.AsArray() ?? [])
            {
                var teamName = t?["name"]?.GetValue<string>() ?? "";
                if (teamName.Length == 0) continue;

                var motto = t?["motto"]?.GetValue<string>() ?? "";
                var homeTown = t?["home_town"]?.GetValue<string>() ?? "";
                var established = t?["established"]?.GetValue<string>() ?? "";
                var features = t?["special_features"]?.GetValue<string>() ?? "";
                var hasBio = motto.Length > 0 || homeTown.Length > 0 || established.Length > 0 || features.Length > 0;
                var hasBots = (t?["bot_roster"]?.AsArray()?.Count ?? 0) > 0;
                if (hasBio || hasBots)
                    data.TeamSpotlights.Add(new TeamSpotlightEntry
                    {
                        Team = teamName,
                        Motto = motto,
                        HomeTown = homeTown,
                        Established = established,
                        SpecialFeatures = features.Length > 260 ? features[..260].TrimEnd() + "…" : features,
                    });

                foreach (var b in t?["bot_roster"]?.AsArray() ?? [])
                {
                    var botName = b?["name"]?.GetValue<string>() ?? "";
                    if (botName.Length == 0) continue;
                    data.Bots.Add(new BotEntry
                    {
                        Name = botName,
                        Team = teamName,
                        Weight = b?["weight"]?.GetValue<string>() ?? "",
                        Weapon = b?["weapon"]?.GetValue<string>() ?? "",
                        Driver = b?["driver"]?.GetValue<string>() ?? "",
                        PhotoUrl = b?["photo_url"]?.GetValue<string>() ?? "",
                    });
                }
            }
        }
        catch { }
        return data;
    }

    /// <summary>
    /// Pre-event download: pulls teams, games, champions, rivalries, aliases,
    /// and events into leagueBundle.json for offline use. Returns a human
    /// summary like "12 teams, 58 games" or null on failure.
    /// </summary>
    public static async Task<string?> DownloadBundleAsync(GameSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.SupabaseUrl)
            || string.IsNullOrWhiteSpace(settings.SupabaseAnonKey))
            return null;

        try
        {
            var bundle = new JsonObject { ["fetched_at"] = DateTimeOffset.Now.ToString("O") };
            var counts = new List<string>();
            foreach (var table in new[] { "teams_public", "games", "champions", "rivalries", "team_aliases", "events", "site_content" })
            {
                var request = NewRequest(settings, HttpMethod.Get, $"/rest/v1/{table}?select=*");
                var response = await _http.SendAsync(request);
                if (!response.IsSuccessStatusCode) return null;

                var rows = JsonNode.Parse(await response.Content.ReadAsStringAsync())!.AsArray();
                bundle[table] = rows;
                counts.Add($"{rows.Count} {table.Replace("teams_public", "teams").Replace("team_aliases", "aliases")}");
            }
            await File.WriteAllTextAsync(BundleFile, bundle.ToJsonString());
            _logoUrlByName = null; // fresh bundle — rebuild the logo map on next use
            return string.Join(", ", counts);
        }
        catch
        {
            return null;
        }
    }
}
