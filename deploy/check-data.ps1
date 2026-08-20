$out = "E:\TTS\Projects\Desktop_Apps\E6_car_care\deploy\db-data.txt"
$log = @()
$log += "=== $(Get-Date) ==="

try {
    $body = @{username="admin"; password="admin123"} | ConvertTo-Json
    $resp = Invoke-RestMethod -Uri "http://localhost:5080/api/auth/login" -Method POST -Body $body -ContentType "application/json"
    $token = $resp.token
    $headers = @{Authorization = "Bearer $token"}

    # Try to get advances with detail to understand the data
    $adv = Invoke-RestMethod -Uri "http://localhost:5080/api/staffadvances" -Method GET -Headers $headers
    $log += "Advances count: $($adv.Count)"
    foreach ($a in $adv) {
        $log += "  ID: $($a.id) Worker: '$($a.workerName)' StaffId: $($a.staffId) Amount: $($a.amount)"
    }

} catch {
    $log += "ERROR: $($_.Exception.Message)"
}

$log | ForEach-Object { Write-Host $_ }
$log | Out-File $out -Encoding UTF8
