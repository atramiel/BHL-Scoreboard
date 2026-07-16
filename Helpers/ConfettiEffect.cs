using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace Scoreboard.Helpers;

/// <summary>Continuous confetti rain: swaying, spinning pieces falling forever until Stop() clears them.</summary>
public static class ConfettiEffect
{
    private static readonly Random Rand = new();

    public static readonly Color[] DefaultPalette =
    [
        Color.FromRgb(232, 179, 76),   // gold (double weight)
        Color.FromRgb(232, 179, 76),
        Color.FromRgb(255, 215, 0),    // brighter gold
        Colors.White,
        Color.FromRgb(212, 63, 90),    // board red
        Color.FromRgb(125, 184, 232),  // ice blue
    ];

    /// <summary>Plain rectangle confetti (e.g. championship celebration).</summary>
    public static void Start(Canvas canvas, double width, double height, Color[]? palette = null, int count = 140)
    {
        var colors = palette ?? DefaultPalette;
        Start(canvas, width, height, count, () =>
        {
            var size = Rand.Next(9, 20);
            return new Rectangle
            {
                Width = size,
                Height = size * 0.55,
                Fill = new SolidColorBrush(colors[Rand.Next(colors.Length)]),
            };
        });
    }

    /// <summary>Emoji/glyph confetti (e.g. 💵 on a sponsors slide, ⚠️ on a safety slide) — same fall/sway/spin, just a different piece.</summary>
    public static void StartGlyphs(Canvas canvas, double width, double height, string[] glyphs, int count = 70)
    {
        if (glyphs.Length == 0) { Start(canvas, width, height, count: count); return; }
        Start(canvas, width, height, count, () => new TextBlock
        {
            Text = glyphs[Rand.Next(glyphs.Length)],
            FontSize = Rand.Next(28, 48),
        });
    }

    private static void Start(Canvas canvas, double width, double height, int count, Func<FrameworkElement> makePiece)
    {
        canvas.Children.Clear();
        if (width <= 0) width = 1920;
        if (height <= 0) height = 1080;

        for (var i = 0; i < count; i++)
        {
            var piece = makePiece();
            var rotate = new RotateTransform(Rand.Next(360));
            piece.RenderTransformOrigin = new Point(0.5, 0.5);
            piece.RenderTransform = rotate;

            var x = Rand.NextDouble() * width;
            Canvas.SetLeft(piece, x);
            Canvas.SetTop(piece, -30);
            canvas.Children.Add(piece);

            var delay = TimeSpan.FromMilliseconds(Rand.Next(0, 4500));
            piece.BeginAnimation(Canvas.TopProperty, new DoubleAnimation
            {
                From = -30,
                To = height + 40,
                BeginTime = delay,
                Duration = TimeSpan.FromSeconds(3.5 + Rand.NextDouble() * 3.5),
                RepeatBehavior = RepeatBehavior.Forever,
            });
            piece.BeginAnimation(Canvas.LeftProperty, new DoubleAnimation
            {
                From = x - 45,
                To = x + 45,
                BeginTime = delay,
                Duration = TimeSpan.FromSeconds(1.1 + Rand.NextDouble() * 1.6),
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever,
            });
            rotate.BeginAnimation(RotateTransform.AngleProperty, new DoubleAnimation
            {
                From = 0,
                To = Rand.Next(2) == 0 ? 360 : -360,
                BeginTime = delay,
                Duration = TimeSpan.FromSeconds(0.9 + Rand.NextDouble() * 1.8),
                RepeatBehavior = RepeatBehavior.Forever,
            });
        }
    }

    public static void Stop(Canvas canvas) => canvas.Children.Clear();
}
