using E6CarSpa.Api.Services;
using E6CarSpa.Contracts;
using E6CarSpa.Domain.Enums;
using E6CarSpa.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace E6CarSpa.Api.Controllers;


public class SettingsController(AppDbContext db, AuditService audit) : ApiControllerBase
{


    [HttpGet]
    public async Task<ActionResult<CompanySettingsDto>> Get()
    {
        var s = await db.CompanySettings.FirstAsync();
        return new CompanySettingsDto(s.Name, s.AddressLine1, s.AddressLine2, s.City, s.State,
            s.StateCode, s.Pincode, s.Phone, s.Email, s.Gstin, s.InvoicePrefix,
            s.LastInvoiceSequence, s.DefaultGstRate, s.LogoBytes is { Length: > 0 });
    }

    /// <summary>Returns the company logo image (or 404 if none set).</summary>
    // Anonymous: the desktop shell loads its watermark logo before anyone logs in.
    [HttpGet("logo")]
    [AllowAnonymous]
    public async Task<IActionResult> GetLogo()
    {
        var s = await db.CompanySettings.FirstAsync();
        return s.LogoBytes is { Length: > 0 } ? File(s.LogoBytes, "image/png") : NotFound();
    }

    /// <summary>Uploads/replaces the company logo (base64-encoded PNG or JPG).</summary>
    [HttpPut("logo")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> SetLogo(UploadLogoRequest req)
    {
        byte[] bytes;
        try { bytes = Convert.FromBase64String(req.Base64); }
        catch { return BadRequest(new { message = "Invalid image data." }); }
        if (bytes.Length == 0) return BadRequest(new { message = "Empty image." });
        if (bytes.Length > 2 * 1024 * 1024) return BadRequest(new { message = "Logo must be under 2 MB." });

        var s = await db.CompanySettings.FirstAsync();
        s.LogoBytes = bytes;
        s.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        await audit.LogAsync("Settings.LogoUpload", $"bytes={bytes.Length}");
        return Ok();
    }

    /// <summary>Removes the company logo.</summary>
    [HttpDelete("logo")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteLogo()
    {
        var s = await db.CompanySettings.FirstAsync();
        s.LogoBytes = null;
        s.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        await audit.LogAsync("Settings.LogoDelete");
        return Ok();
    }

    [HttpPut]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<CompanySettingsDto>> Update(SaveCompanySettingsRequest req)
    {
        var s = await db.CompanySettings.FirstAsync();
        s.Name = req.Name; s.AddressLine1 = req.AddressLine1; s.AddressLine2 = req.AddressLine2;
        s.City = req.City; s.State = req.State; s.StateCode = req.StateCode; s.Pincode = req.Pincode;
        s.Phone = req.Phone; s.Email = req.Email; s.Gstin = req.Gstin;
        s.InvoicePrefix = req.InvoicePrefix; s.DefaultGstRate = req.DefaultGstRate;
        s.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        await audit.LogAsync("Settings.Update", $"gstin={s.Gstin}; prefix={s.InvoicePrefix}; gst={s.DefaultGstRate}");
        return await Get();
    }

    // Anonymous: the desktop Dashboard is the no-login landing screen.
    [HttpGet("/api/dashboard")]
    [AllowAnonymous]
    public async Task<ActionResult<DashboardSummaryDto>> Dashboard()
    {
        var (start, end) = Services.IndianTime.TodayUtc();

        var jobsToday = await db.Invoices.CountAsync(i => i.CreatedAt >= start && i.CreatedAt < end);
        var quotationsPending = await db.Invoices.CountAsync(i => i.Status == InvoiceStatus.Quotation);
        var invoicesUnpaid = await db.Invoices.CountAsync(i => i.Status == InvoiceStatus.Invoiced);

        var paymentsQuery = db.Payments.AsNoTracking()
            .Where(p => p.PaidAt >= start && p.PaidAt < end);
        var totalCollected = await paymentsQuery.SumAsync(p => (decimal?)p.Amount) ?? 0m;
        var cashToday = await paymentsQuery.Where(p => p.Method == PaymentMethod.Cash).SumAsync(p => (decimal?)p.Amount) ?? 0m;
        var cardToday = await paymentsQuery.Where(p => p.Method == PaymentMethod.Card).SumAsync(p => (decimal?)p.Amount) ?? 0m;
        var upiToday = await paymentsQuery.Where(p => p.Method == PaymentMethod.Upi).SumAsync(p => (decimal?)p.Amount) ?? 0m;

        var lowStock = await db.Products.CountAsync(p => p.IsActive && p.StockQuantity <= p.ReorderLevel);

        var activeJobs = await db.Invoices.AsNoTracking()
            .Where(i => i.CreatedAt >= start && i.CreatedAt < end && (i.Status == InvoiceStatus.Quotation || i.Status == InvoiceStatus.InProgress))
            .OrderByDescending(i => i.CreatedAt)
            .Select(i => new LiveJobDto(i.InvoiceNumber ?? "", i.Vehicle!.CarNumber, i.Vehicle.CarModel, i.Status, i.GrandTotal))
            .ToListAsync();

        return new DashboardSummaryDto(
            TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Services.IndianTime.Zone).Date,
            jobsToday, quotationsPending, invoicesUnpaid,
            totalCollected, cashToday, cardToday, upiToday,
            lowStock,
            activeJobs);
    }
}
