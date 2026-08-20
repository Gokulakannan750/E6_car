$ErrorActionPreference = 'Stop'
$outFile = "E:\TTS\Projects\Desktop_Apps\E6_car_care\deploy\db-result.txt"
$log = @()

$log += "=== $(Get-Date) ==="

# Read appsettings via elevated process
try {
    $raw = Get-Content "C:\Program Files\E6 Car Spa\Api\appsettings.json" -Raw -ErrorAction Stop
    $log += "appsettings.json read OK"
    $json = $raw | ConvertFrom-Json
    $connStr = $json.ConnectionStrings.Default
    $log += "Connection: $($connStr -replace 'Password=([^;]+)', 'Password=***')"
} catch {
    $log += "Cannot read appsettings: $($_.Exception.Message)"
    $log | Out-File $outFile -Encoding UTF8
    exit 1
}

# Connect to DB
try {
    $conn = New-Object Npgsql.NpgsqlConnection($connStr)
    $conn.Open()
    $log += "Connected to DB OK"

    # List tables
    $cmd = $conn.CreateCommand()
    $cmd.CommandText = "SELECT tablename FROM pg_tables WHERE schemaname='public' ORDER BY tablename;"
    $reader = $cmd.ExecuteReader()
    $log += ""
    $log += "Tables:"
    while ($reader.Read()) { $log += "  $($reader.GetString(0))" }
    $reader.Close()

    # Check migrations
    $cmd.CommandText = "SELECT ""MigrationId"" FROM ""__EFMigrationsHistory"" ORDER BY ""MigrationId"";"
    $reader = $cmd.ExecuteReader()
    $log += ""
    $log += "Migrations:"
    while ($reader.Read()) { $log += "  $($reader.GetString(0))" }
    $reader.Close()

    # Check Staff table
    $log += ""
    try {
        $cmd.CommandText = "SELECT COUNT(*) FROM ""Staff"";"
        $count = $cmd.ExecuteScalar()
        $log += "Staff count: $count"
    } catch {
        $log += "Staff table ERROR: $($_.Exception.Message)"
    }

    # Check StaffAdvances StaffId distribution
    $log += ""
    try {
        $cmd.CommandText = "SELECT ""StaffId"", COUNT(*) FROM ""StaffAdvances"" GROUP BY ""StaffId"" ORDER BY COUNT(*) DESC LIMIT 10;"
        $reader = $cmd.ExecuteReader()
        $log += "StaffAdvances StaffId distribution:"
        while ($reader.Read()) { $log += "  $($reader.GetGuid(0)) -> $($reader.GetInt32(1)) rows" }
        $reader.Close()
    } catch {
        $log += "StaffAdvances check ERROR: $($_.Exception.Message)"
    }

    $conn.Close()
} catch {
    $log += "DB ERROR: $($_.Exception.Message)"
}

$log | Out-File $outFile -Encoding UTF8
$log | ForEach-Object { Write-Host $_ }
Write-Host "=== Written to $outFile ==="
