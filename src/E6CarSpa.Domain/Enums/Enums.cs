namespace E6CarSpa.Domain.Enums;

/// <summary>Application roles that gate what a logged-in user may do.</summary>
public enum UserRole
{
    /// <summary>Full access: settings, users, pricing, inventory, reports.</summary>
    Admin = 0,

    /// <summary>Day-to-day management: billing, inventory, reports — but not user/settings administration.</summary>
    Manager = 1,

    /// <summary>Shop-floor billing only: create job cards, quotations, invoices, take payments.</summary>
    Worker = 2
}

/// <summary>
/// Lifecycle of a bill. A Quotation and an Invoice are the SAME record at different stages.
/// </summary>
public enum InvoiceStatus
{
    /// <summary>Estimate given when the car arrives, before/while work is done.</summary>
    Quotation = 0,

    /// <summary>Work is underway.</summary>
    InProgress = 1,

    /// <summary>Finalised and printed as a tax invoice; awaiting payment.</summary>
    Invoiced = 2,

    /// <summary>Fully paid.</summary>
    Paid = 3,

    /// <summary>Voided.</summary>
    Cancelled = 4
}

/// <summary>How the customer settled the bill.</summary>
public enum PaymentMethod
{
    Cash = 0,
    Card = 1,
    Upi = 2
}

/// <summary>Why a product's stock level changed. Drives the sign of the quantity.</summary>
public enum StockMovementType
{
    /// <summary>Stock received from a supplier (increases stock).</summary>
    Purchase = 0,

    /// <summary>Stock used up performing a service (decreases stock).</summary>
    Consumption = 1,

    /// <summary>Manual correction after a stock-take (can be + or -).</summary>
    Adjustment = 2,

    /// <summary>Stock returned to a supplier (decreases stock).</summary>
    ReturnToSupplier = 3
}

/// <summary>Delivery state of the post-payment WhatsApp message.</summary>
public enum NotificationStatus
{
    Pending = 0,
    Sent = 1,
    Failed = 2
}
