namespace Scoreboard.Models;

public enum GameAction
{
    IncreaseHome,
    IncreaseAway,
    PenalizeHome,
    PenalizeAway,
    Undo,
    Redo,
    PlayPause,
    Reset,
    ResetClock,
    None,
    SwapSides
}