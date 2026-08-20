@echo off
echo ========================================
echo   E6 Car Spa - Final Deploy
echo ========================================
echo.

echo [1/5] Killing any running API process...
taskkill /F /IM E6CarSpa.Api.exe 2>&1
taskkill /F /IM dotnet.exe 2>&1
timeout /t 3 /nobreak >nul
echo    Done.

echo.
echo [2/5] Removing old service...
sc.exe delete E6CarSpaApi 2>&1
timeout /t 3 /nobreak >nul
echo    Done.

echo.
echo [3/5] Copying API files to Program Files...
xcopy "E:\TTS\Projects\Desktop_Apps\E6_car_care\dist\api\*" "C:\Program Files\E6 Car Spa\Api\" /E /Y 2>&1
echo    Done.

echo.
echo [4/5] Copying Desktop files...
taskkill /F /IM E6CarSpa.Desktop.exe >nul 2>&1
xcopy "E:\TTS\Projects\Desktop_Apps\E6_car_care\dist\desktop\*" "C:\Program Files\E6 Car Spa\Desktop\" /E /Y 2>&1
echo    Done.

echo.
echo [5/5] Creating Windows service...
sc.exe create E6CarSpaApi binPath= "C:\Program Files\E6 Car Spa\Api\E6CarSpa.Api.exe" DisplayName= "E6 Car Spa API" start= auto obj= "LocalSystem" 2>&1
if errorlevel 1 (
    echo    FAILED to create service!
    goto :check
)

echo    Setting failure actions...
sc.exe failure E6CarSpaApi reset= 86400 actions= restart/5000/restart/5000/restart/5000 2>&1

echo    Opening firewall port 5000...
netsh advfirewall firewall add rule name="E6 Car Spa API" dir=in action=allow protocol=TCP localport=5000 >nul 2>&1

echo    Starting service...
sc.exe start E6CarSpaApi 2>&1
if errorlevel 1 (
    echo    Service start returned error (may take a moment to initialize DB)
)

:check
echo.
echo ========================================
echo   Final Status:
echo ========================================
sc.exe query E6CarSpaApi

echo.
echo   API health (waiting 8s for startup)...
timeout /t 8 /nobreak >nul
powershell -Command "try { $r = Invoke-RestMethod http://localhost:5000/health -TimeoutSec 5; Write-Host '   API Status: ' $r.status } catch { Write-Host '   API not responding (check event log)' }"

echo.
echo ========================================
echo   Deploy finished.
echo ========================================
pause
