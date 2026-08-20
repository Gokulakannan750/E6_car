import sys
content = open(sys.argv[1], 'r').read()

if 'GetCustomerNotesAsync' in content:
 print('Already exists!')
else:
 notes_block = (
 ' // ---------- Customer notes ----------\n'
 ' public Task<List<CustomerNoteDto>?> GetCustomerNotesAsync(Guid customerId) =>\n'
 ' GetAsync<List<CustomerNoteDto>>($"api/customers/{customerId}/notes");\n'
 '\n'
 ' public Task<CustomerNoteDto> CreateCustomerNoteAsync(Guid customerId, string text) =>\n'
 ' PostAsync<CustomerNoteDto>($"api/customers/{customerId}/notes", new { text });\n'
 '\n'
 ' public Task<CustomerNoteDto> UpdateCustomerNoteAsync(Guid customerId, Guid noteId, string text) =>\n'
 ' PutAsync<CustomerNoteDto>($"api/customers/{customerId}/notes/{noteId}", new { text });\n'
 '\n'
 ' public async Task DeleteCustomerNoteAsync(Guid customerId, Guid noteId)\n'
 ' {\n'
 ' var resp = await http.DeleteAsync($"api/customers/{customerId}/notes/{noteId}");\n'
 ' await EnsureSuccess(resp);\n'
 ' }\n'
 '\n'
 )
 content = content.replace(' // ---------- Plumbing ----------',
 notes_block + ' // ---------- Plumbing ----------')
 with open(sys.argv[1], 'w') as f:
 f.write(content)
 print('Done!')
