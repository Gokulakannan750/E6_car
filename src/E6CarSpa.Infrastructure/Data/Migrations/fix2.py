import re

with open('AppDbContextModelSnapshot.cs', 'r') as f:
 content = f.read()

# Fix the Staff.Salaries navigation block - it lost its entity declaration
old = """ modelBuilder.Entity("E6CarSpa.Domain.Entities.Staff", b =>
\t{
\tb.Navigation("Salaries");
\t});"""

new = """ modelBuilder.Entity("E6CarSpa.Domain.Entities.Staff", b =>
 {
 b.Navigation("Salaries");
 });"""

content = content.replace(old, new)

with open('AppDbContextModelSnapshot.cs', 'w') as f:
 f.write(content)
print('Done')
