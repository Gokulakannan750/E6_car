$f = Get-Item 'E:\TTS\Projects\Desktop_Apps\E6_car_care\dist\api\E6CarSpa.Api.exe'
$mb = [math]::Round($f.Length / 1MB, 2)
Write-Host "dist/api: $($f.LastWriteTime) ($mb MB)"
