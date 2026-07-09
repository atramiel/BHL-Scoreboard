using System.Windows;
using System.Windows.Input;

namespace Scoreboard.Windows;

public partial class BetweenGameWindow : Window
{
    public BetweenGameWindow()
    {
        InitializeComponent();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Key.Escape)
            Close();
    }
}
