import re

filepath = r"src/E6CarSpa.Client/ApiClient.cs"

with open(filepath, "r", encoding="utf-8") as f:
 content = f.read()

# Check what's there
if "GetCustomerNotesAsync" in content:
 print("Methods already exist!")
else:
 print("Methods missing - need to add them")

# Find the insertion point: right before "// ---------- Plumbing ----------"
insert_marker = "// ---------- Plumbing ----------"

notes_methods = """// ---------- Customer notes ----------
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

"""

if insert_marker in content:
 content = content.replace(insert_marker, notes_methods + insert_marker)
 with open(filepath, "w", encoding="utf-8") as f:
 f.write(content)
 print("Inserted CustomerNote methods successfully!")
else:
 print(f"Could not find insertion marker: {insert_marker!r}")
