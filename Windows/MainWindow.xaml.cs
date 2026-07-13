using Scoreboard.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace Scoreboard.Windows;
/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private readonly Random _rand = new();

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
                if (viewModel.GameDone && viewModel.IsChampionship) StartConfetti();
                else ConfettiCanvas.Children.Clear();
            });
        };
    }

    /// <summary>Continuous confetti rain: gold-heavy, swaying, spinning.</summary>
    private void StartConfetti()
    {
        ConfettiCanvas.Children.Clear();
        Color[] colors =
        [
            Color.FromRgb(232, 179, 76),   // gold (double weight)
            Color.FromRgb(232, 179, 76),
            Color.FromRgb(255, 215, 0),    // brighter gold
            Colors.White,
            Color.FromRgb(212, 63, 90),    // board red
            Color.FromRgb(125, 184, 232),  // ice blue
        ];
        var width = ActualWidth > 0 ? ActualWidth : 1920;
        var height = ActualHeight > 0 ? ActualHeight : 1080;

        for (var i = 0; i < 140; i++)
        {
            var size = _rand.Next(9, 20);
            var rotate = new RotateTransform(_rand.Next(360));
            var piece = new Rectangle
            {
                Width = size,
                Height = size * 0.55,
                Fill = new SolidColorBrush(colors[_rand.Next(colors.Length)]),
                RenderTransformOrigin = new Point(0.5, 0.5),
                RenderTransform = rotate,
            };
            var x = _rand.NextDouble() * width;
            Canvas.SetLeft(piece, x);
            Canvas.SetTop(piece, -30);
            ConfettiCanvas.Children.Add(piece);

            var delay = TimeSpan.FromMilliseconds(_rand.Next(0, 4500));
            piece.BeginAnimation(Canvas.TopProperty, new DoubleAnimation
            {
                From = -30,
                To = height + 40,
                BeginTime = delay,
                Duration = TimeSpan.FromSeconds(3.5 + _rand.NextDouble() * 3.5),
                RepeatBehavior = RepeatBehavior.Forever,
            });
            piece.BeginAnimation(Canvas.LeftProperty, new DoubleAnimation
            {
                From = x - 45,
                To = x + 45,
                BeginTime = delay,
                Duration = TimeSpan.FromSeconds(1.1 + _rand.NextDouble() * 1.6),
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever,
            });
            rotate.BeginAnimation(RotateTransform.AngleProperty, new DoubleAnimation
            {
                From = 0,
                To = _rand.Next(2) == 0 ? 360 : -360,
                BeginTime = delay,
                Duration = TimeSpan.FromSeconds(0.9 + _rand.NextDouble() * 1.8),
                RepeatBehavior = RepeatBehavior.Forever,
            });
        }
    }

    private void Window_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        this.Focus();
    }
}
