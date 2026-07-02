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

        // Single tax figure for the whole invoice, computed ONCE on the final TaxableValue.
        var totalTax = invoice.IsGstApplicable ? Round(taxable * rate / 100m) : 0m;

        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];
            item.TaxableValue = lineTaxables[i];

            // No per-line GST splits
            item.CgstAmount = 0m;
            item.SgstAmount = 0m;
            item.IgstAmount = 0m;
            
            item.LineTotal = lineTaxables[i]; // No tax added to line total
        }

        invoice.SubTotal = Round(subTotal);
        invoice.TaxableValue = taxable;

        if (interState)
        {
            invoice.IgstAmount = totalTax;
            invoice.CgstAmount = 0m;
            invoice.SgstAmount = 0m;
        }
        else
        {
            var half = Round(totalTax / 2m);
            invoice.CgstAmount = half;
            invoice.SgstAmount = totalTax - half; // keep the pair summing to totalTax
            invoice.IgstAmount = 0m;
        }

        invoice.TotalTax = totalTax;
        invoice.GrandTotal = Round(invoice.TaxableValue + invoice.TotalTax);
    }
}
