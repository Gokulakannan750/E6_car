using CommunityToolkit.Mvvm.ComponentModel;
using E6CarSpa.Contracts;
using E6CarSpa.Client;

namespace E6CarSpa.Desktop.ViewModels;

public partial class DashboardViewModel(IApiClient api) : ObservableObject, IAsyncInitialize
{
    [ObservableProperty] private DashboardSummaryDto? _summary;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _error = "";

    public async Task InitializeAsync()
    {
        try
        {
            IsBusy = true;
            Error = "";
            Summary = await api.GetDashboardAsync();
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
        finally { IsBusy = false; }
    }
}
