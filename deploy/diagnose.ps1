Write-Host "=== Service ==="
Get-Service E6CarSpaApi | Format-List Name, Status, StartType, ServicesDependedOn
Write-Host "=== Try starting manually to capture error ==="
try {
    Start-Service E6CarSpaApi -ErrorAction Stop
    Start-Sleep -Seconds 8
    Get-Service E6CarSpaApi | Select Name, Status
} catch {
    Write-Host "Start failed: $($_.Exception.Message)"
}
Write-Host ""
Write-Host "=== Recent Application events (E6CarSpa) ==="
$cutoff = (Get-Date).AddMinutes(-5)
Get-EventLog -LogName Application -Source 'E6CarSpa*' -Newest 5 -ErrorAction SilentlyContinue | Where-Object { $_.TimeGenerated -gt $cutoff } | ForEach-Object {
    Write-Host ""
    Write-Host "$($_.TimeGenerated) [$($_.EntryType)]"
    Write-Host $_.Message.Substring(0, [Math]::Min(600, $_.Message.Length))
}
Write-Host ""
Write-Host "=== Recent System events (service) ==="
Get-EventLog -LogName System -Newest 5 -ErrorAction SilentlyContinue | Where-Object { $_.TimeGenerated -gt $cutoff -and $_.Message -like '*E6CarSpa*' } | ForEach-Object {
    Write-Host ""
    Write-Host "$($_.TimeGenerated) [$($_.EntryType)]"
    Write-Host $_.Message.Substring(0, [Math]::Min(400, $_.Message.Length))
}
Write-Host ""
Write-Host "=== Health ==="
try {
    $r = Invoke-RestMethod http://localhost:5000/health -TimeoutSec 5
    Write-Host "API is UP: $($r.status)"
} catch {
    Write-Host "API not responding"
}
