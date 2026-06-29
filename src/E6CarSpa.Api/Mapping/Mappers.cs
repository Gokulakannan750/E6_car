using E6CarSpa.Contracts;
using E6CarSpa.Domain.Entities;

namespace E6CarSpa.Api.Mapping;

/// <summary>Hand-written entity → DTO projections. Kept explicit for clarity over a mapper library.</summary>
public static class Mappers
{
    public static UserDto ToDto(this User u) => new(u.Id, u.FullName, u.Username, u.Role, u.IsActive);

    public static VehicleDto ToDto(this Vehicle v) => new(v.Id, v.CarNumber, v.CarModel);

    public static CustomerDto ToDto(this Customer c) =>
        new(c.Id, c.Name, c.Phone, c.Vehicles.Select(v => v.ToDto()).ToList(), c.CreatedAt);

    public static ServiceDto ToDto(this Service s) =>
        new(s.Id, s.Name, s.Category, s.DefaultPrice, s.HsnSac, s.GstRate, s.IsActive);

    public static ProductDto ToDto(this Product p) =>
        new(p.Id, p.Name, p.Category, p.StockQuantity, p.ReorderLevel, p.UnitCost,
            p.HsnSac, p.GstRate, p.IsActive, p.StockQuantity <= p.ReorderLevel);

    public static StockMovementDto ToDto(this StockMovement m) =>
        new(m.Id, m.ProductId, m.Product?.Name ?? "", m.Type, m.Quantity, m.UnitCost, m.Reference, m.Note, m.CreatedAt);

    public static InvoiceItemDto ToDto(this InvoiceItem i) =>
        new(i.Id, i.ServiceId, i.ProductId, i.Description, i.HsnSac, i.Quantity, i.UnitPrice,
            i.DiscountAmount, i.GstRate, i.TaxableValue, i.CgstAmount, i.SgstAmount, i.IgstAmount, i.LineTotal);

    public static PaymentDto ToDto(this Payment p) =>
        new(p.Id, p.Method, p.Amount, p.Reference, p.PaidAt);

    public static InvoiceDto ToDto(this Invoice inv) =>
        new(inv.Id, inv.InvoiceNumber, inv.Status,
            inv.CustomerId, inv.Customer?.Name ?? "", inv.Customer?.Phone ?? "",
            inv.VehicleId, inv.Vehicle?.CarNumber ?? "", inv.Vehicle?.CarModel ?? "",
            inv.SubTotal, inv.DiscountAmount, inv.TaxableValue,
            inv.CgstAmount, inv.SgstAmount, inv.IgstAmount, inv.TotalTax,
            inv.GrandTotal, inv.AmountPaid, inv.Balance,
            inv.Notes, inv.CreatedAt, inv.CompletedAt,
            inv.Items.Select(x => x.ToDto()).ToList(),
            inv.Payments.Select(x => x.ToDto()).ToList(),
            inv.IsGstApplicable);

    public static InvoiceListItemDto ToListItem(this Invoice inv) =>
        new(inv.Id, inv.InvoiceNumber, inv.Status,
            inv.Customer?.Name ?? "", inv.Vehicle?.CarNumber ?? "", inv.Vehicle?.CarModel ?? "",
            inv.GrandTotal, inv.Balance, inv.CreatedAt);
}
