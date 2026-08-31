using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace RiseOverlay.UI.Converters;

public sealed class BoolToVisibilityConverter : IValueConverter
{
    public bool Invert { get; set; }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var flag = value is true;
        if (Invert)
            flag = !flag;
        return flag ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is Visibility.Visible ^ Invert;
}

public sealed class FractionToLeftMarginConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Length < 2
            || values[0] is not double fraction
            || values[1] is not double width
            || width <= 0)
            return new Thickness(0);

        var left = Math.Clamp(fraction, 0, 1) * width - 1;
        return new Thickness(Math.Max(0, left), 0, 0, 0);
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
