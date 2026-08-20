$ErrorActionPreference = 'Stop'
$env:PGPASSWORD = "E6CarSpa@2024"
& "C:\Program Files\PostgreSQL\18\bin\psql.exe" -U postgres -d e6carspa -c "DROP INDEX IF EXISTS `"IX_Staff_FullName`";"
Write-Host "Done"
