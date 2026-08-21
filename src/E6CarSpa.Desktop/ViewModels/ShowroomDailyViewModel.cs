using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using E6CarSpa.Client;
using E6CarSpa.Contracts;
using E6CarSpa.Desktop.Views;

namespace E6CarSpa.Desktop.ViewModels;

public partial class ShowroomDailyViewModel(IApiClient api) : ObservableObject, IAsyncInitialize
{
 // ── data ────────────────────────────────────────────────────
 public ObservableCollection<ShowroomDailyStaffDto> Assignments { get; } = new();
 public ObservableCollection<ShowroomPickDto> ShowroomList { get; } = new();
 public ObservableCollection<StaffDto> StaffList { get; } = new();
 public ObservableCollection<string> AttendanceOptions { get; } = ["Present", "Absent", "Half Day", "Leave"];

 // ── filters ─────────────────────────────────────────────────
 [ObservableProperty] private DateTime _selectedDate = DateTime.Today;
 [ObservableProperty] private ShowroomPickDto? _selectedShowroom;

 [ObservableProperty] private bool _isBusy;
 [ObservableProperty] private string _error = "";
 [ObservableProperty] private string _info = "";

 partial void OnSelectedDateChanged(DateTime value) => _ = LoadForShowroomAsync();
 partial void OnSelectedShowroomChanged(ShowroomPickDto? value) => _ = LoadForShowroomAsync();

 private int _searchGen;
 private string _search = "";
 public string Search
 {
 get => _search;
 set => SetProperty(ref _search, value);
 }

 public bool IsShowroomSelected => SelectedShowroom is not null;

 public string PanelHeader => SelectedShowroom is null
 ? "Staff"
 : $"Staff — {SelectedShowroom.Name} ({Assignments.Count})";

 public string SelectedShowroomName => SelectedShowroom?.Name ?? "";

 public string SummaryText => IsShowroomSelected && Assignments.Count > 0
 ? $"Total: {Assignments.Count} staff | Vehicles: {Assignments.Sum(a => a.VehiclesAttended)} attended, {Assignments.Sum(a => a.VehiclesCompleted)} completed | Amount: ₹{Assignments.Sum(a => a.AmountGenerated):N0}"
 : "";

 public Task InitializeAsync() => LoadReferenceDataAsync();

 private async Task LoadReferenceDataAsync()
 {
 try
 {
 IsBusy = true;
 var showrooms = await api.GetShowroomsForPickerAsync() ?? new();
 var oldSelectedShowroom = SelectedShowroom;
 ShowroomList.Clear();
 foreach (var s in showrooms) ShowroomList.Add(s);
 if (oldSelectedShowroom != null) SelectedShowroom = ShowroomList.FirstOrDefault(s => s.Id == oldSelectedShowroom.Id);

 var staff = await api.GetStaffAsync(includeInactive: false) ?? new();
 var oldSelectedStaff = SelectedStaff;
 StaffList.Clear();
 foreach (var s in staff) StaffList.Add(s);
 if (oldSelectedStaff != null) SelectedStaff = StaffList.FirstOrDefault(s => s.Id == oldSelectedStaff.Id);
 }
 catch (Exception ex) { Error = ex.Message; }
 finally { IsBusy = false; }
 }

 private async Task LoadForShowroomAsync()
 {
 try
 {
 IsBusy = true; Error = ""; Info = "";

 if (SelectedShowroom is null)
 {
 Assignments.Clear();
 OnPropertyChanged(nameof(PanelHeader));
 OnPropertyChanged(nameof(SummaryText));
 OnPropertyChanged(nameof(IsShowroomSelected));
 OnPropertyChanged(nameof(SelectedShowroomName));
 return;
 }

 var data = await api.GetDailyAssignmentsByDateAsync(SelectedDate) ?? new();
 data = data.Where(d => d.ShowroomId == SelectedShowroom.Id).ToList();

 if (!string.IsNullOrWhiteSpace(Search))
 {
 var q = Search.Trim().ToLower();
 data = data.Where(d =>
 d.StaffName.ToLower().Contains(q)).ToList();
 }

 Assignments.Clear();
 foreach (var d in data) Assignments.Add(d);

 OnPropertyChanged(nameof(PanelHeader));
 OnPropertyChanged(nameof(SummaryText));
 OnPropertyChanged(nameof(IsShowroomSelected));
 OnPropertyChanged(nameof(SelectedShowroomName));
 }
 catch (Exception ex) { Error = ex.Message; }
 finally { IsBusy = false; }
 }

