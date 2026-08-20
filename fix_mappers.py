import re

with open('src/E6CarSpa.Api/Mapping/Mappers.cs', 'r') as f:
 lines = f.readlines()

# Find and fix lines with wrong indentation (starts with single space instead of 4)
fixed = []
for line in lines:
 # If line starts with exactly one space followed by 'public', 'new', etc. - fix it
 if re.match(r'^ (public static|new\(| })', line):
 fixed.append(' ' + line.lstrip())
 else:
 fixed.append(line)

with open('src/E6CarSpa.Api/Mapping/Mappers.cs', 'w') as f:
 f.writelines(fixed)

# Verify
with open('src/E6CarSpa.Api/Mapping/Mappers.cs') as f:
 for i, line in enumerate(f, 1):
 if i <= 22:
 print(f'{i:2}: {line.rstrip()}')
