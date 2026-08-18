using E6CarSpa.Contracts;
using E6CarSpa.Domain.Enums;

namespace E6CarSpa.Client;

/// <summary>
/// Typed surface of the E6 Car Spa Web API, shared by the desktop (WPF) and mobile (MAUI)
/// clients so the two apps can never drift apart on how they talk to the server.
/// </summary>
public interface IApiClient
{
    UserDto? CurrentUser { get; }
    bool IsLoggedIn { get; }

    /// <summary>
    /// True when the signed-in account is on a machine-generated password. The API refuses every
    /// call except <see cref="ChangeMyPasswordAsync"/> until a new one is set, so the UI must show
    /// a change-password prompt instead of the main app.
    /// </summary>
    bool MustChangePassword { get; }

    event Action? OnUnauthorized;

    /// <summary>Point the client at a different server (used by the mobile Settings screen).</summary>
    void SetBaseUrl(string baseUrl);

    Task<UserDto> LoginAsync(string username, string password);
    void Logout();
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
    Task<InvoiceDto> CancelInvoiceAsync(Guid id);
    Task<InvoiceDto> PayAsync(Guid id, RecordPaymentRequest req);
    Task<byte[]> GetInvoicePdfAsync(Guid id);
    Task<byte[]> GetJobCardPdfAsync(Guid id);
    Task<List<ProductDto>?> GetProductsAsync(bool lowStockOnly = false);
    Task<ProductDto> PurchaseAsync(StockPurchaseRequest req);
    Task<ProductDto> AdjustAsync(StockAdjustmentRequest req);
    Task<DashboardSummaryDto?> GetDashboardAsync();
    Task<List<UserDto>?> GetUsersAsync();
    Task<UserDto> CreateUserAsync(CreateUserRequest req);
    Task<UserDto> UpdateUserAsync(Guid id, UpdateUserRequest req);
    Task ChangeMyPasswordAsync(ChangeMyPasswordRequest req);
    /// <param name="includeDeleted">Also return entries marked obsolete, for the audit trail.</param>
    Task<List<StaffAdvanceDto>?> GetStaffAdvancesAsync(string? worker = null, bool includeDeleted = false);
    Task<List<StaffAdvanceSummaryDto>?> GetStaffAdvanceSummaryAsync();
    Task<StaffAdvanceDto> CreateStaffAdvanceAsync(SaveStaffAdvanceRequest req);
    Task DeleteStaffAdvanceAsync(Guid id);

    // ----- Staff master -----
    Task<List<StaffDto>?> GetStaffAsync(bool includeInactive = false);
    Task<StaffDto> CreateStaffAsync(SaveStaffRequest req);
    Task<StaffDto> UpdateStaffAsync(Guid id, SaveStaffRequest req);
    Task DeleteStaffAsync(Guid id);
    Task RestoreStaffAsync(Guid id);

    // ----- Income -----
    Task<List<IncomeDto>?> GetIncomeAsync(string? source = null, bool includeDeleted = false);
    Task<List<IncomeSummaryDto>?> GetIncomeSummaryAsync(DateTime? from = null, DateTime? to = null);
    Task<IncomeDto> CreateIncomeAsync(SaveIncomeRequest req);
    Task DeleteIncomeAsync(Guid id);

    // ----- Staff Salary -----
    Task<List<StaffSalaryDto>?> GetStaffSalariesAsync(Guid? staffId = null, bool includeDeleted = false);
    Task<List<StaffSalarySummaryDto>?> GetStaffSalarySummaryAsync();
    Task<StaffSalaryDto> CreateStaffSalaryAsync(SaveStaffSalaryRequest req);
    Task DeleteStaffSalaryAsync(Guid id);
    Task<CompanySettingsDto?> GetSettingsAsync();
    Task<CompanySettingsDto> UpdateSettingsAsync(SaveCompanySettingsRequest req);
    Task UploadLogoAsync(byte[] imageBytes);
    Task<byte[]?> GetLogoAsync();
    Task DeleteLogoAsync();
    Task<SalesReportDto?> GetSalesReportAsync(DateTime from, DateTime to);
    Task<GstSummaryDto?> GetGstSummaryAsync(DateTime from, DateTime to);
    Task<CustomerHistoryDto?> GetCustomerHistoryAsync(string phone);
}
