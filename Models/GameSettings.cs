using Scoreboard.Enums;
using System.Windows.Input;
using System.Windows.Media;

namespace Scoreboard.Models;

public class GameSettings
{
    public int GameLengthMinutes { get; set; } = 10;
    public TimingMode TimingMode { get; set; } = TimingMode.StopTime;
    public string? HomeTeamName { get; set; } = "Home";
    public string? VisitorTeamName { get; set; } = "Visitor";
    public int PenaltyLengthMinutes { get; set; } = 2;
    public Dictionary<GameAction, Key> KeyBindings = new Dictionary<GameAction, Key>()
    {
        {GameAction.IncreaseHome, Key.PageUp },
        {GameAction.IncreaseAway, Key.PageDown },
        {GameAction.PenalizeHome, Key.Home },
        {GameAction.PenalizeAway, Key.End },
        {GameAction.PlayPause, Key.Space },
        {GameAction.Undo, Key.Back },
        {GameAction.Redo, Key.Enter },
        {GameAction.Reset, Key.Delete },
        {GameAction.ResetClock, Key.RWin },
        {GameAction.SwapSides, Key.OemTilde },
        {GameAction.BetweenGame, Key.B },
    };
    public readonly Dictionary<string, Brush> StringToColor = new()
        {
            {"Red", Brushes.Red},
            {"Orange",  Brushes.Orange},
            {"Yellow",  Brushes.Yellow},
            {"Green",  Brushes.Green },
            {"Blue",  Brushes.Blue },
            {"Indigo",  Brushes.Indigo },
            {"Violet",  Brushes.Violet },
            {"White",  Brushes.White },
        };
    public string? LedAddress { get; set; } = $"C:\\Users\\user\\AppData\\Local\\VortxEngine";
    public bool IsKioskMode { get; set; }
    public bool SoundEnabled { get; set; } = true;
    public bool HalfTimeEnabled { get; set; } = true;
    public string? BracketUrl { get; set; }
    // Other Challonge tournaments to also show live standings for in Attract Mode
    // (e.g. a 4th-8th place bracket), one per line as "Name = URL". Reuses the same
    // ChallongeApiKey above; never used for match selection or reporting, just standings.
    public string? SecondaryBracketsRaw { get; set; }
    public string? LearnMoreUrl { get; set; }
    public string? ChallongeApiKey { get; set; }
    public string? RelayUrl { get; set; }
    public string BackgroundTheme { get; set; } = "Hockey Rink";
    public string? EventName { get; set; }          // current event; attached to every result posted to the league site
    public string? SupabaseUrl { get; set; }        // league website database, e.g. https://xyz.supabase.co
    public string? SupabaseAnonKey { get; set; }    // public read key (safe to share)
    public string? LeagueAdminKey { get; set; }     // secret; authorizes result posting
    public string? DiscordWebhookUrl { get; set; }  // secret — anyone holding this can post to the channel
    public bool DiscordPostFinalScores { get; set; } = true;
    public bool DiscordPostNextUp { get; set; } = true;
    public bool DiscordPostHypePings { get; set; } = true;
    public bool DiscordPostOnDeck { get; set; } = true;
    public string? PaceEventStartTime { get; set; }     // "HH:mm", 24-hour, time of day
    public string? PaceLastGameStartTime { get; set; }  // "HH:mm" — the real scheduling anchor, not a flat "end time"
    public int PaceTotalPlannedMatches { get; set; }    // across ALL brackets played that day, not just the main one
    public string? HomeColor { get; set; } = "White";
    public string? VisitorColor { get; set; } = "White";
    // Pre-Game Speech slide deck — one slide per press of the Stream Deck button,
    // slides separated by a line containing just "---". {home}/{away} in a slide's
    // text get that team's name substituted in and show that team's logo big
    // (background tints to that team's configured color); {sponsors} shows the
    // configured sponsor logo images; {branding} shows the BHL crest plus the
    // configured event logo. {caution}/{hype} pick a matching confetti glyph set
    // (⚠️ vs 🔥📣 etc.) without changing the background. Pressing past the last
    // slide starts the between-game countdown.
    public string? PreGameSpeechSlidesRaw { get; set; } =
        "{branding}\n🎉 Welcome to the Bot Hockey League 2026 Nationals! 🎉\n" +
        "---\n" +
        "🙌 Thank you to our sponsors! 🙌\n{sponsors}\n" +
        "---\n" +
        "🤖 What is Bot Hockey? 🏒\n2 teams of 3 robots enter the arena and try to score as many goals as they can against the other team in 10 minutes. Full contact!\n" +
        "---\n" +
        "{caution}\n✋ Do not touch the arena, but please cheer! 📣\n" +
        "---\n" +
        "{caution}\n⚠️ People will reach into the arena — those people are part of the teams and know what they're doing. Don't be like them! ⚠️\n" +
        "---\n" +
        "⏱️ We will be switching sides at halftime, 5 minutes. ⏱️\n" +
        "---\n" +
        "🔥 {home}, are you ready?! 🔥\n" +
        "---\n" +
        "🔥 {away}, are you ready?! 🔥\n" +
        "---\n" +
        "{hype}\n📣 AUDIENCE, ARE YOU READY?! 📣\n" +
        "---\n" +
        "{hype}\n🎬 Count down with me, everyone! 🎬";

