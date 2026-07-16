using CommunityToolkit.Mvvm.ComponentModel;
using System.IO;
using System.Windows.Media;

namespace Scoreboard.ViewModels;

/// <summary>
/// A hardcoded slide deck the operator pages through with one Stream Deck button
/// before a game — welcome, sponsors, rules, safety reminders, then introducing
/// each team and the audience separately. Slides come from Settings
/// (PreGameSpeechSlidesRaw, "---" separated); {home}/{away} show that team's name,
/// logo, and tint the background to that team's configured color; {sponsors}
/// shows the configured sponsor logo images; {branding} shows the BHL crest plus
/// the event logo. Slides without any of those get a background cycling through
/// a general vivid palette, so the background always reflects what's actually
/// on screen rather than just rotating for its own sake.
/// </summary>
public class PreGameSpeechViewModel : ObservableObject
{
    private static readonly Brush BrandingBackground =
        MakeGradient(Color.FromRgb(0x3a, 0x1f, 0x00), Color.FromRgb(0xe8, 0xb3, 0x4c)); // dark bronze -> BHL gold

    private static readonly Brush SponsorsBackground =
        MakeGradient(Color.FromRgb(0x14, 0x1a, 0x24), Color.FromRgb(0x3a, 0x46, 0x58)); // neutral slate, keeps logos readable

    private static readonly Brush[] GeneralBackgrounds =
    [
        MakeGradient(Color.FromRgb(0x0a, 0x2a, 0x43), Color.FromRgb(0x1c, 0x92, 0xd2)), // navy -> cyan
        MakeGradient(Color.FromRgb(0x5c, 0x11, 0x0a), Color.FromRgb(0xe8, 0x53, 0x2c)), // deep red -> orange
        MakeGradient(Color.FromRgb(0x0b, 0x4a, 0x2e), Color.FromRgb(0x1f, 0xc9, 0x7c)), // forest -> teal green
        MakeGradient(Color.FromRgb(0x5c, 0x0a, 0x3a), Color.FromRgb(0xe8, 0x2c, 0x8a)), // maroon -> hot pink
    ];

    private static Brush MakeGradient(Color a, Color b)
    {
        var brush = new LinearGradientBrush(a, b, 45);
        brush.Freeze();
        return brush;
    }

    /// <summary>A dark-to-team-color gradient so a team's own scoreboard color shows up here too.</summary>
    private static Brush MakeTeamGradient(Brush? teamBrush)
    {
        var color = (teamBrush as SolidColorBrush)?.Color ?? Colors.SlateGray;
        return MakeGradient(Color.FromRgb(0x12, 0x12, 0x16), color);
    }

    // Each slide (by position — slide 1, slide 2, ...) gets its own folder of up
    // to 4 GIFs to randomly pick from, e.g. Resources/PreGameSlideGifs/Slide1/*.gif.
    // Lives under Resources/ (not bin/) and is wildcard-included + copied to the
    // output directory in the .csproj, so GIFs dropped in there are tracked by git
    // and travel with the repo to a new machine — unlike a bin/-only runtime folder,
    // which would never get committed. Reordering or adding/removing slides in
    // Settings shifts which folder applies to which slide, since this is purely
    // positional, not tied to slide content. Missing or empty folders just mean no
    // GIF for that slide — never an error.
    private const string GifRootDir = "Resources/PreGameSlideGifs";
    private static readonly Random GifRand = new();

    private readonly List<string> _slides;
    private readonly string _homeTeam;
    private readonly string _awayTeam;
    private readonly Brush _homeBackground;
    private readonly Brush _awayBackground;
    private int _index;

    public ImageSource? HomeLogo { get; }
    public ImageSource? AwayLogo { get; }
    public ImageSource? EventLogo { get; }
    public List<ImageSource> SponsorLogos { get; }

    /// <summary>"3 / 10" — shown on the Stream Deck button itself so the operator knows where they are without looking at the audience screen.</summary>
    public string ProgressText => $"{_index + 1} / {_slides.Count}";

    public PreGameSpeechViewModel(string? slidesRaw, string homeTeam, string awayTeam,
        ImageSource? homeLogo, ImageSource? awayLogo, ImageSource? eventLogo,
        List<ImageSource> sponsorLogos, Brush? homeTeamColor, Brush? awayTeamColor)
    {
        _homeTeam = homeTeam;
        _awayTeam = awayTeam;
        HomeLogo = homeLogo;
        AwayLogo = awayLogo;
        EventLogo = eventLogo;
        SponsorLogos = sponsorLogos;
        _homeBackground = MakeTeamGradient(homeTeamColor);
        _awayBackground = MakeTeamGradient(awayTeamColor);
        _slides = ParseSlides(slidesRaw);
        if (_slides.Count == 0) _slides.Add("");
    }

    private string _currentText = "";
    public string CurrentText
    {
        get => _currentText;
        private set => SetProperty(ref _currentText, value);
    }

