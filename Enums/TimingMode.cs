namespace Scoreboard.Enums
{
    public enum TimingMode
    {
        StopTime,
        RunTime
    }

    public static class TimingModeValues
    {
        public static TimingMode[] All { get; } = [TimingMode.StopTime, TimingMode.RunTime];
    }
}
