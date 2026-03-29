using System.Globalization;

namespace WarpBusiness.MobileApp.Converters;

public class StatusToColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value?.ToString()?.ToLowerInvariant() switch
        {
            "active" or "paid" or "approved" or "completed" => Color.FromArgb("#4caf50"),
            "inactive" or "cancelled" or "void" or "rejected" => Color.FromArgb("#f44336"),
            "draft" or "pending" => Color.FromArgb("#ff9800"),
            "sent" or "submitted" => Color.FromArgb("#2196f3"),
            "overdue" => Color.FromArgb("#e91e63"),
            _ => Color.FromArgb("#9e9e9e")
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