    // Local copies of uploaded sponsor logo images (see Settings → "Add Sponsor
    // Logo"), shown on any Pre-Game Speech slide containing {sponsors}.
    public List<string> SponsorLogoPaths { get; set; } = [];

    // This event's own logo (separate from the BHL league crest, which is a
    // bundled app resource) — shown alongside it on any slide containing {branding}.
    public string? EventLogoPath { get; set; }



    private string _gameRunning = "Rainbow";
    public string GameRunningEffect
    {
        get => _gameRunning;
        set => EncodeProperty(ref _gameRunning, value);
    }

    private string _gameStopped = "Solid%20Color";
    public string GameStoppedEffect
    {
        get => _gameStopped;
        set => EncodeProperty(ref _gameStopped, value);
    }
    private string _penatlyAdd = "Rainbow";
    public string PenaltyAddEffect
    {
        get => _penatlyAdd;
        set => EncodeProperty(ref _penatlyAdd, value);
    }
    private string _penaltyDrop = "Pipeline";
    public string PenaltyDropEffect
    {
        get => _penaltyDrop;
        set => EncodeProperty(ref _penaltyDrop, value);
    }
    private string _gameOver = "Bullet%20Hell";
    public string GameOverEffect
    {
        get => _gameOver;
        set => EncodeProperty(ref _gameOver, value);
    }
    private string _suddenDeath = "Side%20To%20Side";
    public string SuddenDeathEffect
    {
        get => _suddenDeath;
        set => EncodeProperty(ref _suddenDeath, value);
    }
    private string _homeScore = "Screen%20Ambience";
    public string HomeScoreEffect
    {
        get => _homeScore;
        set => EncodeProperty(ref _homeScore, value);
    }
    private string _visitorScore = "Radar";
    public string VisitorScoreEffect
    {
        get => _visitorScore;
        set => EncodeProperty(ref _visitorScore, value);
    }
    private string _slow = "Rgbarz";
    public string SlowPulseEffect
    {
        get => _slow;
        set => EncodeProperty(ref _slow, value);
    }
    private string _medium = "Radar";
    public string MediumPulseEffect
    {
        get => _medium;
        set => EncodeProperty(ref _medium, value);
    }
    private string _fast = "Neon%20Shift";
    public string FastPulseEffect
    {
        get => _fast;
        set => EncodeProperty(ref _fast, value);
    }

    private void EncodeProperty(ref string result, string value)
    {
        if (value.Contains(" "))
            result = value.Replace(" ", "%20");
        else
            result = value;
    }
}