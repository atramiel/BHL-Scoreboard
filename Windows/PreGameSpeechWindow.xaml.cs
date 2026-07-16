using Scoreboard.Helpers;
using Scoreboard.ViewModels;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;

namespace Scoreboard.Windows;

public partial class PreGameSpeechWindow : Window
{
    public PreGameSpeechWindow()
    {
        InitializeComponent();
        Loaded += (_, _) => BurstConfetti();
        DataContextChanged += (_, e) =>
        {
            if (e.OldValue is PreGameSpeechViewModel oldVm) oldVm.PropertyChanged -= OnViewModelPropertyChanged;
            if (e.NewValue is PreGameSpeechViewModel newVm) newVm.PropertyChanged += OnViewModelPropertyChanged;
        };
    }

    // Every slide change gets a quick pop-in — small scale bounce from 0.85 to 1.0 —
    // plus a fresh confetti set. Confetti keeps falling continuously for as long as
    // that slide is up (slides stay up however long the operator is talking, not a
    // fixed duration) — a light, lower-density fall rather than a short burst that
    // cuts off mid-fall (some pieces start falling up to 4.5s late, so a 3s auto-stop
    // was clearing the canvas before they'd even appeared — the abrupt cutoff read
    // as jarring rather than exciting).
    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(PreGameSpeechViewModel.CurrentText)) return;
        var animation = new DoubleAnimation
        {
            From = 0.85,
            To = 1.0,
            Duration = TimeSpan.FromMilliseconds(280),
            EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.4 }
        };
        ContentScale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty, animation);
        ContentScale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleYProperty, animation);
        BurstConfetti();
    }

    private void BurstConfetti()
    {
        if (DataContext is PreGameSpeechViewModel vm)
            ConfettiEffect.StartGlyphs(ConfettiCanvas, ActualWidth, ActualHeight, vm.ConfettiGlyphs, count: 35);
        else
            ConfettiEffect.Start(ConfettiCanvas, ActualWidth, ActualHeight, count: 35);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Key.Escape)
            Close();
    }
}
