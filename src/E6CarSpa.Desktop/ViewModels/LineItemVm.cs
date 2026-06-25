using CommunityToolkit.Mvvm.ComponentModel;

namespace E6CarSpa.Desktop.ViewModels;

/// <summary>An editable invoice line shown in the billing grid.</summary>
public partial class LineItemVm : ObservableObject
{
    public Guid? ServiceId { get; init; }
    public Guid? ProductId { get; init; }

    [ObservableProperty] private string _description = "";
    [ObservableProperty] private decimal _quantity = 1m;
    [ObservableProperty] private decimal _unitPrice;
    [ObservableProperty] private decimal _discountAmount;
    [ObservableProperty] private decimal _gstRate = 18m;

    public decimal Taxable => Math.Max(0, (Quantity * UnitPrice) - DiscountAmount);
    public decimal TaxAmount => Math.Round(Taxable * GstRate / 100m, 2);
    public decimal LineTotal => Math.Round(Taxable + TaxAmount, 2);

    /// <summary>Raised so the parent VM can recompute invoice totals.</summary>
    public event Action? Changed;

    partial void OnQuantityChanged(decimal value) => Recalc();
    partial void OnUnitPriceChanged(decimal value) => Recalc();
    partial void OnDiscountAmountChanged(decimal value) => Recalc();
    partial void OnGstRateChanged(decimal value) => Recalc();

    private void Recalc()
    {
        OnPropertyChanged(nameof(Taxable));
        OnPropertyChanged(nameof(TaxAmount));
        OnPropertyChanged(nameof(LineTotal));
        Changed?.Invoke();
    }
}
