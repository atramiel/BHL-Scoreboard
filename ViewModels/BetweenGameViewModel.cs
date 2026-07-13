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
        }
    }

    public string StartsAtDisplay => $"Starts at {(DateTime.Now + NextMatchTime):h:mm tt}";

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
        if (!attract.HasData) return;

        async Task<List<AttractItem>> ItemsAsync(IEnumerable<LeagueSiteService.AttractLine> lines)
        {
            var items = new List<AttractItem>();
            foreach (var line in lines)
                items.Add(new AttractItem { Text = line.Text, Logo = await TeamLogos.LoadAsync(line.Team) });
            return items;
        }

        var today = await ItemsAsync(attract.TodayResults);
        if (today.Count == 0)
            today.Add(new AttractItem { Text = "No games yet today — stay tuned!" });

        var todayPanel = new AttractPanel { Title = "TODAY'S RESULTS", Items = today };
        AttractPanel? aboutPanel = attract.AboutBody.Length > 0
            ? new AttractPanel { Title = attract.AboutTitle, Body = attract.AboutBody }
            : null;

        // Everything else shares the remaining weight evenly: trophy case,
        // one panel per rivalry, one panel per team spotlight.
        var others = new List<AttractPanel>();
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
            others.Add(new AttractPanel { Title = t.Team, Items = items, Body = string.Join("\n\n", body) });
        }

        // Weighted draw: About 25%, Today's Results 20%, the remaining 55%
        // split evenly across trophy case / rivalries / team spotlights.
        _attractWeights = [];
        if (aboutPanel != null) _attractWeights.Add((aboutPanel, 0.25));
        _attractWeights.Add((todayPanel, 0.20));
        if (others.Count > 0)
        {
            var each = 0.55 / others.Count;
            foreach (var p in others) _attractWeights.Add((p, each));
        }

        if (_attractWeights.Count < 2) return;

        CurrentLeft = PickPanel(null);
        CurrentRight = PickPanel(CurrentLeft);
        HasAttract = true;
        _attractTimer = new Timer(_ =>
        {
            CurrentRight = CurrentLeft;
            CurrentLeft = PickPanel(CurrentRight);
        }, null, 15000, 15000);
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
