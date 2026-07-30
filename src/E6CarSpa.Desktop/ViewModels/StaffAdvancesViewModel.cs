using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using E6CarSpa.Client;
using E6CarSpa.Contracts;

namespace E6CarSpa.Desktop.ViewModels;

/// <summary>
/// Simple record of cash advances given to workers. Worker names are typed in (they aren't app
/// users), and this only tracks money GIVEN — no repayments or payroll.
/// </summary>
public partial class StaffAdvancesViewModel(IApiClient api) : ObservableObject, IAsyncInitialize
{
    /// <summary>Every advance, newest first.</summary>
    public ObservableCollection<StaffAdvanceDto> Advances { get; } = new();

    /// <summary>Total advanced per worker.</summary>
    public ObservableCollection<StaffAdvanceSummaryDto> Summary { get; } = new();

    /// <summary>Distinct worker names already used — offered as suggestions to avoid typos.</summary>
    public ObservableCollection<string> KnownWorkers { get; } = new();

    // ----- entry form -----
    [ObservableProperty] private string _workerName = "";
    [ObservableProperty] private decimal _amount;
    [ObservableProperty] private DateTime _advanceDate = DateTime.Today;
    [ObservableProperty] private string _note = "";

    [ObservableProperty] private string _search = "";

    /// <summary>
    /// Show entries that were marked obsolete. Deleting keeps the row for the audit trail rather
    /// than erasing it, so this reveals what was removed, by whom and when.
    /// </summary>
    [ObservableProperty] private bool _showDeleted;
    partial void OnShowDeletedChanged(bool value) => _ = LoadAsync();

    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _error = "";
    [ObservableProperty] private string _info = "";

    /// <summary>Grand total of everything advanced (all workers).</summary>
    public decimal GrandTotal => Summary.Sum(s => s.TotalAdvanced);

    public async Task InitializeAsync() => await LoadAsync();

    [RelayCommand]
    private async Task LoadAsync()
    {
        try
        {
            IsBusy = true; Error = "";
            var list = await api.GetStaffAdvancesAsync(
                string.IsNullOrWhiteSpace(Search) ? null : Search, ShowDeleted) ?? new();
            Advances.Clear();
            foreach (var a in list) Advances.Add(a);

            var summary = await api.GetStaffAdvanceSummaryAsync() ?? new();
            Summary.Clear();
            foreach (var s in summary) Summary.Add(s);

            KnownWorkers.Clear();
            foreach (var name in summary.Select(s => s.WorkerName).OrderBy(n => n)) KnownWorkers.Add(name);

            OnPropertyChanged(nameof(GrandTotal));
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task RecordAdvanceAsync()
    {
        Error = ""; Info = "";
        if (string.IsNullOrWhiteSpace(WorkerName)) { Error = "Enter the worker's name."; return; }
        if (Amount <= 0) { Error = "Enter an amount greater than zero."; return; }

        try
        {
            IsBusy = true;
            await api.CreateStaffAdvanceAsync(new SaveStaffAdvanceRequest(
                WorkerName.Trim(), Amount, AdvanceDate, string.IsNullOrWhiteSpace(Note) ? null : Note.Trim()));

            Info = $"Advance of ₹{Amount:N2} recorded for {WorkerName.Trim()}.";
            WorkerName = ""; Amount = 0; Note = ""; AdvanceDate = DateTime.Today;
            await LoadAsync();
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task DeleteAdvanceAsync(StaffAdvanceDto? advance)
    {
        if (advance is null || advance.IsDeleted) return;

        var confirm = MessageBox.Show(
            $"Mark the ₹{advance.Amount:N2} advance for {advance.WorkerName} on {advance.AdvanceDate:dd-MM-yyyy} as deleted?\n\n" +
            "The entry is kept for the record — stamped with your name — and stops counting towards the totals.",
            "Delete advance", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (confirm != MessageBoxResult.Yes) return;

        try
        {
            IsBusy = true; Error = ""; Info = "";
            await api.DeleteStaffAdvanceAsync(advance.Id);
            Info = $"Advance for {advance.WorkerName} marked deleted. Tick 'Show deleted' to see it.";
            await LoadAsync();
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsBusy = false; }
    }
}
