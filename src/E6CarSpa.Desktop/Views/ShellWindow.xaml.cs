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

        // Size to the screen's work area minus a visible margin on all four sides, then
        // center — rather than letting the window touch the screen edges.
        const double margin = 40;
        var work = SystemParameters.WorkArea;
        Width = Math.Max(MinWidth, Math.Min(Width, work.Width - margin * 2));
        Height = Math.Max(MinHeight, Math.Min(Height, work.Height - margin * 2));
        Left = work.Left + (work.Width - Width) / 2;
        Top = work.Top + (work.Height - Height) / 2;

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
