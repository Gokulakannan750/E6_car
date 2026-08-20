$out = "E:\TTS\Projects\Desktop_Apps\E6_car_care\deploy\deploy-final-result.txt"
$log = @()
$log += "=== $(Get-Date) ==="

$log += "[1] Stop service..."
Stop-Service E6CarSpaApi -ErrorAction SilentlyContinue
Start-Sleep -Seconds 3

$log += "[2] Kill dotnet..."
taskkill /F /IM dotnet.exe 2>&1 | Out-Null
Start-Sleep -Seconds 3

$log += "[3] Copy API..."
Copy-Item "E:\TTS\Projects\Desktop_Apps\E6_car_care\dist\api\*" "C:\Program Files\E6 Car Spa\Api\" -Recurse -Force
$exe = Get-Item "C:\Program Files\E6 Car Spa\Api\E6CarSpa.Api.exe"
$log += "API timestamp: $($exe.LastWriteTime)"

$log += "[4] Copy Desktop..."
Copy-Item "E:\TTS\Projects\Desktop_Apps\E6_car_care\dist\desktop\*" "C:\Program Files\E6 Car Spa\Desktop\" -Recurse -Force
$log += "Desktop copied."

$log += "[5] Start service..."
Start-Service E6CarSpaApi -ErrorAction SilentlyContinue
Start-Sleep -Seconds 15

$svc = Get-Service E6CarSpaApi
$log += "Service: $($svc.Status)"

$log += "[6] Verify..."
try {
    $body = @{username="admin"; password="admin123"} | ConvertTo-Json
    $resp = Invoke-RestMethod -Uri "http://localhost:5080/api/auth/login" -Method POST -Body $body -ContentType "application/json"
    $token = $resp.token
    $headers = @{Authorization = "Bearer $token"}

    try {
        $staff = Invoke-RestMethod -Uri "http://localhost:5080/api/staffadvances/staff" -Method GET -Headers $headers
        $log += "Staff: OK ($($staff.Count) rows)"
    } catch {
        $log += "Staff ERROR: $($_.Exception.Message)"
    }

    try {
        $sum = Invoke-RestMethod -Uri "http://localhost:5080/api/staffadvances/summary" -Method GET -Headers $headers
        $log += "Summary: OK ($($sum.Count) rows)"
    } catch {
        $log += "Summary ERROR: $($_.Exception.Message)"
    }
} catch {
    $log += "LOGIN FAILED: $($_.Exception.Message)"
}

$log | ForEach-Object { Write-Host $_ }
$log | Out-File $out -Encoding UTF8