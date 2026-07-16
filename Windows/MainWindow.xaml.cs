using Scoreboard.Helpers;
using Scoreboard.ViewModels;
using System.Windows;
using System.Windows.Input;

namespace Scoreboard.Windows;
/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        var viewModel = new MainWindowViewModel();
        DataContext = viewModel;
        this.Closing += (_, _) => viewModel.Dispose();

        // Confetti when a championship game ends; cleared on reset
        viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName != nameof(MainWindowViewModel.GameDone)) return;
            Dispatcher.BeginInvoke(() =>
            {
                if (viewModel.GameDone && viewModel.IsChampionship) ConfettiEffect.Start(ConfettiCanvas, ActualWidth, ActualHeight);
                else ConfettiEffect.Stop(ConfettiCanvas);
            });
        };
    }

    private void Window_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        this.Focus();
    }
}
