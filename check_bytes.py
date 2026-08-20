import sys

with open('src/E6CarSpa.Api/Mapping/Mappers.cs', 'rb') as f:
 content = f.read()

for i, b in enumerate(content):
 if b > 127:
 print(f'Non-ASCII at offset {i}: 0x{b:02x}')

# Also check for BOM
if content[:3] == b'\xef\xbb\xbf':
 print('Has UTF-8 BOM')
else:
 print('No BOM')

print(f'Total bytes: {len(content)}')
