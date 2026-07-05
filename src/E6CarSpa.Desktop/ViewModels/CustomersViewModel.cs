using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using E6CarSpa.Contracts;
using E6CarSpa.Client;

namespace E6CarSpa.Desktop.ViewModels;

public partial class CustomersViewModel(IApiClient api) : ObservableObject, IAsyncInitialize
{
    public ObservableCollection<CustomerDto> Customers { get; } = new();

    [ObservableProperty] private string _searchText = "";
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _info = "";
    [ObservableProperty] private string _error = "";

    [ObservableProperty] private CustomerDto? _selectedCustomer;
    [ObservableProperty] private CustomerHistoryDto? _customerHistory;
    [ObservableProperty] private bool _isLoadingDetails;

    [ObservableProperty] private VehicleDto? _selectedVehicle;
    [ObservableProperty] private List<InvoiceListItemDto> _filteredInvoices = new();

    // Bumped on every keystroke; lets a debounced/in-flight search know it has been superseded.
    private int _searchGeneration;

    public async Task InitializeAsync()
    {
        await LoadCustomersAsync();
    }

    // Live search: each keystroke schedules a load after a short quiet period so we don't hit the
    // API on every character, and a stale request can't overwrite the results of a newer one.
    partial void OnSearchTextChanged(string value) => _ = DebouncedSearchAsync();

    private async Task DebouncedSearchAsync()
    {
        var gen = ++_searchGeneration;
        await Task.Delay(250);
        if (gen != _searchGeneration) return;   // the user typed again — let the newer keystroke win
        await LoadCustomersAsync();
    }

    partial void OnSelectedCustomerChanged(CustomerDto? value)
    {
        if (value is not null)
            _ = LoadCustomerDetailsAsync(value.Phone);
        else
        {
            CustomerHistory = null;
            SelectedVehicle = null;
            FilteredInvoices = new();
        }
    }

    partial void OnCustomerHistoryChanged(CustomerHistoryDto? value)
    {
        if (value is not null && SelectedCustomer?.Vehicles.Count > 0)
            SelectVehicle(SelectedCustomer.Vehicles[0]);
        else
        {
            SelectedVehicle = null;
            FilteredInvoices = value?.Invoices ?? new();
        }
    }

    [RelayCommand]
    private void SelectVehicle(VehicleDto? vehicle)
    {
        SelectedVehicle = vehicle;
        if (vehicle is not null && CustomerHistory is not null)
        {
            FilteredInvoices = CustomerHistory.Invoices
                .Where(i => i.CarNumber == vehicle.CarNumber)
                .ToList();
        }
        else
        {
            FilteredInvoices = CustomerHistory?.Invoices ?? new();
        }
    }

    [RelayCommand]
    private async Task LoadCustomersAsync()
    {
        var gen = _searchGeneration;
        IsBusy = true;
        Error = "";
        try
        {
            var data = await api.GetCustomersAsync(SearchText);
            if (gen != _searchGeneration) return;   // a newer search started while this was in flight
            Customers.Clear();
            if (data != null)
            {
                foreach (var c in data) Customers.Add(c);
            }
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task LoadCustomerDetailsAsync(string phone)
    {
        IsLoadingDetails = true;
        CustomerHistory = null;
        try
        {
            CustomerHistory = await api.GetCustomerHistoryAsync(phone);
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
        finally
        {
            IsLoadingDetails = false;
        }
    }
}
