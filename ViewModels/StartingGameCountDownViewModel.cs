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

        public StartingGameCountDownViewModel()
        {
            StartCountDownCommand = new RelayCommand(StartCountDown);
            Clock = TimeSpan.FromSeconds(5);
            var player = new SoundPlayer();
        }
        public StartingGameCountDownViewModel(int seconds, bool sound = false)
        {
            StartCountDownCommand = new RelayCommand(StartCountDown);
            Clock = TimeSpan.FromSeconds(seconds);
            var player = new SoundPlayer();
            if (sound)
            {
                player.SoundLocation = AppDomain.CurrentDomain.BaseDirectory + "/Resources/Sounds/shortHeartbeat.wav";
                player.Play();
            }
        }

        private void StartCountDown()
        {
            IsAlternateColor = false;
            _timer = new Timer(DecreaseClock, Clock, 0, 1000);
        }

        public void PauseCountdown()
        {
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
            if (Clock > TimeSpan.Zero)
            {
                Clock -= new TimeSpan(0, 0, 1);
                IsAlternateColor = !IsAlternateColor;
            }
            else
            {
                OnCompleted?.Invoke(this, new EventArgs());
                _timer?.Dispose();
                App.Current.Dispatcher.Invoke(() =>
                {
                    if (App.Current.Windows.Count > 0)
                    {
                        foreach (Window window in App.Current.Windows)
                        {
                            if (window.GetType() == typeof(StartingGameCountDownWindow))
                            {
                                window.Close();
                            }
                        }
                    }
                });
            }
        }
    }
}
