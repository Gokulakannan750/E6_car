using System.Collections.ObjectModel;
using E6CarSpa.Contracts;
using E6CarSpa.Domain.Enums;
using E6CarSpa.Mobile.Services;
using E6CarSpa.Mobile.ViewModels;

namespace E6CarSpa.Mobile.Pages;

[QueryProperty(nameof(InvoiceId), "id")]
public partial class InvoiceDetailPage : ContentPage
{
    private static readonly PaymentMethod[] Methods = { PaymentMethod.Cash, PaymentMethod.Card, PaymentMethod.Upi };

    private readonly ObservableCollection<LineItemVm> _lines = new();
    private List<ServiceDto> _catalogue = new();
    private InvoiceDto? _invoice;
    private Guid _id;

    public string InvoiceId
    {
        set
        {
            if (Guid.TryParse(value, out var g)) { _id = g; _ = LoadAsync(); }
        }
    }

    public InvoiceDetailPage()
    {
        InitializeComponent();
        BindableLayout.SetItemsSource(LinesHost, _lines);
        foreach (var m in Methods) MethodPicker.Items.Add(m.ToString());
        MethodPicker.SelectedIndex = 0;
    }

    private async Task LoadAsync()
    {
        SetBusy(true);
        ErrorLabel.IsVisible = false;
        InfoLabel.IsVisible = false;
        try
        {
            if (_catalogue.Count == 0)
            {
                _catalogue = await AppServices.Api.GetServicesAsync() ?? new();
                ServicePicker.ItemsSource = _catalogue;
            }
            var inv = await AppServices.Api.GetInvoiceAsync(_id);
            if (inv is null) { ShowError("Invoice not found."); return; }
            Bind(inv);
        }
        catch (Exception ex)
        {
            ShowError(ex is ApiException a ? a.Message : "Cannot load the invoice.");
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void Bind(InvoiceDto inv)
    {
        _invoice = inv;

        bool isQuotation = inv.Status is InvoiceStatus.Quotation or InvoiceStatus.InProgress;
        bool canEdit = isQuotation || inv.Status == InvoiceStatus.Invoiced;
        bool canPay = inv.Status != InvoiceStatus.Paid && inv.Status != InvoiceStatus.Cancelled && inv.Balance > 0;

        Title = isQuotation ? "Quotation" : "Invoice";
        TitleLabel.Text = $"{(isQuotation ? "Quotation" : "Invoice")} {inv.InvoiceNumber ?? "(draft)"}";
        CustomerLabel.Text = $"{inv.CustomerName}  •  {inv.CustomerPhone}";
        CarLabel.Text = string.IsNullOrWhiteSpace(inv.CarModel) ? inv.CarNumber : $"{inv.CarNumber}  ({inv.CarModel})";
        StatusLabel.Text = inv.Status.ToString();

        DiscountEntry.Text = inv.DiscountAmount.ToString("0.##");
        NotesEditor.Text = inv.Notes ?? "";
        GstSwitch.IsToggled = inv.IsGstApplicable;

        foreach (var l in _lines) l.Changed -= Recalc;
        _lines.Clear();
        foreach (var i in inv.Items)
        {
            var line = new LineItemVm
            {
                ServiceId = i.ServiceId,
                ProductId = i.ProductId,
                Description = i.Description,
                Quantity = i.Quantity,
                UnitPrice = i.UnitPrice,
                DiscountAmount = i.DiscountAmount,
                GstRate = i.GstRate
            };
            line.Changed += Recalc;
            _lines.Add(line);
        }

        // Show edit controls only when the bill can still be edited.
        AddRow.IsVisible = canEdit;
        DiscountRow.IsEnabled = canEdit;
        NotesEditor.IsEnabled = canEdit;
        GstRow.IsEnabled = canEdit;
        SaveButton.IsVisible = canEdit;
        FinaliseButton.IsVisible = isQuotation;
        PaymentPanel.IsVisible = canPay;
        AmountEntry.Text = inv.Balance.ToString("0.##");

        Recalc();
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
        Recalc();
    }

    private void OnRemoveLineClicked(object? sender, EventArgs e)
    {
        if (sender is Button { BindingContext: LineItemVm line })
        {
            line.Changed -= Recalc;
            _lines.Remove(line);
            Recalc();
        }
    }

    private void OnTotalsInputChanged(object? sender, TextChangedEventArgs e) => Recalc();
    private void OnGstToggled(object? sender, ToggledEventArgs e) => Recalc();

    private void Recalc()
    {
        var subTotal = _lines.Sum(l => l.Quantity * l.UnitPrice);
        var lineDiscounts = _lines.Sum(l => l.DiscountAmount);
        var headerDiscount = ParseDecimal(DiscountEntry.Text);
        var tax = GstSwitch.IsToggled ? _lines.Sum(l => l.TaxAmount) : 0m;
        var grand = Math.Max(0, subTotal - lineDiscounts - headerDiscount + tax);

        SubTotalLabel.Text = $"₹{subTotal:N0}";
        TaxLabel.Text = $"₹{tax:N0}";
        GrandLabel.Text = $"₹{grand:N0}";
        BalanceLabel.Text = $"₹{(_invoice?.Balance ?? 0):N0}";
    }

    private async Task<bool> SaveChangesAsync()
    {
        if (_invoice is null) return false;
        var req = new UpdateInvoiceRequest(
            ParseDecimal(DiscountEntry.Text),
            string.IsNullOrWhiteSpace(NotesEditor.Text) ? null : NotesEditor.Text.Trim(),
            _lines.Select(l => new InvoiceItemInput(
                l.ServiceId, l.ProductId, l.Description, l.Quantity, l.UnitPrice, l.DiscountAmount)).ToList(),
            GstSwitch.IsToggled);
        var updated = await AppServices.Api.UpdateInvoiceAsync(_invoice.Id, req);
        Bind(updated);
        return true;
    }

    private async void OnSaveClicked(object? sender, EventArgs e)
    {
        SetBusy(true);
        try
        {
            await SaveChangesAsync();
            ShowInfo("Changes saved.");
        }
        catch (Exception ex) { ShowError(ex is ApiException a ? a.Message : "Could not save."); }
        finally { SetBusy(false); }
    }

    private async void OnFinaliseClicked(object? sender, EventArgs e)
    {
        if (_invoice is null) return;
        bool ok = await DisplayAlertAsync("Generate invoice",
            "This assigns an invoice number and deducts stock. Continue?", "Generate", "Cancel");
        if (!ok) return;

        SetBusy(true);
        try
        {
            await SaveChangesAsync();                      // persist edits first
            var finalised = await AppServices.Api.FinaliseAsync(_invoice!.Id);
            Bind(finalised);
            ShowInfo($"Invoice {finalised.InvoiceNumber} generated. Inventory updated.");
        }
        catch (Exception ex) { ShowError(ex is ApiException a ? a.Message : "Could not finalise."); }
        finally { SetBusy(false); }
    }

    private async void OnPayClicked(object? sender, EventArgs e)
    {
        if (_invoice is null) return;
        var amount = ParseDecimal(AmountEntry.Text);
        if (amount <= 0) { ShowError("Enter a payment amount."); return; }

        SetBusy(true);
        try
        {
            var method = Methods[Math.Max(0, MethodPicker.SelectedIndex)];
            var req = new RecordPaymentRequest(method, amount,
                string.IsNullOrWhiteSpace(ReferenceEntry.Text) ? null : ReferenceEntry.Text.Trim());
            var updated = await AppServices.Api.PayAsync(_invoice.Id, req);
            ReferenceEntry.Text = "";
            Bind(updated);
            ShowInfo(updated.Status == InvoiceStatus.Paid
                ? "Paid in full. WhatsApp thank-you sent to the customer."
                : $"Payment recorded. Balance ₹{updated.Balance:N0}.");
        }
        catch (Exception ex) { ShowError(ex is ApiException a ? a.Message : "Could not record payment."); }
        finally { SetBusy(false); }
    }

    private async void OnPdfClicked(object? sender, EventArgs e)
    {
        if (_invoice is null) return;
        SetBusy(true);
        try
        {
            var bytes = await AppServices.Api.GetInvoicePdfAsync(_invoice.Id);
            var name = (_invoice.InvoiceNumber ?? _invoice.Id.ToString()).Replace("/", "-");
            var path = Path.Combine(FileSystem.CacheDirectory, $"E6_{name}.pdf");
            await File.WriteAllBytesAsync(path, bytes);
            await Launcher.Default.OpenAsync(new OpenFileRequest("Invoice", new ReadOnlyFile(path)));
        }
        catch (Exception ex) { ShowError(ex is ApiException a ? a.Message : "Could not open the PDF."); }
        finally { SetBusy(false); }
    }

    private static decimal ParseDecimal(string? s) => decimal.TryParse(s, out var v) ? v : 0m;

    private void ShowError(string message)
    {
        ErrorLabel.Text = message; ErrorLabel.IsVisible = true; InfoLabel.IsVisible = false;
    }

    private void ShowInfo(string message)
    {
        InfoLabel.Text = message; InfoLabel.IsVisible = true; ErrorLabel.IsVisible = false;
    }

    private void SetBusy(bool busy)
    {
        Busy.IsRunning = busy; Busy.IsVisible = busy;
    }
}
