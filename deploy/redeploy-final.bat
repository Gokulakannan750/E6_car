@echo off
echo ========================================
echo   E6 Car Spa - Final Redeploy
echo ========================================
echo.

echo [1/5] Stop any running API process...
taskkill /F /IM E6CarSpa.Api.exe >nul 2>&1
timeout /t 3 /nobreak >nul

echo [2/5] Delete old service (if exists)...
sc.exe delete E6CarSpaApi
timeout /t 3 /nobreak >nul
echo    Done.

echo [3/5] Copy API files...
xcopy "E:\TTS\Projects\Desktop_Apps\E6_car_care\dist\api\*" "C:\Program Files\E6 Car Spa\Api\" /E /Y /Q >nul
if errorlevel 1 echo    ERROR copying API files & goto :end
echo    Done.

echo [4/5] Copy Desktop files...
taskkill /F /IM E6CarSpa.Desktop.exe >nul 2>&1
xcopy "E:\TTS\Projects\Desktop_Apps\E6_car_care\dist\desktop\*" "C:\Program Files\E6 Car Spa\Desktop\" /E /Y /Q >nul
if errorlevel 1 echo    ERROR copying Desktop files & goto :end
echo    Done.

echo [5/5] Create and start service...
sc.exe create E6CarSpaApi binPath= "C:\Program Files\E6 Car Spa\Api\E6CarSpa.Api.exe" DisplayName= "E6 Car Spa API" start= auto obj= "LocalSystem"
if errorlevel 1 echo    ERROR creating service & goto :end
sc.exe failure E6CarSpaApi reset= 86400 actions= restart/5000/restart/5000/restart/5000
netsh advfirewall firewall add rule name="E6 Car Spa API" dir=in action=allow protocol=TCP localport=5000 >nul 2>&1
sc.exe start E6CarSpaApi
if errorlevel 1 echo    ERROR starting service (will retry on next boot) & goto :end

:end
echo.
echo ========================================
echo   Service status:
sc.exe query E6CarSpaApi | findstr STATE
echo.
echo   API health check:
powershell -Command "try { $r = Invoke-RestMethod http://localhost:5000/health -TimeoutSec 5; Write-Host '  UP: ' $r.status } catch { Write-Host '  Not responding yet' }"
echo.
echo ========================================
pause
