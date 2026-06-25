using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using E6CarSpa.Contracts;
using E6CarSpa.Desktop.Services;
using E6CarSpa.Domain.Enums;

namespace E6CarSpa.Desktop.ViewModels;

/// <summary>Step 4 entry point: find a saved job/quotation and open it.</summary>
public partial class JobsViewModel(ApiClient api, ShellViewModel shell) : ObservableObject, IAsyncInitialize
{
    public ObservableCollection<InvoiceListItemDto> Jobs { get; } = new();

    public List<string> StatusOptions { get; } = ["All", "Quotation", "Invoiced", "Paid", "Cancelled"];
    [ObservableProperty] private string _selectedStatus = "All";
    [ObservableProperty] private string _searchText = "";
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _error = "";

    public Task InitializeAsync() => RefreshAsync();

    partial void OnSelectedStatusChanged(string value) => _ = RefreshAsync();

    [RelayCommand]
    private async Task RefreshAsync()
    {
        try
        {
            IsBusy = true;
            Error = "";
            InvoiceStatus? status = SelectedStatus == "All" ? null : Enum.Parse<InvoiceStatus>(SelectedStatus);
            var list = await api.ListInvoicesAsync(status, SearchText) ?? new();
            Jobs.Clear();
            foreach (var j in list) Jobs.Add(j);
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task OpenAsync(InvoiceListItemDto? item)
    {
        if (item is null) return;
        await shell.OpenInvoiceAsync(item.Id);
    }
}
