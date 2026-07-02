using E6CarSpa.Api.Mapping;
using E6CarSpa.Contracts;
using E6CarSpa.Domain.Enums;
using E6CarSpa.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace E6CarSpa.Api.Services;

/// <summary>Read-only reporting queries: sales, GST summary, and customer history.</summary>
public class ReportsService(AppDbContext db)
{
    private static readonly InvoiceStatus[] Finalised = [InvoiceStatus.Invoiced, InvoiceStatus.Paid];

    public async Task<SalesReportDto> SalesAsync(DateTime from, DateTime to)
    {
        var (start, end) = IndianTime.RangeUtc(from, to);

        // One query for invoices, one for payments, one for line items — then aggregate in memory.
        var inv = await db.Invoices.AsNoTracking()
            .Where(i => Finalised.Contains(i.Status) && i.CreatedAt >= start && i.CreatedAt < end)
            .Select(i => new { i.CreatedAt, i.TaxableValue, i.CgstAmount, i.SgstAmount, i.IgstAmount, i.GrandTotal, i.AmountPaid })
            .ToListAsync();

        // Collections counted by payment date (cash flow), independent of invoice date.
        var pay = await db.Payments.AsNoTracking()
            .Where(p => p.PaidAt >= start && p.PaidAt < end)
            .Select(p => new { p.Method, p.Amount })
            .ToListAsync();

        var items = await db.InvoiceItems.AsNoTracking()
            .Where(it => it.Invoice!.CreatedAt >= start && it.Invoice.CreatedAt < end
                         && Finalised.Contains(it.Invoice.Status))
            .Select(it => new { it.Description, it.Quantity, it.LineTotal })
            .ToListAsync();

        var cgst = inv.Sum(x => x.CgstAmount);
        var sgst = inv.Sum(x => x.SgstAmount);
        var igst = inv.Sum(x => x.IgstAmount);

        var daily = inv
            .GroupBy(x => IndianTime.ToIstDate(x.CreatedAt))
            .Select(g => new DailySalesRow(g.Key, g.Count(), g.Sum(x => x.GrandTotal), g.Sum(x => x.AmountPaid)))
            .OrderBy(r => r.Date)
            .ToList();

        var topServices = items
            .GroupBy(x => x.Description)
            .Select(g => new TopServiceRow(g.Key, g.Sum(x => x.Quantity), g.Sum(x => x.LineTotal)))
            .OrderByDescending(r => r.Amount)
            .Take(10)
            .ToList();

        return new SalesReportDto(from.Date, to.Date, inv.Count,
            inv.Sum(x => x.TaxableValue), cgst, sgst, igst, cgst + sgst + igst,
            inv.Sum(x => x.GrandTotal),
            pay.Sum(x => x.Amount),
            pay.Where(x => x.Method == PaymentMethod.Cash).Sum(x => x.Amount),
            pay.Where(x => x.Method == PaymentMethod.Card).Sum(x => x.Amount),
            pay.Where(x => x.Method == PaymentMethod.Upi).Sum(x => x.Amount),
            inv.Sum(x => x.GrandTotal - x.AmountPaid),
            daily, topServices);
    }

    public async Task<GstSummaryDto> GstAsync(DateTime from, DateTime to)
    {
        var (start, end) = IndianTime.RangeUtc(from, to);

        // GST is charged once per invoice (on the total taxable value, at the first line's rate —
        // see GstCalculator), so per-line tax columns are always zero. Aggregate from the invoice
        // headers, keyed by each invoice's effective rate; group in memory (small data).
        var invoices = await db.Invoices.AsNoTracking()
            .Where(i => Finalised.Contains(i.Status) && i.CreatedAt >= start && i.CreatedAt < end)
            .Select(i => new
            {
                Rate = i.Items.Select(it => (decimal?)it.GstRate).FirstOrDefault() ?? 0m,
                i.TaxableValue, i.CgstAmount, i.SgstAmount, i.IgstAmount
            })
            .ToListAsync();

        var rows = invoices
            .GroupBy(x => x.Rate)
            .Select(g => new GstRateRow(
                g.Key,
                g.Sum(x => x.TaxableValue),
                g.Sum(x => x.CgstAmount),
                g.Sum(x => x.SgstAmount),
                g.Sum(x => x.IgstAmount),
                g.Sum(x => x.CgstAmount + x.SgstAmount + x.IgstAmount)))
            .OrderBy(r => r.GstRate)
            .ToList();

        return new GstSummaryDto(from.Date, to.Date, rows,
            rows.Sum(r => r.TaxableValue), rows.Sum(r => r.Total));
    }

    public async Task<CustomerHistoryDto?> CustomerAsync(string phone)
    {
        var customer = await db.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Phone == phone.Trim());
        if (customer is null) return null;

        var invoices = await db.Invoices.AsNoTracking()
            .Include(i => i.Customer).Include(i => i.Vehicle)
            .Where(i => i.CustomerId == customer.Id && i.Status != InvoiceStatus.Cancelled)
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync();

        var finalised = invoices.Where(i => Finalised.Contains(i.Status)).ToList();
        return new CustomerHistoryDto(
            customer.Id, customer.Name, customer.Phone,
            finalised.Count,
            finalised.Sum(i => i.GrandTotal),
            finalised.Sum(i => i.GrandTotal - i.AmountPaid),
            invoices.Select(i => i.ToListItem()).ToList());
    }
}
