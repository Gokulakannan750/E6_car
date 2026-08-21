using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using E6CarSpa.Client;
using E6CarSpa.Contracts;

namespace E6CarSpa.Desktop.ViewModels;

/// <summary>Reports for the Showroom module.</summary>
public partial class ShowroomReportViewModel(IApiClient api) : ObservableObject, IAsyncInitialize
{
 public ObservableCollection<ShowroomPickDto> ShowroomList { get; } = new();
 public ObservableCollection<StaffDto> StaffList { get; } = new();

 [ObservableProperty] private DateTime _fromDate = new(DateTime.Today.Year, DateTime.Today.Month, 1);
 [ObservableProperty] private DateTime _toDate = DateTime.Today;

 [ObservableProperty] private Guid? _selectedShowroomId;
 [ObservableProperty] private Guid? _selectedStaffId;

 public ObservableCollection<ShowroomReportRowDto> Rows { get; } = new();
 [ObservableProperty] private ShowroomReportSummaryDto? _summary;
 [ObservableProperty] private string _summaryText = "";

 [ObservableProperty] private bool _isBusy;
 [ObservableProperty] private string _error = "";

 partial void OnSummaryChanged(ShowroomReportSummaryDto? value)
 {
 if (value is null) { SummaryText = ""; return; }
 SummaryText = $"Vehicles: {value.TotalVehiclesAttended} | Completed: {value.TotalVehiclesCompleted} | Amount: ₹{value.TotalAmount:N2} | Staff Days: {value.StaffDays}";
 }

 partial void OnFromDateChanged(DateTime value) => _ = RunAsync();
 partial void OnToDateChanged(DateTime value) => _ = RunAsync();
 partial void OnSelectedShowroomIdChanged(Guid? value) => _ = RunAsync();
 partial void OnSelectedStaffIdChanged(Guid? value) => _ = RunAsync();

 public async Task InitializeAsync()
 {
 try
 {
 IsBusy = true;
 var showrooms = await api.GetShowroomsForPickerAsync() ?? new();
 var oldShowroomId = SelectedShowroomId;
 ShowroomList.Clear();
 foreach (var s in showrooms) ShowroomList.Add(s);
 if (oldShowroomId != null && !showrooms.Any(s => s.Id == oldShowroomId)) SelectedShowroomId = null;

 var staff = await api.GetStaffAsync(includeInactive: false) ?? new();
 var oldStaffId = SelectedStaffId;
 StaffList.Clear();
 foreach (var s in staff) StaffList.Add(s);
 if (oldStaffId != null && !staff.Any(s => s.Id == oldStaffId)) SelectedStaffId = null;

 await RunAsync();
 }
 catch (Exception ex) { Error = ex.Message; }
 finally { IsBusy = false; }
 }

 [RelayCommand]
 private async Task RunAsync()
 {
 try
 {
 IsBusy = true; Error = "";
 var rows = await api.GetShowroomReportAsync(FromDate, ToDate, SelectedShowroomId, SelectedStaffId) ?? new();
 Rows.Clear();
 foreach (var r in rows) Rows.Add(r);

 Summary = await api.GetShowroomReportSummaryAsync(FromDate, ToDate, SelectedShowroomId, SelectedStaffId);
 }
 catch (Exception ex) { Error = ex.Message; }
 finally { IsBusy = false; }
 }

 [RelayCommand]
 private async Task RefreshAsync() => await RunAsync();
}
