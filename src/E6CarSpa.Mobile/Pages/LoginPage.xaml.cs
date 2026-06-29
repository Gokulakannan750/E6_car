using E6CarSpa.Mobile.Services;

namespace E6CarSpa.Mobile.Pages;

public partial class LoginPage : ContentPage
{
    public LoginPage()
    {
        InitializeComponent();
        UsernameEntry.Text = Settings.LastUsername;
        ServerEntry.Text = Settings.ApiUrl;
    }

    private async void OnLoginClicked(object? sender, EventArgs e)
    {
        var username = UsernameEntry.Text?.Trim() ?? "";
        var password = PasswordEntry.Text ?? "";

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            ShowError("Enter username and password.");
            return;
        }

        // Persist + apply the server URL before the first call.
        Settings.ApiUrl = ServerEntry.Text ?? "";
        AppServices.Api.SetBaseUrl(Settings.ApiUrl);

        SetBusy(true);
        try
        {
            await AppServices.Api.LoginAsync(username, password);
            Settings.LastUsername = username;
            // Swap the whole window over to the tabbed monitor shell.
            if (Application.Current?.Windows.Count > 0)
                Application.Current.Windows[0].Page = new AppShell();
        }
        catch (ApiException ex)
        {
            ShowError(ex.Message);
        }
        catch (Exception)
        {
            ShowError("Cannot reach the server. Check the server address and your internet.");
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void ShowError(string message)
    {
        ErrorLabel.Text = message;
        ErrorLabel.IsVisible = true;
    }

    private void SetBusy(bool busy)
    {
        Busy.IsRunning = busy;
        Busy.IsVisible = busy;
        LoginButton.IsEnabled = !busy;
        if (busy) ErrorLabel.IsVisible = false;
    }
}
