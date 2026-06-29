using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using E6CarSpa.Contracts;
using E6CarSpa.Desktop.Services;

namespace E6CarSpa.Desktop.ViewModels;

/// <summary>
/// Workflow steps 1-3: capture the customer + car (with auto-lookup), pick services
/// from the catalogue, then save as a quotation.
/// </summary>
public partial class NewJobViewModel(IApiClient api, ShellViewModel shell) : ObservableObject, IAsyncInitialize
{
    // Step 1 — intake
    [ObservableProperty] private string _customerName = "";
    [ObservableProperty] private string _phone = "";
    [ObservableProperty] private string _carNumber = "";
    [ObservableProperty] private string _carModel = "";
    [ObservableProperty] private string _lookupInfo = "";

    public ObservableCollection<VehicleDto> KnownVehicles { get; } = new();

    // Step 2 — catalogue + chosen lines
    public ObservableCollection<ServiceDto> Catalogue { get; } = new();
    public ObservableCollection<LineItemVm> Lines { get; } = new();
    [ObservableProperty] private ServiceDto? _selectedService;

    [ObservableProperty] private decimal _discountAmount;
    [ObservableProperty] private string _notes = "";

    /// <summary>When off, this quotation is a non-GST bill (no tax charged).</summary>
    [ObservableProperty] private bool _applyGst = true;

    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _error = "";

    // Totals (client-side preview; the server is authoritative)
    public decimal SubTotal => Lines.Sum(l => l.Quantity * l.UnitPrice);
    public decimal TotalTax => ApplyGst ? Lines.Sum(l => l.TaxAmount) : 0m;
    public decimal GrandTotal => Math.Max(0, SubTotal - Lines.Sum(l => l.DiscountAmount) - DiscountAmount + TotalTax);

    public async Task InitializeAsync()
    {
        try
        {
            var services = await api.GetServicesAsync() ?? new();
            Catalogue.Clear();
            foreach (var s in services) Catalogue.Add(s);
        }
        catch (Exception ex) { Error = ex.Message; }
    }

    [RelayCommand]
    private async Task LookupAsync()
    {
        LookupInfo = "";
        KnownVehicles.Clear();
        try
        {
            CustomerLookupResult? result = null;
            if (!string.IsNullOrWhiteSpace(Phone))
                result = await api.LookupByPhoneAsync(Phone.Trim());
            if ((result is null || !result.Found) && !string.IsNullOrWhiteSpace(CarNumber))
                result = await api.LookupByCarAsync(CarNumber.Trim());

            if (result is { Found: true, Customer: { } c })
            {
                CustomerName = c.Name;
                Phone = c.Phone;
                
                foreach (var v in c.Vehicles)
                    KnownVehicles.Add(v);

                if (string.IsNullOrWhiteSpace(CarNumber) && c.Vehicles.Count == 1)
                {
                    CarNumber = c.Vehicles[0].CarNumber;
                    CarModel = c.Vehicles[0].CarModel;
                }
                LookupInfo = $"Existing customer — {c.Vehicles.Count} vehicle(s) on file.";
            }
            else
            {
                LookupInfo = "New customer.";
            }
        }
        catch (Exception ex) { Error = ex.Message; }
    }

    [RelayCommand]
    private void SelectVehicle(VehicleDto v)
    {
        if (v is not null)
        {
            CarNumber = v.CarNumber;
            CarModel = v.CarModel;
        }
    }

    [RelayCommand]
    private void AddService()
    {
        if (SelectedService is null) return;
        AddServiceLine(SelectedService);
    }

    public void AddServiceLine(ServiceDto s)
    {
        var line = new LineItemVm
        {
            ServiceId = s.Id,
            Description = s.Name,
            Quantity = 1m,
            UnitPrice = s.DefaultPrice,
            GstRate = s.GstRate
        };
        line.Changed += RecalcTotals;
        Lines.Add(line);
        RecalcTotals();
    }

    [RelayCommand]
    private void RemoveLine(LineItemVm? line)
    {
        if (line is null) return;
        line.Changed -= RecalcTotals;
        Lines.Remove(line);
        RecalcTotals();
    }

    partial void OnDiscountAmountChanged(decimal value) => RecalcTotals();
    partial void OnApplyGstChanged(bool value) => RecalcTotals();

    private void RecalcTotals()
    {
        OnPropertyChanged(nameof(SubTotal));
        OnPropertyChanged(nameof(TotalTax));
        OnPropertyChanged(nameof(GrandTotal));
    }

    [RelayCommand]
    private async Task SaveQuotationAsync()
    {
        Error = "";
        if (string.IsNullOrWhiteSpace(CustomerName) || string.IsNullOrWhiteSpace(Phone) || string.IsNullOrWhiteSpace(CarNumber))
        {
            Error = "Customer name, phone and car number are required.";
            return;
        }
        if (Lines.Count == 0)
        {
            Error = "Add at least one service.";
            return;
        }

        try
        {
            IsBusy = true;
            var req = new CreateQuotationRequest(
                CustomerName.Trim(), Phone.Trim(), CarNumber.Trim(), CarModel?.Trim() ?? "",
                DiscountAmount, string.IsNullOrWhiteSpace(Notes) ? null : Notes.Trim(),
                Lines.Select(l => new InvoiceItemInput(
                    l.ServiceId, l.ProductId, l.Description, l.Quantity, l.UnitPrice, l.DiscountAmount)).ToList(),
                ApplyGst);

            var invoice = await api.CreateQuotationAsync(req);
            Reset();
            await shell.OpenInvoiceAsync(invoice.Id);
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsBusy = false; }
    }

    private void Reset()
    {
        CustomerName = Phone = CarNumber = CarModel = Notes = LookupInfo = "";
        DiscountAmount = 0;
        ApplyGst = true;
        KnownVehicles.Clear();
        foreach (var l in Lines) l.Changed -= RecalcTotals;
        Lines.Clear();
        RecalcTotals();
    }
}
