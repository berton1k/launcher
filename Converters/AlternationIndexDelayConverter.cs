using System;
using System.Globalization;
using System.Windows.Data;

namespace Launcher.Converters;

public sealed class AlternationIndexDelayConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int index)
        {
            var delayMs = 35 * Math.Max(0, index);
            return TimeSpan.FromMilliseconds(delayMs);
        }

        return TimeSpan.Zero;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return 0;
    }
}
