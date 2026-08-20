$out = "E:\TTS\Projects\Desktop_Apps\E6_car_care\deploy\err-detail.txt"
$log = @()
$log += "=== $(Get-Date) ==="

try {
    $body = @{username="admin"; password="admin123"} | ConvertTo-Json
    $resp = Invoke-RestMethod -Uri "http://localhost:5080/api/auth/login" -Method POST -Body $body -ContentType "application/json"
    $token = $resp.token
    $headers = @{Authorization = "Bearer $token"}

    # Test staff list with detailed error
    try {
        $staff = Invoke-RestMethod -Uri "http://localhost:5080/api/staffadvances/staff" -Method GET -Headers $headers
        $log += "Staff: OK ($($staff.Count))"
    } catch {
        $errResp = $_.Exception.Response
        $stream = $errResp.GetResponseStream()
        $reader = New-Object System.IO.StreamReader($stream)
        $body = $reader.ReadToEnd()
        $log += "Staff ERROR (500):"
        $log += $body
    }

    # Test summary with detailed error
    try {
        $sum = Invoke-RestMethod -Uri "http://localhost:5080/api/staffadvances/summary" -Method GET -Headers $headers
        $log += "Summary: OK"
    } catch {
        $errResp = $_.Exception.Response
        $stream = $errResp.GetResponseStream()
        $reader = New-Object System.IO.StreamReader($stream)
        $body = $reader.ReadToEnd()
        $log += "Summary ERROR (500):"
        $log += $body
    }

} catch {
    $log += "LOGIN FAILED: $($_.Exception.Message)"
}

$log | ForEach-Object { Write-Host $_ }
$log | Out-File $out -Encoding UTF8
