Write-Host "=== Service Status ==="
Get-Service E6CarSpaApi | Format-List Name, Status, StartType

Write-Host "=== Try to start service ==="
try {
    Start-Service E6CarSpaApi -ErrorAction Stop
    Start-Sleep -Seconds 8
    $svc = Get-Service E6CarSpaApi
    Write-Host "After start: $($svc.Status) / $($svc.StartType)"
} catch {
    Write-Host "Start failed: $($_.Exception.Message)"
}

Write-Host ""
Write-Host "=== Recent E6CarSpa Application Events ==="
$cutoff = (Get-Date).AddMinutes(-5)
Get-EventLog -LogName Application -Source 'E6CarSpa*' -Newest 5 -ErrorAction SilentlyContinue | Where-Object { $_.TimeGenerated -gt $cutoff } | ForEach-Object {
    Write-Host ""
    Write-Host "$($_.TimeGenerated) [$($_.EntryType)]"
    Write-Host $_.Message.Substring(0, [Math]::Min(600, $_.Message.Length))
}

Write-Host ""
Write-Host "=== Recent System Events ==="
Get-EventLog -LogName System -Newest 5 -ErrorAction SilentlyContinue | Where-Object { $_.TimeGenerated -gt $cutoff -and $_.Message -like '*E6CarSpa*' } | ForEach-Object {
    Write-Host ""
    Write-Host "$($_.TimeGenerated) [$($_.EntryType)]"
    Write-Host $_.Message.Substring(0, [Math]::Min(400, $_.Message.Length))
}

Write-Host ""
Write-Host "=== Health Check ==="
try {
    $r = Invoke-RestMethod http://localhost:5000/health -TimeoutSec 5
    Write-Host "API: $($r.status)"
} catch {
    Write-Host "API not responding"
}
