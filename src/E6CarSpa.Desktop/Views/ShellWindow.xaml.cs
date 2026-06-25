using System.Windows;
using E6CarSpa.Desktop.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace E6CarSpa.Desktop.Views;

public partial class ShellWindow : Window
{
    private readonly ShellViewModel _vm;

    public ShellWindow(ShellViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = vm;
        Loaded += async (_, _) =>
        {
            await _vm.LoadLogoAsync();
            await _vm.NavigateAsync<DashboardViewModel>("Dashboard");
        };
    }

    private void Logout_Click(object sender, RoutedEventArgs e)
    {
        var login = App.Services.GetRequiredService<LoginWindow>();
        login.Show();
        Close();
    }
}
