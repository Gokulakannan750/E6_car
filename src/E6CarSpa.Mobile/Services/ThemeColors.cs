using E6CarSpa.Domain.Enums;

namespace E6CarSpa.Mobile.Services;

/// <summary>
/// Theme-aware colours applied from code-behind where XAML falls short: Picker.TextColor is
/// not reliably themed by the implicit style on Android (selected text renders grey-on-dark),
/// and bill status text gets semantic colours (Paid green, Cancelled red).
/// </summary>
public static class ThemeColors
{
    private static Color Res(string key) => (Color)Application.Current!.Resources[key];

    public static Color Text =>
        Application.Current?.RequestedTheme == AppTheme.Dark ? Res("DarkText") : Res("LightText");

    public static Color ForStatus(InvoiceStatus status) => status switch
    {
        InvoiceStatus.Paid      => Res("Success"),
        InvoiceStatus.Cancelled => Res("Error"),
        _                       => Text,
    };

    /// <summary>Call from OnAppearing — covers both the first load and later theme switches.</summary>
    public static void ApplyTo(params Picker[] pickers)
    {
        foreach (var p in pickers) p.TextColor = Text;
    }
}
