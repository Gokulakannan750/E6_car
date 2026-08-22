using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using E6CarSpa.Contracts;
using E6CarSpa.Domain.Enums;

namespace E6CarSpa.Client;

/// <summary>
/// Thin typed wrapper over the E6 Car Spa Web API, shared by the desktop and mobile apps.
/// Holds the JWT after login and attaches it to every request. All calls throw
/// <see cref="ApiException"/> on non-success responses.
/// </summary>
public class ApiClient(HttpClient http) : IApiClient
{
 private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

 private HttpClient http = http;

 public void SetBaseUrl(string baseUrl)
 {
 var old = http;
 http = new HttpClient { BaseAddress = new Uri(baseUrl), Timeout = old.Timeout };
 http.DefaultRequestHeaders.Authorization = old.DefaultRequestHeaders.Authorization;
 old.Dispose();
 }

 public UserDto? CurrentUser { get; private set; }
 public bool IsLoggedIn => CurrentUser is not null;
 public string? CurrentToken => http.DefaultRequestHeaders.Authorization?.Parameter;
 public bool MustChangePassword { get; private set; }

 // ---------- Auth ----------
 public async Task<UserDto> LoginAsync(string username, string password)
 {
 var resp = await PostAsync<LoginResponse>("api/auth/login", new LoginRequest(username, password));
 http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", resp.Token);
 CurrentUser = resp.User;
 MustChangePassword = resp.MustChangePassword;
 return resp.User;
 }

 public void Logout()
 {
 http.DefaultRequestHeaders.Authorization = null;
 CurrentUser = null;
 MustChangePassword = false;
 OnLogout?.Invoke();
 }

