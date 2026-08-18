using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using E6CarSpa.Client;
using E6CarSpa.Contracts;

namespace E6CarSpa.Desktop.ViewModels;

/// <summary>
/// Salary payments to floor workers. One of the three trackers under the "Cash" section,
/// sharing the Staff master with advances.
/// </summary>
public partial class StaffSalariesViewModel(IApiClient api) : ObservableObject, IAsyncInitialize
{
    public ObservableCollection<StaffSalaryDto> Salaries { get; } = new();
    public ObservableCollection<StaffSalarySummaryDto> Summary { get; } = new();
    public ObservableCollection<StaffDto> StaffList { get; } = new();

    [ObservableProperty] private StaffDto? _selectedStaff;
    [ObservableProperty] private decimal _amount;
    [ObservableProperty] private DateTime _salaryDate = DateTime.Today;
    [ObservableProperty] private string _note = "";

    [ObservableProperty] private bool _showDeleted;
    partial void OnShowDeletedChanged(bool value) => _ = LoadAsync();

    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _error = "";
    [ObservableProperty] private string _info = "";

    public decimal GrandTotal => Summary.Sum(s => s.TotalPaid);

    public Task InitializeAsync() => LoadAsync();

    [RelayCommand]
    private async Task LoadAsync()
    {
        try
        {
            IsBusy = true; Error = "";

            var staff = await api.GetStaffAsync(includeInactive: false) ?? new();
            StaffList.Clear();
            foreach (var s in staff) StaffList.Add(s);

            var list = await api.GetStaffSalariesAsync(includeDeleted: ShowDeleted) ?? new();
            Salaries.Clear();
            foreach (var s in list) Salaries.Add(s);

            var summary = await api.GetStaffSalarySummaryAsync() ?? new();
            Summary.Clear();
            foreach (var s in summary) Summary.Add(s);

            OnPropertyChanged(nameof(GrandTotal));
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task RecordSalaryAsync()
    {
        Error = ""; Info = "";
        if (SelectedStaff is null) { Error = "Select a worker from the list."; return; }
        if (Amount <= 0) { Error = "Enter an amount greater than zero."; return; }

        try
        {
            IsBusy = true;
            await api.CreateStaffSalaryAsync(new SaveStaffSalaryRequest(
                SelectedStaff.Id, Amount, SalaryDate,
                string.IsNullOrWhiteSpace(Note) ? null : Note.Trim()));

            Info = $"Salary of ₹{Amount:N2} recorded for {SelectedStaff.FullName}.";
            SelectedStaff = null; Amount = 0; Note = ""; SalaryDate = DateTime.Today;
            await LoadAsync();
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task DeleteSalaryAsync(StaffSalaryDto? salary)
    {
        if (salary is null || salary.IsDeleted) return;

        var confirm = MessageBox.Show(
            $"Mark the ₹{salary.Amount:N2} salary for {salary.StaffName} on {salary.SalaryDate:dd-MM-yyyy} as deleted?\n\n" +
            "The entry is kept for the record — stamped with your name — and stops counting towards the totals.",
            "Delete salary", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (confirm != MessageBoxResult.Yes) return;

        try
        {
            IsBusy = true; Error = ""; Info = "";
            await api.DeleteStaffSalaryAsync(salary.Id);
            Info = $"Salary for {salary.StaffName} marked deleted.";
            await LoadAsync();
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsBusy = false; }
    }

    // ----- Staff CRUD (read-only, add/rename only) -----

    [RelayCommand]
    private async Task AddStaffAsync()
    {
        var name = Views.InputDialog.Show("Enter the worker's full name:", "Add Staff Member", "");
        if (string.IsNullOrWhiteSpace(name)) return;

        try
        {
            IsBusy = true;
            await api.CreateStaffAsync(new SaveStaffRequest(name.Trim()));
            await LoadAsync();
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task RenameStaffAsync(StaffDto? staff)
    {
        if (staff is null) return;

        var name = Views.InputDialog.Show("Rename this staff member:", "Edit Name", staff.FullName);
        if (string.IsNullOrWhiteSpace(name) || name.Trim() == staff.FullName) return;

        try
        {
            IsBusy = true;
            await api.UpdateStaffAsync(staff.Id, new SaveStaffRequest(name.Trim()));
            await LoadAsync();
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsBusy = false; }
    }
}
