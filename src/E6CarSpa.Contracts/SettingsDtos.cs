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
    int LowStockCount,
    List<LiveJobDto> ActiveJobs);

public record LiveJobDto(string InvoiceNumber, string VehicleNumber, string VehicleModel, InvoiceStatus Status, decimal Total);
