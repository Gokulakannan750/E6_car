using E6CarSpa.Domain.Entities;
using E6CarSpa.Domain.Services;

namespace E6CarSpa.Tests;

/// <summary>
/// Pure GST maths — the most important logic to lock down, since every rupee billed flows through it.
/// No database needed: build an Invoice graph in memory and assert the recomputed figures.
/// </summary>
public class GstCalculatorTests
{
    private static InvoiceItem Item(decimal qty, decimal price, decimal gstRate, decimal lineDiscount = 0m) =>
        new() { Quantity = qty, UnitPrice = price, GstRate = gstRate, DiscountAmount = lineDiscount };

    private static Invoice Invoice(bool gst, decimal headerDiscount, params InvoiceItem[] items) =>
        new() { IsGstApplicable = gst, DiscountAmount = headerDiscount, Items = items.ToList() };

    [Fact]
    public void SingleLine_IntraState_SplitsCgstSgst()
    {
        var inv = Invoice(gst: true, headerDiscount: 0m, Item(1, 1000m, 18m));

        GstCalculator.Recalculate(inv, interState: false);

        Assert.Equal(1000m, inv.SubTotal);
        Assert.Equal(1000m, inv.TaxableValue);
        Assert.Equal(90m, inv.CgstAmount);
        Assert.Equal(90m, inv.SgstAmount);
        Assert.Equal(0m, inv.IgstAmount);
        Assert.Equal(180m, inv.TotalTax);
        Assert.Equal(1180m, inv.GrandTotal);
    }

    [Fact]
    public void SingleLine_InterState_UsesIgstOnly()
    {
        var inv = Invoice(gst: true, headerDiscount: 0m, Item(1, 1000m, 18m));

        GstCalculator.Recalculate(inv, interState: true);

        Assert.Equal(180m, inv.IgstAmount);
        Assert.Equal(0m, inv.CgstAmount);
        Assert.Equal(0m, inv.SgstAmount);
        Assert.Equal(180m, inv.TotalTax);
        Assert.Equal(1180m, inv.GrandTotal);
    }

    [Fact]
    public void NonGstBill_ChargesNoTax()
    {
        var inv = Invoice(gst: false, headerDiscount: 0m, Item(1, 1000m, 18m));

        GstCalculator.Recalculate(inv, interState: false);

        Assert.Equal(0m, inv.TotalTax);
        Assert.Equal(1000m, inv.TaxableValue);
        Assert.Equal(1000m, inv.GrandTotal);
    }

    [Fact]
    public void HeaderDiscount_IsAppliedBeforeTax_GstCompliant()
    {
        // 17,900 taxable, ₹1,122 invoice-level discount → GST charged on the discounted value.
        var inv = Invoice(gst: true, headerDiscount: 1122m, Item(1, 17900m, 18m));

        GstCalculator.Recalculate(inv, interState: false);

        Assert.Equal(16778m, inv.TaxableValue);
        Assert.Equal(3020.04m, inv.TotalTax);
        Assert.Equal(19798.04m, inv.GrandTotal);
    }

    [Fact]
    public void HeaderDiscount_SpreadProportionally_LinesReconcileToTotal()
    {
        var inv = Invoice(gst: true, headerDiscount: 100m,
            Item(1, 1000m, 18m), Item(1, 333m, 18m));

        GstCalculator.Recalculate(inv, interState: false);

        // 1333 base − 100 discount = 1233 taxable, and the per-line taxables must sum to it exactly.
        Assert.Equal(1233m, inv.TaxableValue);
        Assert.Equal(1233m, inv.Items.Sum(i => i.TaxableValue));
    }

    [Fact]
    public void CgstPlusSgst_AlwaysEqualsTotalTax_EvenWithOddPaise()
    {
        // 5% of 100.10 = 5.005 → 5.01; the half-split must still reconcile to the line tax.
        var inv = Invoice(gst: true, headerDiscount: 0m, Item(1, 100.10m, 5m));

        GstCalculator.Recalculate(inv, interState: false);

        Assert.Equal(5.01m, inv.TotalTax);
        Assert.Equal(inv.TotalTax, inv.CgstAmount + inv.SgstAmount);
    }

