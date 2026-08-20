$ErrorActionPreference = 'Stop'
$log = @("=== $(Get-Date) ===")

Stop-Service E6CarSpaApi -ErrorAction SilentlyContinue
Start-Sleep -Seconds 3
taskkill /F /IM dotnet.exe 2>&1 | Out-Null
Start-Sleep -Seconds 3

Copy-Item "E:\TTS\Projects\Desktop_Apps\E6_car_care\dist\api\*" "C:\Program Files\E6 Car Spa\Api\" -Recurse -Force

Start-Service E6CarSpaApi -ErrorAction SilentlyContinue
Start-Sleep -Seconds 20

$svc = Get-Service E6CarSpaApi
$log += "Service: $($svc.Status)"

$body = @{username="admin"; password="admin123"} | ConvertTo-Json
$resp = Invoke-RestMethod -Uri "http://localhost:5080/api/auth/login" -Method POST -Body $body -ContentType "application/json"
$token = $resp.token
$headers = @{Authorization = "Bearer $token"}

try {
    $staff = Invoke-RestMethod -Uri "http://localhost:5080/api/staffadvances/staff" -Method GET -Headers $headers
    $log += "Staff OK: $($staff.Count) rows"
    foreach ($s in $staff) { $log += "  $($s.id) $($s.fullName) active=$($s.isActive)" }
} catch {
    $log += "Staff ERROR: $($_.Exception.Message)"
}

try {
    $sum = Invoke-RestMethod -Uri "http://localhost:5080/api/staffadvances/summary" -Method GET -Headers $headers
    $log += "Summary OK: $($sum.Count) rows"
    foreach ($r in $sum) { $log += "  $($r.staffId) $($r.staffName) total=$($r.totalAdvanced) count=$($r.advanceCount)" }
} catch {
    $log += "Summary ERROR: $($_.Exception.Message)"
}

$log | ForEach-Object { Write-Host $_ }
$log | Out-File "E:\TTS\Projects\Desktop_Apps\E6_car_care\deploy\fix-result.txt" -Encoding UTF8