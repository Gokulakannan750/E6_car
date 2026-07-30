using E6CarSpa.Api.Mapping;
using E6CarSpa.Api.Services;
using E6CarSpa.Api.Auth;
using E6CarSpa.Contracts;
using E6CarSpa.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace E6CarSpa.Api.Controllers;

/// <summary>
/// The billing workflow. Anonymous by design (except Cancel): the desktop app is used at the
/// counter without a login, so the deny-by-default fallback policy is opted out per action here.
/// </summary>
[RequirePermission(Permission.Billing)]
public class InvoicesController(InvoiceService invoices, PdfInvoiceService pdf) : ApiControllerBase
{
    /// <summary>Steps 1-3: create a quotation from customer + car + chosen services.</summary>
    [HttpPost("quotation")]
    public async Task<ActionResult<InvoiceDto>> CreateQuotation(CreateQuotationRequest req)
    {
        var inv = await invoices.CreateQuotationAsync(req, CurrentUserId);
        return inv.ToDto();
    }

    [HttpGet]
    public async Task<ActionResult<List<InvoiceListItemDto>>> List(
        [FromQuery] InvoiceStatus? status, [FromQuery] string? search)
    {
        var list = await invoices.ListAsync(status, search);
        return list.Select(i => i.ToListItem()).ToList();
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<InvoiceDto>> Get(Guid id)
    {
        var inv = await invoices.GetEntityAsync(id);
        return inv is null ? NotFound() : inv.ToDto();
    }

    /// <summary>Step 4: edit a quotation/invoice (add jobs done, change discount/notes).</summary>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<InvoiceDto>> Update(Guid id, UpdateInvoiceRequest req)
    {
        var inv = await invoices.UpdateAsync(id, req);
        return inv is null ? NotFound() : inv.ToDto();
    }

    /// <summary>Step 4: finalise — assign invoice number, mark Invoiced, deduct inventory.</summary>
    [HttpPost("{id:guid}/finalise")]
    public async Task<ActionResult<InvoiceDto>> Finalise(Guid id)
    {
        var inv = await invoices.FinaliseAsync(id, CurrentUserId);
        return inv is null ? NotFound() : inv.ToDto();
    }

    /// <summary>Step 5: record a payment; sends the WhatsApp thank-you when fully paid.</summary>
    [HttpPost("{id:guid}/payments")]
    public async Task<ActionResult<InvoiceDto>> Pay(Guid id, RecordPaymentRequest req)
    {
        var inv = await invoices.RecordPaymentAsync(id, req, CurrentUserId);
        return inv is null ? NotFound() : inv.ToDto();
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<ActionResult<InvoiceDto>> Cancel(Guid id)
    {
        var inv = await invoices.CancelAsync(id);
        return inv is null ? NotFound() : inv.ToDto();
    }

    /// <summary>Print: returns the invoice (or quotation) as a PDF.</summary>
    [HttpGet("{id:guid}/pdf")]
    public async Task<IActionResult> Pdf(Guid id)
    {
        var inv = await invoices.GetEntityAsync(id);
        if (inv is null) return NotFound();
        var bytes = await pdf.RenderAsync(inv);
        var name = (inv.InvoiceNumber ?? "quotation").Replace("/", "-");
        return File(bytes, "application/pdf", $"{name}.pdf");
    }

    /// <summary>Print the JOB CARD (services to do, NO prices) for the workshop floor.</summary>
    [HttpGet("{id:guid}/jobcard")]
    public async Task<IActionResult> JobCard(Guid id)
    {
        var inv = await invoices.GetEntityAsync(id);
        if (inv is null) return NotFound();
        var bytes = await pdf.RenderJobCardAsync(inv);
        var car = (inv.Vehicle?.CarNumber ?? "car").Replace("/", "-").Replace(" ", "");
        return File(bytes, "application/pdf", $"jobcard-{car}.pdf");
    }
}
