@echo off
echo ========================================
echo   E6 Car Spa - Full Redeploy
echo ========================================
echo.

echo [1/5] Stopping service if running...
net stop E6CarSpaApi 2>nul
timeout /t 2 /nobreak >nul

echo [2/5] Deleting old service...
sc.exe delete E6CarSpaApi >nul 2>&1
timeout /t 3 /nobreak >nul

echo [3/5] Copying API files...
xcopy "E:\TTS\Projects\Desktop_Apps\E6_car_care\dist\api\*" "C:\Program Files\E6 Car Spa\Api\" /E /Y /Q >nul 2>&1
echo   Done.

echo [4/5] Creating new service...
sc.exe create E6CarSpaApi binPath= "C:\Program Files\E6 Car Spa\Api\E6CarSpa.Api.exe" DisplayName= "E6 Car Spa API" start= auto >nul
sc.exe failure E6CarSpaApi reset= 86400 actions= restart/5000/restart/5000/restart/5000 >nul
netsh advfirewall firewall add rule name="E6 Car Spa API" dir=in action=allow protocol=TCP localport=5000 >nul 2>&1
echo   Done.

echo [5/5] Starting service...
net start E6CarSpaApi
timeout /t 5 /nobreak >nul

echo.
echo ========================================
echo   Status:
sc.exe query E6CarSpaApi | findstr STATE

echo.
echo [Check] API health:
powershell -Command "try { $r = Invoke-RestMethod http://localhost:5000/health -TimeoutSec 5; Write-Host '  Status: ' $r.status } catch { Write-Host '  Not responding yet (may need a few more seconds)' }"

echo.
echo ========================================
echo   Deploy complete.
echo ========================================
pause
