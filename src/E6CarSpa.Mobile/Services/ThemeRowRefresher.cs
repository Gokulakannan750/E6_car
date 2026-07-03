namespace E6CarSpa.Mobile.Services;

/// <summary>
/// Works around a MAUI limitation: AppThemeBinding is NOT re-evaluated inside rows that were
/// already built from a DataTemplate, so after a runtime light/dark switch existing list rows
/// keep the previous theme's colours (e.g. black text on a dark card). A page holds one
/// instance and calls <see cref="RowsAreStale"/> from OnAppearing — the theme toggle lives on
/// the Settings tab, so an affected page is always re-entered through OnAppearing after a
/// switch. When it returns true the page re-sets its ItemsSource (null, then the data again),
/// which rebuilds the rows under the current theme.
/// </summary>
public sealed class ThemeRowRefresher
{
    private AppTheme _rowsBuiltUnder = CurrentTheme;

    private static AppTheme CurrentTheme => Application.Current?.RequestedTheme ?? AppTheme.Unspecified;

    public bool RowsAreStale()
    {
        var now = CurrentTheme;
        if (now == _rowsBuiltUnder) return false;
        _rowsBuiltUnder = now;
        return true;
    }
}
