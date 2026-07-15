using CommunityToolkit.Mvvm.ComponentModel;
using QRCoder;
using Scoreboard.Helpers;
using Scoreboard.Models;
using Scoreboard.Services;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Scoreboard.ViewModels;

public class AttractItem
{
    public ImageSource? Logo { get; set; }
    public string Text { get; set; } = "";
    // Set only for matchup rows (Upcoming Matches) — the visitor team's logo,
    // rendered on the opposite side of the row from Logo. Null everywhere else.
    public ImageSource? Logo2 { get; set; }
}

public class AttractPanel
{
    public string Title { get; set; } = "";
    public List<AttractItem> Items { get; set; } = [];
    public string Body { get; set; } = "";   // text panels (What is Bot Hockey) use this instead of Items
}

public class BetweenGameViewModel : ObservableObject
{
    private Timer? _timer;
    private Timer? _attractTimer;

    // Attract carousel: a weighted-random pool of panels; each enters on the
    // left, shifts to the right on the next tick, then cycles off. Trophy
    // Case, Today's Results, and About are the "main" panels and appear far
    // more often than the rest (rivalries, per-team spotlights).
    private readonly Random _attractRand = new();
    private List<(AttractPanel Panel, double Weight)> _attractWeights = [];

    private bool _hasAttract;
    public bool HasAttract
    {
        get => _hasAttract;
        set => SetProperty(ref _hasAttract, value);
    }
    private AttractPanel? _currentLeft;
    public AttractPanel? CurrentLeft
    {
        get => _currentLeft;
        set => SetProperty(ref _currentLeft, value);
    }
    private AttractPanel? _currentRight;
    public AttractPanel? CurrentRight
    {
        get => _currentRight;
        set => SetProperty(ref _currentRight, value);
    }

    private BitmapSource? _bracketQRCode;
    public BitmapSource? BracketQRCode
    {
        get => _bracketQRCode;
        set => SetProperty(ref _bracketQRCode, value);
    }

    private BitmapSource? _learnMoreQRCode;
    public BitmapSource? LearnMoreQRCode
    {
        get => _learnMoreQRCode;
        set => SetProperty(ref _learnMoreQRCode, value);
    }

    private TimeSpan _nextMatchTime = TimeSpan.FromMinutes(5);
    public TimeSpan NextMatchTime
    {
        get => _nextMatchTime;
        set
        {
            SetProperty(ref _nextMatchTime, value);
            OnPropertyChanged(nameof(StartsAtDisplay));
            UpdatePaceWarning();
        }
    }

    public string StartsAtDisplay => $"Starts at {(DateTime.Now + NextMatchTime):h:mm tt}";

    // Pace Tracker's suggested break at a detected round boundary — set once per
    // boundary, then the operator can freely dial the timer up or down from there.
    private TimeSpan? _suggestedBreak;
    private TimeSpan _driftAtSuggestion;

    private string _paceWarningText = "";
    public string PaceWarningText
    {
        get => _paceWarningText;
        set => SetProperty(ref _paceWarningText, value);
    }

    public void SetPaceSuggestion(TimeSpan suggestedBreak, TimeSpan driftAtSuggestion)
    {
        _suggestedBreak = suggestedBreak;
        _driftAtSuggestion = driftAtSuggestion;
        NextMatchTime = suggestedBreak; // also updates the warning via the setter above
    }

    // Never blocks the operator from choosing more time than suggested — just keeps
    // them informed, same non-blocking spirit as every other override in this app.
    private void UpdatePaceWarning()
    {
        if (_suggestedBreak == null) { PaceWarningText = ""; return; }

        var extra = NextMatchTime - _suggestedBreak.Value;
        if (extra <= TimeSpan.Zero) { PaceWarningText = ""; return; }

        var projectedDrift = _driftAtSuggestion + extra;
        var extraMin = (int)Math.Round(extra.TotalMinutes);
        var projMin = (int)Math.Round(Math.Abs(projectedDrift.TotalMinutes));
        PaceWarningText = projectedDrift > TimeSpan.Zero
            ? $"That's {extraMin} min more than suggested — you'd be running ~{projMin} min behind pace after this break."
            : $"That's {extraMin} min more than suggested.";
    }

    private bool _isCountingDown;
    public bool IsCountingDown
    {
        get => _isCountingDown;
        set => SetProperty(ref _isCountingDown, value);
    }

    private string _nextUpDisplay = "";
    public string NextUpDisplay
    {
        get => _nextUpDisplay;
        set => SetProperty(ref _nextUpDisplay, value);
    }

    public event EventHandler? CountdownComplete;

    private readonly GameSettings? _leagueSettings;

