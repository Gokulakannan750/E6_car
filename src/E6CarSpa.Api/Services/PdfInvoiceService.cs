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
                    var isQuotation = inv.Status is Domain.Enums.InvoiceStatus.Quotation or Domain.Enums.InvoiceStatus.InProgress;
                    var title = isQuotation
                        ? "QUOTATION"
                        : (inv.IsGstApplicable ? "TAX INVOICE" : "INVOICE");
                    c.Item().AlignRight().Text(title).FontSize(16).Bold();
                    // A quotation has no invoice number yet — don't print a "(draft)" number.
                    if (!string.IsNullOrEmpty(inv.InvoiceNumber))
                        c.Item().AlignRight().Text($"No: {inv.InvoiceNumber}");
                    c.Item().AlignRight().Text($"Date: {IndianTime.ToIstDate(inv.CreatedAt):dd-MM-yyyy}");
                });
            });
            col.Item().PaddingTop(6).LineHorizontal(1).LineColor(Colors.Grey.Medium);
        });
    }

    private static void ComposeBody(IContainer container, CompanySettings s, Invoice inv)
    {
        var isQuotation = inv.Status is Domain.Enums.InvoiceStatus.Quotation or Domain.Enums.InvoiceStatus.InProgress;
        container.PaddingTop(8).Column(col =>
        {
            col.Item().Row(row =>
            {
                row.RelativeItem().Column(c =>
                {
                    c.Item().Text(isQuotation ? "Customer" : "Bill To").Bold();
                    c.Item().Text(inv.Customer?.Name ?? "");
                    c.Item().Text($"Phone: {inv.Customer?.Phone}");
                });
                row.RelativeItem().AlignRight().Column(c =>
                {
                    c.Item().Text("Vehicle").Bold();
                    c.Item().Text(inv.Vehicle?.CarNumber ?? "");
                    if (!string.IsNullOrWhiteSpace(inv.Vehicle?.CarModel))
                        c.Item().Text(inv.Vehicle!.CarModel);
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
                    // Payment status only makes sense on a real invoice; a quotation is just an estimate.
                    if (!isQuotation)
                    {
                        Line("Paid", inv.AmountPaid);
                        Line("Balance", inv.Balance, bold: true);
                    }
                });
            });

            if (isQuotation)
                col.Item().PaddingTop(10).Text("This is a quotation / estimate, not a tax invoice. Prices are valid for 15 days.")
                    .Italic().FontColor(Colors.Grey.Darken1);

            if (!string.IsNullOrWhiteSpace(inv.Notes))
                col.Item().PaddingTop(6).Text($"Notes: {inv.Notes}").Italic().FontColor(Colors.Grey.Darken1);
        });
    }

    // ---------- Job card (work order for the workshop floor — NO prices) ----------

    /// <summary>
    /// Renders a JOB CARD: the customer, the vehicle, and the list of services to perform — with
    /// NO rates, amounts, tax or totals. Printed at the quotation stage and handed to the workers.
    /// </summary>
    public async Task<byte[]> RenderJobCardAsync(Invoice invoice)
    {
        var s = await db.CompanySettings.FirstAsync();

        var doc = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(28);
                page.DefaultTextStyle(t => t.FontSize(10).FontColor(Colors.Black));

                page.Header().Element(h => ComposeJobCardHeader(h, s, invoice));
                page.Content().Element(c => ComposeJobCardBody(c, invoice));
                page.Footer().AlignCenter().Text("Work order — for workshop use only. This is not a bill.")
                    .FontSize(8).FontColor(Colors.Grey.Darken1);
            });
        });

        return doc.GeneratePdf();
    }

    private const string BrandRed = "#B71C1C";

    private static void ComposeJobCardHeader(IContainer container, CompanySettings s, Invoice inv)
    {
        container.Column(col =>
        {
            col.Item().Row(row =>
            {
                row.RelativeItem().Column(c =>
                {
                    if (s.LogoBytes is { Length: > 0 })
                        c.Item().PaddingBottom(4).Width(140).Image(s.LogoBytes).FitWidth();
                    c.Item().Text(s.Name).FontSize(14).Bold().FontColor(BrandRed);
                });
                row.ConstantItem(200).AlignRight().Column(c =>
                {
                    c.Item().AlignRight().Text("JOB CARD").FontSize(22).Bold().FontColor(BrandRed);
                    c.Item().AlignRight().Text("Workshop work order").FontSize(9).FontColor(Colors.Grey.Darken1);
                });
            });
            col.Item().PaddingTop(6).LineHorizontal(2).LineColor(BrandRed);
        });
    }

    // A form-style job card (bordered fields + a services checklist) — deliberately NOT the
    // invoice/quotation layout, and with NO prices, so it can be handed to the workshop floor.
    private static void ComposeJobCardBody(IContainer container, Invoice inv)
    {
        container.PaddingTop(12).Column(col =>
        {
            col.Spacing(14);

            // ── Job details form ──
            col.Item().Table(t =>
            {
                t.ColumnsDefinition(c =>
                {
                    c.ConstantColumn(95);   // label
                    c.RelativeColumn();     // value
                    c.ConstantColumn(75);   // label
                    c.RelativeColumn();     // value
                });

                void Label(string text) => t.Cell().Border(0.75f).BorderColor(Colors.Grey.Medium)
                    .Background(Colors.Grey.Lighten3).PaddingVertical(6).PaddingHorizontal(8)
                    .Text(text).Bold().FontSize(9);
                void Value(string text, bool big = false)
                {
                    var span = t.Cell().Border(0.75f).BorderColor(Colors.Grey.Medium)
                        .PaddingVertical(6).PaddingHorizontal(8).Text(text).FontSize(big ? 13 : 10);
                    if (big) span.Bold();
                }

                Label("Date");        Value($"{IndianTime.ToIstDate(inv.CreatedAt):dd-MM-yyyy}");
                Label("Job Ref");     Value(inv.Vehicle?.CarNumber ?? "");
                Label("Customer");    Value(inv.Customer?.Name ?? "");
                Label("Phone");       Value(inv.Customer?.Phone ?? "");
                Label("Vehicle No");  Value(inv.Vehicle?.CarNumber ?? "", big: true);
                Label("Model");       Value(inv.Vehicle?.CarModel ?? "");
            });

            // ── Jobs to be done (checklist — no prices) ──
            col.Item().Column(c =>
            {
                c.Item().PaddingBottom(4).Text("Jobs to be done").Bold().FontSize(12).FontColor(BrandRed);
                c.Item().Table(table =>
                {
                    table.ColumnsDefinition(cols =>
                    {
                        cols.ConstantColumn(30);   // #
                        cols.RelativeColumn();     // service
                        cols.ConstantColumn(50);   // qty
                        cols.ConstantColumn(70);   // done
                    });

                    table.Header(header =>
                    {
                        void H(string t2) => header.Cell().Border(0.75f).BorderColor(Colors.Grey.Medium)
                            .Background(Colors.Grey.Lighten2).Padding(6).Text(t2).Bold();
                        H("#"); H("Service"); H("Qty"); H("Done");
                    });

                    int n = 1;
                    foreach (var item in inv.Items)
                    {
                        void C(string t2, bool center = false)
                        {
                            var cell = table.Cell().Border(0.75f).BorderColor(Colors.Grey.Lighten1).Padding(6).MinHeight(24);
                            (center ? cell.AlignCenter() : cell).Text(t2);
                        }
                        C((n++).ToString(), true);
                        C(item.Description);
                        C(item.Quantity.ToString("0.##"), true);
                        C("");   // left blank for the worker to tick
                    }
                });
            });

            if (!string.IsNullOrWhiteSpace(inv.Notes))
                col.Item().Text($"Notes: {inv.Notes}").Italic().FontColor(Colors.Grey.Darken1);

            // ── Sign-off ──
            col.Item().PaddingTop(24).Row(row =>
            {
                row.RelativeItem().Column(c =>
                {
                    c.Item().LineHorizontal(0.75f);
                    c.Item().PaddingTop(2).Text("Worker signature").FontSize(9).FontColor(Colors.Grey.Darken1);
                });
                row.ConstantItem(50);
                row.RelativeItem().Column(c =>
                {
                    c.Item().LineHorizontal(0.75f);
                    c.Item().PaddingTop(2).Text("Checked by").FontSize(9).FontColor(Colors.Grey.Darken1);
                });
            });
        });
    }
}
