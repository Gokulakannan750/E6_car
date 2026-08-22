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

 public string UserName => api.CurrentUser?.FullName ?? "Signed out";
 public string RoleName => api.CurrentUser?.Role.ToString() ?? "";
 public bool IsLoggedIn => api.IsLoggedIn;
 public bool IsManagerOrAdmin =>
 api.CurrentUser?.Role is Domain.Enums.UserRole.Admin or Domain.Enums.UserRole.Manager;
 public bool IsAdmin => api.CurrentUser?.Role is Domain.Enums.UserRole.Admin;

 private bool Can(Domain.Enums.Permission p) => api.CurrentUser?.Can(p) == true;

 public bool CanBilling => Can(Domain.Enums.Permission.Billing);
 public bool CanCustomers => Can(Domain.Enums.Permission.Customers);
 public bool CanCatalogue => Can(Domain.Enums.Permission.Catalogue);
 public bool CanStaff => Can(Domain.Enums.Permission.StaffAdvances) || Can(Domain.Enums.Permission.StaffManage);
 public bool CanShowroom => Can(Domain.Enums.Permission.Showroom);
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
 OnPropertyChanged(nameof(CanStaff));
 OnPropertyChanged(nameof(CanShowroom));
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
 [RelayCommand] private Task ShowStaff() => NavigateAsync<StaffModuleViewModel>("Staff");
 [RelayCommand] private Task ShowShowrooms() => NavigateAsync<ShowroomViewModel>("Showroom");
 [RelayCommand] private Task ShowReports() => NavigateAsync<ReportsViewModel>("Reports");
 [RelayCommand] private Task ShowSettings() => NavigateAsync<SettingsViewModel>("Settings");

 private bool _navigating;

 public async Task NavigateAsync<TViewModel>(string navKey) where TViewModel : class
 {
 if (_navigating) return;
 _navigating = true;
 try
 {
 var vm = App.Services.GetRequiredService<TViewModel>();
 if (vm is IAsyncInitialize init) await init.InitializeAsync();

 ActiveNav = navKey;
 CurrentView = vm;
 }
 catch (Exception ex)
 {
 AppLog.Error($"Could not open the '{navKey}' screen.", ex);
 MessageBox.Show(
 $"Could not open {navKey}.\n\n{ex.Message}",
 "E6 Car Spa", MessageBoxButton.OK, MessageBoxImage.Warning);
 }
 finally { _navigating = false; }
 }

 public async Task OpenInvoiceAsync(Guid invoiceId)
 {
 ActiveNav = "Jobs";
 var vm = App.Services.GetRequiredService<InvoiceDetailViewModel>();
 await vm.LoadAsync(invoiceId);
 CurrentView = vm;
 }
}
