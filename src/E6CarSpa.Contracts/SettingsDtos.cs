using E6CarSpa.Domain.Enums;

namespace E6CarSpa.Contracts;

public record CompanySettingsDto(
    string Name, string? AddressLine1, string? AddressLine2, string? City, string? State,
    string? StateCode, string? Pincode, string? Phone, string? Email, string? Gstin,
    string InvoicePrefix, long LastInvoiceSequence,
    string NonGstInvoicePrefix, long LastNonGstSequence, int LastNonGstYear,
    decimal DefaultGstRate, bool HasLogo);

/// <summary>Upload a company logo (PNG/JPG) as a base64 string; replaces any existing logo.</summary>
public record UploadLogoRequest(string Base64);

public record SaveCompanySettingsRequest(
    string Name, string? AddressLine1, string? AddressLine2, string? City, string? State,
    string? StateCode, string? Pincode, string? Phone, string? Email, string? Gstin,
    string InvoicePrefix, string NonGstInvoicePrefix, decimal DefaultGstRate);

/// <summary>Owner dashboard summary for a given day.</summary>
public record DashboardSummaryDto(
    DateTime Date,
    int JobsToday, int QuotationsPending, int InvoicesUnpaid,
    decimal CollectedToday, decimal CashToday, decimal CardToday, decimal UpiToday,
    decimal IncomeToday,
    int LowStockCount,
    List<LiveJobDto> ActiveJobs);

public record LiveJobDto(string InvoiceNumber, string VehicleNumber, string VehicleModel, InvoiceStatus Status, decimal Total);

/// <summary>A recorded cash advance given to a worker.</summary>
/// <param name="DeletedAt">Set when the entry has been marked obsolete; it is kept for the audit
/// trail rather than erased, and is excluded from the per-worker totals.</param>
/// <param name="DeletedBy">Username of whoever marked it obsolete.</param>
public record StaffAdvanceDto(
    Guid Id, Guid StaffId, string StaffName, decimal Amount, DateTime AdvanceDate, string? Note,
    DateTime? DeletedAt = null, string? DeletedBy = null)
{
    public bool IsDeleted => DeletedAt is not null;

    /// <summary>Ready-made caption for the clients, e.g. "Deleted by gokul on 30-07-2026".</summary>
    public string DeletedCaption =>
        DeletedAt is null ? "" : $"Deleted by {DeletedBy ?? "unknown"} on {DeletedAt:dd-MM-yyyy}";
}

public record SaveStaffAdvanceRequest(Guid StaffId, decimal Amount, DateTime AdvanceDate, string? Note);

/// <summary>Total advanced per worker, for the summary panel.</summary>
public record StaffAdvanceSummaryDto(Guid StaffId, string StaffName, decimal TotalAdvanced, int Count);

// ----- Staff master -----

/// <summary>A floor worker whose name appears on cash-advance records.</summary>
public record StaffDto(Guid Id, string FullName, bool IsActive);

public record SaveStaffRequest(string FullName);

// ----- Income -----

/// <summary>A non-invoice income entry (tips, miscellaneous, part sale, etc.).</summary>
public record IncomeDto(
    Guid Id, string Source, decimal Amount, DateTime IncomeDate, string? Note,
    DateTime? DeletedAt = null, string? DeletedBy = null)
{
    public bool IsDeleted => DeletedAt is not null;
    public string DeletedCaption =>
        DeletedAt is null ? "" : $"Deleted by {DeletedBy ?? "unknown"} on {DeletedAt:dd-MM-yyyy}";
}

public record SaveIncomeRequest(string Source, decimal Amount, DateTime IncomeDate, string? Note);
public record IncomeSummaryDto(string Source, decimal TotalAmount, int Count);

// ----- Staff Salary -----

/// <summary>A salary payment to a floor worker.</summary>
public record StaffSalaryDto(
    Guid Id, Guid StaffId, string StaffName, decimal Amount, DateTime SalaryDate, string? Note,
    DateTime? DeletedAt = null, string? DeletedBy = null)
{
    public bool IsDeleted => DeletedAt is not null;
    public string DeletedCaption =>
        DeletedAt is null ? "" : $"Deleted by {DeletedBy ?? "unknown"} on {DeletedAt:dd-MM-yyyy}";
}

public record SaveStaffSalaryRequest(Guid StaffId, decimal Amount, DateTime SalaryDate, string? Note);
public record StaffSalarySummaryDto(Guid StaffId, string StaffName, decimal TotalPaid, int Count);
