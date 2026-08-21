using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using E6CarSpa.Desktop.Views;
using Microsoft.Extensions.DependencyInjection;

namespace E6CarSpa.Desktop.ViewModels;

/// <summary>Container for the Showroom module with four tabbed sub-pages.</summary>
public partial class ShowroomViewModel : ObservableObject, IAsyncInitialize
{
 [ObservableProperty] private int _selectedTab = 0;
 [ObservableProperty] private object? _currentView;

 public ObservableCollection<string> Tabs { get; } = new() { "Showrooms", "Daily Staff", "Performance", "Reports" };

 private ShowroomsViewModel _showroomsVm = default!;
 private ShowroomDailyViewModel _dailyVm = default!;
 private ShowroomPerformanceViewModel _performanceVm = default!;
 private ShowroomReportViewModel _reportVm = default!;
 private bool _subVmsLoaded;

 // Cache view instances to preserve UI state across tab switches
 private ShowroomsView? _showroomsView;
 private ShowroomDailyView? _dailyView;
 private ShowroomPerformanceView? _performanceView;
 private ShowroomReportView? _reportView;

 private static IServiceProvider Services => App.Services;

 partial void OnSelectedTabChanged(int value)
 {
 LoadSubVms();

 _ = value switch
 {
 0 => _showroomsVm.InitializeAsync(),
 1 => _dailyVm.InitializeAsync(),
 2 => _performanceVm.InitializeAsync(),
 3 => _reportVm.InitializeAsync(),
 _ => Task.CompletedTask
 };

 CurrentView = value switch
 {
 0 => GetOrCreateShowroomsView(),
 1 => GetOrCreateDailyView(),
 2 => GetOrCreatePerformanceView(),
 3 => GetOrCreateReportView(),
 _ => GetOrCreateDailyView()
 };
 }

 public async Task InitializeAsync()
 {
 LoadSubVms();
 await InitializeSubVmAsync(_showroomsVm, "showrooms");
 await InitializeSubVmAsync(_dailyVm, "daily staff");
 await InitializeSubVmAsync(_performanceVm, "performance");
 await InitializeSubVmAsync(_reportVm, "reports");

 CurrentView = GetOrCreateShowroomsView();
 }

 private ShowroomsView GetOrCreateShowroomsView()
 {
 return _showroomsView ??= new ShowroomsView { DataContext = _showroomsVm };
 }

 private ShowroomDailyView GetOrCreateDailyView()
 {
 return _dailyView ??= new ShowroomDailyView { DataContext = _dailyVm };
 }

 private ShowroomPerformanceView GetOrCreatePerformanceView()
 {
 return _performanceView ??= new ShowroomPerformanceView { DataContext = _performanceVm };
 }

 private ShowroomReportView GetOrCreateReportView()
 {
 return _reportView ??= new ShowroomReportView { DataContext = _reportVm };
 }

 private static async Task InitializeSubVmAsync(object vm, string name)
 {
 if (vm is not IAsyncInitialize init) return;
 try
 {
 await init.InitializeAsync();
 }
 catch (Exception ex)
 {
 if (vm is ObservableObject oo)
 {
 var errorProp = oo.GetType().GetProperty("Error");
 if (errorProp is not null && errorProp.PropertyType == typeof(string))
 errorProp.SetValue(oo, $"Failed to load {name}: {ex.Message}");
 }
 }
 }

 private void LoadSubVms()
 {
 if (_subVmsLoaded) return;
 _showroomsVm = Services.GetRequiredService<ShowroomsViewModel>();
 _dailyVm = Services.GetRequiredService<ShowroomDailyViewModel>();
 _performanceVm = Services.GetRequiredService<ShowroomPerformanceViewModel>();
 _reportVm = Services.GetRequiredService<ShowroomReportViewModel>();
 _subVmsLoaded = true;
 }
}
