using E6CarSpa.Contracts;
using E6CarSpa.Domain.Entities;
using E6CarSpa.Domain.Enums;
using E6CarSpa.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace E6CarSpa.Api.Services;

/// <summary>Stock movements: receiving purchases, manual adjustments, and stock queries.</summary>
public class InventoryService(AppDbContext db)
{
 public async Task<Product> ReceivePurchaseAsync(StockPurchaseRequest req, Guid? userId)
 {
 var product = await db.Products.FindAsync(req.ProductId)
 ?? throw new InvalidOperationException("Product not found.");
 if (req.Quantity <= 0) throw new InvalidOperationException("Quantity must be positive.");

 product.StockQuantity += req.Quantity;
 product.UnitCost = req.UnitCost > 0 ? req.UnitCost : product.UnitCost; // refresh latest cost

 db.StockMovements.Add(new StockMovement
 {
 ProductId = product.Id,
 Type = StockMovementType.Purchase,
 Quantity = req.Quantity,
 UnitCost = req.UnitCost,
 Reference = req.Reference,
 Note = req.Note,
 CreatedByUserId = userId
 });

 await db.SaveChangesAsync();
 return product;
 }

 public async Task<Product> AdjustAsync(StockAdjustmentRequest req, Guid? userId)
 {
 var product = await db.Products.FindAsync(req.ProductId)
 ?? throw new InvalidOperationException("Product not found.");

 var proposed = product.StockQuantity + req.Delta;
 if (proposed < 0)
 throw new InvalidOperationException(
 $"Cannot reduce stock below zero. Current stock: {product.StockQuantity}, adjustment: {req.Delta}.");

 product.StockQuantity = proposed;
 db.StockMovements.Add(new StockMovement
 {
 ProductId = product.Id,
 Type = StockMovementType.Adjustment,
 Quantity = req.Delta,
 UnitCost = product.UnitCost,
 Note = req.Note,
 CreatedByUserId = userId
 });

 await db.SaveChangesAsync();
 return product;
 }

 public Task<List<Product>> ListProductsAsync(bool lowStockOnly) =>
 db.Products.AsNoTracking()
 .Where(p => !lowStockOnly || p.StockQuantity <= p.ReorderLevel)
 .OrderBy(p => p.Category).ThenBy(p => p.Name)
 .ToListAsync();

 public Task<List<StockMovement>> MovementsAsync(Guid productId) =>
 db.StockMovements.AsNoTracking().Include(m => m.Product)
 .Where(m => m.ProductId == productId)
 .OrderByDescending(m => m.CreatedAt)
 .Take(200)
 .ToListAsync();
}
