@echo off
chcp 65001 >nul
echo ========================================
echo E6 Car Spa — Deploy Fix
echo ========================================
echo.

set SOURCE=C:\Users\gokul\.claude\projects\E--TTS-Projects-Desktop-Apps-E6-car-care\publish-temp
set DEST=C:\Program Files\E6 Car Spa\Api

echo [1/4] Stopping E6CarSpaApi service...
net stop E6CarSpaApi 2>nul
timeout /t 3 /nobreak >nul

echo [2/4] Backing up current deployment...
if not exist "%DEST%\backup" mkdir "%DEST%\backup"
xcopy "%DEST%\E6CarSpa.Api.dll" "%DEST%\backup\" /Y /Q 2>nul
xcopy "%DEST%\E6CarSpa.Api.deps.json" "%DEST%\backup\" /Y /Q 2>nul
xcopy "%DEST%\E6CarSpa.Api.runtimeconfig.json" "%DEST%\backup\" /Y /Q 2>nul

echo [3/4] Deploying new files...
xcopy "%SOURCE%\E6CarSpa.Api.dll" "%DEST%\" /Y /Q
xcopy "%SOURCE%\E6CarSpa.Api.deps.json" "%DEST%\" /Y /Q
xcopy "%SOURCE%\E6CarSpa.Api.runtimeconfig.json" "%DEST%\" /Y /Q
xcopy "%SOURCE%\E6CarSpa.Infrastructure.dll" "%DEST%\" /Y /Q
xcopy "%SOURCE%\E6CarSpa.Domain.dll" "%DEST%\" /Y /Q
xcopy "%SOURCE%\E6CarSpa.Contracts.dll" "%DEST%\" /Y /Q

echo [4/4] Starting E6CarSpaApi service...
net start E6CarSpaApi

echo.
echo ========================================
echo Deploy complete!
echo ========================================
pause