    private bool _showHomeLogo;
    public bool ShowHomeLogo
    {
        get => _showHomeLogo;
        private set => SetProperty(ref _showHomeLogo, value);
    }

    private bool _showAwayLogo;
    public bool ShowAwayLogo
    {
        get => _showAwayLogo;
        private set => SetProperty(ref _showAwayLogo, value);
    }

    private bool _showSponsors;
    public bool ShowSponsors
    {
        get => _showSponsors;
        private set => SetProperty(ref _showSponsors, value);
    }

    private bool _showBranding;
    public bool ShowBranding
    {
        get => _showBranding;
        private set => SetProperty(ref _showBranding, value);
    }

    private Brush _background = GeneralBackgrounds[0];
    public Brush Background
    {
        get => _background;
        private set => SetProperty(ref _background, value);
    }

    private string? _currentGifPath;
    public string? CurrentGifPath
    {
        get => _currentGifPath;
        private set => SetProperty(ref _currentGifPath, value);
    }

    public void ShowFirstSlide() => ShowSlide(0);

    /// <summary>Advances to the next slide. Returns false if already on the last slide (nothing more to show — caller should close and move on).</summary>
    public bool Advance()
    {
        if (_index >= _slides.Count - 1) return false;
        ShowSlide(_index + 1);
        return true;
    }

    private static readonly string[] BrandingGlyphs = ["🎉", "🎊", "✨", "🏒", "🥇"];
    private static readonly string[] SponsorGlyphs = ["💵", "💰", "⭐", "🙌"];
    private static readonly string[] CautionGlyphs = ["⚠️", "🚧", "✋", "🛑"];
    private static readonly string[] TeamGlyphs = ["🔥", "⚡", "🏆"];
    private static readonly string[] HypeGlyphs = ["📣", "🎬", "🔥", "🚀"];
    private static readonly string[] GeneralGlyphs = ["🎉", "🎊", "✨", "🏒"];

    /// <summary>Which emoji fall as confetti on this slide — set alongside Background so both track what's actually on screen.</summary>
    private string[] _confettiGlyphs = GeneralGlyphs;
    public string[] ConfettiGlyphs
    {
        get => _confettiGlyphs;
        private set => SetProperty(ref _confettiGlyphs, value);
    }

    private void ShowSlide(int index)
    {
        _index = index;
        var raw = _slides[index];
        ShowHomeLogo = raw.Contains("{home}");
        ShowAwayLogo = raw.Contains("{away}");
        ShowSponsors = raw.Contains("{sponsors}");
        ShowBranding = raw.Contains("{branding}");
        var isCaution = raw.Contains("{caution}");
        var isHype = raw.Contains("{hype}");

        // Set before CurrentText — the window reacts to CurrentText changing and
        // reads Background/ConfettiGlyphs synchronously at that point, so those
        // need to already reflect the new slide by then.
        Background = ShowBranding ? BrandingBackground
            : ShowSponsors ? SponsorsBackground
            : ShowHomeLogo ? _homeBackground
            : ShowAwayLogo ? _awayBackground
            : GeneralBackgrounds[index % GeneralBackgrounds.Length];

        ConfettiGlyphs = ShowBranding ? BrandingGlyphs
            : ShowSponsors ? SponsorGlyphs
            : isCaution ? CautionGlyphs
            : ShowHomeLogo || ShowAwayLogo ? TeamGlyphs
            : isHype ? HypeGlyphs
            : GeneralGlyphs;

        CurrentText = raw
            .Replace("{home}", _homeTeam)
            .Replace("{away}", _awayTeam)
            .Replace("{sponsors}", "")
            .Replace("{branding}", "")
            .Replace("{caution}", "")
            .Replace("{hype}", "")
            .Trim();

        CurrentGifPath = PickRandomGif(index);
    }

    private static string? PickRandomGif(int index)
    {
        try
        {
            var dir = Path.Combine(GifRootDir, $"Slide{index + 1}");
            if (!Directory.Exists(dir)) return null;
            var gifs = Directory.GetFiles(dir, "*.gif");
            if (gifs.Length == 0) return null;
            // WPF's image/URI resolution treats a bare relative string as relative to
            // the app's pack resources, not the current working directory the way
            // File/Directory APIs do — so a relative path here silently fails to
            // resolve as an ImageSource. Absolute path avoids that ambiguity entirely.
            return Path.GetFullPath(gifs[GifRand.Next(gifs.Length)]);
        }
        catch { return null; }
    }

    private static List<string> ParseSlides(string? raw)
    {
        var slides = new List<string>();
        if (string.IsNullOrWhiteSpace(raw)) return slides;
        var current = new List<string>();
        foreach (var line in raw.Replace("\r\n", "\n").Split('\n'))
        {
            if (line.Trim() == "---")
            {
                var slide = string.Join("\n", current).Trim();
                if (slide.Length > 0) slides.Add(slide);
                current.Clear();
            }
            else
            {
                current.Add(line);
            }
        }
        var last = string.Join("\n", current).Trim();
        if (last.Length > 0) slides.Add(last);
        return slides;
    }
}
