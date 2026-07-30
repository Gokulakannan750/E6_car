using System.Globalization;
using E6CarSpa.Client;
using E6CarSpa.Contracts;
using E6CarSpa.Mobile.Services;

namespace E6CarSpa.Mobile.Pages;

/// <summary>
/// Phone version of the desktop Staff Advances screen: cash given to workers, typed-in names,
/// advances only — no repayments or payroll. Same anonymous API as the counter screens.
/// </summary>
public partial class AdvancesPage : ContentPage
{
    private readonly ThemeRowRefresher _themeRows = new();
    private bool _loadedOnce;

    // Bumped on every keystroke in the search box; a pending debounce that is no longer the
    // newest one drops out instead of firing a second request.
    private int _searchGeneration;

    public AdvancesPage()
    {
        InitializeComponent();
        AdvanceDatePicker.Date = DateTime.Today;
        SelectSegment(perWorker: true);   // default to the totals view
    }

    private void OnShowPerWorker(object? sender, EventArgs e) => SelectSegment(perWorker: true);
    private void OnShowAllAdvances(object? sender, EventArgs e) => SelectSegment(perWorker: false);

    /// <summary>Flip between the "Per worker" and "All advances" views and paint the segment buttons.</summary>
    private void SelectSegment(bool perWorker)
    {
        PerWorkerSection.IsVisible = perWorker;
        AllAdvancesSection.IsVisible = !perWorker;

        var accent = Application.Current?.Resources.TryGetValue("Primary", out var c) == true && c is Color col
            ? col : Color.FromArgb("#FF3B30");

        SegPerWorker.BackgroundColor = perWorker ? accent : Colors.Transparent;
        SegPerWorker.TextColor = perWorker ? Colors.White : accent;
        SegAllAdvances.BackgroundColor = perWorker ? Colors.Transparent : accent;
        SegAllAdvances.TextColor = perWorker ? accent : Colors.White;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_themeRows.RowsAreStale())
        {
            RebuildRows(AdvancesList);
            RebuildRows(SummaryList);
        }
        if (_loadedOnce) return;
        _loadedOnce = true;
        await LoadAsync();
    }

    private static void RebuildRows(CollectionView list)
    {
        if (list.ItemsSource is not { } rows) return;
        list.ItemsSource = null;
        list.ItemsSource = rows;
    }

    private async void OnRefreshing(object? sender, EventArgs e)
    {
        await LoadAsync();
        Refresh.IsRefreshing = false;
    }

    private async void OnSearchChanged(object? sender, TextChangedEventArgs e)
    {
        var mine = ++_searchGeneration;
        await Task.Delay(300);
        if (mine != _searchGeneration) return;   // superseded by a newer keystroke
        await LoadAsync();
    }

    private async void OnShowDeletedToggled(object? sender, ToggledEventArgs e) => await LoadAsync();

    private void OnPickWorkerClicked(object? sender, EventArgs e)
    {
        if (sender is Button { BindingContext: string name })
            WorkerEntry.Text = name;
    }

    private async void OnSettingsClicked(object? sender, EventArgs e) =>
        await Shell.Current.GoToAsync("settings");

    private async Task LoadAsync()
    {
        ErrorLabel.IsVisible = false;
        var search = string.IsNullOrWhiteSpace(SearchEntry.Text) ? null : SearchEntry.Text.Trim();
        try
        {
            var advances = await AppServices.Api.GetStaffAdvancesAsync(search, ShowDeletedSwitch.IsToggled) ?? new();
            AdvancesList.ItemsSource = advances;
            NoAdvancesLabel.IsVisible = advances.Count == 0;

            var summary = await AppServices.Api.GetStaffAdvanceSummaryAsync() ?? new();
            SummaryList.ItemsSource = summary;
            NoWorkersLabel.IsVisible = summary.Count == 0;
            GrandTotalLabel.Text = $"₹{summary.Sum(s => s.TotalAdvanced):N2}";

            // Suggestion chips come from the summary, so they cover every worker on file —
            // not just the ones matching the current search.
            var workers = summary.Select(s => s.WorkerName).OrderBy(n => n).ToList();
            BindableLayout.SetItemsSource(KnownWorkersHost, workers);
            KnownWorkersHost.IsVisible = workers.Count > 0;
        }
        catch (Exception ex)
        {
            ShowError(ex is ApiException a ? a.Message : "Cannot reach the server. Pull down to retry.");
        }
    }

    private async void OnRecordClicked(object? sender, EventArgs e)
    {
        ErrorLabel.IsVisible = false;
        InfoLabel.IsVisible = false;

        var worker = WorkerEntry.Text?.Trim() ?? "";
        if (worker.Length == 0)
        {
            ShowError("Enter the worker's name.");
            return;
        }
        if (!decimal.TryParse(AmountEntry.Text?.Trim(), NumberStyles.Number, CultureInfo.CurrentCulture, out var amount)
            || amount <= 0)
        {
            ShowError("Enter an amount greater than zero.");
            return;
        }

        var note = NoteEntry.Text?.Trim();
        SetBusy(true);
        try
        {
            await AppServices.Api.CreateStaffAdvanceAsync(new SaveStaffAdvanceRequest(
                worker, amount, AdvanceDatePicker.Date ?? DateTime.Today,
                string.IsNullOrWhiteSpace(note) ? null : note));

            InfoLabel.Text = $"Advance of ₹{amount:N2} recorded for {worker}.";
            InfoLabel.IsVisible = true;

            WorkerEntry.Text = "";
            AmountEntry.Text = "";
            NoteEntry.Text = "";
            AdvanceDatePicker.Date = DateTime.Today;

            await LoadAsync();
        }
        catch (Exception ex)
        {
            ShowError(ex is ApiException a ? a.Message : "Could not save the advance.");
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async void OnDeleteClicked(object? sender, EventArgs e)
    {
        if (sender is not Button { BindingContext: StaffAdvanceDto advance } || advance.IsDeleted) return;

        var confirm = await DisplayAlertAsync(
            "Delete advance",
            $"Mark the ₹{advance.Amount:N2} advance for {advance.WorkerName} on {advance.AdvanceDate:dd-MM-yyyy} as deleted?\n\n" +
            "It is kept for the record — stamped with your name — and stops counting towards the totals.",
            "Delete", "Cancel");
        if (!confirm) return;

        ErrorLabel.IsVisible = false;
        InfoLabel.IsVisible = false;
        SetBusy(true);
        try
        {
            await AppServices.Api.DeleteStaffAdvanceAsync(advance.Id);
            await LoadAsync();
        }
        catch (Exception ex)
        {
            ShowError(ex is ApiException a ? a.Message : "Could not delete the advance.");
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void SetBusy(bool busy)
    {
        Busy.IsRunning = busy;
        Busy.IsVisible = busy;
        RecordButton.IsEnabled = !busy;
    }

    private void ShowError(string message)
    {
        ErrorLabel.Text = message;
        ErrorLabel.IsVisible = true;
    }
}
