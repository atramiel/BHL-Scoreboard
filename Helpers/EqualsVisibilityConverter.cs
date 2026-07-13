using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Scoreboard.Helpers;

/// <summary>Visible when the bound value's string form equals the converter parameter.</summary>
public class EqualsVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        string.Equals(value?.ToString(), parameter?.ToString(), StringComparison.Ordinal)
            ? Visibility.Visible
            : Visibility.Collapsed;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
