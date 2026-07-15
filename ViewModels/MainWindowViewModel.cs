using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Scoreboard.Enums;
using Scoreboard.Helpers;
using Scoreboard.Models;
using Scoreboard.Services;
using Scoreboard.Windows;
using System.Diagnostics;
using System.Linq;
using System.Media;
using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Scoreboard.ViewModels
{
    public class MainWindowViewModel : ObservableObject
    {
        #region Variables
        private readonly int _defaultDelayLengthSeconds = 15;
        private readonly UndoRedo? _undoRedo;
        private readonly SoundPlayer _player = new();
        private readonly SoundPlayer _heartBeatPlayer = new();

        private Timer? _gameTimer;
        private Timer? _homePenaltyOneTimer;
        private Timer? _homePenaltyTwoTimer;
        private Timer? _visitorPenaltyOneTimer;
        private Timer? _visitorPenaltyTwoTimer;
        private Dictionary<GameAction, Key>? _keyBindings;
        private GameSettings _settings = new();
        private TcpBridgeService? _tcpBridge;
        private WebBroadcastService? _webBroadcast;
        private RelayPublisherService? _relayPublisher;
        private StartingGameCountDownViewModel? _finalCountdown;
        private BetweenGameViewModel? _betweenGameViewModel;
        private BetweenGameWindow? _betweenGameWindow;
        private List<PendingMatch> _pendingMatches = [];
        private PendingMatch? _currentMatch;
        private readonly PaceTracker _paceTracker = new();
        private Timer? _ceremonyTimer;
        private int? _ceremonyCountdownSeconds;
        private Windows.AwardsCeremonyWindow? _ceremonyWindow;
        private PendingMatch? _reportedMatch;
        private int _reportedP1Score;
        private int _reportedP2Score;
        #endregion

        #region Properties
        public bool NewGame { get; set; } = true;
        public bool FinalTenSeconds { get; set; } = false;
        [JsonIgnore]
        public IRelayCommand PlayCommand { get; set; }
        [JsonIgnore]
        public IRelayCommand PauseCommand { get; set; }
        [JsonIgnore]
        public IRelayCommand ResetCommand { get; set; }
        [JsonIgnore]
        public IRelayCommand LoadCommand { get; set; }
        [JsonIgnore]
        public IRelayCommand ShowConfigurationCommand { get; set; }
        [JsonIgnore]
        public IRelayCommand<TeamType> AddPenaltyCommand { get; set; }
        [JsonIgnore]
        public IRelayCommand<TeamType> AdvanceScoreCommand { get; set; }
        [JsonIgnore]
        public IRelayCommand<KeyEventArgs> UserInputCommand { get; set; }
        [JsonIgnore]
        public IRelayCommand SwapSidesCommand { get; set; }
        [JsonIgnore]
        public IRelayCommand ResubmitResultCommand { get; set; }
        public bool UseLeds { get; set; }
        #endregion

        #region ObservableProperties
        private string _homeTeam = "HOME"; public string HomeTeam
        {
            get => _homeTeam;
            set => SetProperty(ref _homeTeam, value);
        }
        private string _visitorTeam = "VISITOR"; public string VisitorTeam
        {
            get => _visitorTeam;
            set => SetProperty(ref _visitorTeam, value);
        }
        private TimeSpan _gameClock; public TimeSpan GameClock
        {
            get => _gameClock;
            set => SetProperty(ref _gameClock, value);
        }
        private TimeSpan _countdownClock; public TimeSpan CountDownClock
        {
            get => _countdownClock;
            set => SetProperty(ref _countdownClock, value);
        }
        private bool _isSuddendeath; public bool IsSuddenDeath
        {
            get => _isSuddendeath;
            set => SetProperty(ref _isSuddendeath, value);
        }
        private int _homeScore; public int HomeScore
        {
            get => _homeScore;
            set => SetProperty(ref _homeScore, value);
        }
        private int _visitorScore; public int VisitorScore
        {
            get => _visitorScore;
            set => SetProperty(ref _visitorScore, value);
        }
        private bool _isRunning; public bool IsRunning
        {
            get => _isRunning;
            set => SetProperty(ref _isRunning, value);
        }
        private TimeSpan _homePenaltyOne; public TimeSpan HomePenaltyOne
        {
            get => _homePenaltyOne;
            set => SetProperty(ref _homePenaltyOne, value);
        }
        private TimeSpan _homePenaltyTwo; public TimeSpan HomePenaltyTwo
        {
            get => _homePenaltyTwo;
            set => SetProperty(ref _homePenaltyTwo, value);
        }
        private TimeSpan _visitorPenaltyOne; public TimeSpan VisitorPenaltyOne
        {
            get => _visitorPenaltyOne;
            set => SetProperty(ref _visitorPenaltyOne, value);
        }
        private TimeSpan _visitorPenaltyTwo; public TimeSpan VisitorPenaltyTwo
        {
            get => _visitorPenaltyTwo;
            set => SetProperty(ref _visitorPenaltyTwo, value);
        }
        private bool _activeHomePenaltyOne; public bool ActiveHomePenaltyOne
        {
            get => _activeHomePenaltyOne;
            set => SetProperty(ref _activeHomePenaltyOne, value);
        }
        private bool _activeHomePenaltyTwo; public bool ActiveHomePenaltyTwo
        {
            get => _activeHomePenaltyTwo;
            set => SetProperty(ref _activeHomePenaltyTwo, value);
        }
        private bool _activeVisitorPenaltyOne; public bool ActiveVisitorPenaltyOne
        {
            get => _activeVisitorPenaltyOne;
            set => SetProperty(ref _activeVisitorPenaltyOne, value);
        }
        private bool _activeVisitorPenaltyTwo; public bool ActiveVisitorPenaltyTwo
        {
            get => _activeVisitorPenaltyTwo;
            set => SetProperty(ref _activeVisitorPenaltyTwo, value);
        }
        private bool _isHighTick; public bool IsHighTick
        {
            get => _isHighTick;
            set => SetProperty(ref _isHighTick, value);
        }
        private bool _isFocusLocked; public bool IsFocusLocked
        {
            get => _isFocusLocked;
            set => SetProperty(ref _isFocusLocked, value);
        }
        private bool _highlightHome; public bool HighlightHome
        {
            get => _highlightHome;
            set => SetProperty(ref _highlightHome, value);
        }
        private bool _highlightVisitor; public bool HighlightVisitor
        {
            get => _highlightVisitor;
            set => SetProperty(ref _highlightVisitor, value);
        }
        private bool _clockWithinThirtySecond; public bool ClockWithinThirtySeconds
        {
            get => _clockWithinThirtySecond;
            set => SetProperty(ref _clockWithinThirtySecond, value);
        }
        private bool _clockWithinMinute; public bool ClockWithinMinute
        {
            get => _clockWithinMinute;
            set => SetProperty(ref _clockWithinMinute, value);
        }
        private bool _defaultClock = true; public bool DefaultClock
        {
            get => _defaultClock;
            set => SetProperty(ref _defaultClock, value);
        }
        private bool _gameDone; public bool GameDone
        {
            get => _gameDone;
            set => SetProperty(ref _gameDone, value);
        }
        private Brush _homeColor = Brushes.White;
        [JsonIgnore]
        public Brush HomeColor
        {
            get => _homeColor;
            set => SetProperty(ref _homeColor, value);
        }
        private Brush _visitorColor = Brushes.White;
        [JsonIgnore]
        public Brush VisitorColor
        {
            get => _visitorColor;
            set => SetProperty(ref _visitorColor, value);
        }
        private bool _ledsConnected; public bool IsLedConnected
        {
            get => _ledsConnected;
            set => SetProperty(ref _ledsConnected, value);
        }
        private bool _isReverse; public bool IsReverse
        {
            get => _isReverse;
            set => SetProperty(ref _isReverse, value);
        }
        private bool _isHalfTime; public bool IsHalfTime
        {
            get => _isHalfTime;
            set => SetProperty(ref _isHalfTime, value);
        }
        private bool _halfTimeWarning; public bool HalfTimeWarning
        {
            get => _halfTimeWarning;
            set => SetProperty(ref _halfTimeWarning, value);
        }
        private bool _halfTimeReached; public bool HalfTimeReached
        {
            get => _halfTimeReached;
            set => SetProperty(ref _halfTimeReached, value);
        }
        private bool _halfTimeTaken;
        private int _countdownSeconds; public int CountdownSeconds
        {
            get => _countdownSeconds;
            set => SetProperty(ref _countdownSeconds, value);
        }
        private bool _reportPending;
        [JsonIgnore]
        public bool ReportPending
        {
            get => _reportPending;
            set => SetProperty(ref _reportPending, value);
        }
        private bool _reportSucceeded;
        [JsonIgnore]
        public bool ReportSucceeded
        {
            get => _reportSucceeded;
            set => SetProperty(ref _reportSucceeded, value);
        }
        private bool _reportFailed;
        [JsonIgnore]
        public bool ReportFailed
        {
            get => _reportFailed;
            set => SetProperty(ref _reportFailed, value);
        }
        private bool _scoreChangedSinceReport;
        [JsonIgnore]
        public bool ScoreChangedSinceReport
        {
            get => _scoreChangedSinceReport;
            set => SetProperty(ref _scoreChangedSinceReport, value);
        }
        private bool _showResubmit;
        [JsonIgnore]
        public bool ShowResubmit
        {
            get => _showResubmit;
            set => SetProperty(ref _showResubmit, value);
        }
        private bool _leaguePostPending;
        [JsonIgnore]
        public bool LeaguePostPending
        {
            get => _leaguePostPending;
            set => SetProperty(ref _leaguePostPending, value);
        }
        private bool _leaguePostSucceeded;
        [JsonIgnore]
        public bool LeaguePostSucceeded
        {
            get => _leaguePostSucceeded;
            set => SetProperty(ref _leaguePostSucceeded, value);
        }
        private bool _leaguePostQueued;
        [JsonIgnore]
        public bool LeaguePostQueued
        {
            get => _leaguePostQueued;
            set => SetProperty(ref _leaguePostQueued, value);
        }
        private bool _leaguePosted;
        private Timer? _celebrationTimer;
        private bool _isDramaMode;
        [JsonIgnore]
        public bool IsDramaMode
        {
            get => _isDramaMode;
            set => SetProperty(ref _isDramaMode, value);
        }
        private bool _isChampionship;
        [JsonIgnore]
        public bool IsChampionship
        {
            get => _isChampionship;
            set => SetProperty(ref _isChampionship, value);
        }
        private string _ceremonyCountdownText = "";
        [JsonIgnore]
        public string CeremonyCountdownText
        {
            get => _ceremonyCountdownText;
            set => SetProperty(ref _ceremonyCountdownText, value);
        }
        private bool _isCelebrating;
        [JsonIgnore]
        public bool IsCelebrating
        {
            get => _isCelebrating;
            set => SetProperty(ref _isCelebrating, value);
        }
        private string _celebrationTeamName = "";
        [JsonIgnore]
        public string CelebrationTeamName
        {
            get => _celebrationTeamName;
            set => SetProperty(ref _celebrationTeamName, value);
        }
        private Brush _celebrationColor = Brushes.White;
        [JsonIgnore]
        public Brush CelebrationColor
        {
            get => _celebrationColor;
            set => SetProperty(ref _celebrationColor, value);
        }
        private ImageSource? _homeLogo;
        [JsonIgnore]
        public ImageSource? HomeLogo
        {
            get => _homeLogo;
            set => SetProperty(ref _homeLogo, value);
        }
        private ImageSource? _visitorLogo;
        [JsonIgnore]
        public ImageSource? VisitorLogo
        {
            get => _visitorLogo;
            set => SetProperty(ref _visitorLogo, value);
        }
        private ImageSource? _celebrationLogo;
        [JsonIgnore]
        public ImageSource? CelebrationLogo
        {
            get => _celebrationLogo;
            set => SetProperty(ref _celebrationLogo, value);
        }

        private static readonly Dictionary<string, Brush> _themeMap;

        static MainWindowViewModel()
        {
            // Carbon Fiber: checkerboard of dark/lighter 2x2 squares on an 8x8 tile
            var carbonDraw = new DrawingGroup();
            carbonDraw.Children.Add(new GeometryDrawing(new SolidColorBrush(Color.FromRgb(14, 14, 14)), null, new RectangleGeometry(new Rect(0, 0, 8, 8))));
            carbonDraw.Children.Add(new GeometryDrawing(new SolidColorBrush(Color.FromRgb(26, 26, 26)), null, new RectangleGeometry(new Rect(0, 0, 4, 4))));
            carbonDraw.Children.Add(new GeometryDrawing(new SolidColorBrush(Color.FromRgb(26, 26, 26)), null, new RectangleGeometry(new Rect(4, 4, 4, 4))));
            var carbonBrush = new DrawingBrush(carbonDraw) { TileMode = TileMode.Tile, Viewport = new Rect(0, 0, 8, 8), ViewportUnits = BrushMappingMode.Absolute, Stretch = Stretch.None };
            carbonBrush.Freeze();

            // Grid: dark navy bg with faint blue grid lines every 60px
            var gridDraw = new DrawingGroup();
            gridDraw.Children.Add(new GeometryDrawing(new SolidColorBrush(Color.FromRgb(5, 8, 14)), null, new RectangleGeometry(new Rect(0, 0, 60, 60))));
            var gridLines = new GeometryGroup();
            gridLines.Children.Add(new LineGeometry(new Point(0, 0), new Point(0, 60)));
            gridLines.Children.Add(new LineGeometry(new Point(0, 0), new Point(60, 0)));
            gridDraw.Children.Add(new GeometryDrawing(null, new Pen(new SolidColorBrush(Color.FromArgb(30, 80, 130, 210)), 0.5), gridLines));
            var gridBrush = new DrawingBrush(gridDraw) { TileMode = TileMode.Tile, Viewport = new Rect(0, 0, 60, 60), ViewportUnits = BrushMappingMode.Absolute, Stretch = Stretch.None };
            gridBrush.Freeze();

            // Dots: dark bg with subtle circular dot grid every 28px
            var dotsDraw = new DrawingGroup();
            dotsDraw.Children.Add(new GeometryDrawing(new SolidColorBrush(Color.FromRgb(7, 9, 15)), null, new RectangleGeometry(new Rect(0, 0, 28, 28))));
            dotsDraw.Children.Add(new GeometryDrawing(new SolidColorBrush(Color.FromArgb(70, 100, 130, 200)), null, new EllipseGeometry(new Point(14, 14), 1.5, 1.5)));
            var dotsBrush = new DrawingBrush(dotsDraw) { TileMode = TileMode.Tile, Viewport = new Rect(0, 0, 28, 28), ViewportUnits = BrushMappingMode.Absolute, Stretch = Stretch.None };
            dotsBrush.Freeze();

            // Diagonal Stripes: dark bg with faint 45° white stripes every 24px
            var stripesDraw = new DrawingGroup();
            stripesDraw.Children.Add(new GeometryDrawing(new SolidColorBrush(Color.FromRgb(8, 8, 10)), null, new RectangleGeometry(new Rect(0, 0, 24, 24))));
            var stripeLines = new GeometryGroup();
            stripeLines.Children.Add(new LineGeometry(new Point(-6, 6),  new Point(6,  -6)));
            stripeLines.Children.Add(new LineGeometry(new Point(6,  30), new Point(30,  6)));
            stripeLines.Children.Add(new LineGeometry(new Point(18, 30), new Point(30, 18)));
            stripesDraw.Children.Add(new GeometryDrawing(null, new Pen(new SolidColorBrush(Color.FromArgb(22, 255, 255, 255)), 1.5), stripeLines));
            var stripesBrush = new DrawingBrush(stripesDraw) { TileMode = TileMode.Tile, Viewport = new Rect(0, 0, 24, 24), ViewportUnits = BrushMappingMode.Absolute, Stretch = Stretch.None };
            stripesBrush.Freeze();

            // Vignette: radial gradient — slightly lighter at center, black at edges
            var vignetteBrush = new RadialGradientBrush(new GradientStopCollection
            {
                new GradientStop(Color.FromRgb(26, 32, 46), 0.0),
                new GradientStop(Color.FromRgb(10, 14, 22), 0.55),
                new GradientStop(Color.FromRgb(0,  0,  0),  1.0),
            });
            vignetteBrush.Freeze();

            _themeMap = new Dictionary<string, Brush>
            {
                ["Hockey Rink"]      = Brushes.Transparent,
                ["Midnight"]         = new SolidColorBrush(Color.FromRgb(0,   0,   0)),
                ["Deep Navy"]        = new SolidColorBrush(Color.FromRgb(2,   11,  24)),
                ["Ember"]            = new SolidColorBrush(Color.FromRgb(21,  2,   5)),
                ["Forest"]           = new SolidColorBrush(Color.FromRgb(5,   15,  5)),
                ["Vignette"]         = vignetteBrush,
                ["Carbon Fiber"]     = carbonBrush,
                ["Grid"]             = gridBrush,
                ["Dots"]             = dotsBrush,
                ["Diagonal Stripes"] = stripesBrush,
            };
        }

        private Brush _backgroundBrush = Brushes.Transparent;
        [JsonIgnore]
        public Brush BackgroundBrush
        {
            get => _backgroundBrush;
            private set => SetProperty(ref _backgroundBrush, value);
        }

        [JsonIgnore]
        public bool IsRinkBackground => _settings?.BackgroundTheme == "Hockey Rink" || string.IsNullOrEmpty(_settings?.BackgroundTheme);

        #endregion

        public MainWindowViewModel()
        {
            _undoRedo = new UndoRedo();

            LoadCommand = new AsyncRelayCommand(LoadSettingsAsync);
            PlayCommand = new RelayCommand(Play);
            PauseCommand = new RelayCommand(Pause);
            ResetCommand = new RelayCommand(ResetGameState);
            AdvanceScoreCommand = new RelayCommand<TeamType>(AdvanceScore);
            AddPenaltyCommand = new RelayCommand<TeamType>(AddPenalty);
            UserInputCommand = new RelayCommand<KeyEventArgs>(HandleInput);
            ShowConfigurationCommand = new RelayCommand(ShowConfiguration);
            SwapSidesCommand = new RelayCommand(SwapSides);
            ResubmitResultCommand = new RelayCommand(ResubmitResult);

            ResetGameState();

            _tcpBridge = new TcpBridgeService();
            _tcpBridge.CommandReceived += (_, action) =>
                Application.Current.Dispatcher.BeginInvoke(() => ExecuteGameCommand(action));
            _tcpBridge.ClientConnected += (_, _) => SendStateToPlugin();

            _webBroadcast = new WebBroadcastService();
            if (_webBroadcast.NeedsFirewallSetup)
                Application.Current.Dispatcher.BeginInvoke(() =>
                {
                    var command = $"New-NetFirewallRule -DisplayName \"{WebBroadcastService.FirewallRuleName}\" -Direction Inbound -Protocol TCP -LocalPort {WebBroadcastService.Port} -Action Allow";
                    var dialog = new Scoreboard.Windows.FirewallSetupWindow(command)
                    {
                        Owner = Application.Current.MainWindow
                    };
                    dialog.ShowDialog();
                });
        }

        public void Dispose()
        {
            _tcpBridge?.Dispose();
            _webBroadcast?.Dispose();
            _relayPublisher?.Dispose();
        }

        #region IndicatorMethods
        private void ActivateFirstWarningClockColor()
        {
            ClockWithinMinute = true;
            ClockWithinThirtySeconds = false;
            DefaultClock = false;
        }
        private void ActivateSecondWarningClockColor()
        {
            ClockWithinMinute = false;
            ClockWithinThirtySeconds = true;
            DefaultClock = false;
        }
        private void ActivateDefaultClockColor()
        {
            ClockWithinMinute = false;
            ClockWithinThirtySeconds = false;
            DefaultClock = true;
        }
        private void HighlightScore(TeamType type)
        {
            Timer timer;
            switch (type)
            {
                case TeamType.Home:
                    HighlightHome = true;
                    timer = new Timer(UnHighlight, null, 2000, Timeout.Infinite);
                    break;
                case TeamType.Visitor:
                    HighlightVisitor = true;
                    timer = new Timer(UnHighlight, null, 2000, Timeout.Infinite);
                    break;
            }
        }
        private void UnHighlight(object? state)
        {
            HighlightHome = false;
            HighlightVisitor = false;
        }
        private void ApplyLightingEffect(LightingType type)
        {
            var effect = "";
            switch (type)
            {
                case LightingType.GameRun:
                    effect = _settings.GameRunningEffect;
                    break;
                case LightingType.GamePause:
                    effect = _settings.GameStoppedEffect;
                    break;
                case LightingType.GameOver:
                    effect = _settings.GameOverEffect;
                    break;
                case LightingType.HomeScore:
                    effect = _settings.HomeScoreEffect;
                    break;
                case LightingType.VisitorScore:
                    effect = _settings.VisitorScoreEffect;
                    break;
                case LightingType.PenatlyAdd:
                    effect = _settings.PenaltyAddEffect;
                    break;
                case LightingType.PenatlyRemove:
                    effect = _settings.PenaltyDropEffect;
                    break;
                case LightingType.SuddenDeath:
                    effect = _settings.SuddenDeathEffect;
                    break;
                case LightingType.SlowPulse:
                    effect = _settings.SlowPulseEffect;
                    break;
                case LightingType.FastPulse:
                    effect = _settings.FastPulseEffect;
                    break;
                case LightingType.MediumPulse:
                    effect = _settings.MediumPulseEffect;
                    break;
            }


            var process = new Process();
            var startInfo = new ProcessStartInfo("cmd.exe", $"/C {_settings.LedAddress}\\SignalRgbLauncher.exe --url=effect/apply/{effect}?-silentlaunch-")
            {
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };
            process.StartInfo = startInfo;
            process.Start();
        }
        #endregion

        #region PopupMethods
        private void ShowCountdown(int seconds, bool final = false)
        {
            // Checked live when the countdown actually reaches zero, not here —
            // a goal during the countdown can still break or create the tie.
            Func<bool>? isTieCheck = final ? () => HomeScore == VisitorScore : null;
            var viewModel = new StartingGameCountDownViewModel(seconds, _settings.SoundEnabled, final, isTieCheck);

            // Mirror countdown to Stream Deck
            CountdownSeconds = seconds;
            SendStateToPlugin();
            viewModel.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(StartingGameCountDownViewModel.Clock))
                {
                    CountdownSeconds = (int)viewModel.Clock.TotalSeconds;
                    SendStateToPlugin();
                }
            };
            viewModel.OnCompleted += (_, _) =>
            {
                CountdownSeconds = 0;
                SendStateToPlugin();
            };

            App.Current.Dispatcher.Invoke(() =>
            {
                Window countDown = new StartingGameCountDownWindow
                {
                    Owner = App.Current.MainWindow,
                    DataContext = viewModel
                };

                if (final)
                {
                    _finalCountdown = viewModel;
                    countDown.ShowActivated = false;
                    countDown.Show();
                }
                else
                    countDown.ShowDialog();
            });
        }
        private void ShowConfiguration()
        {
            var config = new ConfigurationWindow();
            var viewModel = new ConfigurationViewModel { Game = this };
            config.DataContext = viewModel;
            config.Owner = App.Current.MainWindow;
            config.ShowDialog();

            _settings = viewModel.Settings;
            _keyBindings = _settings.KeyBindings.ToDictionary<GameAction, Key>();
            if (_currentMatch == null)
                ResetNames(); // Don't overwrite names when a Challonge match is active
            ResetColors();
            ApplyRelaySettings();
            ApplyBackground();
            _ = UpdateTeamLogosAsync();
            // Apply a changed game length immediately when no game is underway;
            // a game in progress keeps its clock until the next reset.
            if (NewGame && !IsRunning)
                ResetGameClock();
        }
        #endregion

        #region GameLogicMethods
        private void ExecuteGameCommand(GameAction gameAction)
        {
            // Any action instantly clears the GOAL flash so the operator is never waiting on it
            DismissCelebration();
            switch (gameAction)
            {
                case GameAction.IncreaseHome:
                    ApplyLightingEffect(LightingType.HomeScore);
                    if (_settings.TimingMode == Enums.TimingMode.StopTime) Pause();
                    AdvanceScore(TeamType.Home);
                    break;
                case GameAction.IncreaseAway:
                    ApplyLightingEffect(LightingType.VisitorScore);
                    if (_settings.TimingMode == Enums.TimingMode.StopTime) Pause();
                    AdvanceScore(TeamType.Visitor);
                    break;
                case GameAction.PenalizeHome:
                    ApplyLightingEffect(LightingType.PenatlyAdd);
                    Pause();
                    AddPenalty(TeamType.Home);
                    break;
                case GameAction.PenalizeAway:
                    ApplyLightingEffect(LightingType.PenatlyAdd);
                    Pause();
                    AddPenalty(TeamType.Visitor);
                    break;
                case GameAction.Undo:
                    Pause();
                    SetValues(_undoRedo?.Undo(this));
                    break;
                case GameAction.Redo:
                    Pause();
                    SetValues(_undoRedo?.Redo(this));
                    break;
                case GameAction.Reset:
                    Pause();
                    ResetGameState();
                    break;
                case GameAction.PlayPause:
                    Play();
                    break;
                case GameAction.ResetClock:
                    Pause();
                    ResetGameClock();
                    break;
                case GameAction.SwapSides:
                    Pause();
                    IsHalfTime = false;
                    HalfTimeReached = false;
                    HalfTimeWarning = false;
                    SwapSides();
                    break;
                case GameAction.HalfTime:
                    Pause();
                    IsHalfTime = true;
                    HalfTimeReached = false;
                    HalfTimeWarning = false;
                    _halfTimeTaken = true;
                    SendStateToPlugin();
                    break;
                case GameAction.IncreaseNextMatch:
                    if (_ceremonyCountdownSeconds != null) AdjustCeremonyCountdown(10);
                    else _betweenGameViewModel?.Adjust(1);
                    SendStateToPlugin();
                    break;
                case GameAction.DecreaseNextMatch:
                    if (_ceremonyCountdownSeconds != null) AdjustCeremonyCountdown(-10);
                    else _betweenGameViewModel?.Adjust(-1);
                    SendStateToPlugin();
                    break;
                case GameAction.StartNextMatch:
                    if (_ceremonyCountdownSeconds != null) { CancelCeremonyCountdown(); _ = LaunchAwardsCeremonyAsync(); }
                    else _betweenGameViewModel?.StartCountdown();
                    break;
                case GameAction.BetweenGame:
                    if (_betweenGameWindow == null)
                        Application.Current.Dispatcher.BeginInvoke(ShowBetweenGameWindow);
                    else
                        Application.Current.Dispatcher.BeginInvoke(CloseBetweenGameWindow);
                    break;
                case GameAction.SelectMatch0:
                case GameAction.SelectMatch1:
                case GameAction.SelectMatch2:
                case GameAction.SelectMatch3:
                case GameAction.SelectMatch4:
                case GameAction.SelectMatch5:
                    var idx = (int)gameAction - (int)GameAction.SelectMatch0;
                    if (idx < _pendingMatches.Count)
                    {
                        var match = _pendingMatches[idx];
                        _currentMatch = match;
                        ClearReportState();
                        // A leftover swap from the previous game must not carry into this one —
                        // otherwise the Challonge un-swap math reports the wrong team's score.
                        IsReverse = false;
                        HomeTeam = match.Player1Name;
                        VisitorTeam = match.Player2Name;
                        _settings.HomeTeamName = match.Player1Name;
                        _settings.VisitorTeamName = match.Player2Name;
                        if (_betweenGameViewModel != null)
                            _betweenGameViewModel.NextUpDisplay = match.Label;
                        _ = UpdateTeamLogosAsync();
                        _ = DiscordService.PostNextUpAsync(_settings, HomeTeam, VisitorTeam);
                        SendStateToPlugin();
                    }
                    break;
            }
        }
        private void SendStateToPlugin()
        {
            var nextMatch = _betweenGameViewModel is { } vm
                ? $"{(int)vm.NextMatchTime.TotalMinutes:D2}:{vm.NextMatchTime.Seconds:D2}"
                : "--:--";

            var slots = new string[6];
            for (int i = 0; i < 6; i++)
                slots[i] = i < _pendingMatches.Count ? _pendingMatches[i].Label : "";

            // Halftime has no meaning between games, so the Stream Deck's Halftime
            // button shows pace drift there instead, reverting to normal halftime
            // display the instant a match starts (see the plugin's halfTimeAction.ts).
            var isBetweenGames = _betweenGameWindow != null;
            var paceStatus = _paceTracker.GetPaceStatusText(_settings, DateTime.Now);

            _ = _tcpBridge?.SendStateAsync(
                HomeTeam, VisitorTeam,
                HomeScore, VisitorScore,
                $"{(int)GameClock.TotalMinutes:D2}:{GameClock.Seconds:D2}",
                IsRunning, GameDone, nextMatch, slots,
                IsHalfTime, HalfTimeWarning, CountdownSeconds, HalfTimeReached,
                isBetweenGames, paceStatus);

            _webBroadcast?.BroadcastState(
                HomeTeam, VisitorTeam,
                HomeScore, VisitorScore,
                $"{(int)GameClock.TotalMinutes:D2}:{GameClock.Seconds:D2}",
                IsRunning, GameDone, IsSuddenDeath,
                $"{(int)HomePenaltyOne.TotalMinutes:D2}:{HomePenaltyOne.Seconds:D2}",
                $"{(int)HomePenaltyTwo.TotalMinutes:D2}:{HomePenaltyTwo.Seconds:D2}",
                $"{(int)VisitorPenaltyOne.TotalMinutes:D2}:{VisitorPenaltyOne.Seconds:D2}",
                $"{(int)VisitorPenaltyTwo.TotalMinutes:D2}:{VisitorPenaltyTwo.Seconds:D2}",
                ActiveHomePenaltyOne, ActiveHomePenaltyTwo,
                ActiveVisitorPenaltyOne, ActiveVisitorPenaltyTwo);
        }

        private void HandleInput(KeyEventArgs? args)
        {
            if (args == null)
                return;
            if (_keyBindings != null && _keyBindings.ContainsValue((args.Key)))
                ExecuteGameCommand(_keyBindings.Where(b => b.Value == args.Key).First().Key);
        }
        private void Tick(object? state)
        {
            if (GameClock <= TimeSpan.Zero)
            {
                if (!IsSuddenDeath) GameFinished();
                _gameTimer?.Dispose();
                return;
            }

            if (GameClock.Minutes <= 0 && GameClock.Seconds <= 60 && GameClock.Seconds > 30)
            {
                if (ClockWithinMinute == false)
                {
                    ApplyLightingEffect(LightingType.SlowPulse);
                    ClockWithinMinute = true;
                }
                ActivateFirstWarningClockColor();
                PlayFirstTimeWarning();
            }
            else if (GameClock.Minutes <= 0 && GameClock.Seconds <= 30 && GameClock.Seconds > 10)
            {
                if (ClockWithinThirtySeconds == false)
                {
                    ApplyLightingEffect(LightingType.MediumPulse);
                    ClockWithinThirtySeconds = true;
                }
                ActivateSecondWarningClockColor();
                PlaySecondTimeWarning();
            }
            else if (GameClock.Minutes <= 0 && GameClock.Seconds <= 10 && !FinalTenSeconds)
            {
                if (FinalTenSeconds == false)
                {
                    ApplyLightingEffect(LightingType.FastPulse);
                    FinalTenSeconds = true;
                }
                StopTimeWarning();
                ShowCountdown(10, true);
            }
            else
                ActivateDefaultClockColor();

            GameClock -= TimeSpan.FromSeconds(1);
            IsHighTick = !IsHighTick;
            UpdateDramaMode();

            // Halftime warning: 30 seconds before the halfway point of the game.
            // Skipped entirely when halftime reminders are turned off in Settings.
            if (_settings.HalfTimeEnabled)
            {
                var halfPoint = TimeSpan.FromMinutes(_settings.GameLengthMinutes / 2.0);
                var newWarning = GameClock <= halfPoint + TimeSpan.FromSeconds(30) && GameClock > halfPoint && !_halfTimeTaken;
                if (newWarning != HalfTimeWarning) HalfTimeWarning = newWarning;
                var newReached = GameClock <= halfPoint && !IsHalfTime && !_halfTimeTaken;
                if (newReached != HalfTimeReached) HalfTimeReached = newReached;
            }
            else
            {
                if (HalfTimeWarning) HalfTimeWarning = false;
                if (HalfTimeReached) HalfTimeReached = false;
            }

            SendStateToPlugin();
        }
        private void Pause()
        {
            IsRunning = false;
            _gameTimer?.Dispose();
            _finalCountdown?.PauseCountdown();
            SendStateToPlugin();
        }
        private void Play()
        {
            DismissCelebration();
            if (GameDone)
                return;

            if (IsRunning)
            {
                Pause();
                PlayStartStopSound();
                ApplyLightingEffect(LightingType.GamePause);
            }
            else
            {
                if (NewGame)
                {
                    ApplyLightingEffect(LightingType.FastPulse);
                    PlayNewGameStartSound();
                    ShowCountdown(6);
                    NewGame = false;
                    Play();
                }
                else
                {
                    PlayStartStopSound();
                    ApplyLightingEffect(LightingType.GameRun);
                    IsRunning = true;
                    _gameTimer = new Timer(Tick, null, 0, 1000);
                    _finalCountdown?.ResumeCountdown();
                    SendStateToPlugin();
                }
            }
        }


        private void AdvanceScore(TeamType type)
        {
            DismissCelebration();
            // After game end this is a score correction, not a goal — no celebration
            if (!GameDone)
            {
                PlayScoreSound(type);
                StartCelebration(type);
            }
            switch (type)
            {
                case TeamType.Home:
                    _undoRedo?.Cache(this);
                    if (!GameDone) HighlightScore(type);
                    HomeScore++;
                    break;
                case TeamType.Visitor:
                    _undoRedo?.Cache(this);
                    if (!GameDone) HighlightScore(type);
                    VisitorScore++;
                    break;
                default:
                    break;
            }
            if (GameDone)
            {
                UpdateReportDesyncState();
            }
            else if (IsSuddenDeath)
            {
                Pause();
                GameDone = true;
                OnGameEnded();
                ReportResultToChallonge();
            }
            UpdateDramaMode();
            SendStateToPlugin();
        }


        private void AddPenalty(TeamType type)
        {
            DismissCelebration();
            PlayPenaltySound();
            _undoRedo?.Cache(this);

            var penalty = TimeSpan.FromMinutes(_settings.PenaltyLengthMinutes);
            switch (type)
            {
                case TeamType.Home:
                    if (HomePenaltyOne <= TimeSpan.Zero)
                    {
                        ActiveHomePenaltyOne = true;
                        HomePenaltyOne = penalty;
                    }
                    else if (HomePenaltyTwo <= TimeSpan.Zero)
                    {
                        ActiveHomePenaltyTwo = true;
                        HomePenaltyTwo = penalty;
                    }
                    break;
                case TeamType.Visitor:
                    if (VisitorPenaltyOne <= TimeSpan.Zero)
                    {
                        ActiveVisitorPenaltyOne = true;
                        VisitorPenaltyOne = penalty;
                    }
                    else if (VisitorPenaltyTwo <= TimeSpan.Zero)
                    {
                        ActiveVisitorPenaltyTwo = true;
                        VisitorPenaltyTwo = penalty;
                    }
                    break;
            }

            RefreshPenaltyTimers();
        }


        private void DecreasePenalty(TeamType team, int index)
        {
            if (IsRunning)
            {
                if (index > 1)
                    return;
                switch (team)
                {
                    case TeamType.Home:
                        switch (index)
                        {
                            case 0:
                                HomePenaltyOne -= TimeSpan.FromSeconds(1);
                                if (HomePenaltyOne <= TimeSpan.Zero)
                                {
                                    ActiveHomePenaltyOne = false;
                                    _homePenaltyOneTimer?.Dispose();
                                    HomePenaltyOne = TimeSpan.Zero;
                                    PlayPenaltyExpireSound();
                                    ApplyLightingEffect(LightingType.PenatlyRemove);
                                }
                                break;
                            case 1:
                                HomePenaltyTwo -= TimeSpan.FromSeconds(1);
                                if (HomePenaltyTwo <= TimeSpan.Zero)
                                {
                                    ActiveHomePenaltyTwo = false;
                                    _homePenaltyTwoTimer?.Dispose();
                                    HomePenaltyTwo = TimeSpan.Zero;
                                    PlayPenaltyExpireSound();
                                    ApplyLightingEffect(LightingType.PenatlyRemove);
                                }
                                break;
                            default:
                                break;
                        }
                        break;
                    case TeamType.Visitor:
                        switch (index)
                        {
                            case 0:
                                VisitorPenaltyOne -= TimeSpan.FromSeconds(1);
                                if (VisitorPenaltyOne <= TimeSpan.Zero)
                                {
                                    ActiveVisitorPenaltyOne = false;
                                    _visitorPenaltyOneTimer?.Dispose();
                                    VisitorPenaltyOne = TimeSpan.Zero;
                                    PlayPenaltyExpireSound();
                                    ApplyLightingEffect(LightingType.PenatlyRemove);
                                }
                                break;
                            case 1:
                                VisitorPenaltyTwo -= TimeSpan.FromSeconds(1);
                                if (VisitorPenaltyTwo <= TimeSpan.Zero)
                                {
                                    ActiveVisitorPenaltyTwo = false;
                                    _visitorPenaltyTwoTimer?.Dispose();
                                    VisitorPenaltyTwo = TimeSpan.Zero;
                                    PlayPenaltyExpireSound();
                                    ApplyLightingEffect(LightingType.PenatlyRemove);
                                }
                                break;
                            default:
                                break;
                        }
                        break;
                }
            }
        }
        private void GameFinished()
        {
            // Halftime never being taken is a valid choice — but the reminder
            // must not keep flashing once the game is actually over.
            HalfTimeWarning = false;
            HalfTimeReached = false;

            if (HomeScore == VisitorScore)
            {
                if (_settings.SoundEnabled)
                {
                    _player.SoundLocation = AppDomain.CurrentDomain.BaseDirectory + "/Resources/Sounds/suddenDeath.wav";
                    _player.Stop();
                    _player.Play();
                }
                TriggerSuddenDeath();
                ApplyLightingEffect(LightingType.SuddenDeath);
            }
            else
            {
                if (_settings.SoundEnabled)
                {
                    _player.SoundLocation = AppDomain.CurrentDomain.BaseDirectory + "/Resources/Sounds/gameEndBuzzer.wav";
                    _player.Stop();
                    _player.Play();
                }
                GameDone = true;
                ApplyLightingEffect(LightingType.GameOver);
                OnGameEnded();
                ReportResultToChallonge();
            }
        }

        // Runs at both places a game legitimately ends (clock-zero and the golden-goal
        // path). Every game posts its final score to Discord — exhibition games
        // included, since Discord is just hype/community, not a permanent record like
        // Challonge/league-site reporting. Every game also counts toward the pace
        // tracker's matches-completed-today total, regardless of bracket or
        // exhibition status — it's real clock time either way.
        private void OnGameEnded()
        {
            _ = DiscordService.PostFinalScoreAsync(_settings, HomeTeam, HomeScore, VisitorTeam, VisitorScore, IsSuddenDeath, IsChampionship);
            _paceTracker.RecordGameFinished(HomeTeam, VisitorTeam);
            if (IsChampionship) StartCeremonyCountdown();
        }

        // Championship is always played last at BHL, so the buzzer that ends it is
        // genuinely the last thing that happens — this auto-launches the closing
        // podium ~30s later. Visible and adjustable via the same dial that adjusts
        // the between-game "Next Match In" timer (only one of the two is ever
        // active at once): rotate to add/remove time, press to launch right now.
        // Reset cancels it outright (see ResetFlags).
        private void StartCeremonyCountdown()
        {
            _ceremonyCountdownSeconds = 30;
            UpdateCeremonyCountdownText();
            _ceremonyTimer?.Dispose();
            _ceremonyTimer = new Timer(_ =>
            {
                if (_ceremonyCountdownSeconds is not { } s) return;
                s--;
                _ceremonyCountdownSeconds = s;
                if (s <= 0)
                {
                    _ceremonyTimer?.Dispose();
                    _ceremonyTimer = null;
                    _ceremonyCountdownSeconds = null;
                    Application.Current.Dispatcher.BeginInvoke(() => { UpdateCeremonyCountdownText(); _ = LaunchAwardsCeremonyAsync(); });
                    return;
                }
                Application.Current.Dispatcher.BeginInvoke(UpdateCeremonyCountdownText);
            }, null, 1000, 1000);
        }

        private void CancelCeremonyCountdown()
        {
            _ceremonyTimer?.Dispose();
            _ceremonyTimer = null;
            _ceremonyCountdownSeconds = null;
            UpdateCeremonyCountdownText();
        }

        private void AdjustCeremonyCountdown(int deltaSeconds)
        {
            if (_ceremonyCountdownSeconds is not { } s) return;
            _ceremonyCountdownSeconds = Math.Clamp(s + deltaSeconds, 1, 120);
            UpdateCeremonyCountdownText();
        }

        private void UpdateCeremonyCountdownText() =>
            CeremonyCountdownText = _ceremonyCountdownSeconds is { } s ? $"Awards Ceremony in {s}s" : "";

        private async Task LaunchAwardsCeremonyAsync()
        {
            var champion = HomeScore > VisitorScore ? HomeTeam : VisitorTeam;
            var runnerUp = HomeScore > VisitorScore ? VisitorTeam : HomeTeam;

            List<string> thirdPlace = [];
            if (!string.IsNullOrWhiteSpace(_settings.BracketUrl) && !string.IsNullOrWhiteSpace(_settings.ChallongeApiKey))
                thirdPlace = await ChallongeService.FetchThirdPlaceAsync(_settings.BracketUrl!, _settings.ChallongeApiKey!, champion, runnerUp);

            var trophies = await LeagueSiteService.FetchAwardsAsync(_settings, _settings.EventName ?? "");

            var championLogo = await TeamLogos.LoadAsync(champion);
            var runnerUpLogo = await TeamLogos.LoadAsync(runnerUp);

            var viewModel = new AwardsCeremonyViewModel(champion, runnerUp, championLogo, runnerUpLogo, thirdPlace, trophies);
            _ceremonyWindow?.Close();
            _ceremonyWindow = new Windows.AwardsCeremonyWindow { DataContext = viewModel };
            _ceremonyWindow.Closed += (_, _) => _ceremonyWindow = null;
            _ceremonyWindow.Show();
        }

        private void ReportResultToChallonge()
        {
            if (_currentMatch == null) return;
            if (string.IsNullOrEmpty(_settings.BracketUrl) || string.IsNullOrEmpty(_settings.ChallongeApiKey)) return;

            // Keep the match remembered so a post-game edit can resubmit a corrected score
            _reportedMatch = _currentMatch;
            _currentMatch = null;

            // If sides were swapped, home side is actually Player2 — un-swap before reporting
            var p1Score = IsReverse ? VisitorScore : HomeScore;
            var p2Score = IsReverse ? HomeScore : VisitorScore;
            _ = SubmitResultAsync(_reportedMatch, p1Score, p2Score);
        }

        private async Task SubmitResultAsync(PendingMatch match, int p1Score, int p2Score)
        {
            ReportSucceeded = false;
            ReportFailed = false;
            ReportPending = true;

            var winnerId = p1Score > p2Score ? match.Player1Id : match.Player2Id;
            var ok = await ChallongeService.ReportResultAsync(
                _settings.BracketUrl!, _settings.ChallongeApiKey!,
                match.MatchId, winnerId,
                p1Score, p2Score);

            _reportedP1Score = p1Score;
            _reportedP2Score = p2Score;
            ReportPending = false;
            ReportSucceeded = ok;
            ReportFailed = !ok;
            UpdateReportDesyncState();

            // First submission also posts the result to the league website
            // (resubmits don't repost — record_game inserts, it doesn't update)
            if (!_leaguePosted)
            {
                _leaguePosted = true;
                await PostResultToLeagueAsync(ok);
            }
        }

        private async Task PostResultToLeagueAsync(bool challongeOk)
        {
            if (string.IsNullOrWhiteSpace(_settings.EventName) || !LeagueSiteService.IsConfigured(_settings))
                return;

            // Winner first, matching the website's convention
            var homeWon = HomeScore > VisitorScore;
            var result = new LeagueResult
            {
                EventName = _settings.EventName!,
                Team1 = homeWon ? HomeTeam : VisitorTeam,
                Team2 = homeWon ? VisitorTeam : HomeTeam,
                Score1 = homeWon ? HomeScore : VisitorScore,
                Score2 = homeWon ? VisitorScore : HomeScore,
                Overtime = IsSuddenDeath,
                Championship = IsChampionship,
                ChallongeMatchId = _reportedMatch?.MatchId,
                ReportedToChallonge = challongeOk,
                PlayedAt = DateTimeOffset.Now,
            };

            LeaguePostSucceeded = false;
            LeaguePostQueued = false;
            LeaguePostPending = true;
            var ok = await LeagueSiteService.PostResultAsync(_settings, result);
            LeaguePostPending = false;
            LeaguePostSucceeded = ok;
            LeaguePostQueued = !ok;
            // Keep the offline bundle current so Today's Results (attract mode)
            // reflects this game without waiting for a manual re-download
            if (ok) _ = LeagueSiteService.DownloadBundleAsync(_settings);
        }

        /// <summary>
        /// Final-minute drama: under 1:00 in a close game (tied or one-goal lead),
        /// the screen gets a pulsing red edge. Stands down if the lead grows,
        /// comes back if the gap closes again.
        /// </summary>
        private void UpdateDramaMode()
        {
            IsDramaMode = !GameDone && !IsSuddenDeath
                && GameClock > TimeSpan.Zero
                && GameClock <= TimeSpan.FromMinutes(1)
                && Math.Abs(HomeScore - VisitorScore) <= 1;
        }

        /// <summary>Brief full-screen GOAL moment for the scoring team; clears itself after ~2.5 s.</summary>
        private void StartCelebration(TeamType type)
        {
            CelebrationTeamName = type == TeamType.Home ? HomeTeam : VisitorTeam;
            CelebrationColor = type == TeamType.Home ? HomeColor : VisitorColor;
            CelebrationLogo = type == TeamType.Home ? HomeLogo : VisitorLogo;
            IsCelebrating = true;
            _celebrationTimer?.Dispose();
            _celebrationTimer = new Timer(_ => IsCelebrating = false, null, 2500, Timeout.Infinite);
        }

        private void DismissCelebration()
        {
            if (!IsCelebrating) return;
            _celebrationTimer?.Dispose();
            IsCelebrating = false;
        }

        /// <summary>Refreshes both team logos from the league bundle (cached to disk after first download).</summary>
        private async Task UpdateTeamLogosAsync()
        {
            HomeLogo = await Helpers.TeamLogos.LoadAsync(HomeTeam);
            VisitorLogo = await Helpers.TeamLogos.LoadAsync(VisitorTeam);
        }

        private void ResubmitResult()
        {
            if (_reportedMatch == null || ReportPending) return;

            var p1Score = IsReverse ? VisitorScore : HomeScore;
            var p2Score = IsReverse ? HomeScore : VisitorScore;
            if (p1Score == p2Score) return; // Challonge needs a winner — a tie can't be submitted

            _ = SubmitResultAsync(_reportedMatch, p1Score, p2Score);
        }

        /// <summary>
        /// After a result has been reported, watches for the on-screen score drifting from
        /// what Challonge has (post-game undo or correction) and offers a resubmit.
        /// </summary>
        private void UpdateReportDesyncState()
        {
            if (_reportedMatch == null)
            {
                ScoreChangedSinceReport = false;
                ShowResubmit = false;
                return;
            }

            var p1Score = IsReverse ? VisitorScore : HomeScore;
            var p2Score = IsReverse ? HomeScore : VisitorScore;
            ScoreChangedSinceReport = ReportSucceeded
                && (p1Score != _reportedP1Score || p2Score != _reportedP2Score);
            ShowResubmit = !ReportPending && (ReportFailed || ScoreChangedSinceReport);
        }

        private void ClearReportState()
        {
            _reportedMatch = null;
            ReportPending = false;
            ReportSucceeded = false;
            ReportFailed = false;
            ScoreChangedSinceReport = false;
            ShowResubmit = false;
            _leaguePosted = false;
            LeaguePostPending = false;
            LeaguePostSucceeded = false;
            LeaguePostQueued = false;
        }

        private async void ShowBetweenGameWindow()
        {
            _betweenGameViewModel = new BetweenGameViewModel(_settings.BracketUrl, _settings.LearnMoreUrl, _settings);
            // Mirror the between-game countdown to the Stream Deck every tick
            _betweenGameViewModel.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(BetweenGameViewModel.NextMatchTime))
                    SendStateToPlugin();
            };
            _betweenGameViewModel.CountdownComplete += (_, _) =>
                Application.Current.Dispatcher.BeginInvoke(() =>
                {
                    CloseBetweenGameWindow();
                    ResetGameState();
                });

            _betweenGameWindow = new BetweenGameWindow
            {
                Owner = App.Current.MainWindow,
                DataContext = _betweenGameViewModel
            };
            _betweenGameWindow.Closed += (_, _) =>
            {
                // Handles ESC-close or any external close; CloseBetweenGameWindow guards against double-dispose
                if (_betweenGameWindow != null)
                    Application.Current.Dispatcher.BeginInvoke(CloseBetweenGameWindow);
            };
            _betweenGameWindow.Show();

            // Fetch Challonge matches in background (only triggers on open, ~2 API calls)
            if (!string.IsNullOrWhiteSpace(_settings.ChallongeApiKey)
                && !string.IsNullOrWhiteSpace(_settings.BracketUrl))
            {
                _pendingMatches = await ChallongeService.FetchOpenMatchesAsync(
                    _settings.BracketUrl, _settings.ChallongeApiKey);

                if (_betweenGameViewModel != null && _pendingMatches.Count > 0)
                    _betweenGameViewModel.NextUpDisplay = _pendingMatches[0].Label;

                if (_pendingMatches.Count > 0)
                    _ = DiscordService.PostOnDeckAsync(_settings, _pendingMatches[0].Player1Name, _pendingMatches[0].Player2Name);

                // Round boundary: any open match needing a team that already played
                // in the current batch means it's a natural break point.
                var upcomingTeams = _pendingMatches.SelectMany(m => new[] { m.Player1Name, m.Player2Name });
                if (_betweenGameViewModel != null && _paceTracker.CheckRoundBoundary(upcomingTeams))
                {
                    var suggested = _paceTracker.ComputeSuggestedBreak(_settings, DateTime.Now);
                    if (suggested != null)
                    {
                        var drift = _paceTracker.ComputeDrift(_settings, DateTime.Now) ?? TimeSpan.Zero;
                        _betweenGameViewModel.SetPaceSuggestion(suggested.Value, drift);
                    }
                }
            }

            SendStateToPlugin();
        }

        private void CloseBetweenGameWindow()
        {
            _betweenGameViewModel?.Dispose();
            _betweenGameViewModel = null;
            var w = _betweenGameWindow;
            _betweenGameWindow = null; // null before Close so the Closed handler skips re-entry
            w?.Close();
            SendStateToPlugin();
        }
        private void TriggerSuddenDeath()
        {
            IsSuddenDeath = true;
            GameDone = false;
            IsRunning = false;
            UpdateDramaMode();
            SendStateToPlugin();
            // Same match-linked-only gate as the final-score post — exhibition
            // games (no Challonge match selected) never post to Discord.
            if (_currentMatch != null)
                _ = DiscordService.PostSuddenDeathAsync(_settings, HomeTeam, VisitorTeam);
        }
        #endregion

        #region GameStateMethods
        private async Task LoadSettingsAsync()
        {
            _settings = await ConfigurationViewModel.LoadSettingsAsync();
            _keyBindings = _settings.KeyBindings.ToDictionary<GameAction, Key>();
            ResetGameState();
            ApplyRelaySettings();
            ApplyBackground();
            // Drain any league results queued while offline (e.g. venue Wi-Fi died)
            _ = LeagueSiteService.FlushQueueAsync(_settings);
        }

        private void ApplyBackground()
        {
            var theme = _settings?.BackgroundTheme ?? "Hockey Rink";
            BackgroundBrush = _themeMap.TryGetValue(theme, out var brush) ? brush : Brushes.Transparent;
            OnPropertyChanged(nameof(IsRinkBackground));
        }

        private void ApplyRelaySettings()
        {
            _relayPublisher?.Dispose();
            _relayPublisher = null;

            if (!string.IsNullOrWhiteSpace(_settings.RelayUrl) && _webBroadcast != null)
            {
                try
                {
                    _relayPublisher = new RelayPublisherService(_settings.RelayUrl);
                    _webBroadcast.OnStateBroadcast = json => _relayPublisher.SendAsync(json);
                }
                catch { /* bad URL — relay silently disabled */ }
            }
            else if (_webBroadcast != null)
            {
                _webBroadcast.OnStateBroadcast = null;
            }
        }
        private void SetValues(MainWindowViewModel? mainWindowViewModel)
        {
            if (mainWindowViewModel != null)
            {
                this.HomeScore = mainWindowViewModel.HomeScore;
                this.HomePenaltyOne = mainWindowViewModel.HomePenaltyOne;
                this.HomePenaltyTwo = mainWindowViewModel.HomePenaltyTwo;
                this.ActiveHomePenaltyOne = mainWindowViewModel.ActiveHomePenaltyOne;
                this.ActiveHomePenaltyTwo = mainWindowViewModel.ActiveHomePenaltyTwo;

                this.VisitorScore = mainWindowViewModel.VisitorScore;
                this.VisitorPenaltyOne = mainWindowViewModel.VisitorPenaltyOne;
                this.VisitorPenaltyTwo = mainWindowViewModel.VisitorPenaltyTwo;
                this.ActiveVisitorPenaltyOne = mainWindowViewModel.ActiveVisitorPenaltyOne;
                this.ActiveVisitorPenaltyTwo = mainWindowViewModel.ActiveVisitorPenaltyTwo;

                RefreshPenaltyTimers();
                UpdateReportDesyncState();
                UpdateDramaMode();
                SendStateToPlugin();
            }
        }
        private void ResetGameState()
        {
            DisposeTimers();
            ResetClocks();
            ResetScores();
            ResetNames();
            ActivateDefaultClockColor();
            ResetFlags();
            ResetColors();
            _ = UpdateTeamLogosAsync();
            SendStateToPlugin();
        }

        private void ResetColors()
        {
            HomeColor = _settings.StringToColor[_settings.HomeColor ?? "White"];
            VisitorColor = _settings.StringToColor[_settings.VisitorColor ?? "White"];
        }

        private void DisposeTimers()
        {
            _gameTimer?.Dispose();
            _homePenaltyOneTimer?.Dispose();
            _homePenaltyTwoTimer?.Dispose();
            _visitorPenaltyOneTimer?.Dispose();
            _visitorPenaltyTwoTimer?.Dispose();
            _finalCountdown?.CloseWindow();
            _finalCountdown = null;
            CloseBetweenGameWindow();
        }
        private void ResetClocks()
        {
            HomePenaltyOne = TimeSpan.Zero;
            HomePenaltyTwo = TimeSpan.Zero;
            VisitorPenaltyOne = TimeSpan.Zero;
            VisitorPenaltyTwo = TimeSpan.Zero;
            GameClock = new TimeSpan(0, _settings.GameLengthMinutes, 0);
            CountDownClock = new TimeSpan(0, 0, _defaultDelayLengthSeconds);
        }
        private void ResetGameClock() =>
            GameClock = new TimeSpan(0, _settings.GameLengthMinutes, 0);
        private void ResetScores()
        {
            HomeScore = 0;
            VisitorScore = 0;
        }
        private void ResetNames()
        {
            HomeTeam = _settings.HomeTeamName ?? "Home";
            VisitorTeam = _settings.VisitorTeamName ?? "Visitor";
        }
        private void ResetFlags()
        {
            IsRunning = false;
            NewGame = true;
            GameDone = false;
            FinalTenSeconds = false;

            IsFocusLocked = _settings.IsKioskMode;

            ActiveHomePenaltyOne = false;
            ActiveHomePenaltyTwo = false;
            ActiveVisitorPenaltyOne = false;
            ActiveVisitorPenaltyTwo = false;
            IsSuddenDeath = false;
            IsHalfTime = false;
            HalfTimeWarning = false;
            HalfTimeReached = false;
            _halfTimeTaken = false;
            CountdownSeconds = 0;
            _celebrationTimer?.Dispose();
            IsCelebrating = false;
            IsDramaMode = false;
            IsChampionship = false; // set it fresh right before the final
            // A leftover swap from the previous game must not carry into this one —
            // otherwise the Challonge un-swap math reports the wrong team's score.
            IsReverse = false;
            ClearReportState();
            CancelCeremonyCountdown();
        }
        private void SwapSides()
        {
            DismissCelebration();
            if (IsRunning)
                Pause();

            var nameBuffer = HomeTeam;
            var scoreBuffer = HomeScore;

            var penaltyOneBuffer = HomePenaltyOne;
            var penaltyTwoBuffer = HomePenaltyTwo;

            var activePenaltyOneBuffer = ActiveHomePenaltyOne;
            var activePenaltyTwoBuffer = ActiveHomePenaltyTwo;

            var scoreActionBuffer = _settings.KeyBindings[GameAction.IncreaseHome];
            var penaltyActionBuffer = _settings.KeyBindings[GameAction.PenalizeHome];

            var colorBuffer = HomeColor;
            var ledActionBuffer = _settings.HomeScoreEffect;

            var penaltyTimerOneBuffer = _homePenaltyOneTimer;
            var penaltyTimerTwoBuffer = _homePenaltyTwoTimer;




            HomeTeam = VisitorTeam;
            HomeScore = VisitorScore;

            HomePenaltyOne = VisitorPenaltyOne;
            HomePenaltyTwo = VisitorPenaltyTwo;

            ActiveHomePenaltyOne = ActiveVisitorPenaltyOne;
            ActiveHomePenaltyTwo = ActiveVisitorPenaltyTwo;

            _settings.KeyBindings[GameAction.IncreaseHome] = _settings.KeyBindings[GameAction.IncreaseAway];
            _settings.KeyBindings[GameAction.PenalizeHome] = _settings.KeyBindings[GameAction.PenalizeAway];

            HomeColor = VisitorColor;
            _settings.HomeScoreEffect = _settings.VisitorScoreEffect;

            _homePenaltyOneTimer = _visitorPenaltyOneTimer;
            _homePenaltyTwoTimer = _visitorPenaltyTwoTimer;


            VisitorTeam = nameBuffer;
            VisitorScore = scoreBuffer;

            VisitorPenaltyOne = penaltyOneBuffer;
            VisitorPenaltyTwo = penaltyTwoBuffer;

            ActiveVisitorPenaltyOne = activePenaltyOneBuffer;
            ActiveVisitorPenaltyTwo = activePenaltyTwoBuffer;

            _settings.KeyBindings[GameAction.IncreaseAway] = scoreActionBuffer;
            _settings.KeyBindings[GameAction.PenalizeAway] = penaltyActionBuffer;

            VisitorColor = colorBuffer;
            _settings.VisitorScoreEffect = ledActionBuffer;

            _visitorPenaltyOneTimer = penaltyTimerOneBuffer;
            _visitorPenaltyTwoTimer = penaltyTimerTwoBuffer;

            RefreshPenaltyTimers();
            IsReverse = !IsReverse;
            (HomeLogo, VisitorLogo) = (VisitorLogo, HomeLogo);
            SendStateToPlugin();
        }

        private void RefreshPenaltyTimers()
        {
            if (ActiveVisitorPenaltyOne)
                _visitorPenaltyOneTimer = new Timer((state)
                            =>
                { DecreasePenalty(TeamType.Visitor, 0); }, 0, 0, 1000);
            if (ActiveVisitorPenaltyTwo)
                _visitorPenaltyTwoTimer = new Timer((state)
                            =>
                { DecreasePenalty(TeamType.Visitor, 1); }, 0, 0, 1000);
            if (ActiveHomePenaltyOne)
                _homePenaltyOneTimer = new Timer((state)
                            =>
                { DecreasePenalty(TeamType.Home, 0); }, 0, 0, 1000);
            if (ActiveHomePenaltyTwo)
                _homePenaltyTwoTimer = new Timer((state)
                            =>
                { DecreasePenalty(TeamType.Home, 1); }, 0, 0, 1000);
        }
        #endregion

        #region SoundMethods
        private void StopTimeWarning() => _heartBeatPlayer.Stop();
        private void PlayFirstTimeWarning()
        {
            if (!_settings.SoundEnabled) return;
            _heartBeatPlayer.SoundLocation = AppDomain.CurrentDomain.BaseDirectory + "/Resources/Sounds/longHeartbeat.wav";
            _heartBeatPlayer.Play();
        }
        private void PlaySecondTimeWarning()
        {
            if (!_settings.SoundEnabled) return;
            _heartBeatPlayer.SoundLocation = AppDomain.CurrentDomain.BaseDirectory + "/Resources/Sounds/mediumHeartbeat.wav";
            _heartBeatPlayer.Stop();
            _heartBeatPlayer.Play();
        }
        private void PlayPenaltyExpireSound()
        {
            if (!_settings.SoundEnabled) return;
            _player.SoundLocation = AppDomain.CurrentDomain.BaseDirectory + "/Resources/Sounds/penaltyEndChime.wav";
            _player.Stop();
            _player.Play();
        }
        private void PlayScoreSound(TeamType type)
        {
            if (!_settings.SoundEnabled) return;
            _player.SoundLocation = FindGoalHorn(type)
                ?? AppDomain.CurrentDomain.BaseDirectory + "/Resources/Sounds/gameScore.wav";
            _player.Stop();
            _player.Play();
        }

        /// <summary>
        /// A team's custom goal horn: Resources/Sounds/Horns/&lt;TeamName&gt;.wav,
        /// matched case-insensitively against the scoring team's current name.
        /// </summary>
        private string? FindGoalHorn(TeamType type)
        {
            try
            {
                var team = type == TeamType.Home ? HomeTeam : VisitorTeam;
                var hornsDir = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "Sounds", "Horns");
                if (!System.IO.Directory.Exists(hornsDir)) return null;
                return System.IO.Directory.EnumerateFiles(hornsDir, "*.wav")
                    .FirstOrDefault(f => System.IO.Path.GetFileNameWithoutExtension(f)
                        .Equals(team, StringComparison.OrdinalIgnoreCase));
            }
            catch { return null; }
        }
        private void PlayPenaltySound()
        {
            if (!_settings.SoundEnabled) return;
            _player.SoundLocation = AppDomain.CurrentDomain.BaseDirectory + "/Resources/Sounds/penaltyStartbuzzer.wav";
            _player.Stop();
            _player.Play();
        }
        private void PlayStartStopSound()
        {
            if (!_settings.SoundEnabled) return;
            _player.SoundLocation = AppDomain.CurrentDomain.BaseDirectory + "/Resources/Sounds/cartoonWhistle.wav";
            _player.Stop();
            _player.Play();
        }
        private void PlayNewGameStartSound()
        {
            if (!_settings.SoundEnabled) return;
            _player.SoundLocation = AppDomain.CurrentDomain.BaseDirectory + "/Resources/Sounds/gameStart.wav";
            _player.Stop();
            _player.Play();
        }
        #endregion
    }
}
