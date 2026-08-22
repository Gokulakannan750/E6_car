using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using E6CarSpa.Client;
using E6CarSpa.Contracts;

namespace E6CarSpa.Desktop.ViewModels;

public partial class StaffMasterViewModel(IApiClient api) : ObservableObject, IAsyncInitialize
{
    public ObservableCollection<StaffDto> StaffList { get; } = new();

    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _error = "";
    [ObservableProperty] private string _info = "";
    
    [ObservableProperty] private bool _showInactive;
    partial void OnShowInactiveChanged(bool value) => _ = LoadAsync();

    public async Task InitializeAsync() => await LoadAsync();

    [RelayCommand]
    private async Task LoadAsync()
    {
        try
        {
            IsBusy = true; Error = ""; Info = "";
            var list = await api.GetStaffAsync(includeInactive: ShowInactive) ?? new();
            StaffList.Clear();
            foreach (var s in list) StaffList.Add(s);
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task AddStaffAsync()
    {
        var name = Views.InputDialog.Show("Enter the worker's full name:", "Add Staff Member", "");
        if (string.IsNullOrWhiteSpace(name)) return;

        try
        {
            IsBusy = true; Error = ""; Info = "";
            await api.CreateStaffAsync(new SaveStaffRequest(name.Trim()));
            Info = $"Added '{name.Trim()}'.";
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
            IsBusy = true; Error = ""; Info = "";
            await api.UpdateStaffAsync(staff.Id, new SaveStaffRequest(name.Trim()));
            Info = $"Renamed to '{name.Trim()}'.";
            await LoadAsync();
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task RemoveStaffAsync(StaffDto? staff)
    {
        if (staff is null) return;

        var confirm = MessageBox.Show(
            $"Remove \"{staff.FullName}\" from the active staff list?\n\n" +
            "The person's name will be hidden from pickers, but all their advance and assignment history remains intact.",
            "Remove staff", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (confirm != MessageBoxResult.Yes) return;

        try
        {
            IsBusy = true; Error = ""; Info = "";
            await api.DeleteStaffAsync(staff.Id);
            Info = $"'{staff.FullName}' deactivated.";
            await LoadAsync();
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task RestoreStaffAsync(StaffDto? staff)
    {
        if (staff is null) return;

        try
        {
            IsBusy = true; Error = ""; Info = "";
            await api.RestoreStaffAsync(staff.Id);
            Info = $"'{staff.FullName}' restored and is active again.";
            await LoadAsync();
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsBusy = false; }
    }
}
