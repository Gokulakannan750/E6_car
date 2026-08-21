using E6CarSpa.Client;
using E6CarSpa.Contracts;
using E6CarSpa.Mobile.Services;

namespace E6CarSpa.Mobile.Pages;

public partial class ShowroomPage : ContentPage
{
 int _activeTab = 0;
 bool _loaded;
 bool _isBusy;
 string _error = "";

 readonly string[] _tabColors = new[]
 {
 "#FF3B30", "#007AFF", "#34C759", "#FF9500"
 };

 public ShowroomPage()
 {
 InitializeComponent();
 SelectTab(0);
 }

 void SelectTab(int index)
 {
 _activeTab = index;
 var accent = Color.FromArgb(_tabColors[index]);
 TabShowrooms.BackgroundColor = index == 0 ? accent : Colors.Transparent;
 TabShowrooms.TextColor = index == 0 ? Colors.White : accent;
 TabDaily.BackgroundColor = index == 1 ? accent : Colors.Transparent;
 TabDaily.TextColor = index == 1 ? Colors.White : accent;
 TabPerformance.BackgroundColor = index == 2 ? accent : Colors.Transparent;
 TabPerformance.TextColor = index == 2 ? Colors.White : accent;
 TabReports.BackgroundColor = index == 3 ? accent : Colors.Transparent;
 TabReports.TextColor = index == 3 ? Colors.White : accent;

 ShowroomsPanel.IsVisible = index == 0;
 DailyPanel.IsVisible = index == 1;
 PerformancePanel.IsVisible = index == 2;
 ReportsPanel.IsVisible = index == 3;

 if (!_loaded)
 {
 _loaded = true;
 _ = LoadShowrooms();
 }
 else if (index == 1) _ = LoadShowroomPicker();
 else if (index == 2) _ = RefreshPerformance();
 else if (index == 3) _ = RefreshReports();
 }

 void OnSettingsClicked(object? sender, EventArgs e) =>
 Shell.Current.GoToAsync("settings");

 void OnTabShowrooms(object? s, EventArgs e) => SelectTab(0);
 void OnTabDaily(object? s, EventArgs e) => SelectTab(1);
 void OnTabPerformance(object? s, EventArgs e) => SelectTab(2);
 void OnTabReports(object? s, EventArgs e) => SelectTab(3);

 void SetBusy(bool busy)
 {
 _isBusy = busy;
 Busy.IsRunning = busy;
 Busy.IsVisible = busy;
 }

 void ShowError(string msg)
 {
 _error = msg;
 ErrorLabel.Text = msg;
 ErrorLabel.IsVisible = !string.IsNullOrEmpty(msg);
 }

 async Task LoadShowrooms()
 {
 SetBusy(true); ShowError("");
 try
 {
 var list = await AppServices.Api.GetShowroomsAsync(ShowInactiveSwitch.IsToggled) ?? new();
 ShowroomsList.ItemsSource = list;
 NoShowroomsLabel.IsVisible = list.Count == 0;
 }
 catch (Exception ex)
 {
 ShowError(ex is ApiException a ? a.Message : "Cannot reach server.");
 }
 finally { SetBusy(false); }
 }

 void OnSearchChanged(object? s, TextChangedEventArgs e) => _ = LoadShowrooms();
 void OnShowInactiveToggled(object? s, ToggledEventArgs e) => _ = LoadShowrooms();

 async void OnAddShowroom(object? s, EventArgs e)
 {
 var name = await DisplayPromptAsync("New Showroom", "Showroom name:");
 if (string.IsNullOrWhiteSpace(name)) return;
 var address = await DisplayPromptAsync("New Showroom", "Address:", initialValue: "", maxLength: 200);
 try
 {
 await AppServices.Api.CreateShowroomAsync(new SaveShowroomRequest(name.Trim(), address?.Trim() ?? ""));
 await DisplayAlert("Done", $"{name.Trim()} added.", "OK");
 await LoadShowrooms();
 }
 catch (Exception ex)
 {
 ShowError(ex is ApiException a ? a.Message : "Failed to add showroom.");
 }
 }

