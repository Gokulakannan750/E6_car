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
        if (await _vm.LoginAsync(PasswordBox.Password))
        {
            // Signal success to the caller. Shown with ShowDialog() at startup (and for
            // re-authentication after a 401), so DialogResult is how App/Shell tells a real
            // login apart from the user closing the window.
            if (ComponentDispatcher.IsThreadModal) DialogResult = true;
            Close();
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        if (ComponentDispatcher.IsThreadModal) DialogResult = false;
        Close();
    }
}
