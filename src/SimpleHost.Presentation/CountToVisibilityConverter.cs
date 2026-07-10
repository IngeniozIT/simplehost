using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace SimpleHost.Presentation;

/// <summary>
/// Converts a collection count (an <see cref="int"/>) to <see cref="Visibility"/>: visible when the
/// count is greater than zero, collapsed otherwise. Used to reveal the contextual bulk-action bar only
/// while at least one entry is selected.
/// </summary>
public sealed class CountToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is int count && count > 0 ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
