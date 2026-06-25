using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using E6CarSpa.Contracts;
using E6CarSpa.Desktop.Services;

namespace E6CarSpa.Desktop.ViewModels;

/// <summary>Inventory: view stock, receive purchases, and make manual adjustments.</summary>
public partial class InventoryViewModel(ApiClient api) : ObservableObject, IAsyncInitialize
{
    public ObservableCollection<ProductDto> Products { get; } = new();
    [ObservableProperty] private ProductDto? _selectedProduct;
    [ObservableProperty] private bool _lowStockOnly;

    // Receive stock
    [ObservableProperty] private decimal _purchaseQuantity;
    [ObservableProperty] private decimal _purchaseCost;
    [ObservableProperty] private string _purchaseReference = "";

    // Adjust stock
    [ObservableProperty] private decimal _adjustDelta;
    [ObservableProperty] private string _adjustNote = "";

    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _error = "";
    [ObservableProperty] private string _info = "";

    public Task InitializeAsync() => RefreshAsync();

    partial void OnLowStockOnlyChanged(bool value) => _ = RefreshAsync();

    partial void OnSelectedProductChanged(ProductDto? value)
    {
        if (value is not null) PurchaseCost = value.UnitCost;
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        try
        {
            IsBusy = true; Error = "";
            var list = await api.GetProductsAsync(LowStockOnly) ?? new();
            Products.Clear();
            foreach (var p in list) Products.Add(p);
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task ReceivePurchaseAsync()
    {
        if (SelectedProduct is null) { Error = "Select a product."; return; }
        if (PurchaseQuantity <= 0) { Error = "Enter a quantity greater than zero."; return; }
        try
        {
            IsBusy = true; Error = ""; Info = "";
            await api.PurchaseAsync(new StockPurchaseRequest(
                SelectedProduct.Id, PurchaseQuantity, PurchaseCost,
                string.IsNullOrWhiteSpace(PurchaseReference) ? null : PurchaseReference.Trim(), null));
            Info = $"Received {PurchaseQuantity} into {SelectedProduct.Name}.";
            PurchaseQuantity = 0; PurchaseReference = "";
            await RefreshAsync();
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task AdjustAsync()
    {
        if (SelectedProduct is null) { Error = "Select a product."; return; }
        if (AdjustDelta == 0) { Error = "Enter a non-zero adjustment."; return; }
        try
        {
            IsBusy = true; Error = ""; Info = "";
            await api.AdjustAsync(new StockAdjustmentRequest(
                SelectedProduct.Id, AdjustDelta, string.IsNullOrWhiteSpace(AdjustNote) ? null : AdjustNote.Trim()));
            Info = $"Adjusted {SelectedProduct.Name} by {AdjustDelta}.";
            AdjustDelta = 0; AdjustNote = "";
            await RefreshAsync();
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsBusy = false; }
    }
}
