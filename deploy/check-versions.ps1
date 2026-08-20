$src = Get-Item 'E:\TTS\Projects\Desktop_Apps\E6_car_care\src\E6CarSpa.Api\bin\Release\net10.0\E6CarSpa.Api.dll'
$dst = Get-Item 'C:\Program Files\E6 Car Spa\Api\E6CarSpa.Api.dll'
Write-Host "Source: $($src.LastWriteTime) ($($src.Length) bytes)"
Write-Host "Deployed: $($dst.LastWriteTime) ($($dst.Length) bytes)"
