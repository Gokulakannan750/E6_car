using System.Windows;
using System.Windows.Input;
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
            Close();
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
