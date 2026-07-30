using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using E6CarSpa.Desktop.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace E6CarSpa.Desktop.Views;

public partial class LoginWindow : Window
{
    private readonly LoginViewModel _vm;

    public LoginWindow(LoginViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = vm;
        MouseLeftButtonDown += (_, e) => { if (e.ButtonState == MouseButtonState.Pressed) DragMove(); };
    }

    private async void Login_Click(object sender, RoutedEventArgs e) => await TryLogin();

    private async void PasswordBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) await TryLogin();
    }

    private async Task TryLogin()
    {
        if (!await _vm.LoginAsync(PasswordBox.Password)) return;

        var api = App.Services.GetRequiredService<E6CarSpa.Client.IApiClient>();
        if (api.MustChangePassword)
        {
            // Temporary machine-generated password: the API allows nothing else, so force the
            // change here instead of letting the shell open and fail every call.
            var dialog = new ForcePasswordChangeWindow(api, PasswordBox.Password) { Owner = this };
            var changed = dialog.ShowDialog() == true;

            // Either way the session is gone — the password change revoked the token, and a
            // declined change must not grant access. Stay on this window for a fresh sign-in.
            api.Logout();
            PasswordBox.Clear();
            _vm.ErrorMessage = changed
                ? "Password updated. Please sign in with your new password."
                : "You must set a new password before using this account.";
            return;
        }

        // Signal success to the caller. Shown with ShowDialog() at startup (and for
        // re-authentication after a 401), so DialogResult is how App/Shell tells a real
        // login apart from the user closing the window.
        if (ComponentDispatcher.IsThreadModal) DialogResult = true;
        Close();
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        if (ComponentDispatcher.IsThreadModal) DialogResult = false;
        Close();
    }
}
