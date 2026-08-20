$cutoff = (Get-Date).AddMinutes(-15)
$events = Get-EventLog -LogName Application -Source 'E6CarSpa*' -Newest 20 -ErrorAction SilentlyContinue
foreach ($e in $events) {
    if ($e.TimeGenerated -gt $cutoff) {
        Write-Host "$($e.TimeGenerated) [$($e.EntryType)]"
        Write-Host "  $($e.Message.Substring(0, [Math]::Min(300, $e.Message.Length)))"
        Write-Host ""
    }
}
Write-Host "---Service status---"
Get-Service E6CarSpaApi | Select-Object Name, Status, StartType
Write-Host "---Recent service history---"
Get-EventLog -LogName System -Source '*Service*' -Newest 5 -ErrorAction SilentlyContinue | Where-Object { $_.Message -like '*E6CarSpa*' } | ForEach-Object {
    Write-Host "$($_.TimeGenerated) [$($e.EntryType)]: $($_.Message.Substring(0, [Math]::Min(200, $_.Message.Length)))"
}
