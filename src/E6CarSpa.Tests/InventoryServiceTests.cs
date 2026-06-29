using E6CarSpa.Api.Services;
using E6CarSpa.Contracts;
using E6CarSpa.Domain.Entities;
using E6CarSpa.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace E6CarSpa.Tests;

public class InventoryServiceTests
{
    private static async Task<(Domain.Entities.Product product, Infrastructure.Data.AppDbContext db)> SeedProductAsync(
        decimal stock = 10m, decimal reorder = 3m, decimal unitCost = 50m)
    {
        var db = TestDb.Create();
        var product = new Product
        {
            Name = "Ceramic Coating", Category = "Coating",
            StockQuantity = stock, ReorderLevel = reorder, UnitCost = unitCost
        };
        db.Products.Add(product);
        await db.SaveChangesAsync();
        return (product, db);
    }

    [Fact]
    public async Task ReceivePurchase_IncreasesStock_AndLogsMovement()
    {
        var (product, db) = await SeedProductAsync(stock: 10m, unitCost: 50m);
        var service = new InventoryService(db);

        var updated = await service.ReceivePurchaseAsync(
            new StockPurchaseRequest(product.Id, Quantity: 5m, UnitCost: 60m, Reference: "PO-1", Note: "restock"),
            userId: null);

        Assert.Equal(15m, updated.StockQuantity);
        Assert.Equal(60m, updated.UnitCost); // latest cost refreshed
        var movement = await db.StockMovements.SingleAsync();
        Assert.Equal(StockMovementType.Purchase, movement.Type);
        Assert.Equal(5m, movement.Quantity);
    }

    [Fact]
    public async Task ReceivePurchase_NonPositiveQuantity_Throws()
    {
        var (product, db) = await SeedProductAsync();
        var service = new InventoryService(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ReceivePurchaseAsync(
                new StockPurchaseRequest(product.Id, Quantity: 0m, UnitCost: 10m, Reference: null, Note: null),
                userId: null));
    }

    [Fact]
    public async Task ReceivePurchase_ZeroUnitCost_KeepsExistingCost()
    {
        var (product, db) = await SeedProductAsync(unitCost: 50m);
        var service = new InventoryService(db);

        var updated = await service.ReceivePurchaseAsync(
            new StockPurchaseRequest(product.Id, Quantity: 2m, UnitCost: 0m, Reference: null, Note: null),
            userId: null);

        Assert.Equal(50m, updated.UnitCost);
    }

    [Fact]
    public async Task Adjust_NegativeDelta_ReducesStock_AndLogsMovement()
    {
        var (product, db) = await SeedProductAsync(stock: 10m);
        var service = new InventoryService(db);

        var updated = await service.AdjustAsync(
            new StockAdjustmentRequest(product.Id, Delta: -4m, Note: "stock-take"), userId: null);

        Assert.Equal(6m, updated.StockQuantity);
        var movement = await db.StockMovements.SingleAsync();
        Assert.Equal(StockMovementType.Adjustment, movement.Type);
        Assert.Equal(-4m, movement.Quantity);
    }

    [Fact]
    public async Task Operations_OnMissingProduct_Throw()
    {
        var db = TestDb.Create();
        var service = new InventoryService(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.AdjustAsync(new StockAdjustmentRequest(Guid.NewGuid(), -1m, null), null));
    }

    [Fact]
    public async Task ListProducts_LowStockOnly_FiltersToReorderLevel()
    {
        var db = TestDb.Create();
        db.Products.AddRange(
            new Product { Name = "Healthy", Category = "A", StockQuantity = 10m, ReorderLevel = 3m },
            new Product { Name = "Low", Category = "A", StockQuantity = 2m, ReorderLevel = 3m },
            new Product { Name = "AtThreshold", Category = "A", StockQuantity = 3m, ReorderLevel = 3m });
        await db.SaveChangesAsync();
        var service = new InventoryService(db);

        var low = await service.ListProductsAsync(lowStockOnly: true);

        Assert.Equal(2, low.Count); // "Low" and "AtThreshold" (<=)
        Assert.DoesNotContain(low, p => p.Name == "Healthy");
    }
}
