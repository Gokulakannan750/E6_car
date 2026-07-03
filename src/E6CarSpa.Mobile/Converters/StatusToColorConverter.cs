using System.Globalization;
using E6CarSpa.Domain.Enums;
using E6CarSpa.Mobile.Services;

namespace E6CarSpa.Mobile.Converters;

/// <summary>Colours a bill's status text in lists: Paid green, Cancelled red, others theme text.</summary>
public sealed class StatusToColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is InvoiceStatus s ? ThemeColors.ForStatus(s) : ThemeColors.Text;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
