using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using E6CarSpa.Client;
using E6CarSpa.Desktop.Services;
using Microsoft.Extensions.DependencyInjection;

namespace E6CarSpa.Desktop.ViewModels;

/// <summary>Hosts the navigation rail and the current page. Acts as the in-app navigation service.</summary>
public partial class ShellViewModel(IApiClient api) : ObservableObject
{
    [ObservableProperty] private object? _currentView;
    [ObservableProperty] private string _activeNav = "Dashboard";

    /// <summary>Company logo shown as a faint full-height watermark behind the sidebar.</summary>
    [ObservableProperty] private byte[]? _logoBytes;
    public bool HasLogo => LogoBytes is { Length: > 0 };
    partial void OnLogoBytesChanged(byte[]? value) => OnPropertyChanged(nameof(HasLogo));

    public async Task LoadLogoAsync()
    {
        try { LogoBytes = await api.GetLogoAsync(); } catch { /* watermark is optional */ }
    }

    // The app is login-first, so a session always has a user; the fallbacks only ever show
    // during the brief moment between logout and the login window taking over.
    public string UserName => api.CurrentUser?.FullName ?? "Signed out";
    public string RoleName => api.CurrentUser?.Role.ToString() ?? "";
    public bool IsLoggedIn => api.IsLoggedIn;
    public bool IsManagerOrAdmin =>
        api.CurrentUser?.Role is Domain.Enums.UserRole.Admin or Domain.Enums.UserRole.Manager;
    public bool IsAdmin => api.CurrentUser?.Role is Domain.Enums.UserRole.Admin;

    // Nav visibility follows the user's permissions, so people only see what they can actually
    // open. The server enforces the same set — hiding a button is convenience, not the boundary.
    private bool Can(Domain.Enums.Permission p) => api.CurrentUser?.Can(p) == true;

    public bool CanBilling => Can(Domain.Enums.Permission.Billing);
    public bool CanCustomers => Can(Domain.Enums.Permission.Customers);
    public bool CanCatalogue => Can(Domain.Enums.Permission.Catalogue);
    public bool CanStaffAdvances => Can(Domain.Enums.Permission.StaffAdvances);
    public bool CanReports => Can(Domain.Enums.Permission.Reports);
    public bool CanInventory => Can(Domain.Enums.Permission.Inventory);
    public bool CanSettings => Can(Domain.Enums.Permission.Settings) ||
                               Can(Domain.Enums.Permission.ManageUsers);

    public void RefreshAuthState()
    {
        OnPropertyChanged(nameof(UserName));
        OnPropertyChanged(nameof(RoleName));
        OnPropertyChanged(nameof(IsLoggedIn));
        OnPropertyChanged(nameof(IsManagerOrAdmin));
        OnPropertyChanged(nameof(IsAdmin));
        OnPropertyChanged(nameof(CanBilling));
        OnPropertyChanged(nameof(CanCustomers));
        OnPropertyChanged(nameof(CanCatalogue));
        OnPropertyChanged(nameof(CanStaffAdvances));
        OnPropertyChanged(nameof(CanReports));
        OnPropertyChanged(nameof(CanInventory));
        OnPropertyChanged(nameof(CanSettings));
    }

    [RelayCommand] private Task ShowDashboard() => NavigateAsync<DashboardViewModel>("Dashboard");
    [RelayCommand] private Task ShowCustomers() => NavigateAsync<CustomersViewModel>("Customers");
    [RelayCommand] private Task ShowNewJob() => NavigateAsync<NewJobViewModel>("NewJob");
    [RelayCommand] private Task ShowJobs() => NavigateAsync<JobsViewModel>("Jobs");
    [RelayCommand] private Task ShowInventory() => NavigateAsync<InventoryViewModel>("Inventory");
    [RelayCommand] private Task ShowCatalogue() => NavigateAsync<CatalogueViewModel>("Catalogue");
    [RelayCommand] private Task ShowStaffAdvances() => NavigateAsync<StaffAdvancesViewModel>("StaffAdvances");
    [RelayCommand] private Task ShowStaffSalaries() => NavigateAsync<StaffSalariesViewModel>("StaffSalaries");
    [RelayCommand] private Task ShowIncome() => NavigateAsync<IncomeViewModel>("Income");
    [RelayCommand] private Task ShowReports() => NavigateAsync<ReportsViewModel>("Reports");
    [RelayCommand] private Task ShowSettings() => NavigateAsync<SettingsViewModel>("Settings");

    /// <summary>Guards against a second navigation starting while one is still loading.</summary>
    private bool _navigating;

    public async Task NavigateAsync<TViewModel>(string navKey) where TViewModel : class
    {
        // No per-screen login prompt: the app is login-first, so every session is already
        // authenticated by the time any page is reachable. Role-based visibility (IsAdmin /
        // IsManagerOrAdmin) still governs WHICH pages a signed-in user may open, and the API
        // re-checks the role on every call.

        // Two clicks in quick succession used to race, and whichever load finished last won —
        // which could be the screen the user did NOT click.
        if (_navigating) return;
        _navigating = true;
        try
        {
            var vm = App.Services.GetRequiredService<TViewModel>();
            if (vm is IAsyncInitialize init) await init.InitializeAsync();

            // Commit only once the screen is actually ready. Setting ActiveNav first meant a
            // failed load left the sidebar highlighting a page the content area never showed.
            ActiveNav = navKey;
            CurrentView = vm;
        }
        catch (Exception ex)
        {
            // These commands are AsyncRelayCommands, whose faults land in a Task nobody awaits —
            // so without this the screen simply never opened and nothing was reported.
            AppLog.Error($"Could not open the '{navKey}' screen.", ex);
            MessageBox.Show(
                $"Could not open {navKey}.\n\n{ex.Message}",
                "E6 Car Spa", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally { _navigating = false; }
    }

    /// <summary>Open an existing invoice/quotation in the detail page.</summary>
    public async Task OpenInvoiceAsync(Guid invoiceId)
    {
        ActiveNav = "Jobs";
        var vm = App.Services.GetRequiredService<InvoiceDetailViewModel>();
        await vm.LoadAsync(invoiceId);
        CurrentView = vm;
    }
}
