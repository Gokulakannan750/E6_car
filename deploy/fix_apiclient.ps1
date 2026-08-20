$file = "src/E6CarSpa.Client/ApiClient.cs"
$c = Get-Content $file -Raw

# Add customer notes methods before "// ---------- Plumbing ----------"
$c = $c -replace '( // -{2} Reports -{2}-\r\n)', @'
 // ---------- Reports ----------
 public Task<SalesReportDto?> GetSalesReportAsync(DateTime from, DateTime to) =>
 GetAsync<SalesReportDto>($"api/reports/sales?from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}");

 public Task<GstSummaryDto?> GetGstSummaryAsync(DateTime from, DateTime to) =>
 GetAsync<GstSummaryDto>($"api/reports/gst?from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}");

 public Task<CustomerHistoryDto?> GetCustomerHistoryAsync(string phone) =>
 GetAsync<CustomerHistoryDto>($"api/reports/customer?phone={Uri.EscapeDataString(phone)}");

 // ---------- Customer notes ----------
 public Task<List<CustomerNoteDto>?> GetCustomerNotesAsync(Guid customerId) =>
 GetAsync<List<CustomerNoteDto>>($"api/customers/{customerId}/notes");

 public Task<CustomerNoteDto> CreateCustomerNoteAsync(Guid customerId, string text) =>
 PostAsync<CustomerNoteDto>($"api/customers/{customerId}/notes", new { text });

 public Task<CustomerNoteDto> UpdateCustomerNoteAsync(Guid customerId, Guid noteId, string text) =>
 PutAsync<CustomerNoteDto>($"api/customers/{customerId}/notes/{noteId}", new { text });

 public async Task DeleteCustomerNoteAsync(Guid customerId, Guid noteId)
 {
 var resp = await http.DeleteAsync($"api/customers/{customerId}/notes/{noteId}");
 await EnsureSuccess(resp);
 }

 // ---------- Plumbing ----------
'@

Set-Content $file $c
Write-Host "Done"
