using System.Windows.Input;
using System.Windows.Media;

namespace Scoreboard.Models;

public class GameSettings
{
    public int GameLengthMinutes { get; set; } = 10;
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
    public string? BracketUrl { get; set; }
    public string? LearnMoreUrl { get; set; }
    public string? HomeColor { get; set; } = "White";
    public string? VisitorColor { get; set; } = "White";



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