 async void OnEditShowroom(object? s, EventArgs e)
 {
 if (s is not Button { BindingContext: ShowroomDto sr }) return;
 var name = await DisplayPromptAsync("Edit Showroom", "Name:", initialValue: sr.Name);
 if (string.IsNullOrWhiteSpace(name)) return;
 var address = await DisplayPromptAsync("Edit Showroom", "Address:", initialValue: sr.Address, maxLength: 200);
 try
 {
 await AppServices.Api.UpdateShowroomAsync(sr.Id, new SaveShowroomRequest(name.Trim(), address?.Trim() ?? ""));
 sr.Name = name.Trim(); sr.Address = address?.Trim() ?? "";
 await LoadShowrooms();
 }
 catch (Exception ex)
 {
 ShowError(ex is ApiException a ? a.Message : "Failed to update showroom.");
 }
 }

 async void OnToggleShowroom(object? s, EventArgs e)
 {
 if (s is not Button { BindingContext: ShowroomDto sr }) return;
 try
 {
 if (sr.IsActive)
 await AppServices.Api.DeactivateShowroomAsync(sr.Id);
 else
 await AppServices.Api.RestoreShowroomAsync(sr.Id);
 sr.IsActive = !sr.IsActive;
 await LoadShowrooms();
 }
 catch (Exception ex)
 {
 ShowError(ex is ApiException a ? a.Message : "Failed to toggle showroom.");
 }
 }

 async Task LoadShowroomPicker()
 {
 try
 {
 var list = await AppServices.Api.GetShowroomsForPickerAsync() ?? new();
 ShowroomPicker.ItemsSource = list;
 }
 catch (Exception ex) { ShowError(ex is ApiException a ? a.Message : "Cannot load showrooms."); }
 }

 void OnShowroomPickerChanged(object? s, EventArgs e) => _ = LoadDailyAssignments();

 async Task LoadDailyAssignments()
 {
 if (ShowroomPicker.SelectedItem is not ShowroomPickDto sr)
 {
 DailyStaffList.ItemsSource = null;
 NoDailyLabel.IsVisible = true;
 return;
 }
 NoDailyLabel.IsVisible = false;
 SetBusy(true); ShowError("");
 try
 {
 var date = DailyDatePicker.Date;
 var list = await AppServices.Api.GetDailyAssignmentsByDateAsync(date) ?? new();
 var filtered = list.Where(x => x.ShowroomId == sr.Id).ToList();
 DailyStaffList.ItemsSource = filtered;
 }
 catch (Exception ex)
 {
 ShowError(ex is ApiException a ? a.Message : "Cannot load assignments.");
 }
 finally { SetBusy(false); }
 }

 async void OnAddStaff(object? s, EventArgs e)
 {
 if (ShowroomPicker.SelectedItem is not ShowroomPickDto sr)
 {
 await DisplayAlert("Select showroom", "Pick a showroom first.", "OK");
 return;
 }

 var staff = await AppServices.Api.GetStaffAsync(includeInactive: false) ?? new();
 if (staff.Count == 0)
 {
 await DisplayAlert("No staff", "Add staff members first.", "OK");
 return;
 }

 var staffNames = staff.Select(st => st.FullName).ToArray();
 var staffName = await DisplayActionSheet("Select staff", "Cancel", null, staffNames);
 if (string.IsNullOrEmpty(staffName) || staffName == "Cancel") return;
 var selectedStaff = staff.First(st => st.FullName == staffName);

 var attendance = await DisplayActionSheet("Attendance", "Cancel", null, "Present", "Absent", "Half Day", "Leave");
 if (string.IsNullOrEmpty(attendance) || attendance == "Cancel") return;

 var vehiclesAttendedStr = await DisplayPromptAsync("Vehicles", "Vehicles attended:", keyboard: Keyboard.Numeric, initialValue: "0");
 if (vehiclesAttendedStr is null) return;
 var vehiclesCompletedStr = await DisplayPromptAsync("Vehicles", "Vehicles completed:", keyboard: Keyboard.Numeric, initialValue: "0");
 if (vehiclesCompletedStr is null) return;
 var amountStr = await DisplayPromptAsync("Amount", "Amount generated:", keyboard: Keyboard.Numeric, initialValue: "0");
 if (amountStr is null) return;
 var remarks = await DisplayPromptAsync("Remarks", "(optional)", maxLength: 200);

 int.TryParse(vehiclesAttendedStr, out var attended);
 int.TryParse(vehiclesCompletedStr, out var completed);
 decimal.TryParse(amountStr, out var amount);

 SetBusy(true);
 try
 {
 await AppServices.Api.CreateDailyAssignmentAsync(new SaveShowroomDailyStaffRequest(
 DailyDatePicker.Date, sr.Id, selectedStaff.Id, attendance, attended, completed, amount,
 string.IsNullOrWhiteSpace(remarks) ? null : remarks.Trim()));
 await LoadDailyAssignments();
 }
 catch (Exception ex)
 {
 ShowError(ex is ApiException a ? ex.Message : "Failed to save assignment.");
 }
 finally { SetBusy(false); }
 }

