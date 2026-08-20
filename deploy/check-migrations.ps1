$conn = "Host=localhost;Port=5432;Database=e6carspa;Username=postgres"
$connStr = "Host=localhost;Port=5432;Database=e6carspa;Username=postgres"
$pgPass = ""

$proc = Start-Process -FilePath "psql" -ArgumentList "-U postgres -d e6carspa -c `"SELECT MigrationId FROM __EFMigrationsHistory ORDER BY MigrationId;`"" -NoNewWindow -PassThru -Wait -ErrorAction SilentlyContinue

# Use Npgsql to check
Add-Type -Path "C:\Program Files\dotnet\packs\Microsoft.NETCore.App.Ref\*\ref\net10.0\netstandard.dll" -ErrorAction SilentlyContinue

$query = @'
SELECT "MigrationId" FROM "__EFMigrationsHistory" ORDER BY "MigrationId";
'@

Write-Host "Checking migrations in database..."
$result = & psql -U postgres -d e6carspa -t -c "SELECT ""MigrationId"" FROM ""__EFMigrationsHistory"" ORDER BY ""MigrationId"";" 2>&1
Write-Host "Applied migrations:"
Write-Host $result
