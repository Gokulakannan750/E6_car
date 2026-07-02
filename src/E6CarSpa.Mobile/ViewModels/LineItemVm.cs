using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace E6CarSpa.Mobile.ViewModels;

/// <summary>
/// An editable invoice line shown in the billing list. Tax math is a 1:1 port of the desktop
/// LineItemVm so the on-device preview matches what the server computes.
/// </summary>
public class LineItemVm : INotifyPropertyChanged
{
    public Guid? ServiceId { get; init; }
    public Guid? ProductId { get; init; }
    public string Description { get; set; } = "";
    public decimal GstRate { get; set; } = 18m;
    public bool IsEditable { get; set; } = true;

    private decimal _quantity = 1m;
    public decimal Quantity { get => _quantity; set { if (Set(ref _quantity, value)) Recalc(); } }

    private decimal _unitPrice;
    public decimal UnitPrice { get => _unitPrice; set { if (Set(ref _unitPrice, value)) Recalc(); } }

    private decimal _discountAmount;
    public decimal DiscountAmount { get => _discountAmount; set { if (Set(ref _discountAmount, value)) Recalc(); } }

    // Per-line amount EXCLUDING tax — GST is charged once on the whole invoice (see
    // GstCalculator), so lines never show their own tax.
    public decimal Taxable => Math.Max(0, (Quantity * UnitPrice) - DiscountAmount);

    /// <summary>Raised so the parent page can recompute the invoice totals.</summary>
    public event Action? Changed;
    public event PropertyChangedEventHandler? PropertyChanged;

    private void Recalc()
    {
        OnPropertyChanged(nameof(Taxable));
        Changed?.Invoke();
    }

    private bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(name);
        return true;
    }

    private void OnPropertyChanged(string? name) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
