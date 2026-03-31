using Scoreboard.Windows;
using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace Scoreboard;
/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private void Application_Startup(object sender, StartupEventArgs e)
    {
        DispatcherUnhandledException += (_, args) =>
        {
            File.WriteAllText("crash.log", args.Exception.ToString());
            args.Handled = false;
        };
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            File.WriteAllText("crash.log", args.ExceptionObject?.ToString() ?? "unknown");

        this.MainWindow = new MainWindow();
        this.MainWindow.Show();
    }
}

