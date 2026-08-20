with open('AppDbContextModelSnapshot.cs', 'r') as f:
 lines = f.readlines()

output = []
skip = False
for i, line in enumerate(lines):
 if skip:
 if line.strip() == '});':
 skip = False
 continue
 if 'b.HasOne("E6CarSpa.Domain.Entities.Staff"' in line and 'Salaries' not in line:
 if i > 960:
 skip = True
 continue
 output.append(line)

with open('AppDbContextModelSnapshot.cs', 'w') as f:
 f.writelines(output)
print('Fixed model snapshot')
