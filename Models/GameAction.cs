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
    SwapSides,
    IncreaseNextMatch,
    DecreaseNextMatch,
    StartNextMatch,
    BetweenGame,
    SelectMatch0,
    SelectMatch1,
    SelectMatch2,
    SelectMatch3,
    SelectMatch4,
    SelectMatch5,
    HalfTime,
    OnDeckMatch0,
    OnDeckMatch1,
    OnDeckMatch2,
    OnDeckMatch3,
    OnDeckMatch4,
    OnDeckMatch5,
    PreGameSpeech
}