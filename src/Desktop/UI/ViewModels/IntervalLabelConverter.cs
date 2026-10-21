using System.Globalization;
using Avalonia.Data.Converters;

namespace CodexMonitor.UI.ViewModels;

public sealed class IntervalLabelConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is int minutes ? minutes == 0 ? "Manual" : $"{minutes} min" : "—";

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
