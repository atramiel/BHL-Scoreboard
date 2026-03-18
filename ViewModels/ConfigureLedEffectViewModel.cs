using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Scoreboard.Models;
using Scoreboard.Windows;
using System.Windows;

namespace Scoreboard.ViewModels
{
    class ConfigureLedEffectViewModel : ObservableObject
    {
        public RelayCommand ApplyCommand { get; set; }
        public ConfigureLedEffectViewModel(ref GameSettings settings)
        {
            Settings = settings;
            ApplyCommand = new RelayCommand(Close);
        }
        public ConfigureLedEffectViewModel()
        {
            Settings = new();
            ApplyCommand = new RelayCommand(Close);
        }

        public GameSettings Settings { get; set; }



        private void Close()
        {
            foreach (var window in App.Current.Windows)
            {
                if (window.GetType() == typeof(ConfigureLedEffectWindow))
                {
                    var w = (Window)window;
                    w.Close();
                }
            }
        }
    }
}
