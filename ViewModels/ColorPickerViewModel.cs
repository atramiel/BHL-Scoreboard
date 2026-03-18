using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Scoreboard.Windows;
using System.Windows;
using System.Windows.Media;

namespace Scoreboard.ViewModels
{
    public class ColorPickerViewModel : ObservableObject
    {
        public IRelayCommand Apply { get; set; }
        public IRelayCommand Cancel { get; set; }
        public EventHandler<Brush>? OnColorApply = null;

        private Dictionary<string, Brush> _colors = new()
        {
            {"Red", Brushes.Red},
            {"Orange", Brushes.Orange},
            {"Yellow", Brushes.Yellow},
            {"Green", Brushes.Green },
            {"Blue", Brushes.Blue },
            {"Indigo", Brushes.Indigo },
            {"Violet", Brushes.Violet },
            {"White", Brushes.White },
        };
        public Dictionary<string, Brush> ColorChoices
        {
            get => _colors;
            set => SetProperty(ref _colors, value);
        }
        private KeyValuePair<string, Brush> _selectedColor; public KeyValuePair<string, Brush> SelectedColor
        {
            get => _selectedColor;
            set => SetProperty(ref _selectedColor, value);
        }



        public ColorPickerViewModel()
        {
            Apply = new RelayCommand(HandleApply);
            Cancel = new RelayCommand(Close);
        }
        public void HandleApply()
        {
            OnColorApply?.Invoke(this, _colors[SelectedColor.Key]);
            Close();
        }
        public static void Close()
        {
            App.Current.Dispatcher.Invoke(() =>
            {
                if (App.Current.Windows.Count > 0)
                {
                    foreach (Window window in App.Current.Windows)
                        if (window.GetType() == typeof(ColorPickerWindow))
                            window.Close();
                }
            });
        }
    }
}
