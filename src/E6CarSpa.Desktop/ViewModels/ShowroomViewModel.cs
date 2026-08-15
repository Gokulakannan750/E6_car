using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using E6CarSpa.Client;
using E6CarSpa.Contracts;

namespace E6CarSpa.Desktop.ViewModels;

/// <summary>
/// Two-tab screen for managing showroom locations and their visit records.
/// Tab 1 — Showrooms: master list with add/edit/delete.
/// Tab 2 — Visits: log team visits, see per-showroom totals.
/// </summary>
public partial class ShowroomViewModel(IApiClient api) : ObservableObject, IAsyncInitialize
{
    // ═══════════════════════════════════════
    //  Tab state
    // ═══════════════════════════════════════

    [ObservableProperty] private bool _isShowroomsTab = true;
    [ObservableProperty] private bool _isVisitsTab;

    // ═══════════════════════════════════════
    //  Showrooms tab
    // ═══════════════════════════════════════

    public ObservableCollection<ShowroomDto> Showrooms { get; } = new();

    [ObservableProperty] private string _showroomName = "";
    [ObservableProperty] private string _showroomAddress = "";
    [ObservableProperty] private string _showroomPhone = "";
    [ObservableProperty] private bool _isFormVisible;
    [ObservableProperty] private ShowroomDto? _editingShowroom;

    // ═══════════════════════════════════════
    //  Visits tab
    // ═══════════════════════════════════════

    public ObservableCollection<ShowroomVisitDto> Visits { get; } = new();

    public ObservableCollection<ShowroomDto> ShowroomPickList { get; } = new();

    /// <summary>Id of the showroom to filter by. Null means all.</summary>
    [ObservableProperty] private Guid? _selectedShowroomFilterId;

    /// <summary>Total across the visits currently shown.</summary>
    public decimal VisitsTotal => Visits.Sum(v => v.Amount);
    public int TotalVehicles => Visits.Sum(v => v.VehiclesAttended);

    [ObservableProperty] private Guid _visitShowroomId;
    [ObservableProperty] private DateTime _visitDate = DateTime.Today;
    [ObservableProperty] private string _teamSent = "";
    [ObservableProperty] private int _vehiclesAttended;
    [ObservableProperty] private decimal _visitAmount;
    [ObservableProperty] private string _visitNote = "";

    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _error = "";
    [ObservableProperty] private string _info = "";

    public async Task InitializeAsync() => await LoadShowroomsAsync();

    // ═══════════════════════════════════════
    //  Showrooms
    // ═══════════════════════════════════════

