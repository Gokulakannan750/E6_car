using System.Globalization;

namespace E6CarSpa.Mobile.Converters;

/// <summary>
/// Negates a bool for bindings — MAUI has no built-in equivalent. Used to hide a control when a
/// flag is set (e.g. the delete button on an advance that is already marked obsolete).
/// </summary>
public class InverseBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is not bool b || !b;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is not bool b || !b;
}
