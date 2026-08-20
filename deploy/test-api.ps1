$out = "E:\TTS\Projects\Desktop_Apps\E6_car_care\deploy\api-test.txt"
$log = @()
$log += "=== $(Get-Date) ==="

# Test login first
try {
    $body = @{username="admin"; password="admin123"} | ConvertTo-Json
    $resp = Invoke-RestMethod -Uri "http://localhost:5080/api/auth/login" -Method POST -Body $body -ContentType "application/json"
    $token = $resp.token
    $log += "Login OK, token length: $($token.Length)"
    $headers = @{Authorization = "Bearer $token"}

    # Test staff list
    try {
        $staff = Invoke-RestMethod -Uri "http://localhost:5080/api/staffadvances/staff" -Method GET -Headers $headers
        $log += "Staff count: $($staff.Count)"
        foreach ($s in $staff) { $log += "  - $($s.fullName) (active: $($s.isActive))" }
    } catch {
        $log += "Staff ERROR: $($_.Exception.Message)"
        if ($_.Exception.InnerException) { $log += "  Inner: $($_.Exception.InnerException.Message)" }
    }

    # Test advances list
    try {
        $adv = Invoke-RestMethod -Uri "http://localhost:5080/api/staffadvances" -Method GET -Headers $headers
        $log += "Advances count: $($adv.Count)"
    } catch {
        $log += "Advances ERROR: $($_.Exception.Message)"
    }

    # Test summary
    try {
        $sum = Invoke-RestMethod -Uri "http://localhost:5080/api/staffadvances/summary" -Method GET -Headers $headers
        $log += "Summary count: $($sum.Count)"
    } catch {
        $log += "Summary ERROR: $($_.Exception.Message)"
    }

} catch {
    $log += "LOGIN FAILED: $($_.Exception.Message)"
}

$log | ForEach-Object { Write-Host $_ }
$log | Out-File $out -Encoding UTF8
