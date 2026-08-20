@echo off
echo ========================================
echo   E6 Car Spa - Full Redeploy v2
echo ========================================
echo.

echo [1/6] Force-stop API process...
taskkill /F /IM E6CarSpa.Api.exe >nul 2>&1
timeout /t 2 /nobreak >nul

echo [2/6] Delete old service...
sc.exe stop E6CarSpaApi >nul 2>&1
sc.exe delete E6CarSpaApi >nul 2>&1
timeout /t 3 /nobreak >nul

echo [3/6] Copy API files...
xcopy "E:\TTS\Projects\Desktop_Apps\E6_car_care\dist\api\*" "C:\Program Files\E6 Car Spa\Api\" /E /Y /Q
echo   3a. API copied.

echo [4/6] Copy Desktop files...
taskkill /F /IM E6CarSpa.Desktop.exe >nul 2>&1
timeout /t 2 /nobreak >nul
xcopy "E:\TTS\Projects\Desktop_Apps\E6_car_care\dist\desktop\*" "C:\Program Files\E6 Car Spa\Desktop\" /E /Y /Q
echo   4a. Desktop copied.

echo [5/6] Create and start service...
sc.exe create E6CarSpaApi binPath= "C:\Program Files\E6 Car Spa\Api\E6CarSpa.Api.exe" DisplayName= "E6 Car Spa API" start= auto obj= "LocalSystem" >nul
sc.exe failure E6CarSpaApi reset= 86400 actions= restart/5000/restart/5000/restart/5000 >nul
netsh advfirewall firewall add rule name="E6 Car Spa API" dir=in action=allow protocol=TCP localport=5000 >nul 2>&1
sc.exe start E6CarSpaApi
echo   Waiting 10s for startup...
timeout /t 10 /nobreak >nul

echo [6/6] Verify...
echo.
echo   Service status:
sc.exe query E6CarSpaApi | findstr STATE
echo.
echo   API health:
curl -s http://localhost:5000/health 2>nul || echo   (not responding yet)
echo.
echo ========================================
echo   Done.
echo ========================================
pause
