@echo off
chcp 65001 >nul
echo ========================================
echo   POST-REBOOT DEPLOY
echo   Run this AFTER rebooting your PC
echo ========================================
echo.
echo This script will:
echo   1. Delete any leftover service
echo   2. Copy API and Desktop files
echo   3. Create fresh service
echo   4. Start it
echo.
pause

:: Kill any running processes
taskkill /F /IM E6CarSpa.Api.exe >nul 2>&1
taskkill /F /IM E6CarSpa.Desktop.exe >nul 2>&1
taskkill /F /IM dotnet.exe >nul 2>&1
timeout /t 3 /nobreak >nul

:: Delete service if exists
echo Deleting old service...
sc.exe delete E6CarSpaApi >nul 2>&1
timeout /t 3 /nobreak >nul

:: Copy files
echo Copying API...
xcopy "E:\TTS\Projects\Desktop_Apps\E6_car_care\dist\api\*" "C:\Program Files\E6 Car Spa\Api\" /E /Y /Q 2>&1
echo Copying Desktop...
xcopy "E:\TTS\Projects\Desktop_Apps\E6_car_care\dist\desktop\*" "C:\Program Files\E6 Car Spa\Desktop\" /E /Y /Q 2>&1

:: Create service
echo Creating service...
sc.exe create E6CarSpaApi binPath= "C:\Program Files\E6 Car Spa\Api\E6CarSpa.Api.exe" DisplayName= "E6 Car Spa API" start= auto obj= "LocalSystem"
sc.exe failure E6CarSpaApi reset= 86400 actions= restart/5000/restart/5000/restart/5000
netsh advfirewall firewall add rule name="E6 Car Spa API" dir=in action=allow protocol=TCP localport=5000 >nul 2>&1
netsh advfirewall firewall add rule name="E6 Car Spa API 5080" dir=in action=allow protocol=TCP localport=5080 >nul 2>&1

:: Start
echo Starting service...
sc.exe start E6CarSpaApi
timeout /t 12 /nobreak >nul

echo.
echo Service status:
sc.exe query E6CarSpaApi | findstr STATE
echo.
echo Health check:
curl -s http://localhost:5000/health 2>nul || echo   Not on 5000
curl -s http://localhost:5080/health 2>nul || echo   Not on 5080
echo.
echo Done.
pause
