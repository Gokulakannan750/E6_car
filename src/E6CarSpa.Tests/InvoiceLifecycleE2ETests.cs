using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using E6CarSpa.Contracts;
using E6CarSpa.Domain.Enums;

namespace E6CarSpa.Tests;

/// <summary>
/// End-to-end tests over the full invoice lifecycle via the real HTTP API pipeline
/// (in-memory DB, TestServer). Covers: create quotation → finalise → discount edit
/// → payment → paid-lock, reports access, and data consistency.
/// </summary>
public class InvoiceLifecycleE2ETests
{
    // ── helpers ──────────────────────────────────────────────────────────────

    private static async Task<string> LoginAsync(HttpClient client, string user, string pass)
    {
        var resp = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(user, pass));
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<LoginResponse>())!.Token;
    }

    private static void Auth(HttpClient client, string token) =>
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

    private static async Task<(ApiFactory factory, HttpClient client)> SetupAsync()
    {
        var factory = new ApiFactory();
        var client  = factory.CreateClient();
        Auth(client, await LoginAsync(client, "admin", "admin@123"));
        return (factory, client);
    }

    private static CreateQuotationRequest SimpleQuote(
        string  phone = "9800000001",
        string  car   = "TN01 AA 0001",
        decimal price = 6000m,
        bool    gst   = true) =>
        new(CustomerName:   "Test Customer",
            CustomerPhone:  phone,
            CarNumber:      car,
            CarModel:       "Test Car",
            DiscountAmount: 0m,
            Notes:          null,
            Items: new List<InvoiceItemInput>
            {
                new(null, null, "Window Tinting / Sun Film", 1, price, 0m)
            },
            ApplyGst: gst);

    // Create a quotation and return the InvoiceDto
    private static async Task<InvoiceDto> CreateQuotationAsync(HttpClient client, CreateQuotationRequest req)
    {
        var resp = await client.PostAsJsonAsync("/api/invoices/quotation", req);
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<InvoiceDto>())!;
    }

    private static async Task<InvoiceDto> FinaliseAsync(HttpClient client, Guid id)
    {
        var resp = await client.PostAsync($"/api/invoices/{id}/finalise", null);
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<InvoiceDto>())!;
    }

    private static async Task<InvoiceDto> PayAsync(HttpClient client, Guid id, decimal amount,
        PaymentMethod method = PaymentMethod.Cash, string? reference = null)
    {
        var resp = await client.PostAsJsonAsync($"/api/invoices/{id}/payments",
            new RecordPaymentRequest(method, amount, reference));
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<InvoiceDto>())!;
    }

    // ── 1. Full happy-path ────────────────────────────────────────────────────

    [Fact]
    public async Task FullLifecycle_CreateFinalisePayInFull_StatusIsPaid()
    {
        var (factory, client) = await SetupAsync();
        using var _ = factory;

        // Create quotation
        var invoice = await CreateQuotationAsync(client, SimpleQuote());
        Assert.Equal(InvoiceStatus.Quotation, invoice.Status);
        Assert.Null(invoice.InvoiceNumber);

        // Finalise → assigned invoice number, GST: 18% of 6000 = 1080
        var finalised = await FinaliseAsync(client, invoice.Id);
        Assert.NotNull(finalised.InvoiceNumber);
        Assert.Equal(InvoiceStatus.Invoiced, finalised.Status);
        Assert.Equal(6000m, finalised.TaxableValue);
        Assert.Equal(1080m, finalised.TotalTax);
        Assert.Equal(7080m, finalised.GrandTotal);

        // Pay in full
        var paid = await PayAsync(client, finalised.Id, 7080m);
        Assert.Equal(InvoiceStatus.Paid, paid.Status);
        Assert.Equal(0m, paid.Balance);
        Assert.NotNull(paid.CompletedAt);
    }

    // ── 2. GST on discounted amount ───────────────────────────────────────────

    [Fact]
    public async Task Finalise_WithHeaderDiscount_GstOnDiscountedAmount()
    {
        var (factory, client) = await SetupAsync();
        using var _ = factory;

        // Price 6000, discount 1000 → taxable 5000, GST 18% of 5000 = 900, grand 5900
        var invoice = await CreateQuotationAsync(client,
            new CreateQuotationRequest("Cust", "9800000002", "TN02 BB 2222", "Car",
                DiscountAmount: 1000m, Notes: null,
                Items: new List<InvoiceItemInput> { new(null, null, "Ceramic Coating", 1, 6000m, 0m) },
                ApplyGst: true));

        var finalised = await FinaliseAsync(client, invoice.Id);

        Assert.Equal(5000m, finalised.TaxableValue);
        Assert.Equal(900m,  finalised.TotalTax);
        Assert.Equal(5900m, finalised.GrandTotal);
    }

    // ── 3. After finalise: discount edit succeeds ─────────────────────────────

    [Fact]
    public async Task AfterFinalise_UpdateDiscountOnly_Succeeds()
    {
        var (factory, client) = await SetupAsync();
        using var _ = factory;

        var invoice = await CreateQuotationAsync(client, SimpleQuote(price: 5000m));
        await FinaliseAsync(client, invoice.Id);

        // Update discount on Invoiced (unpaid) invoice
        var updateResp = await client.PutAsJsonAsync($"/api/invoices/{invoice.Id}",
            new UpdateInvoiceRequest(
                DiscountAmount: 500m,
                Notes: "Loyalty discount",
                Items: invoice.Items.Select(i => new InvoiceItemInput(
                    i.ServiceId, i.ProductId, i.Description,
                    i.Quantity, i.UnitPrice, i.DiscountAmount)).ToList(),
                ApplyGst: true));
        updateResp.EnsureSuccessStatusCode();
        var updated = await updateResp.Content.ReadFromJsonAsync<InvoiceDto>();

        // Taxable 4500, GST 810, grand 5310
        Assert.Equal(500m,  updated!.DiscountAmount);
        Assert.Equal(4500m, updated.TaxableValue);
        Assert.Equal(810m,  updated.TotalTax);
        Assert.Equal(5310m, updated.GrandTotal);
    }

    // ── 4. After full payment: edits rejected ────────────────────────────────

    [Fact]
    public async Task AfterPaidInFull_UpdateAttempt_ReturnsBadRequest()
    {
        var (factory, client) = await SetupAsync();
        using var _ = factory;

        var invoice = await CreateQuotationAsync(client, SimpleQuote(price: 1000m, gst: false));
        await FinaliseAsync(client, invoice.Id);
        await PayAsync(client, invoice.Id, 1000m, PaymentMethod.Upi, "TXN123");

        var updateResp = await client.PutAsJsonAsync($"/api/invoices/{invoice.Id}",
            new UpdateInvoiceRequest(200m, null,
                invoice.Items.Select(i => new InvoiceItemInput(
                    i.ServiceId, i.ProductId, i.Description,
                    i.Quantity, i.UnitPrice, i.DiscountAmount)).ToList(),
                false));

        Assert.Equal(HttpStatusCode.BadRequest, updateResp.StatusCode);
    }

    // ── 5. Part-payment: balance reduced, stays Invoiced ─────────────────────

    [Fact]
    public async Task PartPayment_BalanceReduced_StatusRemainsInvoiced()
    {
        var (factory, client) = await SetupAsync();
        using var _ = factory;

        var invoice = await CreateQuotationAsync(client, SimpleQuote(price: 2000m, gst: false));
        await FinaliseAsync(client, invoice.Id);

        var after = await PayAsync(client, invoice.Id, 1000m, PaymentMethod.Card);

        Assert.Equal(InvoiceStatus.Invoiced, after.Status);
        Assert.Equal(1000m, after.Balance);
        Assert.Null(after.CompletedAt);
    }

    // ── 6. Non-GST invoice: zero tax ─────────────────────────────────────────

    [Fact]
    public async Task NonGstInvoice_Finalised_ZeroTaxGrandEqualsSubTotal()
    {
        var (factory, client) = await SetupAsync();
        using var _ = factory;

        var invoice = await CreateQuotationAsync(client, SimpleQuote(price: 3500m, gst: false));
        var finalised = await FinaliseAsync(client, invoice.Id);

        Assert.Equal(0m,    finalised.TotalTax);
        Assert.Equal(3500m, finalised.GrandTotal);
        Assert.Equal(3500m, finalised.TaxableValue);
    }

    // ── 7. Reports: all roles can access ─────────────────────────────────────

    [Fact]
    public async Task Reports_AsManager_ReturnsOk()
    {
        using var factory = new ApiFactory();
        var adminClient = factory.CreateClient();
        Auth(adminClient, await LoginAsync(adminClient, "admin", "admin@123"));

        (await adminClient.PostAsJsonAsync("/api/auth/users",
            new CreateUserRequest("Store Manager", "mgr1e2e", "mgr@1234", UserRole.Manager)))
            .EnsureSuccessStatusCode();

        var mgrClient = factory.CreateClient();
        Auth(mgrClient, await LoginAsync(mgrClient, "mgr1e2e", "mgr@1234"));

        var resp = await mgrClient.GetAsync("/api/reports/sales?from=2026-01-01&to=2026-12-31");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task Reports_Unauthenticated_ReturnsUnauthorized()
    {
        using var factory = new ApiFactory();
        var resp = await factory.CreateClient()
            .GetAsync("/api/reports/sales?from=2026-01-01&to=2026-12-31");
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    // ── 8. Reports data: finalised invoice counted ────────────────────────────

    [Fact]
    public async Task Reports_AfterFinalisingInvoice_GrandTotalReflectsIt()
    {
        var (factory, client) = await SetupAsync();
        using var _ = factory;

        var invoice = await CreateQuotationAsync(client, SimpleQuote(price: 5000m, gst: false));
        await FinaliseAsync(client, invoice.Id);

        var from = DateTime.Today.ToString("yyyy-MM-dd");
        var to   = DateTime.Today.ToString("yyyy-MM-dd");
        var reportResp = await client.GetAsync($"/api/reports/sales?from={from}&to={to}");
        reportResp.EnsureSuccessStatusCode();
        var report = await reportResp.Content.ReadFromJsonAsync<SalesReportDto>();

        Assert.NotNull(report);
        Assert.Equal(1,     report!.InvoiceCount);
        Assert.Equal(5000m, report.GrandTotal);
        Assert.Equal(0m,    report.TotalTax);
    }

    // ── 9. Idempotent finalise ────────────────────────────────────────────────

    [Fact]
    public async Task Finalise_CalledTwice_SameInvoiceNumber()
    {
        var (factory, client) = await SetupAsync();
        using var _ = factory;

        var invoice = await CreateQuotationAsync(client, SimpleQuote());

        var first  = await FinaliseAsync(client, invoice.Id);
        var second = await FinaliseAsync(client, invoice.Id);

        Assert.Equal(first.InvoiceNumber, second.InvoiceNumber);
    }

    // ── 10. Same phone → same customer record ─────────────────────────────────

    [Fact]
    public async Task TwoInvoices_SamePhone_ShareCustomer()
    {
        var (factory, client) = await SetupAsync();
        using var _ = factory;

        var inv1 = await CreateQuotationAsync(client,
            SimpleQuote(phone: "9900001111", car: "TN01 X 1"));
        var inv2 = await CreateQuotationAsync(client,
            SimpleQuote(phone: "9900001111", car: "TN01 X 2"));

        Assert.Equal(inv1.CustomerId, inv2.CustomerId);
    }
}
