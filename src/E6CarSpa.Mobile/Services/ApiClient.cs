using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using E6CarSpa.Contracts;
using E6CarSpa.Domain.Enums;
using E6CarSpa.Mobile.Services;

namespace E6CarSpa.Mobile.Services;

/// <summary>
/// Thin typed wrapper over the E6 Car Spa Web API. Holds the JWT after login and attaches
/// it to every request. All calls throw <see cref="ApiException"/> on non-success responses.
/// </summary>
public class ApiClient : IApiClient
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    // Owned (not DI-injected) so the base URL can be changed at runtime from the Settings screen.
    private HttpClient http;

    public ApiClient() => http = Build(Settings.ApiUrl);

    private static HttpClient Build(string baseUrl) =>
        new() { BaseAddress = new Uri(baseUrl), Timeout = TimeSpan.FromSeconds(30) };

    /// <summary>Rebuild the underlying client against a new server URL, preserving any auth header.</summary>
    public void SetBaseUrl(string baseUrl)
    {
        var auth = http.DefaultRequestHeaders.Authorization;
        http = Build(baseUrl);
        http.DefaultRequestHeaders.Authorization = auth;
    }

    public UserDto? CurrentUser { get; private set; }
    public bool IsLoggedIn => CurrentUser is not null;

    // ---------- Auth ----------
    public async Task<UserDto> LoginAsync(string username, string password)
    {
        var resp = await PostAsync<LoginResponse>("api/auth/login", new LoginRequest(username, password));
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", resp.Token);
        CurrentUser = resp.User;
        return resp.User;
    }

    public void Logout()
    {
        http.DefaultRequestHeaders.Authorization = null;
        CurrentUser = null;
    }

    // ---------- Intake / customers ----------
    public Task<CustomerLookupResult?> LookupByPhoneAsync(string phone) =>
        GetAsync<CustomerLookupResult>($"api/customers/by-phone/{Uri.EscapeDataString(phone)}");

    public Task<CustomerLookupResult?> LookupByCarAsync(string car) =>
        GetAsync<CustomerLookupResult>($"api/customers/by-car/{Uri.EscapeDataString(car)}");

    public Task<List<CustomerDto>?> GetCustomersAsync(string? search = null)
    {
        var qs = string.IsNullOrWhiteSpace(search) ? "" : $"?q={Uri.EscapeDataString(search)}";
        return GetAsync<List<CustomerDto>>($"api/customers{qs}");
    }

    // ---------- Catalog ----------
    public Task<List<ServiceDto>?> GetServicesAsync(bool includeInactive = false) =>
        GetAsync<List<ServiceDto>>($"api/services?includeInactive={includeInactive}");

    public Task<ServiceDto> CreateServiceAsync(SaveServiceRequest req) =>
        PostAsync<ServiceDto>("api/services", req);

    public Task<ServiceDto> UpdateServiceAsync(Guid id, SaveServiceRequest req) =>
        PutAsync<ServiceDto>($"api/services/{id}", req);

    public Task<ProductDto> CreateProductAsync(SaveProductRequest req) =>
        PostAsync<ProductDto>("api/products", req);

    public Task<ProductDto> UpdateProductAsync(Guid id, SaveProductRequest req) =>
        PutAsync<ProductDto>($"api/products/{id}", req);

    public Task<List<BomItemDto>?> GetBomAsync(Guid serviceId) =>
        GetAsync<List<BomItemDto>>($"api/services/{serviceId}/bom");

    public Task<List<BomItemDto>> SaveBomAsync(Guid serviceId, SaveBomRequest req) =>
        PutAsync<List<BomItemDto>>($"api/services/{serviceId}/bom", req);

    // ---------- Invoices ----------
    public Task<InvoiceDto> CreateQuotationAsync(CreateQuotationRequest req) =>
        PostAsync<InvoiceDto>("api/invoices/quotation", req);

    public Task<List<InvoiceListItemDto>?> ListInvoicesAsync(InvoiceStatus? status, string? search)
    {
        var q = new List<string>();
        if (status.HasValue) q.Add($"status={status.Value}");
        if (!string.IsNullOrWhiteSpace(search)) q.Add($"search={Uri.EscapeDataString(search)}");
        var qs = q.Count > 0 ? "?" + string.Join("&", q) : "";
        return GetAsync<List<InvoiceListItemDto>>($"api/invoices{qs}");
    }

    public Task<InvoiceDto?> GetInvoiceAsync(Guid id) => GetAsync<InvoiceDto>($"api/invoices/{id}");

    public Task<InvoiceDto> UpdateInvoiceAsync(Guid id, UpdateInvoiceRequest req) =>
        PutAsync<InvoiceDto>($"api/invoices/{id}", req);

    public Task<InvoiceDto> FinaliseAsync(Guid id) =>
        PostAsync<InvoiceDto>($"api/invoices/{id}/finalise", new { });

    public Task<InvoiceDto> PayAsync(Guid id, RecordPaymentRequest req) =>
        PostAsync<InvoiceDto>($"api/invoices/{id}/payments", req);

    public async Task<byte[]> GetInvoicePdfAsync(Guid id)
    {
        var resp = await http.GetAsync($"api/invoices/{id}/pdf");
        await EnsureSuccess(resp);
        return await resp.Content.ReadAsByteArrayAsync();
    }

    // ---------- Inventory ----------
    public Task<List<ProductDto>?> GetProductsAsync(bool lowStockOnly = false) =>
        GetAsync<List<ProductDto>>($"api/products?lowStockOnly={lowStockOnly}");

    public Task<ProductDto> PurchaseAsync(StockPurchaseRequest req) =>
        PostAsync<ProductDto>("api/products/purchase", req);

    public Task<ProductDto> AdjustAsync(StockAdjustmentRequest req) =>
        PostAsync<ProductDto>("api/products/adjust", req);

    // ---------- Dashboard ----------
    public Task<DashboardSummaryDto?> GetDashboardAsync() => GetAsync<DashboardSummaryDto>("api/dashboard");

    // ---------- Users ----------
    public Task<List<UserDto>?> GetUsersAsync() => GetAsync<List<UserDto>>("api/auth/users");

    public Task<UserDto> CreateUserAsync(CreateUserRequest req) =>
        PostAsync<UserDto>("api/auth/users", req);

    public Task<UserDto> UpdateUserAsync(Guid id, UpdateUserRequest req) =>
        PutAsync<UserDto>($"api/auth/users/{id}", req);

    public Task ChangeMyPasswordAsync(ChangeMyPasswordRequest req) =>
        PutAsync<object>("api/auth/users/me/password", req);

    // ---------- Company settings ----------
    public Task<CompanySettingsDto?> GetSettingsAsync() => GetAsync<CompanySettingsDto>("api/settings");

    public Task<CompanySettingsDto> UpdateSettingsAsync(SaveCompanySettingsRequest req) =>
        PutAsync<CompanySettingsDto>("api/settings", req);

    public async Task UploadLogoAsync(byte[] imageBytes)
    {
        var resp = await http.PutAsJsonAsync("api/settings/logo",
            new UploadLogoRequest(Convert.ToBase64String(imageBytes)), JsonOpts);
        await EnsureSuccess(resp);
    }

    public async Task<byte[]?> GetLogoAsync()
    {
        var resp = await http.GetAsync("api/settings/logo");
        if (resp.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        await EnsureSuccess(resp);
        return await resp.Content.ReadAsByteArrayAsync();
    }

    public async Task DeleteLogoAsync()
    {
        var resp = await http.DeleteAsync("api/settings/logo");
        await EnsureSuccess(resp);
    }

    // ---------- Reports ----------
    public Task<SalesReportDto?> GetSalesReportAsync(DateTime from, DateTime to) =>
        GetAsync<SalesReportDto>($"api/reports/sales?from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}");

    public Task<GstSummaryDto?> GetGstSummaryAsync(DateTime from, DateTime to) =>
        GetAsync<GstSummaryDto>($"api/reports/gst?from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}");

    public Task<CustomerHistoryDto?> GetCustomerHistoryAsync(string phone) =>
        GetAsync<CustomerHistoryDto>($"api/reports/customer?phone={Uri.EscapeDataString(phone)}");

    // ---------- Plumbing ----------
    private async Task<T?> GetAsync<T>(string url)
    {
        var resp = await http.GetAsync(url);
        await EnsureSuccess(resp);
        return await resp.Content.ReadFromJsonAsync<T>(JsonOpts);
    }

    private async Task<T> PostAsync<T>(string url, object body)
    {
        var resp = await http.PostAsJsonAsync(url, body, JsonOpts);
        await EnsureSuccess(resp);
        return (await resp.Content.ReadFromJsonAsync<T>(JsonOpts))!;
    }

    private async Task<T> PutAsync<T>(string url, object body)
    {
        var resp = await http.PutAsJsonAsync(url, body, JsonOpts);
        await EnsureSuccess(resp);
        return (await resp.Content.ReadFromJsonAsync<T>(JsonOpts))!;
    }

    public event Action? OnUnauthorized;

    private async Task EnsureSuccess(HttpResponseMessage resp)
    {
        if (resp.IsSuccessStatusCode) return;
        
        if (resp.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            OnUnauthorized?.Invoke();

        var body = await resp.Content.ReadAsStringAsync();
        string message = $"Request failed ({(int)resp.StatusCode}).";
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("detail", out var d) && d.GetString() is { } s) message = s;
            else if (doc.RootElement.TryGetProperty("message", out var m) && m.GetString() is { } s2) message = s2;
        }
        catch { /* keep default */ }
        throw new ApiException(message, resp.StatusCode);
    }
}

public class ApiException(string message, System.Net.HttpStatusCode statusCode) : Exception(message)
{
    public System.Net.HttpStatusCode StatusCode { get; } = statusCode;
}