    public BetweenGameViewModel(string? bracketUrl, string? learnMoreUrl, GameSettings? leagueSettings = null)
    {
        if (!string.IsNullOrWhiteSpace(bracketUrl))
            BracketQRCode = GenerateQR(bracketUrl);
        if (!string.IsNullOrWhiteSpace(learnMoreUrl))
            LearnMoreQRCode = GenerateQR(learnMoreUrl);

        _leagueSettings = leagueSettings;
        // Attract rotation kicks in when league data has been downloaded
        _ = LoadAttractAsync();
    }

    private async Task LoadAttractAsync()
    {
        // Refresh the local bundle right now — otherwise Today's Results races
        // the background refresh kicked off when the last game posted, and
        // loses if the between-game screen opens within a few seconds of it.
        if (_leagueSettings != null
            && !string.IsNullOrWhiteSpace(_leagueSettings.SupabaseUrl)
            && !string.IsNullOrWhiteSpace(_leagueSettings.SupabaseAnonKey))
            await LeagueSiteService.DownloadBundleAsync(_leagueSettings);

        var attract = LeagueSiteService.LoadAttractData();

        // Live rankings + upcoming-matches panels — independent of league-site data,
        // they only need the Challonge bracket URL(s) + API key already in Settings.
        var standingsPanels = new List<AttractPanel>();
        if (!string.IsNullOrWhiteSpace(_leagueSettings?.ChallongeApiKey))
        {
            if (!string.IsNullOrWhiteSpace(_leagueSettings.BracketUrl))
            {
                if (await BuildStandingsPanelAsync("🏆 LIVE RANKINGS", _leagueSettings.BracketUrl, _leagueSettings.ChallongeApiKey) is { } mainStandings)
                    standingsPanels.Add(mainStandings);
                if (await BuildUpcomingPanelAsync("⏭ UPCOMING MATCHES", _leagueSettings.BracketUrl, _leagueSettings.ChallongeApiKey) is { } mainUpcoming)
                    standingsPanels.Add(mainUpcoming);
            }
            foreach (var (name, url) in ParseSecondaryBrackets(_leagueSettings.SecondaryBracketsRaw))
            {
                if (await BuildStandingsPanelAsync(name, url, _leagueSettings.ChallongeApiKey) is { } secondaryStandings)
                    standingsPanels.Add(secondaryStandings);
                if (await BuildUpcomingPanelAsync($"⏭ {name} — Upcoming", url, _leagueSettings.ChallongeApiKey) is { } secondaryUpcoming)
                    standingsPanels.Add(secondaryUpcoming);
            }
        }

        var others = new List<AttractPanel>();
        var spotlights = new List<AttractPanel>();
        if (!attract.HasData)
        {
            BuildRotation(null, null, standingsPanels, others, spotlights);
            return;
        }

        async Task<List<AttractItem>> ItemsAsync(IEnumerable<LeagueSiteService.AttractLine> lines)
        {
            var items = new List<AttractItem>();
            foreach (var line in lines)
            {
                var item = new AttractItem { Text = line.Text, Logo = await TeamLogos.LoadAsync(line.Team) };
                if (line.OpponentTeam.Length > 0) item.Logo2 = await TeamLogos.LoadAsync(line.OpponentTeam);
                items.Add(item);
            }
            return items;
        }

        var today = await ItemsAsync(attract.TodayResults);
        if (today.Count == 0)
            today.Add(new AttractItem { Text = "No games yet today — stay tuned!" });

        var todayPanel = new AttractPanel { Title = "TODAY'S RESULTS", Items = today };
        AttractPanel? aboutPanel = attract.AboutBody.Length > 0
            ? new AttractPanel { Title = attract.AboutTitle, Body = attract.AboutBody }
            : null;

        // Everything else shares the remaining weight evenly: brackets, trophy case,
        // one panel per rivalry, one panel per team spotlight.
        var trophies = await ItemsAsync(attract.TrophyCase);
        if (trophies.Count > 0) others.Add(new AttractPanel { Title = "🏆 TROPHY CASE", Items = trophies });

        foreach (var rv in attract.Rivalries)
        {
            var items = new List<AttractItem>
            {
                new() { Text = rv.TeamA, Logo = await TeamLogos.LoadAsync(rv.TeamA) },
                new() { Text = rv.TeamB, Logo = await TeamLogos.LoadAsync(rv.TeamB) },
            };
            others.Add(new AttractPanel { Title = "🔥 RIVALRY", Items = items, Body = rv.Story });
        }

        // Team Spotlight — one team at a time: bio (motto, home town, established,
        // features) plus that team's own bot roster, so bots aren't a separate panel
        foreach (var t in attract.TeamSpotlights.Take(12))
        {
            var facts = string.Join(" · ", new[] { t.HomeTown, t.Established.Length > 0 ? $"Est. {t.Established}" : "" }
                .Where(s => !string.IsNullOrWhiteSpace(s)));
            var body = new List<string>();
            if (t.Motto.Length > 0) body.Add($"“{t.Motto}”");
            if (t.SpecialFeatures.Length > 0) body.Add(t.SpecialFeatures);

            var items = new List<AttractItem> { new() { Text = facts, Logo = await TeamLogos.LoadAsync(t.Team) } };
            foreach (var b in attract.Bots.Where(b => b.Team == t.Team).Take(6))
            {
                var specs = string.Join(" · ", new[] { b.Weight, b.Weapon }.Where(s => !string.IsNullOrWhiteSpace(s)));
                var botLogo = b.PhotoUrl.Length > 0 ? await TeamLogos.LoadFromUrlAsync(b.PhotoUrl) : null;
                items.Add(new AttractItem { Text = specs.Length > 0 ? $"🤖 {b.Name} — {specs}" : $"🤖 {b.Name}", Logo = botLogo });
            }
            spotlights.Add(new AttractPanel { Title = t.Team, Items = items, Body = string.Join("\n\n", body) });
        }

        BuildRotation(aboutPanel, todayPanel, standingsPanels, others, spotlights);
    }

