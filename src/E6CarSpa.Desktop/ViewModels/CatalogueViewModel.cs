using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using E6CarSpa.Contracts;
using E6CarSpa.Desktop.Services;

namespace E6CarSpa.Desktop.ViewModels;

/// <summary>
/// Admin/Manager screen to maintain the catalogue without code: add/edit services and products,
/// and set each service's bill-of-materials (the products it consumes).
/// </summary>
public partial class CatalogueViewModel(ApiClient api) : ObservableObject, IAsyncInitialize
{
    // ----- Services -----
    public ObservableCollection<ServiceDto> Services { get; } = new();
    [ObservableProperty] private ServiceDto? _selectedService;
    [ObservableProperty] private string _svcName = "";
    [ObservableProperty] private string _svcCategory = "";
    [ObservableProperty] private decimal _svcPrice;
    [ObservableProperty] private string _svcHsn = "999719";
    [ObservableProperty] private decimal _svcGst = 18m;
    [ObservableProperty] private bool _svcActive = true;
    public string ServiceFormTitle => SelectedService is null ? "Add Service" : "Edit Service";

    // ----- Products -----
    public ObservableCollection<ProductDto> Products { get; } = new();
    [ObservableProperty] private ProductDto? _selectedProduct;
    [ObservableProperty] private string _prdName = "";
    [ObservableProperty] private string _prdCategory = "";
    [ObservableProperty] private decimal _prdReorder;
    [ObservableProperty] private decimal _prdCost;
    [ObservableProperty] private string _prdHsn = "";
    [ObservableProperty] private decimal _prdGst = 18m;
    [ObservableProperty] private bool _prdActive = true;
    public string ProductFormTitle => SelectedProduct is null ? "Add Product" : "Edit Product";

    // ----- Bill of materials (for the selected service) -----
    public ObservableCollection<BomLineVm> Bom { get; } = new();
    [ObservableProperty] private ProductDto? _bomSelectedProduct;
    [ObservableProperty] private decimal _bomQuantity = 1m;

    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _info = "";
    [ObservableProperty] private string _error = "";

    public async Task InitializeAsync()
    {
        await LoadProductsAsync();
        await LoadServicesAsync();
    }

    private async Task LoadServicesAsync(Guid? selectId = null)
    {
        var list = await api.GetServicesAsync(includeInactive: true) ?? new();
        Services.Clear();
        foreach (var s in list) Services.Add(s);
        if (selectId is Guid id) SelectedService = Services.FirstOrDefault(s => s.Id == id);
    }

    private async Task LoadProductsAsync()
    {
        var list = await api.GetProductsAsync() ?? new();
        Products.Clear();
        foreach (var p in list) Products.Add(p);
    }

    // ---- Services ----
    partial void OnSelectedServiceChanged(ServiceDto? value)
    {
        OnPropertyChanged(nameof(ServiceFormTitle));
        if (value is null) return;
        SvcName = value.Name; SvcCategory = value.Category; SvcPrice = value.DefaultPrice;
        SvcHsn = value.HsnSac; SvcGst = value.GstRate; SvcActive = value.IsActive;
        _ = LoadBomAsync(value.Id);
    }

    [RelayCommand]
    private void NewService()
    {
        SelectedService = null;
        SvcName = ""; SvcCategory = ""; SvcPrice = 0; SvcHsn = "999719"; SvcGst = 18m; SvcActive = true;
        Bom.Clear();
        OnPropertyChanged(nameof(ServiceFormTitle));
    }

    [RelayCommand]
    private async Task SaveServiceAsync()
    {
        if (string.IsNullOrWhiteSpace(SvcName)) { Error = "Service name is required."; return; }
        try
        {
            IsBusy = true; Error = ""; Info = "";
            var req = new SaveServiceRequest(SvcName.Trim(), SvcCategory.Trim(), SvcPrice, SvcHsn.Trim(), SvcGst, SvcActive);
            var saved = SelectedService is null
                ? await api.CreateServiceAsync(req)
                : await api.UpdateServiceAsync(SelectedService.Id, req);
            await LoadServicesAsync(saved.Id);
            Info = "Service saved.";
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsBusy = false; }
    }

    // ---- Products ----
    partial void OnSelectedProductChanged(ProductDto? value)
    {
        OnPropertyChanged(nameof(ProductFormTitle));
        if (value is null) return;
        PrdName = value.Name; PrdCategory = value.Category;
        PrdReorder = value.ReorderLevel; PrdCost = value.UnitCost; PrdHsn = value.HsnSac;
        PrdGst = value.GstRate; PrdActive = value.IsActive;
    }

    [RelayCommand]
    private void NewProduct()
    {
        SelectedProduct = null;
        PrdName = ""; PrdCategory = ""; PrdReorder = 0; PrdCost = 0;
        PrdHsn = ""; PrdGst = 18m; PrdActive = true;
        OnPropertyChanged(nameof(ProductFormTitle));
    }

    [RelayCommand]
    private async Task SaveProductAsync()
    {
        if (string.IsNullOrWhiteSpace(PrdName)) { Error = "Product name is required."; return; }
        try
        {
            IsBusy = true; Error = ""; Info = "";
            var req = new SaveProductRequest(PrdName.Trim(), PrdCategory.Trim(), PrdReorder, PrdCost, PrdHsn.Trim(), PrdGst, PrdActive);
            if (SelectedProduct is null) await api.CreateProductAsync(req);
            else await api.UpdateProductAsync(SelectedProduct.Id, req);
            await LoadProductsAsync();
            Info = "Product saved. (Stock is changed via the Inventory tab.)";
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsBusy = false; }
    }

    // ---- Bill of materials ----
    private async Task LoadBomAsync(Guid serviceId)
    {
        try
        {
            var lines = await api.GetBomAsync(serviceId) ?? new();
            Bom.Clear();
            foreach (var l in lines)
                Bom.Add(new BomLineVm { ProductId = l.ProductId, ProductName = l.ProductName, Quantity = l.DefaultQuantity });
        }
        catch (Exception ex) { Error = ex.Message; }
    }

    [RelayCommand]
    private void AddBomLine()
    {
        if (BomSelectedProduct is null) { Error = "Pick a product to add."; return; }
        if (BomQuantity <= 0) { Error = "Quantity must be greater than zero."; return; }
        var existing = Bom.FirstOrDefault(b => b.ProductId == BomSelectedProduct.Id);
        if (existing is not null) { existing.Quantity = BomQuantity; return; }
        Bom.Add(new BomLineVm
        {
            ProductId = BomSelectedProduct.Id,
            ProductName = BomSelectedProduct.Name,
            Quantity = BomQuantity
        });
    }

    [RelayCommand]
    private void RemoveBomLine(BomLineVm? line)
    {
        if (line is not null) Bom.Remove(line);
    }

    [RelayCommand]
    private async Task SaveBomAsync()
    {
        if (SelectedService is null) { Error = "Select a service first."; return; }
        try
        {
            IsBusy = true; Error = ""; Info = "";
            var req = new SaveBomRequest(Bom.Select(b => new BomLineInput(b.ProductId, b.Quantity)).ToList());
            await api.SaveBomAsync(SelectedService.Id, req);
            Info = $"Bill-of-materials saved for {SelectedService.Name}.";
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsBusy = false; }
    }
}
