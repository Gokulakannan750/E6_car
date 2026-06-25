namespace E6CarSpa.Contracts;

public record DailySalesRow(DateTime Date, int InvoiceCount, decimal GrandTotal, decimal Collected);

public record TopServiceRow(string Name, decimal Quantity, decimal Amount);

/// <summary>Sales for a date range (invoices that have been finalised: Invoiced or Paid).</summary>
public record SalesReportDto(
    DateTime From, DateTime To,
    int InvoiceCount,
    decimal TaxableValue, decimal Cgst, decimal Sgst, decimal Igst, decimal TotalTax, decimal GrandTotal,
    decimal Collected, decimal Cash, decimal Card, decimal Upi, decimal Outstanding,
    List<DailySalesRow> Daily, List<TopServiceRow> TopServices);

/// <summary>Output GST grouped by rate — for GST return filing.</summary>
public record GstRateRow(decimal GstRate, decimal TaxableValue, decimal Cgst, decimal Sgst, decimal Igst, decimal Total);

public record GstSummaryDto(
    DateTime From, DateTime To, List<GstRateRow> Rows, decimal TotalTaxable, decimal TotalTax);

/// <summary>A customer's full visit history with lifetime totals.</summary>
public record CustomerHistoryDto(
    Guid CustomerId, string Name, string Phone, int VisitCount,
    decimal LifetimeValue, decimal Outstanding, List<InvoiceListItemDto> Invoices);
