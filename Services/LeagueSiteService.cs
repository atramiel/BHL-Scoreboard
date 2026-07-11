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
            return response.IsSuccessStatusCode;
        }
        catch
        {
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
        try
        {
            if (!LoadLogoMap().TryGetValue(teamName.Trim(), out var url)) return null;

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
            foreach (var table in new[] { "teams_public", "games", "champions", "rivalries", "team_aliases", "events" })
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
