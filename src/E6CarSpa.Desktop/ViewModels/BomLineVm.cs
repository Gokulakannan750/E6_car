using CommunityToolkit.Mvvm.ComponentModel;

namespace E6CarSpa.Desktop.ViewModels;

/// <summary>An editable bill-of-materials line: a product and how much of it a service consumes.</summary>
public partial class BomLineVm : ObservableObject
{
    public Guid ProductId { get; init; }
    public string ProductName { get; init; } = "";
    [ObservableProperty] private decimal _quantity;
}
