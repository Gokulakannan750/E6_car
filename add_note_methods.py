with open('src/E6CarSpa.Client/ApiClient.cs', 'r') as f:
 lines = f.readlines()

insert_idx = 330 # insert after line 330 (the GetCustomerHistoryAsync line)
lines.insert(insert_idx, '\n')
lines.insert(insert_idx + 1, ' public Task<List<CustomerNoteDto>?> GetCustomerNotesAsync(Guid customerId) =>\n')
lines.insert(insert_idx + 2, ' GetAsync<List<CustomerNoteDto>>($"api/customers/{customerId}/notes");\n')
lines.insert(insert_idx + 3, '\n')
lines.insert(insert_idx + 4, ' public Task<CustomerNoteDto> CreateCustomerNoteAsync(Guid customerId, string text) =>\n')
lines.insert(insert_idx + 5, ' PostAsync<CustomerNoteDto>($"api/customers/{customerId}/notes", new { text });\n')
lines.insert(insert_idx + 6, '\n')
lines.insert(insert_idx + 7, ' public Task<CustomerNoteDto> UpdateCustomerNoteAsync(Guid customerId, Guid noteId, string text) =>\n')
lines.insert(insert_idx + 8, ' PutAsync<CustomerNoteDto>($"api/customers/{customerId}/notes/{noteId}", new { text });\n')
lines.insert(insert_idx + 9, '\n')
lines.insert(insert_idx + 10, ' public Task DeleteCustomerNoteAsync(Guid customerId, Guid noteId) =>\n')
lines.insert(insert_idx + 11, ' DeleteAsync($"api/customers/{customerId}/notes/{noteId}");\n')

with open('src/E6CarSpa.Client/ApiClient.cs', 'w') as f:
 f.writelines(lines)

# Verify
with open('src/E6CarSpa.Client/ApiClient.cs', 'r') as f:
 fl = f.readlines()
for i in range(328, 345):
 print(f"Line {i+1}: {repr(fl[i])}")
