using E6CarSpa.Domain.Entities;
using E6CarSpa.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace E6CarSpa.Api.Services;

/// <summary>Renders a GST tax invoice to a PDF byte array using QuestPDF (A4).</summary>
public class PdfInvoiceService(AppDbContext db)
{
    static PdfInvoiceService() => QuestPDF.Settings.License = LicenseType.Community; // set once

    public async Task<byte[]> RenderAsync(Invoice invoice)
    {
        var s = await db.CompanySettings.FirstAsync();

        var doc = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(28);
                page.DefaultTextStyle(t => t.FontSize(9).FontColor(Colors.Black));

                page.Header().Element(h => ComposeHeader(h, s, invoice));
                page.Content().Element(c => ComposeBody(c, s, invoice));
                page.Footer().AlignCenter().Text("Thank you for choosing E6 Car Spa — drive safe!")
                    .FontSize(8).FontColor(Colors.Grey.Darken1);
            });
        });

        return doc.GeneratePdf();
    }

    private static void ComposeHeader(IContainer container, CompanySettings s, Invoice inv)
    {
        container.Column(col =>
        {
            col.Item().Row(row =>
            {
                row.RelativeItem().Column(c =>
                {
                    if (s.LogoBytes is { Length: > 0 })
                        c.Item().PaddingBottom(6).Width(150).Image(s.LogoBytes).FitWidth();
                    c.Item().Text(s.Name).FontSize(18).Bold().FontColor("#B71C1C");
                    c.Item().Text($"{s.AddressLine1}");
                    c.Item().Text($"{s.AddressLine2}");
                    c.Item().Text($"{s.City} - {s.Pincode}, {s.State}");
                    c.Item().Text($"Phone: {s.Phone}");
                    if (!string.IsNullOrWhiteSpace(s.Gstin)) c.Item().Text($"GSTIN: {s.Gstin}").Bold();
                });
                row.ConstantItem(190).Column(c =>
                {
                    var title = inv.Status == Domain.Enums.InvoiceStatus.Quotation
                        ? "QUOTATION"
                        : (inv.IsGstApplicable ? "TAX INVOICE" : "INVOICE");
                    c.Item().AlignRight().Text(title).FontSize(16).Bold();
                    c.Item().AlignRight().Text($"No: {inv.InvoiceNumber ?? "(draft)"}");
                    c.Item().AlignRight().Text($"Date: {IndianTime.ToIstDate(inv.CreatedAt):dd-MM-yyyy}");
                });
            });
            col.Item().PaddingTop(6).LineHorizontal(1).LineColor(Colors.Grey.Medium);
        });
    }

    private static void ComposeBody(IContainer container, CompanySettings s, Invoice inv)
    {
        container.PaddingTop(8).Column(col =>
        {
            col.Item().Row(row =>
            {
                row.RelativeItem().Column(c =>
                {
                    c.Item().Text("Bill To").Bold();
                    c.Item().Text(inv.Customer?.Name ?? "");
                    c.Item().Text($"Phone: {inv.Customer?.Phone}");
                });
                row.RelativeItem().AlignRight().Column(c =>
                {
                    c.Item().Text("Vehicle").Bold();
                    c.Item().Text(inv.Vehicle?.CarNumber ?? "");
                });
            });

            col.Item().PaddingTop(8).Table(table =>
            {
                table.ColumnsDefinition(cols =>
                {
                    cols.ConstantColumn(22);   // #
                    cols.RelativeColumn(3);    // description
                    cols.ConstantColumn(52);   // HSN/SAC
                    cols.ConstantColumn(40);   // qty
                    cols.ConstantColumn(72);   // rate
                    cols.ConstantColumn(76);   // amount
                });

                table.Header(header =>
                {
                    void H(string t) => header.Cell().Background(Colors.Grey.Lighten2).Padding(4).Text(t).Bold();
                    H("#"); H("Description"); H("HSN/SAC"); H("Qty"); H("Rate"); H("Amount");
                });

                int n = 1;
                foreach (var item in inv.Items)
                {
                    void C(string t, bool right = false)
                    {
                        var cell = table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten1).Padding(4);
                        (right ? cell.AlignRight() : cell).Text(t);
                    }
                    C((n++).ToString());
                    C(item.Description);
                    C(item.HsnSac);
                    C(item.Quantity.ToString("0.##"), true);
                    C(item.UnitPrice.ToString("0.00"), true);
                    C(item.LineTotal.ToString("0.00"), true);
                }
            });

            col.Item().PaddingTop(10).Row(row =>
            {
                row.RelativeItem();
                row.ConstantItem(240).Column(totals =>
                {
                    void Line(string label, decimal value, bool bold = false)
                    {
                        totals.Item().Row(r =>
                        {
                            var l = r.RelativeItem().Text(label);
                            var v = r.ConstantItem(90).AlignRight().Text(value.ToString("0.00"));
                            if (bold) { l.Bold(); v.Bold(); }
                        });
                    }
                    Line("Sub Total", inv.SubTotal);
                    if (inv.IsGstApplicable)   // GST lines only on a GST bill
                    {
                        Line("Taxable Value", inv.TaxableValue);
                        if (inv.IgstAmount > 0) Line("IGST", inv.IgstAmount);
                        else { Line("CGST", inv.CgstAmount); Line("SGST", inv.SgstAmount); }
                    }
                    totals.Item().PaddingVertical(2).LineHorizontal(0.5f);
                    Line("Grand Total", inv.GrandTotal, bold: true);
                    if (inv.DiscountAmount > 0) Line("Discount", -inv.DiscountAmount);
                    Line("Paid", inv.AmountPaid);
                    Line("Balance", inv.Balance, bold: true);
                });
            });

            if (!string.IsNullOrWhiteSpace(inv.Notes))
                col.Item().PaddingTop(10).Text($"Notes: {inv.Notes}").Italic().FontColor(Colors.Grey.Darken1);
        });
    }
}