    // Rankings/upcoming panels get their own fixed slice (~20% each) alongside
    // About/Today's Results — PickPanel normalizes by whatever the weights
    // actually sum to, so this doesn't need to "fit" a 1.0 budget.
    private const double LiveChallongeWeightEach = 0.20;

    // Weighted draw: About 25%, Today's Results 20% (when league data loaded),
    // rankings/upcoming get a boosted fixed slice each (see above), the remainder
    // split evenly across trophy case / rivalries / team spotlights. With no
    // league data, rankings/upcoming alone can still rotate.
    private void BuildRotation(AttractPanel? aboutPanel, AttractPanel? todayPanel, List<AttractPanel> standingsPanels, List<AttractPanel> others, List<AttractPanel> spotlights)
    {
        _attractWeights = [];
        if (aboutPanel != null) _attractWeights.Add((aboutPanel, 0.25));
        if (todayPanel != null) _attractWeights.Add((todayPanel, 0.20));
        foreach (var p in standingsPanels) _attractWeights.Add((p, LiveChallongeWeightEach));
        var combinedCount = others.Count + spotlights.Count;
        if (combinedCount > 0)
        {
            var usedWeight = _attractWeights.Sum(w => w.Weight);
            var each = Math.Max(0, 1.0 - usedWeight) / combinedCount;
            foreach (var p in others) _attractWeights.Add((p, each));
            foreach (var p in spotlights) _attractWeights.Add((p, each));
        }

        if (_attractWeights.Count < 2) return;

        CurrentLeft = PickPanel(null);
        CurrentRight = PickPanel(CurrentLeft);
        HasAttract = true;
        _lastAttractSwap = DateTime.Now;
        _currentAttractDwell = DefaultAttractDwell;
        _attractTimer = new Timer(SwapAttractPanels, null, DefaultAttractDwell, Timeout.InfiniteTimeSpan);
    }

    private static readonly TimeSpan DefaultAttractDwell = TimeSpan.FromSeconds(15);
    private DateTime _lastAttractSwap;
    private TimeSpan _currentAttractDwell;

    private void SwapAttractPanels(object? state)
    {
        _lastAttractSwap = DateTime.Now;
        _currentAttractDwell = DefaultAttractDwell;
        CurrentRight = CurrentLeft;
        CurrentLeft = PickPanel(CurrentRight);
        _attractTimer?.Change(DefaultAttractDwell, Timeout.InfiniteTimeSpan);
    }

    /// <summary>
    /// Called by the view once it knows a freshly-cycled-in panel needs longer than
    /// the default dwell — e.g. auto-scrolling a long bio/roster at a readable pace —
    /// so it isn't yanked away mid-scroll. Only ever extends this cycle, never shortens it.
    /// </summary>
    public void ExtendCurrentAttractDwell(TimeSpan minDwell)
    {
        if (_attractTimer == null || minDwell <= _currentAttractDwell) return;
        _currentAttractDwell = minDwell;
        var remaining = minDwell - (DateTime.Now - _lastAttractSwap);
        if (remaining < TimeSpan.FromMilliseconds(200)) remaining = TimeSpan.FromMilliseconds(200);
        _attractTimer.Change(remaining, Timeout.InfiniteTimeSpan);
    }

