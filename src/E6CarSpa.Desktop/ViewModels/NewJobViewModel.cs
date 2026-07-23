using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using E6CarSpa.Contracts;
using E6CarSpa.Client;
using E6CarSpa.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;

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

    // Step 2 — catalogue + chosen lines. Catalogue is the *filtered* view shown in the list;
    // _allServices keeps the full set so the search can be cleared without a round trip.
    public ObservableCollection<ServiceDto> Catalogue { get; } = new();
    private List<ServiceDto> _allServices = new();
    public ObservableCollection<LineItemVm> Lines { get; } = new();
    [ObservableProperty] private ServiceDto? _selectedService;

    /// <summary>Filters the services list by name or category as the user types.</summary>
    [ObservableProperty] private string _serviceSearch = "";
    partial void OnServiceSearchChanged(string value) => ApplyServiceFilter();

    [ObservableProperty] private decimal _discountAmount;
    [ObservableProperty] private string _notes = "";

    /// <summary>When off, this quotation is a non-GST bill (no tax charged).</summary>
    [ObservableProperty] private bool _applyGst = true;

    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _error = "";

    // Totals (client-side preview; the server is authoritative).
    // Mirrors GstCalculator.Recalculate: discount (line + header) is deducted from the
    // subtotal first, then GST is charged ONCE on that final taxable amount — not summed
    // per line — since the shop uses a single GST rate for every service.
    public decimal SubTotal => Lines.Sum(l => l.Quantity * l.UnitPrice);
    public decimal DiscountTotal => Lines.Sum(l => l.DiscountAmount) + DiscountAmount;
    public decimal TaxableTotal => Math.Max(0m, Math.Round(SubTotal - DiscountTotal, 2, MidpointRounding.AwayFromZero));
    public decimal TotalTax => ApplyGst
        ? Math.Round(TaxableTotal * (Lines.Count > 0 ? Lines[0].GstRate : 0m) / 100m, 2, MidpointRounding.AwayFromZero)
        : 0m;
    public decimal GrandTotal => Math.Round(TaxableTotal + TotalTax, 2, MidpointRounding.AwayFromZero);
    /// <summary>Amount still owed. Always equals GrandTotal here — a new quotation has no payments yet.</summary>
    public decimal Balance => GrandTotal;

    public async Task InitializeAsync()
    {
        try
        {
            _allServices = await api.GetServicesAsync() ?? new();
            ApplyServiceFilter();
        }
        catch (Exception ex) { Error = ex.Message; }
    }

    private void ApplyServiceFilter()
    {
        var q = ServiceSearch?.Trim() ?? "";
        var matches = string.IsNullOrEmpty(q)
            ? _allServices
            : _allServices.Where(s =>
                s.Name.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                (s.Category ?? "").Contains(q, StringComparison.OrdinalIgnoreCase)).ToList();

        Catalogue.Clear();
        foreach (var s in matches) Catalogue.Add(s);
    }

    /// <summary>
    /// Add a service to the catalogue without leaving the billing screen. Creating a service is a
    /// Manager/Admin action, so the counter (which runs anonymous) gets the same login popup used
    /// for cancelling a bill.
    /// </summary>
    [RelayCommand]
    private async Task AddNewServiceAsync()
    {
        if (api.CurrentUser?.Role is not (UserRole.Admin or UserRole.Manager))
        {
            var login = App.Services.GetRequiredService<Views.LoginWindow>();
            if (Application.Current.MainWindow is { IsVisible: true } owner) login.Owner = owner;
            login.ShowDialog();
            shell.RefreshAuthState();
            if (api.CurrentUser?.Role is not (UserRole.Admin or UserRole.Manager))
            {
                Error = "Adding a service needs a Manager or Admin login.";
                return;
            }
        }

        var dialog = new Views.NewServiceDialog();
        if (Application.Current.MainWindow is { IsVisible: true } main) dialog.Owner = main;
        if (dialog.ShowDialog() != true) return;

        try
        {
            IsBusy = true; Error = "";
            // Category/HSN/GST take sensible defaults — they can be refined on the Catalogue screen.
            var created = await api.CreateServiceAsync(new SaveServiceRequest(
                dialog.ServiceName, "General", dialog.Price, "", 18m, true));

            _allServices = _allServices.Append(created).OrderBy(s => s.Name).ToList();
            ServiceSearch = "";        // clear the filter so the new service is visible
            ApplyServiceFilter();      // (also runs via OnServiceSearchChanged when it was non-empty)
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsBusy = false; }
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
        OnPropertyChanged(nameof(DiscountTotal));
        OnPropertyChanged(nameof(TaxableTotal));
        OnPropertyChanged(nameof(TotalTax));
        OnPropertyChanged(nameof(GrandTotal));
        OnPropertyChanged(nameof(Balance));
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
