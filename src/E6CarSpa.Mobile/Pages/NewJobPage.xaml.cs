using System.Collections.ObjectModel;
using E6CarSpa.Contracts;
using E6CarSpa.Client;
using E6CarSpa.Mobile.Services;
using E6CarSpa.Mobile.ViewModels;

namespace E6CarSpa.Mobile.Pages;

public partial class NewJobPage : ContentPage
{
    private readonly ObservableCollection<LineItemVm> _lines = new();
    private readonly ObservableCollection<VehicleDto> _knownVehicles = new();
    private readonly ThemeRowRefresher _themeRows = new();
    private List<ServiceDto> _catalogue = new();
    private bool _loadedOnce;

    public NewJobPage()
    {
        InitializeComponent();
        BindableLayout.SetItemsSource(LinesHost, _lines);
        BindableLayout.SetItemsSource(VehiclesHost, _knownVehicles);
        _lines.CollectionChanged += (_, _) => { NoLinesLabel.IsVisible = _lines.Count == 0; Recalc(); };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        ThemeColors.ApplyTo(ServicePicker);
        if (_themeRows.RowsAreStale())
        {
            BindableLayout.SetItemsSource(LinesHost, null);
            BindableLayout.SetItemsSource(LinesHost, _lines);
        }
        if (_loadedOnce) return;
        _loadedOnce = true;
        try
        {
            _catalogue = await AppServices.Api.GetServicesAsync() ?? new();
            ServicePicker.ItemsSource = _catalogue;
        }
        catch (Exception ex)
        {
            ShowError(ex is ApiException a ? a.Message : "Cannot load the service list.");
        }
    }

    private async void OnLookupClicked(object? sender, EventArgs e)
    {
        ErrorLabel.IsVisible = false;
        _knownVehicles.Clear();
        VehiclesHost.IsVisible = false;
        var phone = PhoneEntry.Text?.Trim() ?? "";
        var car = CarNumberEntry.Text?.Trim() ?? "";
        if (phone.Length == 0 && car.Length == 0)
        {
            ShowError("Enter a phone or car number to look up.");
            return;
        }
        try
        {
            CustomerLookupResult? result = null;
            if (phone.Length > 0) result = await AppServices.Api.LookupByPhoneAsync(phone);
            if ((result is null || !result.Found) && car.Length > 0)
                result = await AppServices.Api.LookupByCarAsync(car);

            if (result is { Found: true, Customer: { } c })
            {
                NameEntry.Text = c.Name;
                PhoneEntry.Text = c.Phone;
                if (string.IsNullOrWhiteSpace(CarNumberEntry.Text) && c.Vehicles.Count == 1)
                {
                    CarNumberEntry.Text = c.Vehicles[0].CarNumber;
                    CarModelEntry.Text = c.Vehicles[0].CarModel;
                }
                else if (c.Vehicles.Count > 1)
                {
                    // Multiple cars on file — show them as tap-to-fill chips.
                    foreach (var v in c.Vehicles) _knownVehicles.Add(v);
                    VehiclesHost.IsVisible = true;
                }
                LookupInfo.Text = c.Vehicles.Count > 1
                    ? $"Existing customer — tap one of the {c.Vehicles.Count} cars below."
                    : $"Existing customer — {c.Vehicles.Count} vehicle(s) on file.";
            }
            else
            {
                LookupInfo.Text = "New customer.";
            }
            LookupInfo.IsVisible = true;
        }
        catch (Exception ex)
        {
            ShowError(ex is ApiException a ? a.Message : "Lookup failed.");
        }
    }

    private void OnPickVehicleClicked(object? sender, EventArgs e)
    {
        if (sender is Button { BindingContext: VehicleDto v })
        {
            CarNumberEntry.Text = v.CarNumber;
            CarModelEntry.Text = v.CarModel;
            // Car chosen — collapse the chips and confirm which one is in use.
            VehiclesHost.IsVisible = false;
            _knownVehicles.Clear();
            LookupInfo.Text = $"Using {v.CarNumber}.";
        }
    }

    private void OnAddServiceClicked(object? sender, EventArgs e)
    {
        if (ServicePicker.SelectedItem is not ServiceDto s) return;
        var line = new LineItemVm
        {
            ServiceId = s.Id,
            Description = s.Name,
            Quantity = 1m,
            UnitPrice = s.DefaultPrice,
            GstRate = s.GstRate
        };
        line.Changed += Recalc;
        _lines.Add(line);
    }

    // Add a service that isn't in the catalogue yet. Opens one dialog capturing BOTH the name
    // and the price (see NewServiceOverlay in the XAML), then bills it straight away — the same
    // flow as the desktop's "+ New" button.
    private void OnNewServiceClicked(object? sender, EventArgs e)
    {
        NewServiceNameEntry.Text = "";
        NewServicePriceEntry.Text = "";
        NewServiceError.IsVisible = false;
        NewServiceOverlay.IsVisible = true;
        NewServiceNameEntry.Focus();
    }

    private void OnCancelNewService(object? sender, EventArgs e) => NewServiceOverlay.IsVisible = false;

