using E6CarSpa.Domain.Enums;

namespace E6CarSpa.Contracts;

public record InvoiceItemDto(
    Guid Id, Guid? ServiceId, Guid? ProductId, string Description, string HsnSac,
    decimal Quantity, decimal UnitPrice, decimal DiscountAmount, decimal GstRate,
    decimal TaxableValue, decimal CgstAmount, decimal SgstAmount, decimal IgstAmount, decimal LineTotal);

public record PaymentDto(Guid Id, PaymentMethod Method, decimal Amount, string? Reference, DateTime PaidAt);

public record InvoiceDto(
    Guid Id,
    string? InvoiceNumber,
    InvoiceStatus Status,
    Guid CustomerId, string CustomerName, string CustomerPhone,
    Guid VehicleId, string CarNumber, string CarModel,
    decimal SubTotal, decimal DiscountAmount, decimal TaxableValue,
    decimal CgstAmount, decimal SgstAmount, decimal IgstAmount, decimal TotalTax,
    decimal GrandTotal, decimal AmountPaid, decimal Balance,
    string? Notes, DateTime CreatedAt, DateTime? CompletedAt,
    List<InvoiceItemDto> Items, List<PaymentDto> Payments, bool IsGstApplicable);

/// <summary>A line to add to an invoice. Provide a ServiceId or ProductId, or a free-text description.</summary>
public record InvoiceItemInput(
    Guid? ServiceId, Guid? ProductId, string? Description,
    decimal Quantity, decimal? UnitPriceOverride, decimal DiscountAmount);

/// <summary>Steps 1-3: create the quotation (customer, car, chosen services). ApplyGst=false makes a non-GST bill.</summary>
public record CreateQuotationRequest(
    string CustomerName, string CustomerPhone, string CarNumber, string CarModel,
    decimal DiscountAmount, string? Notes, List<InvoiceItemInput> Items, bool ApplyGst = true);

/// <summary>Step 4: edit a quotation/invoice — add jobs done, change discount/notes, toggle GST.</summary>
public record UpdateInvoiceRequest(decimal DiscountAmount, string? Notes, List<InvoiceItemInput> Items, bool ApplyGst = true);

/// <summary>Step 5: record a payment; triggers the WhatsApp thank-you when fully paid.</summary>
public record RecordPaymentRequest(PaymentMethod Method, decimal Amount, string? Reference);

/// <summary>Lightweight row for the jobs/quotations list view.</summary>
public record InvoiceListItemDto(
    Guid Id, string? InvoiceNumber, InvoiceStatus Status,
    string CustomerName, string CarNumber, string CarModel, decimal GrandTotal, decimal Balance, DateTime CreatedAt);
