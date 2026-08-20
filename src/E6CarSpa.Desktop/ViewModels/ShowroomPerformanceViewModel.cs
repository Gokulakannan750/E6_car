using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using E6CarSpa.Client;
using E6CarSpa.Contracts;
using E6CarSpa.Desktop.ViewModels;

namespace E6CarSpa.Desktop.ViewModels;

/// <summary>Showroom performance dashboard.</summary>
public partial class ShowroomPerformanceViewModel(IApiClient api) : ObservableObject, IAsyncInitialize
{
 public ObservableCollection<ShowroomPickDto> ShowroomList { get; } = new();
 public ObservableCollection<StatLine> StatLines { get; } = new();

 [ObservableProperty] private Guid? _selectedShowroomId;
 [ObservableProperty] private DateTime _fromDate = new(DateTime.Today.Year, DateTime.Today.Month, 1);
 [ObservableProperty] private DateTime _toDate = DateTime.Today;

 [ObservableProperty] private ShowroomPerformanceDto? _performance;
 public ObservableCollection<StaffPerformanceDto> StaffPerformance { get; } = new();
 public ObservableCollection<DailyShowroomSummaryDto> DailyBreakdown { get; } = new();

 [ObservableProperty] private bool _isBusy;
 [ObservableProperty] private string _error = "";
 [ObservableProperty] private string _info = "";

 partial void OnPerformanceChanged(ShowroomPerformanceDto? value)
 {
 StatLines.Clear();
 if (value is null) return;
 StatLines.Add(new("Total Vehicles", value.TotalVehiclesAttended.ToString()));
 StatLines.Add(new("Completed", value.TotalVehiclesCompleted.ToString()));
 StatLines.Add(new("Total Amount", $"₹{value.TotalAmount:N2}"));
 StatLines.Add(new("Staff Days", value.StaffDays.ToString()));
 StatLines.Add(new("Avg Vehicles/Day", value.AvgVehiclesPerDay.ToString()));
 StatLines.Add(new("Avg Amount/Day", $"₹{value.AvgAmountPerDay:N2}"));
 }

 partial void OnSelectedShowroomIdChanged(Guid? value) => _ = LoadAsync();
 partial void OnFromDateChanged(DateTime value) => _ = LoadAsync();
 partial void OnToDateChanged(DateTime value) => _ = LoadAsync();

 public async Task InitializeAsync()
 {
 try
 {
 IsBusy = true;
 var list = await api.GetShowroomsForPickerAsync() ?? new();
 ShowroomList.Clear();
 foreach (var s in list) ShowroomList.Add(s);

 if (list.Count > 0) SelectedShowroomId = list[0].Id;
 }
 catch (Exception ex) { Error = ex.Message; }
 finally { IsBusy = false; }
 }

 [RelayCommand]
 private async Task LoadAsync()
 {
 try
 {
 IsBusy = true; Error = "";
 if (SelectedShowroomId == null || SelectedShowroomId == Guid.Empty) return;

 Performance = await api.GetShowroomPerformanceAsync(SelectedShowroomId.Value, FromDate, ToDate);
 StaffPerformance.Clear();
 var sp = await api.GetShowroomPerformanceByStaffAsync(SelectedShowroomId.Value, FromDate, ToDate) ?? new();
 foreach (var s in sp) StaffPerformance.Add(s);

 DailyBreakdown.Clear();
 var dbRows = await api.GetShowroomDailyBreakdownAsync(SelectedShowroomId.Value, FromDate, ToDate) ?? new();
 foreach (var d in dbRows) DailyBreakdown.Add(d);
 }
 catch (Exception ex) { Error = ex.Message; }
 finally { IsBusy = false; }
 }

 [RelayCommand]
 private async Task RefreshAsync() => await LoadAsync();
}
