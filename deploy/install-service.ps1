#Requires -RunAsAdministrator
<#
.SYNOPSIS
  Installs the E6 Car Spa API as an auto-starting Windows Service.
.DESCRIPTION
  Run this in an elevated PowerShell. By default it points the service at
  .\api\E6CarSpa.Api.exe relative to this script; override with -ApiPath.
  Edit api\appsettings.json (database connection, JWT key) before/after install,
  then the service will pick it up on (re)start.
.EXAMPLE
  ./install-service.ps1
  ./install-service.ps1 -ApiPath "C:\E6\api\E6CarSpa.Api.exe"
#>
param(
    [string]$ApiPath = (Join-Path $PSScriptRoot 'api\E6CarSpa.Api.exe'),
    [string]$ServiceName = 'E6CarSpaApi',
    [string]$DisplayName = 'E6 Car Spa API'
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path $ApiPath)) {
    Write-Error "API executable not found at '$ApiPath'. Pass -ApiPath to point at E6CarSpa.Api.exe."
    exit 1
}
$exe = (Resolve-Path $ApiPath).Path

# Remove any existing instance first.
if (Get-Service -Name $ServiceName -ErrorAction SilentlyContinue) {
    Write-Host "Removing existing service '$ServiceName'..."
    Stop-Service -Name $ServiceName -Force -ErrorAction SilentlyContinue
    & sc.exe delete $ServiceName | Out-Null
    Start-Sleep -Seconds 2
}

Write-Host "Creating service '$ServiceName' -> $exe"
New-Service -Name $ServiceName -BinaryPathName "`"$exe`"" -DisplayName $DisplayName `
    -Description 'E6 Car Spa billing API server' -StartupType Automatic | Out-Null

# Auto-restart on failure.
& sc.exe failure $ServiceName reset= 86400 actions= restart/5000/restart/5000/restart/5000 | Out-Null

# Allow the port through the firewall (only needed for LAN access from other PCs).
& netsh.exe advfirewall firewall add rule name="E6 Car Spa API" dir=in action=allow protocol=TCP localport=5080 | Out-Null

Start-Service -Name $ServiceName
Write-Host "Service '$ServiceName' installed and started." -ForegroundColor Green
Write-Host "Edit '$(Split-Path $exe)\appsettings.json' for the database connection, then: Restart-Service $ServiceName"