    [Fact]
    public void Gst_IsChargedOnceOnTheWholeInvoice_UsingTheFirstLinesRate()
    {
        // The shop uses one GST rate for every service, so tax is computed once on the
        // invoice's total taxable value (not summed per line) — avoids per-line rounding
        // drift and matches how the bill is now shown (no per-line GST% column).
        // If a line's own rate ever differs, the first line's rate wins for the whole invoice.
        var inv = Invoice(gst: true, headerDiscount: 0m,
            Item(1, 1000m, 18m), Item(1, 500m, 5m));

        GstCalculator.Recalculate(inv, interState: false);

        Assert.Equal(1500m, inv.TaxableValue);
        Assert.Equal(270m, inv.TotalTax);           // 18% of 1500, not 180 + 25
        Assert.Equal(1770m, inv.GrandTotal);
    }

    [Fact]
    public void DiscountLargerThanTotal_IsClampedToZero()
    {
        var inv = Invoice(gst: true, headerDiscount: 5000m, Item(1, 1000m, 18m));

        GstCalculator.Recalculate(inv, interState: false);

        Assert.Equal(0m, inv.TaxableValue);
        Assert.Equal(0m, inv.TotalTax);
        Assert.Equal(0m, inv.GrandTotal);
    }

    [Fact]
    public void LineLevelDiscount_ReducesThatLineBeforeTax()
    {
        var inv = Invoice(gst: true, headerDiscount: 0m, Item(1, 1000m, 18m, lineDiscount: 200m));

        GstCalculator.Recalculate(inv, interState: false);

        Assert.Equal(800m, inv.TaxableValue);
        Assert.Equal(144m, inv.TotalTax);          // 18% of 800
        Assert.Equal(944m, inv.GrandTotal);
    }

    [Fact]
    public void NoItems_ProducesZeroTotals()
    {
        var inv = Invoice(gst: true, headerDiscount: 0m);

        GstCalculator.Recalculate(inv, interState: false);

        Assert.Equal(0m, inv.SubTotal);
        Assert.Equal(0m, inv.TaxableValue);
        Assert.Equal(0m, inv.TotalTax);
        Assert.Equal(0m, inv.GrandTotal);
    }

    [Fact]
    public void Quantity_MultipliesLineBase()
    {
        var inv = Invoice(gst: true, headerDiscount: 0m, Item(3, 500m, 18m));

        GstCalculator.Recalculate(inv, interState: false);

        Assert.Equal(1500m, inv.TaxableValue);     // 3 × 500
        Assert.Equal(270m, inv.TotalTax);
        Assert.Equal(1770m, inv.GrandTotal);
    }

    [Fact]
    public void GstComputedAfterHeaderDiscount_MatchesMobileRecalc()
    {
        // Mirrors the fix applied to the mobile Recalc():
        //   subTotal=6000, headerDiscount=1000 → taxable=5000, GST 18%=900, grand=5900
        var inv = Invoice(gst: true, headerDiscount: 1000m, Item(1, 6000m, 18m));

        GstCalculator.Recalculate(inv, interState: false);

        Assert.Equal(5000m, inv.TaxableValue);
        Assert.Equal(900m, inv.TotalTax);
        Assert.Equal(5900m, inv.GrandTotal);
    }

    [Fact]
    public void LineItemDiscountAndHeaderDiscount_BothDeductedBeforeGst()
    {
        // Service = 10000, line discount = 1000 → line base 9000
        // Header discount 500 → taxable 8500, GST 18% = 1530, grand = 10030
        var inv = Invoice(gst: true, headerDiscount: 500m, Item(1, 10000m, 18m, lineDiscount: 1000m));

        GstCalculator.Recalculate(inv, interState: false);

        Assert.Equal(8500m, inv.TaxableValue);
        Assert.Equal(1530m, inv.TotalTax);
        Assert.Equal(10030m, inv.GrandTotal);
    }

    [Fact]
    public void ItemsWithZeroGstRate_ProduceZeroTax()
    {
        var inv = Invoice(gst: true, headerDiscount: 0m, Item(2, 1500m, 0m));

        GstCalculator.Recalculate(inv, interState: false);

        Assert.Equal(3000m, inv.TaxableValue);
        Assert.Equal(0m, inv.TotalTax);
        Assert.Equal(3000m, inv.GrandTotal);
    }
}
