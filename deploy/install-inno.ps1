$p = Start-Process -FilePath 'E:\TTS\Projects\Desktop_Apps\E6_car_care\deploy\innosetup-6.exe' -ArgumentList '/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART' -Wait -PassThru
Write-Host "Exit: $($p.ExitCode)"
