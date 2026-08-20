@echo off
chcp 65001 >nul
setlocal ENABLEDELAYEDEXPANSION
set LOG=E:\TTS\Projects\Desktop_Apps\E6_car_care\deploy\deploy-result.txt

echo === %date% %time% === > "%LOG%"

echo Stopping service... >> "%LOG%"
net stop E6CarSpaApi >> "%LOG%" 2>&1
timeout /t 3 /nobreak >nul

echo Copying files... >> "%LOG%"
xcopy "E:\TTS\Projects\Desktop_Apps\E6_car_care\dist\api\*" "C:\Program Files\E6 Car Spa\Api\" /Y /Q >> "%LOG%" 2>&1
echo Files copied. >> "%LOG%"

echo Dropping bad index... >> "%LOG%"
set PGPASSWORD=E6CarSpa@2024
"C:\Program Files\PostgreSQL\18\bin\psql.exe" -U postgres -d e6carspa -c "DROP INDEX IF EXISTS ""IX_Staff_FullName"";" >> "%LOG%" 2>&1
echo Index dropped. >> "%LOG%"

echo Starting service... >> "%LOG%"
net start E6CarSpaApi >> "%LOG%" 2>&1
echo Waiting 25s for startup... >> "%LOG%"
timeout /t 25 /nobreak >nul

sc query E6CarSpaApi >> "%LOG%" 2>&1
echo. >> "%LOG%"

echo Testing API... >> "%LOG%"
powershell -Command "$body = '{\"username\":\"admin\",\"password\":\"admin123\"}'; $r = Invoke-RestMethod -Uri 'http://localhost:5080/api/auth/login' -Method POST -Body $body -ContentType 'application/json'; Write-Host 'Login: OK token=' $r.token.substring(0,10)" >> "%LOG%" 2>&1

echo. >> "%LOG%"
echo === DONE === >> "%LOG%"
type "%LOG%"
