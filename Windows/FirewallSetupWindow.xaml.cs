using System.Windows;

namespace Scoreboard.Windows;

public partial class FirewallSetupWindow : Window
{
    public FirewallSetupWindow(string command)
    {
        InitializeComponent();
        CommandBox.Text = command;
    }

    private void CopyButton_Click(object sender, RoutedEventArgs e)
    {
        Clipboard.SetText(CommandBox.Text);
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
