@echo off
chcp 65001 >nul
echo ========================================
echo   ONE-SHOT: Delete + Create + Start
echo ========================================

:: Step 1: Kill ALL dotnet processes to release service handles
echo.
echo [1] Killing dotnet processes...
taskkill /F /IM dotnet.exe >nul 2>&1
timeout /t 3 /nobreak >nul

:: Step 2: Delete service (may fail if already deleted - that's OK)
echo [2] Deleting old service...
sc.exe stop E6CarSpaApi >nul 2>&1
timeout /t 2 /nobreak >nul
sc.exe delete E6CarSpaApi >nul 2>&1

:: Wait for SCM to fully clean up
echo    Waiting for SCM cleanup...
timeout /t 5 /nobreak >nul

:: Verify it's gone
sc.exe query E6CarSpaApi >nul 2>&1
if %errorlevel%==0 (
    echo    Service still exists, trying again...
    timeout /t 3 /nobreak >nul
    sc.exe delete E6CarSpaApi >nul 2>&1
    timeout /t 5 /nobreak >nul
)

:: Step 3: Create service
echo [3] Creating service...
sc.exe create E6CarSpaApi binPath= "C:\Program Files\E6 Car Spa\Api\E6CarSpa.Api.exe" DisplayName= "E6 Car Spa API" start= auto obj= "LocalSystem"
if errorlevel 1 (
    echo    FAILED to create service
    goto :start
)

sc.exe failure E6CarSpaApi reset= 86400 actions= restart/5000/restart/5000/restart/5000 >nul
netsh advfirewall firewall add rule name="E6 Car Spa API" dir=in action=allow protocol=TCP localport=5000 >nul 2>&1
netsh advfirewall firewall add rule name="E6 Car Spa API 5080" dir=in action=allow protocol=TCP localport=5080 >nul 2>&1

:start
echo [4] Starting service...
sc.exe start E6CarSpaApi 2>&1
if errorlevel 1 echo    Start returned error, waiting to see if it recovers...

echo [5] Waiting 12 seconds for startup...
timeout /t 12 /nobreak >nul

echo.
echo ========================================
echo   Status:
sc.exe query E6CarSpaApi | findstr STATE
echo.
echo   Health check (port 5000):
curl -s http://localhost:5000/health 2>nul || echo    (not on 5000)
echo.
echo   Health check (port 5080):
curl -s http://localhost:5080/health 2>nul || echo    (not on 5080)
echo.
echo ========================================
echo   Done.
echo ========================================
pause
