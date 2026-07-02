using E6CarSpa.Mobile.Services;
using Plugin.Fingerprint;
using Plugin.Fingerprint.Abstractions;

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
            await SecureStorage.SetAsync("saved_password", password);
            
            // Swap to the tabbed monitor shell.
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

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        try
        {
            await CheckBiometricAvailabilityAsync();
        }
        catch (Exception)
        {
            // Biometric check is non-critical; silently ignore failures.
        }
    }

    private async Task CheckBiometricAvailabilityAsync()
    {
        if (!string.IsNullOrWhiteSpace(Settings.LastUsername))
        {
            var isAvailable = await CrossFingerprint.Current.IsAvailableAsync(true);
            if (isAvailable)
            {
                BiometricButton.IsVisible = true;
            }
        }
    }

    private void OnTogglePasswordClicked(object? sender, EventArgs e)
    {
        PasswordEntry.IsPassword = !PasswordEntry.IsPassword;
        TogglePasswordBtn.Opacity = PasswordEntry.IsPassword ? 0.5 : 1.0;
    }

    private async void OnBiometricClicked(object? sender, EventArgs e)
    {
        try
        {
            var request = new AuthenticationRequestConfiguration("Login", "Please authenticate to access E6 Car Spa");
            var result = await CrossFingerprint.Current.AuthenticateAsync(request);
            if (result.Authenticated)
            {
                var password = await SecureStorage.GetAsync("saved_password");
                if (!string.IsNullOrWhiteSpace(password))
                {
                    PasswordEntry.Text = password;
                    OnLoginClicked(this, EventArgs.Empty);
                }
                else
                {
                    ShowError("No password saved for biometric login. Please login with password first.");
                }
            }
        }
        catch (Exception)
        {
            ShowError("Biometric authentication failed. Please login with password.");
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
        BiometricButton.IsEnabled = !busy;
        if (busy) ErrorLabel.IsVisible = false;
    }
}