    private async void OnConfirmNewService(object? sender, EventArgs e)
    {
        NewServiceError.IsVisible = false;

        var name = NewServiceNameEntry.Text?.Trim() ?? "";
        if (name.Length == 0) { ShowNewServiceError("Enter a service name."); return; }
        if (!decimal.TryParse(NewServicePriceEntry.Text?.Trim(), out var price) || price < 0)
        {
            ShowNewServiceError("Enter a valid price.");
            return;
        }

        SetBusy(true);
        try
        {
            // Category/HSN/GST take the same defaults as the desktop dialog.
            var created = await AppServices.Api.CreateServiceAsync(
                new SaveServiceRequest(name, "General", price, "", 18m, true));

            _catalogue = await AppServices.Api.GetServicesAsync() ?? new();
            ServicePicker.ItemsSource = _catalogue;

            var line = new LineItemVm
            {
                ServiceId = created.Id,
                Description = created.Name,
                Quantity = 1m,
                UnitPrice = created.DefaultPrice,
                GstRate = created.GstRate
            };
            line.Changed += Recalc;
            _lines.Add(line);
            NewServiceOverlay.IsVisible = false;
        }
        catch (Exception ex) { ShowNewServiceError(ex is ApiException a ? a.Message : "Could not add the service."); }
        finally { SetBusy(false); }
    }

    private void ShowNewServiceError(string message)
    {
        NewServiceError.Text = message;
        NewServiceError.IsVisible = true;
    }

    private void OnRemoveLineClicked(object? sender, EventArgs e)
    {
        if (sender is Button { BindingContext: LineItemVm line })
        {
            line.Changed -= Recalc;
            _lines.Remove(line);
        }
    }

    private void OnTotalsInputChanged(object? sender, TextChangedEventArgs e) => Recalc();
    private void OnGstToggled(object? sender, ToggledEventArgs e) => Recalc();

    private async void OnSettingsClicked(object? sender, EventArgs e) =>
        await Shell.Current.GoToAsync("settings");

    private void Recalc()
    {
        var subTotal = _lines.Sum(l => l.Quantity * l.UnitPrice);
        var lineDiscounts = _lines.Sum(l => l.DiscountAmount);
        var headerDiscount = ParseDecimal(DiscountEntry.Text);
        // Taxable = subtotal minus ALL discounts first, then GST once on that amount
        var taxable = Math.Max(0, subTotal - lineDiscounts - headerDiscount);
        var gstRate = _lines.Count > 0 ? _lines[0].GstRate : 0m;
        var tax = GstSwitch.IsToggled ? Math.Round(taxable * gstRate / 100m, 2) : 0m;
        var grand = Math.Round(taxable + tax, 2);

        SubTotalLabel.Text = $"₹{subTotal:N0}";
        TaxLabel.Text = $"₹{tax:N0}";
        GrandLabel.Text = $"₹{grand:N0}";
        DiscountLabel.Text = $"- ₹{(lineDiscounts + headerDiscount):N0}";
        BalanceLabel.Text = $"₹{grand:N0}";   // a new quotation has no payments yet
    }

    private async void OnSaveClicked(object? sender, EventArgs e)
    {
        ErrorLabel.IsVisible = false;
        var name = NameEntry.Text?.Trim() ?? "";
        var phone = PhoneEntry.Text?.Trim() ?? "";
        var car = CarNumberEntry.Text?.Trim() ?? "";

        if (name.Length == 0 || phone.Length == 0 || car.Length == 0)
        {
            ShowError("Customer name, phone and car number are required.");
            return;
        }
        if (_lines.Count == 0)
        {
            ShowError("Add at least one service.");
            return;
        }

        SetBusy(true);
        try
        {
            var req = new CreateQuotationRequest(
                name, phone, car, CarModelEntry.Text?.Trim() ?? "",
                ParseDecimal(DiscountEntry.Text),
                string.IsNullOrWhiteSpace(NotesEditor.Text) ? null : NotesEditor.Text.Trim(),
                _lines.Select(l => new InvoiceItemInput(
                    l.ServiceId, l.ProductId, l.Description, l.Quantity, l.UnitPrice, l.DiscountAmount)).ToList(),
                GstSwitch.IsToggled);

            var invoice = await AppServices.Api.CreateQuotationAsync(req);
            ResetForm();
            await Shell.Current.GoToAsync($"invoice?id={invoice.Id}");
        }
        catch (Exception ex)
        {
            ShowError(ex is ApiException a ? a.Message : "Could not save the quotation.");
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void ResetForm()
    {
        NameEntry.Text = PhoneEntry.Text = CarNumberEntry.Text = CarModelEntry.Text = NotesEditor.Text = "";
        DiscountEntry.Text = "0";
        GstSwitch.IsToggled = true;
        LookupInfo.IsVisible = false;
        foreach (var l in _lines) l.Changed -= Recalc;
        _lines.Clear();
    }

    private static decimal ParseDecimal(string? s) =>
        decimal.TryParse(s, out var v) ? v : 0m;

    private void ShowError(string message)
    {
        ErrorLabel.Text = message;
        ErrorLabel.IsVisible = true;
    }

    private void SetBusy(bool busy)
    {
        Busy.IsRunning = busy;
        Busy.IsVisible = busy;
        SaveButton.IsEnabled = !busy;
    }
}
