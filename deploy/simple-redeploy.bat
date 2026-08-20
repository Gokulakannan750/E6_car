@echo off
chcp 65001 >nul
echo ========================================
echo   Simple Redeploy (no service delete)
echo ========================================
echo.

echo [1] Stopping service...
sc.exe stop E6CarSpaApi 2>&1
timeout /t 5 /nobreak >nul

echo [2] Killing dotnet process...
taskkill /F /IM dotnet.exe >nul 2>&1
timeout /t 3 /nobreak >nul

echo [3] Copying API files...
xcopy "E:\TTS\Projects\Desktop_Apps\E6_car_care\dist\api\*" "C:\Program Files\E6 Car Spa\Api\" /E /Y /Q
echo    Done.

echo [4] Starting service...
sc.exe start E6CarSpaApi 2>&1

echo [5] Waiting 12s...
timeout /t 12 /nobreak >nul

echo.
echo Service status:
sc.exe query E6CarSpaApi | findstr STATE
echo.
echo Health check:
powershell -Command "try { $r = Invoke-RestMethod http://localhost:5000/health -TimeoutSec 5; Write-Host '  5000: ' $r.status } catch { Write-Host '  5000: not responding' }"
powershell -Command "try { $r = Invoke-RestMethod http://localhost:5080/health -TimeoutSec 5; Write-Host '  5080: ' $r.status } catch { Write-Host '  5080: not responding' }"
echo.
echo ========================================
pause
