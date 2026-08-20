$ErrorActionPreference = "Stop"
$src = "E:\TTS\Projects\Desktop_Apps\E6_car_care\src\E6CarSpa.Desktop\bin\Release\net10.0-windows\win-x64\E6CarSpa.Desktop.dll"
$dst = "C:\Program Files\E6 Car Spa\Desktop\E6CarSpa.Desktop.dll"
Stop-Process -Name E6CarSpa.Desktop -Force -ErrorAction SilentlyContinue
Start-Sleep -Seconds 2
Copy-Item -Force -Path $src -Destination $dst
Start-Process "C:\Program Files\E6 Car Spa\Desktop\E6CarSpa.Desktop.exe"
Write-Host "DONE"
