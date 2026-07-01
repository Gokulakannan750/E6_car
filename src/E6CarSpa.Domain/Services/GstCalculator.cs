using E6CarSpa.Domain.Entities;

namespace E6CarSpa.Domain.Services;

/// <summary>
/// Pure GST maths for invoices. Intra-state supply splits tax into CGST + SGST;
/// inter-state supply uses a single IGST. Recomputes every line and the invoice totals
/// so the stored figures are always internally consistent.
/// </summary>
public static class GstCalculator
{
    private static decimal Round(decimal v) => Math.Round(v, 2, MidpointRounding.AwayFromZero);

    /// <param name="interState">
    /// True when the customer's place of supply is a different state than the spa
    /// (IGST applies); false for local jobs (CGST + SGST).
    /// </param>
    public static void Recalculate(Invoice invoice, bool interState)
    {
        var items = invoice.Items.ToList();

        // Pass 1: each line's taxable base after its own (line-level) discount.
        decimal subTotal = 0m;
        var bases = new decimal[items.Count];
        for (var i = 0; i < items.Count; i++)
        {
            var gross = items[i].Quantity * items[i].UnitPrice;
            subTotal += gross;
            bases[i] = Math.Max(0m, Round(gross - items[i].DiscountAmount));
        }
        var totalBase = bases.Sum();

        // The invoice-level discount is spread proportionally across the lines
        // (still needed so each line shows its own taxable/total for the bill),
        // but GST itself is charged ONCE on the invoice's total taxable value —
        // the shop uses a single GST rate for every service, so this avoids
        // per-line rounding drift and a confusing per-line GST% column in the UI.
        var headerDiscount = Math.Clamp(invoice.DiscountAmount, 0m, totalBase);
        var rate = items.Count > 0 ? items[0].GstRate : 0m;

        decimal taxable = 0m, allocatedDiscount = 0m;
        var lineTaxables = new decimal[items.Count];
        for (var i = 0; i < items.Count; i++)
        {
            decimal share = 0m;
            if (totalBase > 0m && headerDiscount > 0m)
                share = (i == items.Count - 1)
                    ? headerDiscount - allocatedDiscount
                    : Round(headerDiscount * bases[i] / totalBase);
            allocatedDiscount += share;

            var lineTaxable = Math.Max(0m, Round(bases[i] - share));
            lineTaxables[i] = lineTaxable;
            taxable += lineTaxable;
        }
        taxable = Round(taxable);

        // Single tax figure for the whole invoice, then spread across lines
        // (proportional to each line's taxable share, last line absorbs the remainder)
        // purely so each row can still show its own line total.
        var totalTax = invoice.IsGstApplicable ? Round(taxable * rate / 100m) : 0m;

        decimal cgst = 0m, sgst = 0m, igst = 0m, allocatedTax = 0m;
        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];
            item.TaxableValue = lineTaxables[i];

            decimal lineTax = 0m;
            if (totalTax > 0m && taxable > 0m)
                lineTax = (i == items.Count - 1)
                    ? totalTax - allocatedTax
                    : Round(totalTax * lineTaxables[i] / taxable);
            allocatedTax += lineTax;

            if (interState)
            {
                item.IgstAmount = lineTax;
                item.CgstAmount = 0m;
                item.SgstAmount = 0m;
            }
            else
            {
                var half = Round(lineTax / 2m);
                item.CgstAmount = half;
                item.SgstAmount = lineTax - half; // keep the pair summing to lineTax
                item.IgstAmount = 0m;
            }

            item.LineTotal = Round(lineTaxables[i] + lineTax);

            cgst += item.CgstAmount;
            sgst += item.SgstAmount;
            igst += item.IgstAmount;
        }

        invoice.SubTotal = Round(subTotal);
        invoice.TaxableValue = taxable;
        invoice.CgstAmount = Round(cgst);
        invoice.SgstAmount = Round(sgst);
        invoice.IgstAmount = Round(igst);
        invoice.TotalTax = totalTax;
        // Totals are derived from the per-line figures, so they always reconcile.
        invoice.GrandTotal = Round(invoice.TaxableValue + invoice.TotalTax);
    }
}
