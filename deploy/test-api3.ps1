$out = "E:\TTS\Projects\Desktop_Apps\E6_car_care\deploy\db-check.txt"
$log = @()
$log += "=== $(Get-Date) ==="

try {
    $body = @{username="admin"; password="admin123"} | ConvertTo-Json
    $resp = Invoke-RestMethod -Uri "http://localhost:5080/api/auth/login" -Method POST -Body $body -ContentType "application/json"
    $token = $resp.token
    $headers = @{Authorization = "Bearer $token"}

    # Try to hit staff endpoint with verbose
    $wc = New-Object System.Net.WebClient
    $wc.Headers.Add("Authorization", "Bearer $token")
    try {
        $result = $wc.DownloadString("http://localhost:5080/api/staffadvances/staff")
        $log += "Staff: OK ($result)"
    } catch [System.Net.WebException] {
        $resp2 = $_.Exception.Response
        $stream = $resp2.GetResponseStream()
        $reader = New-Object System.IO.StreamReader($stream)
        $body2 = $reader.ReadToEnd()
        $log += "Staff ERROR status: $($resp2.StatusCode)"
        $log += "Body: $body2"
    }

    # Try summary
    try {
        $result2 = $wc.DownloadString("http://localhost:5080/api/staffadvances/summary")
        $log += "Summary: OK ($result2)"
    } catch [System.Net.WebException] {
        $resp2 = $_.Exception.Response
        $stream = $resp2.GetResponseStream()
        $reader = New-Object System.IO.StreamReader($stream)
        $body2 = $reader.ReadToEnd()
        $log += "Summary ERROR status: $($resp2.StatusCode)"
        $log += "Body: $body2"
    }

} catch {
    $log += "LOGIN FAILED: $($_.Exception.Message)"
}

$log | ForEach-Object { Write-Host $_ }
$log | Out-File $out -Encoding UTF8
