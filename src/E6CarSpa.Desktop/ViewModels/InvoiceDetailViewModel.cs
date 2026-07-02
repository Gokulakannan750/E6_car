using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using E6CarSpa.Contracts;
using E6CarSpa.Client;
using E6CarSpa.Domain.Enums;

namespace E6CarSpa.Desktop.ViewModels;

/// <summary>
/// Workflow steps 4-5: open a saved quotation, add jobs done, finalise to an invoice,
/// print it, and take payment (which triggers the WhatsApp thank-you).
/// </summary>
public partial class InvoiceDetailViewModel(IApiClient api) : ObservableObject
{
    [ObservableProperty] private InvoiceDto? _invoice;
    public ObservableCollection<LineItemVm> Lines { get; } = new();
    public ObservableCollection<ServiceDto> Catalogue { get; } = new();
    [ObservableProperty] private ServiceDto? _selectedService;

    [ObservableProperty] private decimal _discountAmount;
    [ObservableProperty] private string _notes = "";

    /// <summary>When off, this is a non-GST bill (no tax charged).</summary>
    [ObservableProperty] private bool _applyGst = true;

    public List<PaymentMethod> PaymentMethods { get; } = [PaymentMethod.Cash, PaymentMethod.Card, PaymentMethod.Upi];
    [ObservableProperty] private PaymentMethod _selectedPaymentMethod = PaymentMethod.Cash;
    [ObservableProperty] private decimal _paymentAmount;
    [ObservableProperty] private string _paymentReference = "";

    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _error = "";
    [ObservableProperty] private string _info = "";

    // Derived UI state
    public bool CanEdit => Invoice is { Status: InvoiceStatus.Quotation or InvoiceStatus.InProgress or InvoiceStatus.Invoiced };
    public bool IsQuotation => Invoice is { Status: InvoiceStatus.Quotation or InvoiceStatus.InProgress };
    public bool CanPay => Invoice is not null && Invoice.Status != InvoiceStatus.Paid
                          && Invoice.Status != InvoiceStatus.Cancelled && Invoice.Balance > 0;
    public string Title => Invoice is null ? "" :
        $"{(Invoice.Status == InvoiceStatus.Quotation ? "Quotation" : "Invoice")} {Invoice.InvoiceNumber ?? "(draft)"}";

    // Mirrors GstCalculator.Recalculate: discount (line + header) comes off the subtotal
    // first, then GST is charged ONCE on that final taxable amount — not summed per line —
    // since the shop uses a single GST rate for every service.
    public decimal SubTotal => Lines.Sum(l => l.Quantity * l.UnitPrice);
    public decimal DiscountTotal => Lines.Sum(l => l.DiscountAmount) + DiscountAmount;
    public decimal TaxableTotal => Math.Max(0m, Math.Round(SubTotal - DiscountTotal, 2, MidpointRounding.AwayFromZero));
    public decimal TotalTax => ApplyGst
        ? Math.Round(TaxableTotal * (Lines.Count > 0 ? Lines[0].GstRate : 0m) / 100m, 2, MidpointRounding.AwayFromZero)
        : 0m;
    public decimal GrandTotal => Math.Round(TaxableTotal + TotalTax, 2, MidpointRounding.AwayFromZero);

    public async Task LoadAsync(Guid id)
    {
        try
        {
            IsBusy = true; Error = ""; Info = "";
            if (Catalogue.Count == 0)
                foreach (var s in await api.GetServicesAsync() ?? new()) Catalogue.Add(s);

            var inv = await api.GetInvoiceAsync(id);
            if (inv is null) { Error = "Invoice not found."; return; }
            BindInvoice(inv);
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsBusy = false; }
    }

    private void BindInvoice(InvoiceDto inv)
    {
        Invoice = inv;
        DiscountAmount = inv.DiscountAmount;
        Notes = inv.Notes ?? "";
        ApplyGst = inv.IsGstApplicable;
        PaymentAmount = inv.Balance;

        foreach (var l in Lines) l.Changed -= RecalcTotals;
        Lines.Clear();
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
            line.Changed += RecalcTotals;
            Lines.Add(line);
        }
        RaiseAll();
    }

    [RelayCommand]
    private void AddService()
    {
        if (SelectedService is null) return;
        var s = SelectedService;
        var line = new LineItemVm
        {
            ServiceId = s.Id, Description = s.Name, Quantity = 1m,
            UnitPrice = s.DefaultPrice, GstRate = s.GstRate
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

    [RelayCommand]
    private async Task SaveChangesAsync()
    {
        if (Invoice is null) return;
        try
        {
            IsBusy = true; Error = ""; Info = "";
            var req = new UpdateInvoiceRequest(
                DiscountAmount, string.IsNullOrWhiteSpace(Notes) ? null : Notes.Trim(),
                Lines.Select(l => new InvoiceItemInput(
                    l.ServiceId, l.ProductId, l.Description, l.Quantity, l.UnitPrice, l.DiscountAmount)).ToList(),
                ApplyGst);
            var updated = await api.UpdateInvoiceAsync(Invoice.Id, req);
            BindInvoice(updated);
            Info = "Changes saved.";
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task FinaliseAsync()
    {
        if (Invoice is null) return;
        try
        {
            IsBusy = true; Error = ""; Info = "";
            // Persist any edits first, then finalise.
            await SaveChangesAsync();
            var finalised = await api.FinaliseAsync(Invoice.Id);
            BindInvoice(finalised);
            Info = $"Invoice {finalised.InvoiceNumber} generated.";
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task PrintAsync()
    {
        if (Invoice is null) return;
        try
        {
            IsBusy = true; Error = "";
            var bytes = await api.GetInvoicePdfAsync(Invoice.Id);
            var file = Path.Combine(Path.GetTempPath(),
                $"E6_{(Invoice.InvoiceNumber ?? Invoice.Id.ToString()).Replace("/", "-")}.pdf");
            await File.WriteAllBytesAsync(file, bytes);
            Process.Start(new ProcessStartInfo(file) { UseShellExecute = true });
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task RecordPaymentAsync()
    {
        if (Invoice is null) return;
        if (PaymentAmount <= 0) { Error = "Enter a payment amount."; return; }
        try
        {
            IsBusy = true; Error = ""; Info = "";
            var req = new RecordPaymentRequest(SelectedPaymentMethod, PaymentAmount,
                string.IsNullOrWhiteSpace(PaymentReference) ? null : PaymentReference.Trim());
            var updated = await api.PayAsync(Invoice.Id, req);
            BindInvoice(updated);
            PaymentReference = "";
            Info = updated.Status == InvoiceStatus.Paid
                ? "Payment received in full. WhatsApp thank-you sent to the customer."
                : $"Payment recorded. Balance ₹{updated.Balance:0.00}.";
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsBusy = false; }
    }

    private void RecalcTotals() => RaiseAll();

    private void RaiseAll()
    {
        OnPropertyChanged(nameof(SubTotal));
        OnPropertyChanged(nameof(DiscountTotal));
        OnPropertyChanged(nameof(TaxableTotal));
        OnPropertyChanged(nameof(TotalTax));
        OnPropertyChanged(nameof(GrandTotal));
        OnPropertyChanged(nameof(CanEdit));
        OnPropertyChanged(nameof(IsQuotation));
        OnPropertyChanged(nameof(CanPay));
        OnPropertyChanged(nameof(Title));
    }
}
