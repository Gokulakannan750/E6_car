using E6CarSpa.Api.Mapping;
using E6CarSpa.Contracts;
using E6CarSpa.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace E6CarSpa.Api.Controllers;

/// <summary>
/// Customer/vehicle lookups for intake and the Customers screen. Anonymous by design:
/// the desktop app uses these without a login at the counter.
/// </summary>
[AllowAnonymous]
public class CustomersController(AppDbContext db) : ApiControllerBase
{
    /// <summary>Intake lookup: find an existing customer by phone so details auto-fill.</summary>
    [HttpGet("by-phone/{phone}")]
    public async Task<ActionResult<CustomerLookupResult>> ByPhone(string phone)
    {
        var c = await db.Customers.AsNoTracking().Include(x => x.Vehicles)
            .FirstOrDefaultAsync(x => x.Phone == phone.Trim());
        return new CustomerLookupResult(c is not null, c?.ToDto());
    }

    /// <summary>Intake lookup by car number (returns the owning customer + all their cars).</summary>
    [HttpGet("by-car/{carNumber}")]
    public async Task<ActionResult<CustomerLookupResult>> ByCar(string carNumber)
    {
        var car = carNumber.Trim().ToUpperInvariant().Replace(" ", "");
        var vehicle = await db.Vehicles.AsNoTracking()
            .FirstOrDefaultAsync(v => v.CarNumber == car);
        if (vehicle is null)
            return new CustomerLookupResult(false, null);

        var customer = await db.Customers.AsNoTracking().Include(c => c.Vehicles)
            .FirstOrDefaultAsync(c => c.Id == vehicle.CustomerId);
        return new CustomerLookupResult(customer is not null, customer?.ToDto());
    }

    [HttpGet]
    public async Task<ActionResult<List<CustomerDto>>> Search([FromQuery] string? q)
    {
        var query = db.Customers.AsNoTracking().Include(c => c.Vehicles).AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
        {
            var s = q.Trim().ToLower();
            query = query.Where(c => c.Name.ToLower().Contains(s) || c.Phone.Contains(s));
        }
        return await query.OrderBy(c => c.Name).Take(200).Select(c => c.ToDto()).ToListAsync();
    }
}
