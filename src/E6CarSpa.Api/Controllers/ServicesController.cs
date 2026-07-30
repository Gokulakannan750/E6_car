using E6CarSpa.Api.Mapping;
using E6CarSpa.Api.Services;
using E6CarSpa.Api.Auth;
using E6CarSpa.Domain.Enums;
using E6CarSpa.Contracts;
using E6CarSpa.Domain.Entities;
using E6CarSpa.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace E6CarSpa.Api.Controllers;


[RequirePermission(Permission.Catalogue)]
public class ServicesController(AppDbContext db, AuditService audit) : ApiControllerBase
{
    // Anonymous: the no-login New Job screen needs the service catalogue to build a quotation.
    [HttpGet]
    public async Task<ActionResult<List<ServiceDto>>> GetAll([FromQuery] bool includeInactive = false)
    {
        var q = db.Services.AsNoTracking().AsQueryable();
        if (!includeInactive) q = q.Where(s => s.IsActive);
        return await q.OrderBy(s => s.Category).ThenBy(s => s.Name).Select(s => s.ToDto()).ToListAsync();
    }

    // Catalogue is open to the counter (no login) by request.
    [HttpPost]
    public async Task<ActionResult<ServiceDto>> Create(SaveServiceRequest req)
    {
        var s = new Service
        {
            Name = req.Name, Category = req.Category, DefaultPrice = req.DefaultPrice,
            HsnSac = req.HsnSac, GstRate = req.GstRate, IsActive = req.IsActive
        };
        db.Services.Add(s);
        await db.SaveChangesAsync();
        await audit.LogAsync("Service.Create", $"name={s.Name}; price={s.DefaultPrice}; gst={s.GstRate}");
        return s.ToDto();
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ServiceDto>> Update(Guid id, SaveServiceRequest req)
    {
        var s = await db.Services.FindAsync(id);
        if (s is null) return NotFound();
        s.Name = req.Name; s.Category = req.Category; s.DefaultPrice = req.DefaultPrice;
        s.HsnSac = req.HsnSac; s.GstRate = req.GstRate; s.IsActive = req.IsActive;
        s.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        await audit.LogAsync("Service.Update", $"name={s.Name}; price={s.DefaultPrice}; gst={s.GstRate}; active={s.IsActive}");
        return s.ToDto();
    }

    /// <summary>The products (and quantities) a service consumes when performed.</summary>
    [HttpGet("{id:guid}/bom")]
    public async Task<ActionResult<List<BomItemDto>>> GetBom(Guid id) =>
        await db.ServiceProducts.AsNoTracking()
            .Where(sp => sp.ServiceId == id)
            .Include(sp => sp.Product)
            .OrderBy(sp => sp.Product!.Name)
            .Select(sp => new BomItemDto(sp.ProductId, sp.Product!.Name, sp.DefaultQuantity))
            .ToListAsync();

    /// <summary>Replace the whole bill-of-materials for a service.</summary>
    [HttpPut("{id:guid}/bom")]
    public async Task<ActionResult<List<BomItemDto>>> SaveBom(Guid id, SaveBomRequest req)
    {
        if (!await db.Services.AnyAsync(s => s.Id == id)) return NotFound();

        var existing = await db.ServiceProducts.Where(sp => sp.ServiceId == id).ToListAsync();
        db.ServiceProducts.RemoveRange(existing);

        foreach (var line in req.Items)
            db.ServiceProducts.Add(new ServiceProduct
            {
                ServiceId = id,
                ProductId = line.ProductId,
                DefaultQuantity = line.DefaultQuantity
            });

        await db.SaveChangesAsync();
        await audit.LogAsync("Service.BomUpdate", $"serviceId={id}; lines={req.Items.Count}");
        return await GetBom(id);
    }
}