 async void OnDeleteDaily(object? s, EventArgs e)
 {
 if (s is not Button { BindingContext: ShowroomDailyStaffDto d }) return;
 var confirm = await DisplayAlert("Delete", $"Remove {d.StaffName}'s assignment?", "Delete", "Cancel");
 if (!confirm) return;
 try
 {
 await AppServices.Api.DeleteDailyAssignmentAsync(d.Id);
 await LoadDailyAssignments();
 }
 catch (Exception ex)
 {
 ShowError(ex is ApiException a ? ex.Message : "Failed to delete.");
 }
 }

 async Task RefreshPerformance()
 {
 if (!PerformancePanel.IsVisible) return;
 try
 {
 var list = await AppServices.Api.GetShowroomsForPickerAsync() ?? new();
 PerfShowroomPicker.ItemsSource = list;
 if (list.Count > 0) PerfShowroomPicker.SelectedIndex = 0;
 }
 catch (Exception ex) { ShowError(ex is ApiException a ? ex.Message : "Cannot load showrooms."); }
 }

 async void OnLoadPerformance(object? s, EventArgs e)
 {
 if (PerfShowroomPicker.SelectedItem is not ShowroomPickDto sr) return;
 SetBusy(true); ShowError("");
 try
 {
 var perf = await AppServices.Api.GetShowroomPerformanceAsync(sr.Id, PerfFromPicker.Date, PerfToPicker.Date);
 if (perf is null) { ShowError("No data."); SetBusy(false); return; }

 PerfVehicles.Text = perf.TotalVehiclesAttended.ToString();
 PerfCompleted.Text = perf.TotalVehiclesCompleted.ToString();
 PerfAmount.Text = $"₹{perf.TotalAmount:N2}";

 var staffPerf = await AppServices.Api.GetShowroomPerformanceByStaffAsync(sr.Id, PerfFromPicker.Date, PerfToPicker.Date) ?? new();
 StaffPerfList.ItemsSource = staffPerf;
 NoPerfLabel.IsVisible = staffPerf.Count == 0;
 }
 catch (Exception ex)
 {
 ShowError(ex is ApiException a ? ex.Message : "Cannot load performance.");
 }
 finally { SetBusy(false); }
 }

 async Task RefreshReports()
 {
 if (!ReportsPanel.IsVisible) return;
 try
 {
 var list = await AppServices.Api.GetShowroomsForPickerAsync() ?? new();
 ReportShowroomPicker.ItemsSource = list;
 }
 catch (Exception ex) { ShowError(ex is ApiException a ? ex.Message : "Cannot load showrooms."); }
 }

 async void OnLoadReport(object? s, EventArgs e)
 {
 SetBusy(true); ShowError("");
 try
 {
 Guid? showroomId = (ReportShowroomPicker.SelectedItem as ShowroomPickDto)?.Id;
 var list = await AppServices.Api.GetShowroomReportAsync(ReportFromPicker.Date, ReportToPicker.Date, showroomId) ?? new();
 ReportList.ItemsSource = list;
 NoReportLabel.IsVisible = list.Count == 0;
 }
 catch (Exception ex)
 {
 ShowError(ex is ApiException a ? ex.Message : "Cannot load report.");
 }
 finally { SetBusy(false); }
 }
}
