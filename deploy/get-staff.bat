@echo off
chcp 65001 >nul
set LOG=E:\TTS\Projects\Desktop_Apps\E6_car_care\deploy\backfill-log.txt
echo === %date% %time% === > "%LOG%"

REM Get token
powershell -Command "$body = '{\"username\":\"admin\",\"password\":\"admin123\"}'; $r = Invoke-RestMethod -Uri 'http://localhost:5080/api/auth/login' -Method POST -Body $body -ContentType 'application/json'; $r.token" > "E:\TTS\Projects\Desktop_Apps\E6_car_care\deploy\token.txt" 2>&1

echo Token obtained >> "%LOG%"

REM Get staff list
powershell -Command "$t = Get-Content E:\TTS\Projects\Desktop_Apps\E6_car_care\deploy\token.txt; $h = @{Authorization=\"Bearer $t\"}; Invoke-RestMethod -Uri 'http://localhost:5080/api/staffadvances/staff' -Method GET -Headers $h | ConvertTo-Json" > "E:\TTS\Projects\Desktop_Apps\E6_car_care\deploy\staff-list.json" 2>&1

echo Staff list: >> "%LOG%"
type "E:\TTS\Projects\Desktop_Apps\E6_car_care\deploy\staff-list.json" >> "%LOG%"

echo. >> "%LOG%"
echo === DONE === >> "%LOG%"
type "%LOG%"
