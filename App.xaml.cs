using Scoreboard.Windows;
using System.Windows;

namespace Scoreboard;
/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private void Application_Startup(object sender, StartupEventArgs e)
    {
        this.MainWindow = new MainWindow();
        this.MainWindow.Show();
    }
}

