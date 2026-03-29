using System.Globalization;

namespace WarpBusiness.MobileApp.Converters;

public class BoolToColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool b && b)
            return Color.FromArgb("#00c8ff");
        return Color.FromArgb("#666666");
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
