using E6CarSpa.Api.Mapping;
using E6CarSpa.Api.Auth;
using E6CarSpa.Domain.Enums;
using E6CarSpa.Contracts;
using E6CarSpa.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace E6CarSpa.Api.Controllers;

/// <summary>
/// Customer/vehicle lookups for intake and the Customers screen. Requires a signed-in user -
/// this is customer PII, and the clients now authenticate before reaching any screen.
/// </summary>
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

 /// <param name="page">1-based page index.</param>
 /// <param name="pageSize">Rows per page (capped at 500).</param>
 [HttpGet]
 public async Task<ActionResult<List<CustomerDto>>> Search([FromQuery] string? q, [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
 {
 var query = db.Customers.AsNoTracking().Include(c => c.Vehicles).AsQueryable();
 if (!string.IsNullOrWhiteSpace(q))
 {
 var s = q.Trim().ToLower();
 var car = s.Replace(" ", "");
 query = query.Where(c =>
 c.Name.ToLower().Contains(s) ||
 c.Phone.Contains(s) ||
 c.Vehicles.Any(v => v.CarNumber.ToLower().Contains(car) ||
 (v.CarModel != null && v.CarModel.ToLower().Contains(s))));
 }

 pageSize = Math.Clamp(pageSize, 1, 500);
 page = Math.Max(page, 1);

 var total = await query.CountAsync();
 var items = await query.OrderBy(c => c.Name)
 .Skip((page - 1) * pageSize)
 .Take(pageSize)
 .Select(c => c.ToDto())
 .ToListAsync();

 Response.Headers.Append("X-Total-Count", total.ToString());
 return items;
 }
}
