using E6CarSpa.Api.Mapping;
using E6CarSpa.Api.Services;
using E6CarSpa.Api.Auth;
using E6CarSpa.Domain.Enums;
using E6CarSpa.Contracts;
using E6CarSpa.Domain.Entities;
using E6CarSpa.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace E6CarSpa.Api.Controllers;

[Authorize]
public class ProductsController(AppDbContext db, InventoryService inventory, AuditService audit) : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<ProductDto>>> GetAll([FromQuery] bool lowStockOnly = false)
    {
        var products = await inventory.ListProductsAsync(lowStockOnly);
        return products.Select(p => p.ToDto()).ToList();
    }

    [HttpPost]
    public async Task<ActionResult<ProductDto>> Create(SaveProductRequest req)
    {
        var p = new Product
        {
            Name = req.Name, Category = req.Category,
            ReorderLevel = req.ReorderLevel, UnitCost = req.UnitCost,
            HsnSac = req.HsnSac, GstRate = req.GstRate, IsActive = req.IsActive
        };
        db.Products.Add(p);
        await db.SaveChangesAsync();
        await audit.LogAsync("Product.Create", $"name={p.Name}; cost={p.UnitCost}");
        return p.ToDto();
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ProductDto>> Update(Guid id, SaveProductRequest req)
    {
        var p = await db.Products.FindAsync(id);
        if (p is null) return NotFound();
        p.Name = req.Name; p.Category = req.Category;
        p.ReorderLevel = req.ReorderLevel; p.UnitCost = req.UnitCost;
        p.HsnSac = req.HsnSac; p.GstRate = req.GstRate; p.IsActive = req.IsActive;
        p.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        await audit.LogAsync("Product.Update", $"name={p.Name}; cost={p.UnitCost}; active={p.IsActive}");
        return p.ToDto();
    }

    [HttpPost("purchase")]
    public async Task<ActionResult<ProductDto>> Purchase(StockPurchaseRequest req)
    {
        var p = await inventory.ReceivePurchaseAsync(req, CurrentUserId);
        return p.ToDto();
    }

    [HttpPost("adjust")]
    public async Task<ActionResult<ProductDto>> Adjust(StockAdjustmentRequest req)
    {
        var p = await inventory.AdjustAsync(req, CurrentUserId);
        return p.ToDto();
    }

    [HttpGet("{id:guid}/movements")]
    public async Task<ActionResult<List<StockMovementDto>>> Movements(Guid id)
    {
        var moves = await inventory.MovementsAsync(id);
        return moves.Select(m => m.ToDto()).ToList();
    }
}
