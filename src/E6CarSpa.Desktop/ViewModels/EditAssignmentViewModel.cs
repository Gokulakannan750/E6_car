using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using E6CarSpa.Client;
using E6CarSpa.Contracts;

namespace E6CarSpa.Desktop.ViewModels;

/// <summary>Inline editor for a daily showroom assignment row.</summary>
public partial class EditAssignmentViewModel(IApiClient api, ShowroomDailyStaffDto original) : ObservableObject
{
 [ObservableProperty] private Guid _assignmentId = original.Id;
 [ObservableProperty] private DateTime _assignmentDate = original.AssignmentDate.Date;
 [ObservableProperty] private Guid _showroomId = original.ShowroomId;
 [ObservableProperty] private Guid _staffId = original.StaffId;
 [ObservableProperty] private string _attendanceStatus = original.AttendanceStatus;

 [ObservableProperty] private int _vehiclesAttended = original.VehiclesAttended;
 [ObservableProperty] private int _vehiclesCompleted = original.VehiclesCompleted;
 [ObservableProperty] private decimal _amountGenerated = original.AmountGenerated;
 [ObservableProperty] private string _remarks = original.Remarks ?? "";

 [ObservableProperty] private string _error = "";

 public System.Collections.Generic.List<string> AttendanceOptions { get; } = ["Present", "Absent", "Half Day", "Leave"];

 [RelayCommand]
 private async Task SaveAsync()
 {
 Error = "";
 if (VehiclesAttended < 0) { Error = "Vehicles attended cannot be negative."; return; }
 if (VehiclesCompleted < 0) { Error = "Vehicles completed cannot be negative."; return; }
 if (VehiclesCompleted > VehiclesAttended) { Error = "Completed cannot exceed attended."; return; }
 if (AmountGenerated < 0) { Error = "Amount cannot be negative."; return; }


 try
 {
 var req = new SaveShowroomDailyStaffRequest(
 AssignmentDate, ShowroomId, StaffId, AttendanceStatus,
 VehiclesAttended, VehiclesCompleted, AmountGenerated,
 string.IsNullOrWhiteSpace(Remarks) ? null : Remarks);

 await api.UpdateDailyAssignmentAsync(AssignmentId, req);
 Close(true);
 }
 catch { /* dialog stays open */ }
 }

 [RelayCommand]
 private void Cancel() => Close(false);

 public bool? DialogResult { get; private set; }
 private void Close(bool? result) { DialogResult = result; }
}
