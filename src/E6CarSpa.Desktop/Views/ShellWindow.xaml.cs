using System.Windows;
using E6CarSpa.Desktop.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace E6CarSpa.Desktop.Views;

public partial class ShellWindow : Window
{
    private readonly ShellViewModel _vm;

    private readonly System.Windows.Threading.DispatcherTimer _inactivityTimer;

    public ShellWindow(ShellViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = vm;
        
        _inactivityTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMinutes(5)
        };
        _inactivityTimer.Tick += (_, _) =>
        {
            var api = App.Services.GetRequiredService<E6CarSpa.Desktop.Services.IApiClient>();
            if (api.IsLoggedIn)
            {
                api.Logout();
                _vm.NavigateAsync<DashboardViewModel>("Dashboard");
            }
        };

        // Reset timer on any mouse or keyboard input
        System.Windows.Input.InputManager.Current.PreProcessInput += (sender, e) =>
        {
            if (e.StagingItem.Input is System.Windows.Input.MouseEventArgs or System.Windows.Input.KeyboardEventArgs)
            {
                _inactivityTimer.Stop();
                _inactivityTimer.Start();
            }
        };
        
        Loaded += async (_, _) =>
        {
            _inactivityTimer.Start();
            await _vm.LoadLogoAsync();
            await _vm.NavigateAsync<DashboardViewModel>("Dashboard");
        };
    }

    private void Logout_Click(object sender, RoutedEventArgs e)
    {
        var api = App.Services.GetRequiredService<E6CarSpa.Desktop.Services.IApiClient>();
        api.Logout();
        _vm.NavigateAsync<DashboardViewModel>("Dashboard");
    }
}
