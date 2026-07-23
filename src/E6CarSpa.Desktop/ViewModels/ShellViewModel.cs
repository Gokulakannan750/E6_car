using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using E6CarSpa.Client;
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

    public string UserName => api.CurrentUser?.FullName ?? "Shop Floor";
    public string RoleName => api.CurrentUser?.Role.ToString() ?? "Basic Access";
    public bool IsLoggedIn => api.IsLoggedIn;
    public bool IsManagerOrAdmin =>
        api.CurrentUser?.Role is Domain.Enums.UserRole.Admin or Domain.Enums.UserRole.Manager;
    public bool IsAdmin => api.CurrentUser?.Role is Domain.Enums.UserRole.Admin;

    public void RefreshAuthState()
    {
        OnPropertyChanged(nameof(UserName));
        OnPropertyChanged(nameof(RoleName));
        OnPropertyChanged(nameof(IsLoggedIn));
        OnPropertyChanged(nameof(IsManagerOrAdmin));
        OnPropertyChanged(nameof(IsAdmin));
    }

    [RelayCommand] private Task ShowDashboard() => NavigateAsync<DashboardViewModel>("Dashboard");
    [RelayCommand] private Task ShowCustomers() => NavigateAsync<CustomersViewModel>("Customers");
    [RelayCommand] private Task ShowNewJob() => NavigateAsync<NewJobViewModel>("NewJob");
    [RelayCommand] private Task ShowJobs() => NavigateAsync<JobsViewModel>("Jobs");
    [RelayCommand] private Task ShowInventory() => NavigateAsync<InventoryViewModel>("Inventory");
    [RelayCommand] private Task ShowCatalogue() => NavigateAsync<CatalogueViewModel>("Catalogue");
    [RelayCommand] private Task ShowStaffAdvances() => NavigateAsync<StaffAdvancesViewModel>("StaffAdvances");
    [RelayCommand] private Task ShowReports() => NavigateAsync<ReportsViewModel>("Reports");
    [RelayCommand] private Task ShowSettings() => NavigateAsync<SettingsViewModel>("Settings");

    public async Task NavigateAsync<TViewModel>(string navKey) where TViewModel : class
    {
        // Catalogue is open to the counter (no login) — services/prices are edited on the floor.
        if ((navKey == "Reports" || navKey == "Inventory" || navKey == "Settings") && !IsLoggedIn)
        {
            // Trigger the login popup
            var shell = App.Services.GetService<Views.ShellWindow>();
            var login = App.Services.GetRequiredService<Views.LoginWindow>();
            if (shell != null && shell.IsVisible) login.Owner = shell;
            login.ShowDialog();
            
            RefreshAuthState();
            if (!IsLoggedIn) return; // Login was cancelled or failed
        }

        ActiveNav = navKey;
        var vm = App.Services.GetRequiredService<TViewModel>();
        if (vm is IAsyncInitialize init) await init.InitializeAsync();
        CurrentView = vm;
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