 public void RestoreSession(string token, UserDto user, bool mustChangePassword)
 {
 http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
 CurrentUser = user;
 MustChangePassword = mustChangePassword;
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

 public Task<InvoiceDto> CancelInvoiceAsync(Guid id) =>
 PostAsync<InvoiceDto>($"api/invoices/{id}/cancel", new { });

 public Task<InvoiceDto> PayAsync(Guid id, RecordPaymentRequest req) =>
 PostAsync<InvoiceDto>($"api/invoices/{id}/payments", req);

 public async Task<byte[]> GetInvoicePdfAsync(Guid id)
 {
 var resp = await http.GetAsync($"api/invoices/{id}/pdf");
 await EnsureSuccess(resp);
 return await resp.Content.ReadAsByteArrayAsync();
 }

 public async Task<byte[]> GetJobCardPdfAsync(Guid id)
 {
 var resp = await http.GetAsync($"api/invoices/{id}/jobcard");
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

 // ---------- Staff advances ----------
 public Task<List<StaffAdvanceDto>?> GetStaffAdvancesAsync(string? worker = null, bool includeDeleted = false)
 {
 var parts = new List<string>();
 if (!string.IsNullOrWhiteSpace(worker)) parts.Add($"worker={Uri.EscapeDataString(worker)}");
 if (includeDeleted) parts.Add("includeDeleted=true");
 var qs = parts.Count == 0 ? "" : "?" + string.Join("&", parts);
 return GetAsync<List<StaffAdvanceDto>>($"api/staffadvances{qs}");
 }

 public Task<List<StaffAdvanceSummaryDto>?> GetStaffAdvanceSummaryAsync() =>
 GetAsync<List<StaffAdvanceSummaryDto>>("api/staffadvances/summary");

 public Task<StaffAdvanceDto> CreateStaffAdvanceAsync(SaveStaffAdvanceRequest req) =>
 PostAsync<StaffAdvanceDto>("api/staffadvances", req);

 public async Task DeleteStaffAdvanceAsync(Guid id)
 {
 var resp = await http.DeleteAsync($"api/staffadvances/{id}");
 await EnsureSuccess(resp);
 }

 // ---------- Staff master -----
 public Task<List<StaffDto>?> GetStaffAsync(bool includeInactive = false) =>
 GetAsync<List<StaffDto>>($"api/staff?includeInactive={includeInactive}");

 public Task<List<StaffDto>?> GetStaffForPickerAsync() =>
 GetAsync<List<StaffDto>>("api/staff/picker");

 public Task<StaffDto> CreateStaffAsync(SaveStaffRequest req) =>
 PostAsync<StaffDto>("api/staff", req);

 public Task<StaffDto> UpdateStaffAsync(Guid id, SaveStaffRequest req) =>
 PutAsync<StaffDto>($"api/staff/{id}", req);

 public async Task DeleteStaffAsync(Guid id)
 {
 var resp = await http.DeleteAsync($"api/staff/{id}");
 await EnsureSuccess(resp);
 }

 public async Task RestoreStaffAsync(Guid id)
 {
 var resp = await http.PostAsync($"api/staff/{id}/restore", null);
 await EnsureSuccess(resp);
 }

 // ---------- Income ----------
 public Task<List<IncomeDto>?> GetIncomeAsync(string? source = null, bool includeDeleted = false)
 {
 var parts = new List<string>();
 if (!string.IsNullOrWhiteSpace(source)) parts.Add($"source={Uri.EscapeDataString(source)}");
 if (includeDeleted) parts.Add("includeDeleted=true");
 var qs = parts.Count == 0 ? "" : "?" + string.Join("&", parts);
 return GetAsync<List<IncomeDto>>($"api/income{qs}");
 }

 public Task<List<IncomeSummaryDto>?> GetIncomeSummaryAsync(DateTime? from = null, DateTime? to = null)
 {
 var parts = new List<string>();
 if (from.HasValue) parts.Add($"from={from.Value:yyyy-MM-dd}");
 if (to.HasValue) parts.Add($"to={to.Value:yyyy-MM-dd}");
 var qs = parts.Count == 0 ? "" : "?" + string.Join("&", parts);
 return GetAsync<List<IncomeSummaryDto>>($"api/income/summary{qs}");
 }

 public Task<IncomeDto> CreateIncomeAsync(SaveIncomeRequest req) =>
 PostAsync<IncomeDto>("api/income", req);

 public async Task DeleteIncomeAsync(Guid id)
 {
 var resp = await http.DeleteAsync($"api/income/{id}");
 await EnsureSuccess(resp);
 }

 // ---------- Staff Salary ----------
 public Task<List<StaffSalaryDto>?> GetStaffSalariesAsync(Guid? staffId = null, bool includeDeleted = false)
 {
 var parts = new List<string>();
 if (staffId.HasValue) parts.Add($"staffId={staffId.Value}");
 if (includeDeleted) parts.Add("includeDeleted=true");
 var qs = parts.Count == 0 ? "" : "?" + string.Join("&", parts);
 return GetAsync<List<StaffSalaryDto>>($"api/staffsalaries{qs}");
 }

 public Task<List<StaffSalarySummaryDto>?> GetStaffSalarySummaryAsync() =>
 GetAsync<List<StaffSalarySummaryDto>>("api/staffsalaries/summary");

 public Task<StaffSalaryDto> CreateStaffSalaryAsync(SaveStaffSalaryRequest req) =>
 PostAsync<StaffSalaryDto>("api/staffsalaries", req);

 public async Task DeleteStaffSalaryAsync(Guid id)
 {
 var resp = await http.DeleteAsync($"api/staffsalaries/{id}");
 await EnsureSuccess(resp);
 }

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

 // ----- Showrooms -----
 public Task<List<ShowroomDto>?> GetShowroomsAsync(bool includeInactive = false)
 {
 var qs = includeInactive ? "?includeInactive=true" : "";
 return GetAsync<List<ShowroomDto>>($"api/showrooms{qs}");
 }

 public Task<List<ShowroomPickDto>?> GetShowroomsForPickerAsync()
 => GetAsync<List<ShowroomPickDto>>("api/showrooms/picker");

 public Task<ShowroomDto> CreateShowroomAsync(SaveShowroomRequest req)
 => PostAsync<ShowroomDto>("api/showrooms", req);

 public Task<ShowroomDto> UpdateShowroomAsync(Guid id, SaveShowroomRequest req)
 => PutAsync<ShowroomDto>($"api/showrooms/{id}", req);

 public async Task DeactivateShowroomAsync(Guid id)
 {
 var resp = await http.DeleteAsync($"api/showrooms/{id}");
 await EnsureSuccess(resp);
 }

 public async Task RestoreShowroomAsync(Guid id)
 {
 var resp = await http.PostAsync($"api/showrooms/{id}/restore", null);
 await EnsureSuccess(resp);
 }

 // ----- Daily assignments -----
 public Task<List<ShowroomDailyStaffDto>?> GetDailyAssignmentsByDateAsync(DateTime date)
 => GetAsync<List<ShowroomDailyStaffDto>>($"api/showroom-daily/by-date?date={date:yyyy-MM-dd}");

 public Task<ShowroomDailyStaffDto> CreateDailyAssignmentAsync(SaveShowroomDailyStaffRequest req)
 => PostAsync<ShowroomDailyStaffDto>("api/showroom-daily", req);

 public Task<ShowroomDailyStaffDto> UpdateDailyAssignmentAsync(Guid id, SaveShowroomDailyStaffRequest req)
 => PutAsync<ShowroomDailyStaffDto>($"api/showroom-daily/{id}", req);

 public async Task DeleteDailyAssignmentAsync(Guid id)
 {
 var resp = await http.DeleteAsync($"api/showroom-daily/{id}");
 await EnsureSuccess(resp);
 }

 // ----- Performance -----
 public Task<ShowroomPerformanceDto?> GetShowroomPerformanceAsync(Guid showroomId, DateTime from, DateTime to)
 => GetAsync<ShowroomPerformanceDto>($"api/showroom-daily/performance/{showroomId}?from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}");

 public Task<List<StaffPerformanceDto>?> GetShowroomPerformanceByStaffAsync(Guid showroomId, DateTime from, DateTime to)
 => GetAsync<List<StaffPerformanceDto>>($"api/showroom-daily/performance/{showroomId}/staff?from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}");

 public Task<List<DailyShowroomSummaryDto>?> GetShowroomDailyBreakdownAsync(Guid showroomId, DateTime from, DateTime to)
 => GetAsync<List<DailyShowroomSummaryDto>>($"api/showroom-daily/performance/{showroomId}/daily?from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}");

 // ----- Reports -----
 public Task<List<ShowroomReportRowDto>?> GetShowroomReportAsync(DateTime from, DateTime to, Guid? showroomId = null, Guid? staffId = null)
 {
 var qs = new List<string> { $"from={from:yyyy-MM-dd}", $"to={to:yyyy-MM-dd}" };
 if (showroomId.HasValue) qs.Add($"showroomId={showroomId.Value}");
 if (staffId.HasValue) qs.Add($"staffId={staffId.Value}");
 return GetAsync<List<ShowroomReportRowDto>>($"api/showroom-daily/report?{string.Join("&", qs)}");
 }

 public Task<ShowroomReportSummaryDto?> GetShowroomReportSummaryAsync(DateTime from, DateTime to, Guid? showroomId = null, Guid? staffId = null)
 {
 var qs = new List<string> { $"from={from:yyyy-MM-dd}", $"to={to:yyyy-MM-dd}" };
 if (showroomId.HasValue) qs.Add($"showroomId={showroomId.Value}");
 if (staffId.HasValue) qs.Add($"staffId={staffId.Value}");
 return GetAsync<ShowroomReportSummaryDto>($"api/showroom-daily/report/summary?{string.Join("&", qs)}");
 }

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
 public event Action? OnLogout;

 private async Task EnsureSuccess(HttpResponseMessage resp)
 {
 if (resp.IsSuccessStatusCode) return;

 if (resp.StatusCode == System.Net.HttpStatusCode.Unauthorized && CurrentUser is not null)
 {
 Logout();
 OnUnauthorized?.Invoke();
 }

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
