using System;
using System.Globalization;
using System.Windows.Data;

namespace Launcher.Converters;

public sealed class SliderFillWidthConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 4)
        {
            return 0d;
        }

        if (values[0] is double width &&
            values[1] is double minimum &&
            values[2] is double maximum &&
            values[3] is double value)
        {
            var range = maximum - minimum;
            if (range <= 0)
            {
                return 0d;
            }

            var progress = (value - minimum) / range;
            if (double.IsNaN(progress) || double.IsInfinity(progress))
            {
                return 0d;
            }

            progress = Math.Clamp(progress, 0d, 1d);
            return width * progress;
        }

        return 0d;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
