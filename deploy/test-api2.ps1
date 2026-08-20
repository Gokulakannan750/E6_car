$out = "E:\TTS\Projects\Desktop_Apps\E6_car_care\deploy\err-detail2.txt"
$log = @()
$log += "=== $(Get-Date) ==="

try {
    $body = @{username="admin"; password="admin123"} | ConvertTo-Json
    $resp = Invoke-RestMethod -Uri "http://localhost:5080/api/auth/login" -Method POST -Body $body -ContentType "application/json"
    $token = $resp.token
    $headers = @{Authorization = "Bearer $token"}

    # Test staff list with full error body
    try {
        $staff = Invoke-RestMethod -Uri "http://localhost:5080/api/staffadvances/staff" -Method GET -Headers $headers
        $log += "Staff: OK ($($staff.Count))"
    } catch {
        try {
            $resp2 = Invoke-WebRequest -Uri "http://localhost:5080/api/staffadvances/staff" -Method GET -Headers $headers -UseBasicParsing
            $log += "Status: $($resp2.StatusCode)"
            $log += $resp2.Content
        } catch {
            $statusCode = $_.Exception.Response.StatusCode.value__
            $log += "Status: $statusCode"
            try {
                $stream = $_.Exception.Response.GetResponseStream()
                $reader = New-Object System.IO.StreamReader($stream)
                $log += "Body: $($reader.ReadToEnd())"
            } catch {
                $log += "Cannot read body: $($_.Exception.Message)"
            }
            $log += "Outer: $($_.Exception.Message)"
        }
    }

} catch {
    $log += "LOGIN FAILED: $($_.Exception.Message)"
}

$log | ForEach-Object { Write-Host $_ }
$log | Out-File $out -Encoding UTF8
