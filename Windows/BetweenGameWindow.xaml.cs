using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Scoreboard.ViewModels;

namespace Scoreboard.Windows;

public partial class BetweenGameWindow : Window
{
    // A slow, comfortable reading pace — content further off gets proportionally
    // more time instead of every panel being squeezed into the same fixed window.
    private const double ScrollPixelsPerSecond = 40.0;
    private static readonly TimeSpan ScrollBeginDelay = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan ScrollEndHold = TimeSpan.FromSeconds(1.5);
    // Generous sanity bound, not a real limiter — a genuinely long bio/roster (e.g.
    // EVAC, BlueShift) needs more than a few seconds to scroll through at a
    // readable pace; this only guards against a pathological data-entry mistake.
    private static readonly TimeSpan MaxAttractDwell = TimeSpan.FromSeconds(90);

    private BetweenGameViewModel? _viewModel;
    private readonly DispatcherTimer _scrollTimer = new() { Interval = TimeSpan.FromMilliseconds(30) };
    private DateTime _leftPanelStart = DateTime.MinValue;
    private DateTime _rightPanelStart = DateTime.MinValue;
    private bool _leftDwellReported;
    private bool _rightDwellReported;

    public BetweenGameWindow()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        _scrollTimer.Tick += (_, _) =>
        {
            UpdateSide(LeftAttractScroll, _leftPanelStart, ref _leftDwellReported);
            UpdateSide(RightAttractScroll, _rightPanelStart, ref _rightDwellReported);
        };
        _scrollTimer.Start();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Key.Escape)
            Close();
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_viewModel != null) _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        _viewModel = e.NewValue as BetweenGameViewModel;
        if (_viewModel != null) _viewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    // "A new panel was actually selected" (CurrentLeft/CurrentRight changed) resets
    // that side back to the top immediately — synchronously (on the UI thread —
    // see Dispatcher.BeginInvoke below), no measurement or debounce needed for the
    // reset itself, which is what kills the flicker: the starting position is
    // always correct the instant a new panel appears. Whether and how far it
    // needs to scroll is then discovered gradually by the polling timer below,
    // using ScrollViewer's own natively-tracked ScrollableHeight instead of any
    // hand-measured height (which kept racing layout timing).
    //
    // The attract carousel's swap timer (BetweenGameViewModel._attractTimer) is a
    // plain System.Threading.Timer firing on a ThreadPool thread, so this handler
    // can run off the UI thread — touching the ScrollViewer directly from here
    // crashes with "the calling thread cannot access this object because a
    // different thread owns it." Dispatcher.BeginInvoke marshals the actual work
    // back onto the UI thread.
    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(BetweenGameViewModel.CurrentLeft))
        {
            Dispatcher.BeginInvoke(() =>
            {
                _leftPanelStart = DateTime.Now;
                _leftDwellReported = false;
                LeftAttractScroll.ScrollToVerticalOffset(0);
            });
        }
        else if (e.PropertyName == nameof(BetweenGameViewModel.CurrentRight))
        {
            Dispatcher.BeginInvoke(() =>
            {
                _rightPanelStart = DateTime.Now;
                _rightDwellReported = false;
                RightAttractScroll.ScrollToVerticalOffset(0);
            });
        }
    }

    // Attract panels are an unattended kiosk display — nobody's there to scroll a
    // long motto or full bot roster into view. Content shorter than the viewport
    // is centered automatically (see VerticalContentAlignment in XAML) and never
    // scrolls (ScrollableHeight is 0). Content taller than the viewport holds for
    // ScrollBeginDelay, then scrolls at a constant readable pace, computed fresh
    // against ScrollableHeight every tick — self-correcting rather than measuring
    // once and hoping layout had already settled.
    private void UpdateSide(ScrollViewer scroll, DateTime panelStart, ref bool dwellReported)
    {
        if (panelStart == DateTime.MinValue) return;

        var scrollable = scroll.ScrollableHeight;
        if (scrollable <= 0) return;

        if (!dwellReported)
        {
            var scrollDuration = TimeSpan.FromSeconds(Math.Max(4.0, scrollable / ScrollPixelsPerSecond));
            var needed = ScrollBeginDelay + scrollDuration + ScrollEndHold;
            if (needed > MaxAttractDwell) needed = MaxAttractDwell;
            _viewModel?.ExtendCurrentAttractDwell(needed);
            dwellReported = true;
        }

        var elapsed = DateTime.Now - panelStart - ScrollBeginDelay;
        if (elapsed < TimeSpan.Zero) return; // still holding at the top

        var target = Math.Min(scrollable, elapsed.TotalSeconds * ScrollPixelsPerSecond);
        scroll.ScrollToVerticalOffset(target);
    }
}
