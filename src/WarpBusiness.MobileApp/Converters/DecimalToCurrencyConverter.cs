using System.Globalization;

namespace WarpBusiness.MobileApp.Converters;

public class DecimalToCurrencyConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is decimal d)
            return d.ToString("C2", CultureInfo.CurrentCulture);
        return "$0.00";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
