using System.Globalization;
using System.Windows.Data;
using Visibility = System.Windows.Visibility;

namespace RemoteHub.Converters;

/// <summary>
/// Returns <see cref="Visibility.Visible"/> when the bound <see cref="double"/> is greater than zero,
/// otherwise <see cref="Visibility.Collapsed"/>. Surfaces the tab-strip overflow ("...") button only
/// while the header actually overflows (ScrollViewer.ScrollableWidth &gt; 0).
/// </summary>
public sealed class GreaterThanZeroToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is double d && d > 0.0 ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
