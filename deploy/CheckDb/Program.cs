$ErrorActionPreference = 'Stop'
$configPath = "C:\Program Files\E6 Car Spa\Api\appsettings.json"

# Read as admin (the file is readable by SYSTEM and Administrators)
$raw = Get-Content $configPath -Raw
$json = $raw | ConvertFrom-Json
$connStr = $json.ConnectionStrings.Default
$logFile = "E:\TTS\Projects\Desktop_Apps\E6_car_care\deploy\db-check-result.txt"

$log = @()
$log += "=== $(Get-Date) ==="
$log += "Connecting to DB..."

Add-Type -Path "C:\Program Files\E6 Car Spa\Api\Npgsql.dll"
$conn = New-Object Npgsql.NpgsqlConnection($connStr)
$conn.Open()
$log += "Connected!"

# List tables
$cmd = $conn.CreateCommand()
$cmd.CommandText = "SELECT tablename FROM pg_tables WHERE schemaname='public' ORDER BY tablename;"
$reader = $cmd.ExecuteReader()
$log += ""
$log += "Tables:"
while ($reader.Read()) { $log += "  $($reader.GetString(0))" }
$reader.Close()

# Check migrations
$cmd.CommandText = "SELECT `"MigrationId`" FROM `"__EFMigrationsHistory`" ORDER BY `"MigrationId`";"
$reader = $cmd.ExecuteReader()
$log += ""
$log += "Migrations:"
while ($reader.Read()) { $log += "  $($reader.GetString(0))" }
$reader.Close()

# Check Staff table
$log += ""
try {
    $cmd.CommandText = "SELECT COUNT(*) FROM `"Staff`";"
    $count = $cmd.ExecuteScalar()
    $log += "Staff count: $count"
} catch {
    $log += "Staff table ERROR: $($_.Exception.Message)"
}

# Check StaffAdvances StaffId
$log += ""
try {
    $cmd.CommandText = "SELECT `"StaffId`", COUNT(*) FROM `"StaffAdvances`" GROUP BY `"StaffId`" ORDER BY COUNT(*) DESC LIMIT 10;"
    $reader = $cmd.ExecuteReader()
    $log += "StaffAdvances StaffId distribution:"
    while ($reader.Read()) { $log += "  $($reader.GetGuid(0)) -> $($reader.GetInt32(1)) rows" }
    $reader.Close()
} catch {
    $log += "StaffAdvances check ERROR: $($_.Exception.Message)"
}

$conn.Close()
$log | Out-File $logFile -Encoding UTF8
$log | ForEach-Object { Write-Host $_ }
Write-Host "=== Written to $logFile ==="
