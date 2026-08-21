using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using E6CarSpa.Desktop.Views;
using Microsoft.Extensions.DependencyInjection;

namespace E6CarSpa.Desktop.ViewModels;

/// <summary>
/// Container for the Showroom module with four tabbed sub-pages.
/// </summary>
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
 0 => new ShowroomsView { DataContext = _showroomsVm },
 1 => new ShowroomDailyView { DataContext = _dailyVm },
 2 => new ShowroomPerformanceView { DataContext = _performanceVm },
 3 => new ShowroomReportView { DataContext = _reportVm },
 _ => new ShowroomDailyView { DataContext = _dailyVm }
 };
 }

 public async Task InitializeAsync()
 {
 LoadSubVms();
 try
 {
 await _showroomsVm.InitializeAsync();
 }
 catch (Exception ex)
 {
 _showroomsVm.Error = $"Failed to load showrooms: {ex.Message}";
 }
 try { await _dailyVm.InitializeAsync(); }
 catch { /* non-blocking */ }
 try { await _performanceVm.InitializeAsync(); }
 catch { /* non-blocking */ }
 try { await _reportVm.InitializeAsync(); }
 catch { /* non-blocking */ }

 CurrentView = new ShowroomsView { DataContext = _showroomsVm };
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
