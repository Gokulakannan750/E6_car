#Requires -RunAsAdministrator
<#
.SYNOPSIS
  Stops and removes the E6 Car Spa API Windows Service and its firewall rule.
#>
param(
    [string]$ServiceName = 'E6CarSpaApi'
)

if (Get-Service -Name $ServiceName -ErrorAction SilentlyContinue) {
    Stop-Service -Name $ServiceName -Force -ErrorAction SilentlyContinue
    & sc.exe delete $ServiceName | Out-Null
    Write-Host "Service '$ServiceName' removed." -ForegroundColor Green
} else {
    Write-Host "Service '$ServiceName' not found."
}

& netsh.exe advfirewall firewall delete rule name="E6 Car Spa API" 2>$null | Out-Null
Write-Host "Firewall rule removed (if it existed)."
