using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using E6CarSpa.Contracts;
using E6CarSpa.Client;

namespace E6CarSpa.Desktop.ViewModels;

/// <summary>Owner reports: sales for a date range, GST summary for filing, and customer history.</summary>
public partial class ReportsViewModel(IApiClient api) : ObservableObject, IAsyncInitialize
{
    [ObservableProperty] private DateTime _fromDate = new(DateTime.Today.Year, DateTime.Today.Month, 1);
    [ObservableProperty] private DateTime _toDate = DateTime.Today;

    [ObservableProperty] private SalesReportDto? _sales;
    public ObservableCollection<DailySalesRow> Daily { get; } = new();
    public record TopServiceUiRow(string Name, decimal Quantity, decimal Amount, bool IsTop3);
    public ObservableCollection<TopServiceUiRow> TopServices { get; } = new();

    [ObservableProperty] private GstSummaryDto? _gst;
    public ObservableCollection<GstRateRow> GstRows { get; } = new();

    [ObservableProperty] private string _customerPhone = "";
    [ObservableProperty] private CustomerHistoryDto? _customerHistory;
    public ObservableCollection<InvoiceListItemDto> CustomerInvoices { get; } = new();

    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _error = "";

    public Task InitializeAsync() => RunAsync();

    [RelayCommand]
    private async Task RunAsync()
    {
        try
        {
            IsBusy = true; Error = "";
            Sales = await api.GetSalesReportAsync(FromDate, ToDate);
            Daily.Clear();
            foreach (var d in Sales?.Daily ?? new()) Daily.Add(d);
            TopServices.Clear();
            var topList = Sales?.TopServices ?? new();
            for (int i = 0; i < topList.Count; i++)
            {
                var t = topList[i];
                TopServices.Add(new TopServiceUiRow(t.Name, t.Quantity, t.Amount, i < 3));
            }

            Gst = await api.GetGstSummaryAsync(FromDate, ToDate);
            GstRows.Clear();
            foreach (var r in Gst?.Rows ?? new()) GstRows.Add(r);
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task LookupCustomerAsync()
    {
        if (string.IsNullOrWhiteSpace(CustomerPhone)) { Error = "Enter a phone number."; return; }
        try
        {
            IsBusy = true; Error = "";
            CustomerHistory = await api.GetCustomerHistoryAsync(CustomerPhone.Trim());
            CustomerInvoices.Clear();
            foreach (var i in CustomerHistory?.Invoices ?? new()) CustomerInvoices.Add(i);
        }
        catch (ApiException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            CustomerHistory = null; CustomerInvoices.Clear();
            Error = "No customer found with that phone.";
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsBusy = false; }
    }
}
