<#
.SYNOPSIS
 Copies the fixed E6CarSpa.Desktop.dll into Program Files and restarts the app.
#>
$ErrorActionPreference = "Stop"
$src = "E:\TTS\Projects\Desktop_Apps\E6_car_care\src\E6CarSpa.Desktop\bin\Release\net10.0-windows\win-x64\E6CarSpa.Desktop.dll"
$dst = "C:\Program Files\E6 Car Spa\Desktop\E6CarSpa.Desktop.dll"

# Restart as admin if not already
$currentPrincipal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
if (-not $currentPrincipal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
 $psi = New-Object System.Diagnostics.ProcessStartInfo
 $psi.FileName = "powershell"
 $psi.Arguments = "-NoProfile -ExecutionPolicy Bypass -File `"$PSCommandPath`""
 $psi.Verb = "runas"
 [System.Diagnostics.Process]::Start($psi)
 exit
}

Write-Host "Stopping E6CarSpa.Desktop..."
Stop-Process -Name E6CarSpa.Desktop -Force -ErrorAction SilentlyContinue
Start-Sleep -Seconds 2

Write-Host "Copying updated DLL..."
Copy-Item -Force -Path $src -Destination $dst

Write-Host "Starting E6CarSpa.Desktop..."
Start-Process "C:\Program Files\E6 Car Spa\Desktop\E6CarSpa.Desktop.exe"

Write-Host "DONE"
