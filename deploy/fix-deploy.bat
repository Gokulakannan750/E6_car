@echo off
taskkill /F /IM E6CarSpa.Desktop.exe >nul 2>&1
timeout /t 2 /nobreak >nul
powershell -Command "Copy-Item -Path 'E:\TTS\Projects\Desktop_Apps\E6_car_care\src\E6CarSpa.Desktop\bin\Release\net10.0-windows\win-x64\E6CarSpa.Desktop.dll' -Destination 'C:\Program Files\E6 Car Spa\Desktop\E6CarSpa.Desktop.dll' -Force"
powershell -Command "Start-Process 'C:\Program Files\E6 Car Spa\Desktop\E6CarSpa.Desktop.exe'"
echo DONE
