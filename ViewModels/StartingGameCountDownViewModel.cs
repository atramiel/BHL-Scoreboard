using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Scoreboard.Windows;
using System.Media;
using System.Windows;

namespace Scoreboard.ViewModels
{
    class StartingGameCountDownViewModel : ObservableObject
    {
        private Timer? _timer;

        public IRelayCommand StartCountDownCommand { get; set; }
        public EventHandler? OnCompleted = null;

        private bool _soundEnabled;
        private readonly bool _isFinal;

        private TimeSpan _clock = TimeSpan.FromSeconds(5); public TimeSpan Clock
        {
            get => _clock;
            set => SetProperty(ref _clock, value);
        }
        private bool _isAlternateColor; public bool IsAlternateColor
        {
            get => _isAlternateColor;
            set => SetProperty(ref _isAlternateColor, value);
        }
        private bool _isFinalThree; public bool IsFinalThree
        {
            get => _isFinalThree;
            set => SetProperty(ref _isFinalThree, value);
        }
        private bool _isDone; public bool IsDone
        {
            get => _isDone;
            set => SetProperty(ref _isDone, value);
        }
        /// <summary>What the countdown says once it reaches zero, instead of "0".</summary>
        public string CompletionText => _isFinal ? "GAME OVER" : "GO!";

        public StartingGameCountDownViewModel()
        {
            StartCountDownCommand = new RelayCommand(StartCountDown);
            Clock = TimeSpan.FromSeconds(5);
        }
        public StartingGameCountDownViewModel(int seconds, bool sound = false, bool isFinal = false)
        {
            StartCountDownCommand = new RelayCommand(StartCountDown);
            Clock = TimeSpan.FromSeconds(seconds);
            _soundEnabled = sound;
            _isFinal = isFinal;
            if (sound)
            {
                var player = new SoundPlayer();
                player.SoundLocation = AppDomain.CurrentDomain.BaseDirectory + "/Resources/Sounds/shortHeartbeat.wav";
                player.Play();
            }
        }

        /// <summary>Per-second countdown beep — pitch rises for the final three, long blast at zero.</summary>
        private void Beep()
        {
            if (!_soundEnabled) return;
            var seconds = (int)Clock.TotalSeconds;
            _ = Task.Run(() =>
            {
                try
                {
                    if (seconds <= 0) Console.Beep(1400, 600);
                    else if (seconds <= 3) Console.Beep(1000, 160);
                    else Console.Beep(750, 110);
                }
                catch { /* no beep device — visuals still carry it */ }
            });
        }

        private void StartCountDown()
        {
            IsAlternateColor = false;
            _timer = new Timer(DecreaseClock, Clock, 0, 1000);
        }

        public void PauseCountdown()
        {
            if (IsDone) return; // let the GO!/GAME OVER beat finish and close on its own
            _timer?.Dispose();
            _timer = null;
        }

        public void ResumeCountdown()
        {
            if (Clock > TimeSpan.Zero)
                _timer = new Timer(DecreaseClock, null, 0, 1000);
        }

        public void CloseWindow()
        {
            PauseCountdown();
            App.Current.Dispatcher.BeginInvoke(() =>
            {
                foreach (Window window in App.Current.Windows)
                {
                    if (window.GetType() == typeof(StartingGameCountDownWindow))
                        window.Close();
                }
            });
        }

        public void DecreaseClock(object? state)
        {
            // Stop at "1" and jump straight to GO!/GAME OVER — "0" never displays
            if (Clock > TimeSpan.FromSeconds(1))
            {
                Clock -= new TimeSpan(0, 0, 1);
                IsAlternateColor = !IsAlternateColor;
                IsFinalThree = Clock <= TimeSpan.FromSeconds(3);
                Beep();
            }
            else if (!IsDone)
            {
                // Show GO!/GAME OVER for a beat instead of vanishing at zero
                IsDone = true;
                Beep();
                _timer?.Dispose();
                _timer = new Timer(FinishAfterPause, null, 900, Timeout.Infinite);
            }
        }

        private void FinishAfterPause(object? state)
        {
            _timer?.Dispose();
            OnCompleted?.Invoke(this, new EventArgs());
            App.Current.Dispatcher.Invoke(() =>
            {
                foreach (Window window in App.Current.Windows)
                    if (window.GetType() == typeof(StartingGameCountDownWindow))
                        window.Close();
            });
        }
    }
}
