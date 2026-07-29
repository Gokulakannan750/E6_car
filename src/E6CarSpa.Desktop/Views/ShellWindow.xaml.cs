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
            var api = App.Services.GetRequiredService<E6CarSpa.Client.IApiClient>();
            if (api.IsLoggedIn) SignOutAndReauthenticate("locked after 5 minutes of inactivity");
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

    private void Logout_Click(object sender, RoutedEventArgs e) => SignOutAndReauthenticate(null);

    /// <summary>
    /// Clear the session and demand a fresh login. The app is login-first, so signing out must
    /// NOT leave the shell on screen — it hides, shows the login window, and either resumes with
    /// the new session or closes the app if the user declines.
    /// </summary>
    private void SignOutAndReauthenticate(string? reason)
    {
        _inactivityTimer.Stop();

        var api = App.Services.GetRequiredService<E6CarSpa.Client.IApiClient>();
        api.Logout();
        _vm.RefreshAuthState();

        Hide();
        var login = App.Services.GetRequiredService<LoginWindow>();
        if (reason is not null) login.Title = $"E6 Car Spa — {reason}";

        if (login.ShowDialog() != true)
        {
            Application.Current.Shutdown();
            return;
        }

        _vm.RefreshAuthState();
        Show();
        _inactivityTimer.Start();
        _ = _vm.NavigateAsync<DashboardViewModel>("Dashboard");
    }
}
