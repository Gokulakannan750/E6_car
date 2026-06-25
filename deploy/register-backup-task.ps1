#Requires -RunAsAdministrator
<#
.SYNOPSIS
  Registers a nightly Windows Scheduled Task that runs backup-db.ps1.
.DESCRIPTION
  Runs as SYSTEM (works whether or not anyone is logged in) and catches up at next
  startup if the PC was off at the scheduled time. Optionally stores the backup
  encryption password as a machine environment variable so it isn't in the task args.
.EXAMPLE
  ./register-backup-task.ps1 -BackupPassword "MyStrongPass" -Time "21:00"
#>
param(
    [string]$ScriptPath     = (Join-Path $PSScriptRoot 'backup-db.ps1'),
    [string]$BackupPassword,
    [string]$Time           = '21:00',
    [string]$TaskName       = 'E6CarSpa Daily Backup'
)

$ErrorActionPreference = 'Stop'
if (-not (Test-Path $ScriptPath)) { throw "backup-db.ps1 not found at $ScriptPath" }

if ($BackupPassword) {
    [Environment]::SetEnvironmentVariable('E6_BACKUP_PASSWORD', $BackupPassword, 'Machine')
    Write-Host 'Stored encryption password in the E6_BACKUP_PASSWORD machine environment variable.'
}

$action    = New-ScheduledTaskAction -Execute 'powershell.exe' `
                -Argument "-NoProfile -ExecutionPolicy Bypass -File `"$ScriptPath`""
$trigger   = New-ScheduledTaskTrigger -Daily -At $Time
$principal = New-ScheduledTaskPrincipal -UserId 'SYSTEM' -LogonType ServiceAccount -RunLevel Highest
$settings  = New-ScheduledTaskSettingsSet -StartWhenAvailable -ExecutionTimeLimit (New-TimeSpan -Hours 1)

Register-ScheduledTask -TaskName $TaskName -Action $action -Trigger $trigger `
    -Principal $principal -Settings $settings -Force | Out-Null

Write-Host "Scheduled task '$TaskName' registered for daily $Time (runs as SYSTEM)." -ForegroundColor Green
Write-Host "Run it now to test:  Start-ScheduledTask -TaskName '$TaskName'"
Write-Host "Remove it later:     Unregister-ScheduledTask -TaskName '$TaskName' -Confirm:`$false"
