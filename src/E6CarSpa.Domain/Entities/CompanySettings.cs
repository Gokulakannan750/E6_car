namespace E6CarSpa.Domain.Entities;

/// <summary>
/// Single-row table holding the spa's own details for invoice headers, GST and the
/// sequential invoice-number generator.
/// </summary>
public class CompanySettings : BaseEntity
{
    public string Name { get; set; } = "E6 Car Spa";
    public string? AddressLine1 { get; set; }
    public string? AddressLine2 { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }

    /// <summary>GST state code (e.g. "33" for Tamil Nadu) — drives CGST/SGST vs IGST.</summary>
    public string? StateCode { get; set; }
    public string? Pincode { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }

    /// <summary>The spa's GSTIN printed on tax invoices.</summary>
    public string? Gstin { get; set; }

    public string? LogoPath { get; set; }

    /// <summary>The company logo image (PNG/JPG) stored in the DB so it prints on invoices
    /// regardless of which machine runs the API, and survives reinstalls.</summary>
    public byte[]? LogoBytes { get; set; }

    /// <summary>Prefix for invoice numbers, e.g. "E6/". The numeric part never resets yearly.</summary>
    public string InvoicePrefix { get; set; } = "E6/";

    /// <summary>Last issued sequence number; the next invoice gets this + 1.</summary>
    public long LastInvoiceSequence { get; set; } = 0;

    /// <summary>Default GST rate applied to new services/products.</summary>
    public decimal DefaultGstRate { get; set; } = 18m;
}
