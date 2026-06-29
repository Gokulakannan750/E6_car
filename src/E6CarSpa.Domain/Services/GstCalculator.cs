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

        // The invoice-level discount is spread proportionally across the lines BEFORE tax,
        // so GST is charged on the actual (discounted) consideration — GST-compliant.
        var headerDiscount = Math.Clamp(invoice.DiscountAmount, 0m, totalBase);

        decimal taxable = 0m, cgst = 0m, sgst = 0m, igst = 0m, allocated = 0m;
        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];

            decimal share = 0m;
            if (totalBase > 0m && headerDiscount > 0m)
                share = (i == items.Count - 1)
                    ? headerDiscount - allocated            // last line absorbs the rounding remainder
                    : Round(headerDiscount * bases[i] / totalBase);
            allocated += share;

            var lineTaxable = Math.Max(0m, Round(bases[i] - share));
            item.TaxableValue = lineTaxable;

            // Non-GST bill: no tax is charged.
            var lineTax = invoice.IsGstApplicable ? Round(lineTaxable * item.GstRate / 100m) : 0m;
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

            item.LineTotal = Round(lineTaxable + lineTax);

            taxable += lineTaxable;
            cgst += item.CgstAmount;
            sgst += item.SgstAmount;
            igst += item.IgstAmount;
        }

        invoice.SubTotal = Round(subTotal);
        invoice.TaxableValue = Round(taxable);
        invoice.CgstAmount = Round(cgst);
        invoice.SgstAmount = Round(sgst);
        invoice.IgstAmount = Round(igst);
        invoice.TotalTax = Round(cgst + sgst + igst);
        // Totals are derived from the per-line figures, so they always reconcile.
        invoice.GrandTotal = Round(invoice.TaxableValue + invoice.TotalTax);
    }
}