 // ── Add single staff ─────────────────────────────────────────

 [ObservableProperty] private bool _isAddingStaff;
 [ObservableProperty] private StaffDto? _selectedStaff;
 [ObservableProperty] private string _selectedAttendance = "Present";

 [ObservableProperty] private int _vehiclesAttended;
 [ObservableProperty] private int _vehiclesCompleted;
 [ObservableProperty] private decimal _amountGenerated;
 [ObservableProperty] private string _remarks = "";

 [RelayCommand]
 private void OpenAddStaff()
 {
 if (SelectedShowroom is null) return;
 SelectedStaff = null;
 SelectedAttendance = "Present";

 VehiclesAttended = 0;
 VehiclesCompleted = 0;
 AmountGenerated = 0;
 Remarks = "";
 IsAddingStaff = true;
 }

 [RelayCommand]
 private async Task SaveStaffAsync()
 {
 Error = ""; Info = "";

 if (SelectedShowroom is null) { Error = "Select a showroom first."; return; }
 if (SelectedStaff is null) { Error = "Select a staff member."; return; }
 if (VehiclesAttended < 0) { Error = "Vehicles attended cannot be negative."; return; }
 if (VehiclesCompleted < 0) { Error = "Vehicles completed cannot be negative."; return; }
 if (VehiclesCompleted > VehiclesAttended) { Error = "Completed cannot exceed attended."; return; }
 if (AmountGenerated < 0) { Error = "Amount cannot be negative."; return; }


 try
 {
 IsBusy = true;
 var req = new SaveShowroomDailyStaffRequest(
 SelectedDate.Date,
 SelectedShowroom.Id,
 SelectedStaff.Id,
 SelectedAttendance,

 VehiclesAttended,
 VehiclesCompleted,
 AmountGenerated,
 string.IsNullOrWhiteSpace(Remarks) ? null : Remarks);

 await api.CreateDailyAssignmentAsync(req);
 Info = $"{SelectedStaff.FullName} added to {SelectedShowroom.Name}.";
 IsAddingStaff = false;
 await LoadForShowroomAsync();
 }
 catch (Exception ex)
 {
 if (ex.Message.Contains("already assigned") || ex.Message.Contains("already"))
 Error = "This staff member is already assigned for this date.";
 else Error = ex.Message;
 }
 finally { IsBusy = false; }
 }

 [RelayCommand]
 private void CancelAddStaff() => IsAddingStaff = false;



 // ── Edit existing assignment ─────────────────────────────────

 [RelayCommand]
 private async Task EditAssignmentAsync(ShowroomDailyStaffDto? dto)
 {
 if (dto is null) return;

 SelectedStaff = StaffList.FirstOrDefault(s => s.Id == dto.StaffId);
 SelectedShowroom = ShowroomList.FirstOrDefault(s => s.Id == dto.ShowroomId);
 SelectedDate = dto.AssignmentDate.Date;
 SelectedAttendance = dto.AttendanceStatus;

 VehiclesAttended = dto.VehiclesAttended;
 VehiclesCompleted = dto.VehiclesCompleted;
 AmountGenerated = dto.AmountGenerated;
 Remarks = dto.Remarks ?? "";

 var vm = new EditAssignmentViewModel(api, dto);
 var dlg = new EditAssignmentView { DataContext = vm, Owner = Application.Current.MainWindow };
 if (dlg.ShowDialog() == true)
 {
 await LoadForShowroomAsync();
 Info = "Assignment updated.";
 }
 }

 [RelayCommand]
 private async Task DeleteAssignmentAsync(ShowroomDailyStaffDto? dto)
 {
 if (dto is null) return;

 var result = MessageBox.Show(
 $"Remove {dto.StaffName} from {dto.ShowroomName} on {dto.AssignmentDate:dd-MM-yyyy}?",
 "Delete Assignment", MessageBoxButton.YesNo, MessageBoxImage.Question);

 if (result != MessageBoxResult.Yes) return;

 try
 {
 IsBusy = true; Info = "";
 await api.DeleteDailyAssignmentAsync(dto.Id);
 Info = "Assignment removed.";
 await LoadForShowroomAsync();
 }
 catch (Exception ex) { Error = ex.Message; }
 finally { IsBusy = false; }
 }
}
