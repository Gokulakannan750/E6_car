# Run as SYSTEM via scheduled task to fix the locked-down API folder
$action = New-ScheduledTaskAction -Execute 'powershell.exe' -Argument '-NoProfile -Command "takeown /f C:\Program Files\E6 Car Spa\Api /a /r /d y; icacls C:\Program Files\E6 Car Spa\Api /grant Administrators:F /T; icacls C:\Program Files\E6 Car Spa\Api\appsettings.json /grant *S-1-5-18:R *S-1-5-32-544:F *S-1-5-19:(OI)(CI)RX /T; Start-Service E6CarSpaApi; Start-Sleep 3; Get-Service E6CarSpaApi | Select-Object Name,Status"'
$principal = New-ScheduledTaskPrincipal -UserId "SYSTEM" -RunLevel Highest -LogonType ServiceAccount
$settings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries

Register-ScheduledTask -TaskName 'FixE6CarSpaPerms' -Action $action -Principal $principal -Settings $settings -Force | Out-Null
Start-ScheduledTask -TaskName 'FixE6CarSpaPerms'
Write-Host "Task launched as SYSTEM, waiting 15 seconds..."
Start-Sleep 15

$ti = Get-ScheduledTaskInfo -TaskName 'FixE6CarSpaPerms'
Write-Host "Task state: $($ti.LastTaskResult)"
Unregister-ScheduledTask -TaskName 'FixE6CarSpaPerms' -Confirm:$false
Write-Host "Cleanup done."
