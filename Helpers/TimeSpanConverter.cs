using System.Globalization;
using System.Windows.Data;

namespace Scoreboard.Helpers;
internal class TimeSpanConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var span = (TimeSpan)value;
        if (span.TotalMinutes >= 1)
        {
            return $"{span.Minutes}:{span.Seconds.ToString("0#")}";
        }
        else if (span.TotalSeconds <= 60)
        {
            return $"{span.Seconds}";
        }
        else return span.ToString();
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return TimeSpan.Parse(value.ToString() ?? "00:00:00");
    }
}
