using E6CarSpa.Client;
using E6CarSpa.Contracts;
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

            // Temporary machine-generated password: the API refuses everything except setting a
            // new one, so handle it here rather than opening a shell where nothing works.
            if (AppServices.Api.MustChangePassword)
            {
                await ForcePasswordChangeAsync(password);
                return;
            }

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

    /// <summary>
    /// Walk the user through replacing a temporary password. On success the server revokes the
    /// token we just got (the security stamp rotates), so the user signs in again with the new
    /// password — which is also why the biometric copy is cleared.
    /// </summary>
    private async Task ForcePasswordChangeAsync(string currentPassword)
    {
        var next = await DisplayPromptAsync(
            "Set a new password",
            "This account uses a temporary password that was generated automatically. Choose your own password to continue.",
            "Save", "Cancel", placeholder: "New password (min 8 characters)", maxLength: 200);

        if (string.IsNullOrEmpty(next))
        {
            AppServices.Api.Logout();
            ShowError("You must set a new password before using this account.");
            return;
        }
        if (next.Length < 8)
        {
            AppServices.Api.Logout();
            ShowError("Password must be at least 8 characters. Please sign in and try again.");
            return;
        }

        var confirm = await DisplayPromptAsync("Confirm password", "Enter the new password again.",
            "Save", "Cancel", placeholder: "Confirm new password", maxLength: 200);
        if (confirm != next)
        {
            AppServices.Api.Logout();
            ShowError("The passwords did not match. Please sign in and try again.");
            return;
        }

        try
        {
            await AppServices.Api.ChangeMyPasswordAsync(new ChangeMyPasswordRequest(currentPassword, next));
            SecureStorage.Remove("saved_password");
            PasswordEntry.Text = "";
            await DisplayAlertAsync("Password updated",
                "Your password has been changed. Please sign in with your new password.", "OK");
        }
        catch (Exception ex)
        {
            AppServices.Api.Logout();
            ShowError(ex is ApiException a ? a.Message : "Could not change the password. Please try again.");
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
