using E6CarSpa.Mobile.Services;

namespace E6CarSpa.Mobile.Pages;

public partial class SettingsPage : ContentPage
{
    public SettingsPage()
    {
        InitializeComponent();
        var user = AppServices.Api.CurrentUser;
        UserLabel.Text = user?.FullName ?? "—";
        RoleLabel.Text = user?.Role.ToString() ?? "";
        ServerEntry.Text = Settings.ApiUrl;

        // Initialise the toggle to match the current theme
        ThemeSwitch.IsToggled = Application.Current?.UserAppTheme == AppTheme.Dark;
    }

    private void OnThemeToggled(object? sender, ToggledEventArgs e)
    {
        if (Application.Current is null) return;

        Application.Current.UserAppTheme = e.Value ? AppTheme.Dark : AppTheme.Light;

        // Persist so it survives app restarts
        Preferences.Set("AppTheme", e.Value ? "Dark" : "Light");
    }

    private void OnSaveClicked(object? sender, EventArgs e)
    {
        Settings.ApiUrl = ServerEntry.Text ?? "";
        AppServices.Api.SetBaseUrl(Settings.ApiUrl);
        ServerEntry.Text = Settings.ApiUrl;
        SavedLabel.IsVisible = true;
    }

    private async void OnLogoutClicked(object? sender, EventArgs e)
    {
        bool ok = await DisplayAlertAsync("Log out", "Sign out of the monitor?", "Log out", "Cancel");
        if (!ok) return;

        AppServices.Api.Logout();
        if (Application.Current?.Windows.Count > 0)
            Application.Current.Windows[0].Page = new LoginPage();
    }
}
