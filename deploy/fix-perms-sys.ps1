$action = New-ScheduledTaskAction -Execute 'powershell.exe' -Argument '-NoProfile -Command "takeown /f C:\Program Files\E6 Car Spa\Api /a /r /d y; icacls C:\Program Files\E6 Car Spa\Api /grant Administrators:F /T; icacls C:\Program Files\E6 Car Spa\Api\appsettings.json /grant *S-1-5-18:R *S-1-5-32-544:(F) /T; Start-Service E6CarSpaApi; Start-Sleep 5; Get-Service E6CarSpaApi | Format-List"'
$principal = New-ScheduledTaskPrincipal -UserId "SYSTEM" -LogonType ServiceAccount -RunLevel Highest
$settings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries

Register-ScheduledTask -TaskName 'FixE6CarSpaPerms' -Action $action -Principal $principal -Settings $settings -Force | Out-Null
Start-ScheduledTask -TaskName 'FixE6CarSpaPerms'
Write-Host "Launched as SYSTEM, waiting 15s..."
Start-Sleep 15

$ti = Get-ScheduledTaskInfo -TaskName 'FixE6CarSpaPerms' -ErrorAction SilentlyContinue
if ($ti) {
    Write-Host "Result: $($ti.LastTaskResult)"
}
$svc = Get-CimInstance Win32_Service -Filter "Name='E6CarSpaApi'" -ErrorAction SilentlyContinue
if ($svc) {
    Write-Host "Service: $($svc.State)"
}
Unregister-ScheduledTask -TaskName 'FixE6CarSpaPerms' -Confirm:$false 2>$null
