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
        this.DataContext = new MainWindowViewModel();
        this.Closing += (_, _) => (DataContext as MainWindowViewModel)?.Dispose();
    }

    private void Window_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        this.Focus();
    }
}