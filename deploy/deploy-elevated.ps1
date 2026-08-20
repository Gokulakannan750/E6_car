$src = 'E:\TTS\Projects\Desktop_Apps\E6_car_care\dist\api'
$dst = 'C:\Program Files\E6 Car Spa\Api'

Write-Host "=== Stopping API service ==="
Stop-Service E6CarSpaApi -Force -ErrorAction SilentlyContinue
Start-Sleep -Seconds 3

Write-Host "=== Copying API files (elevated) ==="
robocopy $src $dst /E /R:5 /W:2 /NFL /NDL /NP /NJH /NJS
Write-Host "Robocopy exit: $LASTEXITCODE (0-7 = success)"

$srcD = 'E:\TTS\Projects\Desktop_Apps\E6_car_care\dist\desktop'
$dstD = 'C:\Program Files\E6 Car Spa\Desktop'
Write-Host "=== Copying Desktop files (elevated) ==="
robocopy $srcD $dstD /E /R:1 /W:1 /NFL /NDL /NP /NJH /NJS /XO
Write-Host "Robocopy exit: $LASTEXITCODE (0-7 = success)"

Write-Host "=== Restarting API service ==="
Set-Service E6CarSpaApi -StartupType Automatic
Start-Service E6CarSpaApi
Write-Host "Service started. Status:"
Get-Service E6CarSpaApi | Select-Object Name, Status, StartType