    [RelayCommand]
    private async Task LoadShowroomsAsync()
    {
        try
        {
            IsBusy = true; Error = "";
            var list = await api.GetShowroomsAsync() ?? new();
            Showrooms.Clear();
            foreach (var s in list) Showrooms.Add(s);

            // Refresh the pick list too
            ShowroomPickList.Clear();
            foreach (var s in list.OrderBy(s => s.Name)) ShowroomPickList.Add(s);
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private void BeginAddShowroom()
    {
        EditingShowroom = null;
        ShowroomName = ""; ShowroomAddress = ""; ShowroomPhone = "";
        IsFormVisible = true;
    }

    [RelayCommand]
    private void BeginEditShowroom(ShowroomDto? s)
    {
        if (s is null) return;
        EditingShowroom = s;
        ShowroomName = s.Name; ShowroomAddress = s.Address; ShowroomPhone = s.Phone ?? "";
        IsFormVisible = true;
    }

    [RelayCommand]
    private async Task SaveShowroomAsync()
    {
        Error = ""; Info = "";
        if (string.IsNullOrWhiteSpace(ShowroomName)) { Error = "Enter the showroom name."; return; }
        if (string.IsNullOrWhiteSpace(ShowroomAddress)) { Error = "Enter the address."; return; }

        try
        {
            IsBusy = true;
            if (EditingShowroom is null)
            {
                await api.CreateShowroomAsync(new SaveShowroomRequest(ShowroomName.Trim(), ShowroomAddress.Trim(),
                    string.IsNullOrWhiteSpace(ShowroomPhone) ? null : ShowroomPhone.Trim()));
                Info = "Showroom added.";
            }
            else
            {
                await api.UpdateShowroomAsync(EditingShowroom.Id,
                    new SaveShowroomRequest(ShowroomName.Trim(), ShowroomAddress.Trim(),
                        string.IsNullOrWhiteSpace(ShowroomPhone) ? null : ShowroomPhone.Trim()));
                Info = "Showroom updated.";
            }

            IsFormVisible = false;
            await LoadShowroomsAsync();
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task DeleteShowroomAsync(ShowroomDto? s)
    {
        if (s is null) return;

        var confirm = MessageBox.Show(
            $"Remove '{s.Name}' from the active list?\n\nIts visit history is preserved.",
            "Delete showroom", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (confirm != MessageBoxResult.Yes) return;

        try
        {
            IsBusy = true; Error = ""; Info = "";
            await api.DeleteShowroomAsync(s.Id);
            Info = $"{s.Name} marked inactive.";
            await LoadShowroomsAsync();
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsBusy = false; }
    }

    // ═══════════════════════════════════════
    //  Visits
    // ═══════════════════════════════════════

    [RelayCommand]
    private void ShowShowrooms()
    {
        IsShowroomsTab = true;
        IsVisitsTab = false;
    }

    [RelayCommand]
    private void ShowVisits()
    {
        IsShowroomsTab = false;
        IsVisitsTab = true;
        _ = LoadSummaryAsync();
    }

    [RelayCommand]
    private async Task LoadVisitsAsync()
    {
        try
        {
            IsBusy = true; Error = "";
            var list = SelectedShowroomFilterId is Guid sid
                ? await api.GetShowroomVisitsAsync(sid) ?? new()
                : new List<ShowroomVisitDto>();

            Visits.Clear();
            foreach (var v in list.OrderByDescending(v => v.VisitDate)) Visits.Add(v);

            OnPropertyChanged(nameof(VisitsTotal));
            OnPropertyChanged(nameof(TotalVehicles));
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task LoadSummaryAsync()
    {
        try
        {
            IsBusy = true; Error = "";
            var summaries = await api.GetShowroomSummaryAsync() ?? new();

            Visits.Clear();
            foreach (var s in summaries)
            {
                Visits.Add(new ShowroomVisitDto(Guid.Empty, s.ShowroomId, s.ShowroomName,
                    DateTime.UtcNow, $"{s.VisitCount} visits", s.TotalVehicles, s.TotalAmount, null));
            }
            OnPropertyChanged(nameof(VisitsTotal));
            OnPropertyChanged(nameof(TotalVehicles));
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsBusy = false; }
    }

    partial void OnSelectedShowroomFilterIdChanged(Guid? value)
    {
        if (value is Guid)
            _ = LoadVisitsAsync();
        else
            _ = LoadSummaryAsync();
    }

    [RelayCommand]
    private async Task RecordVisitAsync()
    {
        Error = ""; Info = "";
        if (VisitShowroomId == Guid.Empty) { Error = "Select a showroom."; return; }
        if (string.IsNullOrWhiteSpace(TeamSent)) { Error = "Enter the team details."; return; }

        try
        {
            IsBusy = true;
            await api.CreateShowroomVisitAsync(new SaveShowroomVisitRequest(
                VisitShowroomId, VisitDate, TeamSent.Trim(), VehiclesAttended, VisitAmount,
                string.IsNullOrWhiteSpace(VisitNote) ? null : VisitNote.Trim()));

            Info = "Visit recorded.";
            TeamSent = ""; VehiclesAttended = 0; VisitAmount = 0; VisitNote = "";
            await LoadVisitsAsync();
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task DeleteVisitAsync(ShowroomVisitDto? visit)
    {
        if (visit is null || visit.Id == Guid.Empty) return;

        var confirm = MessageBox.Show(
            $"Delete the visit for {visit.ShowroomName} on {visit.VisitDate:dd-MM-yyyy} (₹{visit.Amount:N2})?",
            "Delete visit", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (confirm != MessageBoxResult.Yes) return;

        try
        {
            IsBusy = true; Error = ""; Info = "";
            await api.DeleteShowroomVisitAsync(visit.Id);
            Info = "Visit deleted.";

            if (SelectedShowroomFilterId is Guid)
                await LoadVisitsAsync();
            else
                await LoadSummaryAsync();
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsBusy = false; }
    }
}