    // Live standings, computed from completed matches via the Challonge API — sorted
    // by wins (then fewer losses, then name). This is a straightforward win-loss
    // read, not a reproduction of Challonge's own official tiebreaker math, which
    // is enough to be useful at a glance without risking a different-looking order
    // than the site for a rare close tie.
    private static async Task<AttractPanel?> BuildStandingsPanelAsync(string title, string bracketUrl, string apiKey)
    {
        var standings = await ChallongeService.FetchStandingsAsync(bracketUrl, apiKey);
        if (standings.Count == 0) return null;

        var items = new List<AttractItem>();
        var rank = 1;
        foreach (var s in standings)
        {
            var record = s.Ties > 0 ? $"{s.Wins}-{s.Losses}-{s.Ties}" : $"{s.Wins}-{s.Losses}";
            items.Add(new AttractItem { Text = $"{rank}. {s.Team} — {record}", Logo = await TeamLogos.LoadAsync(s.Team) });
            rank++;
        }
        return new AttractPanel { Title = title, Items = items };
    }

    // Upcoming (not-yet-played) matches — reuses the same open-matches fetch the
    // between-game match-select screen already calls, so no new Challonge query
    // shape, just a different panel around the same data. Each team gets its own
    // row with its logo (AttractItem only carries one logo each), so this is
    // capped at 3 matches (6 rows) to stay in line with how much other panels
    // show — the full open-matches list (up to 6) still drives match selection.
    private static async Task<AttractPanel?> BuildUpcomingPanelAsync(string title, string bracketUrl, string apiKey)
    {
        var matches = await ChallongeService.FetchOpenMatchesAsync(bracketUrl, apiKey);
        if (matches.Count == 0) return null;

        var items = new List<AttractItem>();
        foreach (var m in matches.Take(3))
        {
            items.Add(new AttractItem
            {
                Text = $"{m.Player1Name} vs {m.Player2Name}",
                Logo = await TeamLogos.LoadAsync(m.Player1Name),
                Logo2 = await TeamLogos.LoadAsync(m.Player2Name)
            });
        }
        return new AttractPanel { Title = title, Items = items };
    }

    private static IEnumerable<(string Name, string Url)> ParseSecondaryBrackets(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) yield break;
        foreach (var line in raw.Split('\n'))
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0) continue;
            var idx = trimmed.IndexOf('=');
            if (idx < 0) continue;
            var name = trimmed[..idx].Trim();
            var url = trimmed[(idx + 1)..].Trim();
            if (name.Length > 0 && url.Length > 0) yield return (name, url);
        }
    }

    /// <summary>Weighted random pick, avoiding an immediate repeat when possible.</summary>
    private AttractPanel PickPanel(AttractPanel? avoid)
    {
        for (var attempt = 0; attempt < 8; attempt++)
        {
            var roll = _attractRand.NextDouble() * _attractWeights.Sum(w => w.Weight);
            var cumulative = 0.0;
            var chosen = _attractWeights[^1].Panel;
            foreach (var (panel, weight) in _attractWeights)
            {
                cumulative += weight;
                if (roll <= cumulative) { chosen = panel; break; }
            }
            if (avoid == null || !ReferenceEquals(chosen, avoid) || _attractWeights.Count <= 1)
                return chosen;
        }
        return _attractWeights[_attractRand.Next(_attractWeights.Count)].Panel;
    }

    private static BitmapSource? GenerateQR(string url)
    {
        try
        {
            var qrGenerator = new QRCodeGenerator();
            var qrCodeData = qrGenerator.CreateQrCode(url, QRCodeGenerator.ECCLevel.Q);
            var qrCode = new PngByteQRCode(qrCodeData);
            var pngBytes = qrCode.GetGraphic(10);

            using var ms = new MemoryStream(pngBytes);
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.StreamSource = ms;
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }
        catch
        {
            return null;
        }
    }

    public void Adjust(int deltaMinutes)
    {
        var newTime = NextMatchTime + TimeSpan.FromMinutes(deltaMinutes);
        if (newTime < TimeSpan.FromMinutes(1)) newTime = TimeSpan.FromMinutes(1);
        if (newTime > TimeSpan.FromMinutes(99)) newTime = TimeSpan.FromMinutes(99);
        NextMatchTime = newTime;
    }

    public void StartCountdown()
    {
        if (IsCountingDown) return;
        IsCountingDown = true;
        _timer = new Timer(Tick, null, 1000, 1000);
    }

    private void Tick(object? state)
    {
        if (NextMatchTime <= TimeSpan.Zero)
        {
            _timer?.Dispose();
            _timer = null;
            IsCountingDown = false;
            CountdownComplete?.Invoke(this, EventArgs.Empty);
            return;
        }
        NextMatchTime -= TimeSpan.FromSeconds(1);
    }

    public void Dispose()
    {
        _timer?.Dispose();
        _attractTimer?.Dispose();
    }
}
