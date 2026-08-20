$out = "E:\TTS\Projects\Desktop_Apps\E6_car_care\deploy\db-tables.txt"
$log = @()
$log += "=== $(Get-Date) ==="

try {
    $body = @{username="admin"; password="admin123"} | ConvertTo-Json
    $resp = Invoke-RestMethod -Uri "http://localhost:5080/api/auth/login" -Method POST -Body $body -ContentType "application/json"
    $token = $resp.token
    $headers = @{Authorization = "Bearer $token"}

    # Use a DB admin endpoint if available, or check settings
    try {
        $settings = Invoke-RestMethod -Uri "http://localhost:5080/api/settings" -Method GET -Headers $headers
        $log += "Settings: OK"
        $log += ($settings | ConvertTo-Json -Depth 3)
    } catch {
        $log += "Settings ERROR: $($_.Exception.Message)"
    }

} catch {
    $log += "LOGIN FAILED: $($_.Exception.Message)"
}

$log | ForEach-Object { Write-Host $_ }
$log | Out-File $out -Encoding UTF8
