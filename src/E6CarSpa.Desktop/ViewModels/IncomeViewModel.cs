using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using E6CarSpa.Client;
using E6CarSpa.Contracts;

namespace E6CarSpa.Desktop.ViewModels;

/// <summary>
/// Non-invoice income entries (tips, part sales, miscellaneous). One of the three trackers grouped
/// under the "Cash" section of the app — advances and salaries share the same Staff master.
/// </summary>
public partial class IncomeViewModel(IApiClient api) : ObservableObject, IAsyncInitialize
{
    public ObservableCollection<IncomeDto> Income { get; } = new();
    public ObservableCollection<IncomeSummaryDto> Summary { get; } = new();

    [ObservableProperty] private string _source = "";
    [ObservableProperty] private decimal _amount;
    [ObservableProperty] private DateTime _incomeDate = DateTime.Today;
    [ObservableProperty] private string _note = "";

    [ObservableProperty] private DateTime _fromDate = new(DateTime.Today.Year, DateTime.Today.Month, 1);
    [ObservableProperty] private DateTime _toDate = DateTime.Today;

    [ObservableProperty] private string _search = "";
    [ObservableProperty] private bool _showDeleted;

    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _error = "";
    [ObservableProperty] private string _info = "";

    public decimal GrandTotal => Summary.Sum(s => s.TotalAmount);

    public Task InitializeAsync() => LoadAsync();

    partial void OnShowDeletedChanged(bool value) => _ = LoadAsync();

    [RelayCommand]
    private async Task LoadAsync()
    {
        try
        {
            IsBusy = true; Error = "";

            var list = await api.GetIncomeAsync(
                string.IsNullOrWhiteSpace(Search) ? null : Search, ShowDeleted) ?? new();
            Income.Clear();
            foreach (var i in list) Income.Add(i);

            var summary = await api.GetIncomeSummaryAsync(FromDate, ToDate) ?? new();
            Summary.Clear();
            foreach (var s in summary) Summary.Add(s);

            OnPropertyChanged(nameof(GrandTotal));
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task RecordAsync()
    {
        Error = ""; Info = "";
        if (string.IsNullOrWhiteSpace(Source)) { Error = "Enter the income source."; return; }
        if (Amount <= 0) { Error = "Enter an amount greater than zero."; return; }

        try
        {
            IsBusy = true;
            await api.CreateIncomeAsync(new SaveIncomeRequest(
                Source.Trim(), Amount, IncomeDate,
                string.IsNullOrWhiteSpace(Note) ? null : Note.Trim()));
            Info = $"Income of ₹{Amount:N2} from {Source} recorded.";
            Source = ""; Amount = 0; Note = ""; IncomeDate = DateTime.Today;
            await LoadAsync();
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task DeleteAsync(IncomeDto? income)
    {
        if (income is null || income.IsDeleted) return;
        var confirm = MessageBox.Show(
            $"Mark the ₹{income.Amount:N2} entry from {income.Source} on {income.IncomeDate:dd-MM-yyyy} as deleted?\n\n" +
            "The entry is kept for the record and stops counting towards the totals.",
            "Delete income", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (confirm != MessageBoxResult.Yes) return;

        try
        {
            IsBusy = true; Error = ""; Info = "";
            await api.DeleteIncomeAsync(income.Id);
            await LoadAsync();
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsBusy = false; }
    }
}
