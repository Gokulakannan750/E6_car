$log = @("=== $(Get-Date) ===")
$body = @{username="admin"; password="admin123"} | ConvertTo-Json
$resp = Invoke-RestMethod -Uri "http://localhost:5080/api/auth/login" -Method POST -Body $body -ContentType "application/json"
$token = $resp.token
$headers = @{Authorization = "Bearer $token"}

try {
    $staff = Invoke-RestMethod -Uri "http://localhost:5080/api/staffadvances/staff" -Method GET -Headers $headers
    $log += "Staff OK: $($staff.Count)"
} catch {
    $log += "Staff ERROR: $($_.Exception.Message)"
    $resp2 = $_.Exception.Response
    if ($resp2) {
        $stream = $resp2.GetResponseStream()
        $reader = New-Object System.IO.StreamReader($stream)
        $log += "Body: $($reader.ReadToEnd())"
    }
}

$log | Out-File "E:\TTS\Projects\Desktop_Apps\E6_car_care\deploy\error-detail.txt" -Encoding UTF8
$log | ForEach-Object { Write-Host $_ }
