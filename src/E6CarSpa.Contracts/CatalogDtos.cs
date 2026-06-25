using E6CarSpa.Domain.Enums;

namespace E6CarSpa.Contracts;

public record ServiceDto(
    Guid Id, string Name, string Category, decimal DefaultPrice,
    string HsnSac, decimal GstRate, bool IsActive);

public record SaveServiceRequest(
    string Name, string Category, decimal DefaultPrice,
    string HsnSac, decimal GstRate, bool IsActive);

public record ProductDto(
    Guid Id, string Name, string Category,
    decimal StockQuantity, decimal ReorderLevel, decimal UnitCost,
    string HsnSac, decimal GstRate, bool IsActive, bool IsLowStock);

public record SaveProductRequest(
    string Name, string Category,
    decimal ReorderLevel, decimal UnitCost, string HsnSac, decimal GstRate, bool IsActive);

public record StockMovementDto(
    Guid Id, Guid ProductId, string ProductName, StockMovementType Type,
    decimal Quantity, decimal UnitCost, string? Reference, string? Note, DateTime CreatedAt);

/// <summary>Receive stock from a supplier (stock in).</summary>
public record StockPurchaseRequest(Guid ProductId, decimal Quantity, decimal UnitCost, string? Reference, string? Note);

/// <summary>Manual stock correction after a count (delta may be negative).</summary>
public record StockAdjustmentRequest(Guid ProductId, decimal Delta, string? Note);

// ----- Bill of materials (which products a service consumes) -----

public record BomItemDto(Guid ProductId, string ProductName, decimal DefaultQuantity);

public record BomLineInput(Guid ProductId, decimal DefaultQuantity);

/// <summary>Replace a service's entire bill-of-materials with these lines.</summary>
public record SaveBomRequest(List<BomLineInput> Items);
