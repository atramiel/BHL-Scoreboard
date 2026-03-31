using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Scoreboard.Enums;
using Scoreboard.Models;
using Scoreboard.Services;
using Scoreboard.Windows;
using System.Diagnostics;
using System.Media;
using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

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
        private StartingGameCountDownViewModel? _finalCountdown;
        private BetweenGameViewModel? _betweenGameViewModel;
        private BetweenGameWindow? _betweenGameWindow;
        private List<PendingMatch> _pendingMatches = [];
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
            var viewModel = new StartingGameCountDownViewModel(seconds, _settings.SoundEnabled);
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
            var viewModel = new ConfigurationViewModel();
            config.DataContext = viewModel;
            config.Owner = App.Current.MainWindow;
            config.ShowDialog();

            _settings = viewModel.Settings;
            _keyBindings = _settings.KeyBindings.ToDictionary<GameAction, Key>();
            ResetGameState();
        }
        #endregion

        #region GameLogicMethods
        private void ExecuteGameCommand(GameAction gameAction)
        {

            switch (gameAction)
            {
                case GameAction.IncreaseHome:
                    ApplyLightingEffect(LightingType.HomeScore);
                    Pause();
                    AdvanceScore(TeamType.Home);
                    break;
                case GameAction.IncreaseAway:
                    ApplyLightingEffect(LightingType.VisitorScore);
                    Pause();
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
                    SwapSides();
                    break;
                case GameAction.IncreaseNextMatch:
                    _betweenGameViewModel?.Adjust(1);
                    SendStateToPlugin();
                    break;
                case GameAction.DecreaseNextMatch:
                    _betweenGameViewModel?.Adjust(-1);
                    SendStateToPlugin();
                    break;
                case GameAction.StartNextMatch:
                    _betweenGameViewModel?.StartCountdown();
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
                        HomeTeam = match.Player1Name;
                        VisitorTeam = match.Player2Name;
                        _settings.HomeTeamName = match.Player1Name;
                        _settings.VisitorTeamName = match.Player2Name;
                        if (_betweenGameViewModel != null)
                            _betweenGameViewModel.NextUpDisplay = match.Label;
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

            _ = _tcpBridge?.SendStateAsync(
                HomeTeam, VisitorTeam,
                HomeScore, VisitorScore,
                $"{(int)GameClock.TotalMinutes:D2}:{GameClock.Seconds:D2}",
                IsRunning, GameDone, nextMatch, slots);

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
                GameFinished();
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
            PlayScoreSound();
            switch (type)
            {
                case TeamType.Home:
                    _undoRedo?.Cache(this);
                    HighlightScore(type);
                    HomeScore++;
                    break;
                case TeamType.Visitor:
                    _undoRedo?.Cache(this);
                    HighlightScore(type);
                    VisitorScore++;
                    break;
                default:
                    break;
            }
            if (IsSuddenDeath) GameDone = true;
            SendStateToPlugin();
        }


        private void AddPenalty(TeamType type)
        {
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
            if (HomeScore != 0 && HomeScore == VisitorScore)
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
            }
        }

        private async void ShowBetweenGameWindow()
        {
            _betweenGameViewModel = new BetweenGameViewModel(_settings.BracketUrl, _settings.LearnMoreUrl);
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
            _betweenGameWindow.Show();

            // Fetch Challonge matches in background (only triggers on open, ~2 API calls)
            if (!string.IsNullOrWhiteSpace(_settings.ChallongeApiKey)
                && !string.IsNullOrWhiteSpace(_settings.BracketUrl))
            {
                _pendingMatches = await ChallongeService.FetchOpenMatchesAsync(
                    _settings.BracketUrl, _settings.ChallongeApiKey);

                if (_betweenGameViewModel != null && _pendingMatches.Count > 0)
                    _betweenGameViewModel.NextUpDisplay = _pendingMatches[0].Label;
            }

            SendStateToPlugin();
        }

        private void CloseBetweenGameWindow()
        {
            _betweenGameViewModel?.Dispose();
            _betweenGameViewModel = null;
            _betweenGameWindow?.Close();
            _betweenGameWindow = null;
        }
        private void TriggerSuddenDeath()
        {
            IsSuddenDeath = true;
            GameDone = false;
        }
        #endregion

        #region GameStateMethods
        private async Task LoadSettingsAsync()
        {
            _settings = await ConfigurationViewModel.LoadSettingsAsync();
            _keyBindings = _settings.KeyBindings.ToDictionary<GameAction, Key>();
            ResetGameState();
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
        }
        private void SwapSides()
        {
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
            _settings.KeyBindings[GameAction.IncreaseHome] = penaltyActionBuffer;

            VisitorColor = colorBuffer;
            _settings.HomeScoreEffect = ledActionBuffer;

            _visitorPenaltyOneTimer = penaltyTimerOneBuffer;
            _visitorPenaltyTwoTimer = penaltyTimerTwoBuffer;

            RefreshPenaltyTimers();
            IsReverse = !IsReverse;
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
        private void PlayScoreSound()
        {
            if (!_settings.SoundEnabled) return;
            _player.SoundLocation = AppDomain.CurrentDomain.BaseDirectory + "/Resources/Sounds/gameScore.wav";
            _player.Stop();
            _player.Play();
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
