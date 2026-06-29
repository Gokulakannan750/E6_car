using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using E6CarSpa.Contracts;
using E6CarSpa.Domain.Enums;

namespace E6CarSpa.Desktop.Services;

public interface IApiClient
{
    UserDto? CurrentUser { get; }
    bool IsLoggedIn { get; }
    event Action? OnUnauthorized;

    Task<UserDto> LoginAsync(string username, string password);
    Task<CustomerLookupResult?> LookupByPhoneAsync(string phone);
    Task<CustomerLookupResult?> LookupByCarAsync(string car);
    Task<List<CustomerDto>?> GetCustomersAsync(string? search = null);
    Task<List<ServiceDto>?> GetServicesAsync(bool includeInactive = false);
    Task<ServiceDto> CreateServiceAsync(SaveServiceRequest req);
    Task<ServiceDto> UpdateServiceAsync(Guid id, SaveServiceRequest req);
    Task<ProductDto> CreateProductAsync(SaveProductRequest req);
    Task<ProductDto> UpdateProductAsync(Guid id, SaveProductRequest req);
    Task<List<BomItemDto>?> GetBomAsync(Guid serviceId);
    Task<List<BomItemDto>> SaveBomAsync(Guid serviceId, SaveBomRequest req);
    Task<InvoiceDto> CreateQuotationAsync(CreateQuotationRequest req);
    Task<List<InvoiceListItemDto>?> ListInvoicesAsync(InvoiceStatus? status, string? search);
    Task<InvoiceDto?> GetInvoiceAsync(Guid id);
    Task<InvoiceDto> UpdateInvoiceAsync(Guid id, UpdateInvoiceRequest req);
    Task<InvoiceDto> FinaliseAsync(Guid id);
    Task<InvoiceDto> PayAsync(Guid id, RecordPaymentRequest req);
    Task<byte[]> GetInvoicePdfAsync(Guid id);
    Task<List<ProductDto>?> GetProductsAsync(bool lowStockOnly = false);
    Task<ProductDto> PurchaseAsync(StockPurchaseRequest req);
    Task<ProductDto> AdjustAsync(StockAdjustmentRequest req);
    Task<DashboardSummaryDto?> GetDashboardAsync();
    Task<List<UserDto>?> GetUsersAsync();
    Task<UserDto> CreateUserAsync(CreateUserRequest req);
    Task<UserDto> UpdateUserAsync(Guid id, UpdateUserRequest req);
    Task<CompanySettingsDto?> GetSettingsAsync();
    Task<CompanySettingsDto> UpdateSettingsAsync(SaveCompanySettingsRequest req);
    Task UploadLogoAsync(byte[] imageBytes);
    Task<byte[]?> GetLogoAsync();
    Task DeleteLogoAsync();
    Task<SalesReportDto?> GetSalesReportAsync(DateTime from, DateTime to);
    Task<GstSummaryDto?> GetGstSummaryAsync(DateTime from, DateTime to);
    Task<CustomerHistoryDto?> GetCustomerHistoryAsync(string phone);
}
