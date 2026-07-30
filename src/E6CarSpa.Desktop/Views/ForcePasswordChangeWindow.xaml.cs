using System.Windows;
using System.Windows.Input;
using E6CarSpa.Client;
using E6CarSpa.Contracts;

namespace E6CarSpa.Desktop.Views;

/// <summary>
/// Shown immediately after signing in with a machine-generated password (the first-run admin, or an
/// account rotated off a known default). The API refuses everything except the change-password call
/// until this succeeds, so there is deliberately no way past this window other than setting a
/// password — or closing it, which abandons the sign-in.
/// </summary>
public partial class ForcePasswordChangeWindow : Window
{
    private readonly IApiClient _api;
    private readonly string _currentPassword;

    public ForcePasswordChangeWindow(IApiClient api, string currentPassword)
    {
        InitializeComponent();
        _api = api;
        _currentPassword = currentPassword;
        Loaded += (_, _) => NewBox.Focus();
    }

    private async void ConfirmBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) await SaveAsync();
    }

    private async void Save_Click(object sender, RoutedEventArgs e) => await SaveAsync();

    private async Task SaveAsync()
    {
        var next = NewBox.Password;

        if (next.Length < 8) { ShowError("The new password must be at least 8 characters."); return; }
        if (next != ConfirmBox.Password) { ShowError("The two passwords do not match."); return; }
        if (next == _currentPassword) { ShowError("Choose a different password from the temporary one."); return; }

        SaveButton.IsEnabled = false;
        try
        {
            // Succeeds → the server clears MustChangePassword and rotates the security stamp, which
            // revokes the token we authenticated with; IApiClient drops the session to match. The
            // caller therefore sends the user back to a normal sign-in with the new password.
            await _api.ChangeMyPasswordAsync(new ChangeMyPasswordRequest(_currentPassword, next));
            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            ShowError(ex is ApiException a ? a.Message : "Could not change the password. Please try again.");
            SaveButton.IsEnabled = true;
        }
    }

    private void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorText.Visibility = Visibility.Visible;
    }
}
