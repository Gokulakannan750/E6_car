@echo off
chcp 65001 >nul
set LOG=E:\TTS\Projects\Desktop_Apps\E6_car_care\deploy\api-test.txt
echo === %date% %time% === > "%LOG%"

echo Getting token... >> "%LOG%"
powershell -Command "$body = '{\"username\":\"admin\",\"password\":\"admin123\"}'; $r = Invoke-RestMethod -Uri 'http://localhost:5080/api/auth/login' -Method POST -Body $body -ContentType 'application/json'; $r.token" > "E:\TTS\Projects\Desktop_Apps\E6_car_care\deploy\token.txt" 2>&1

echo Testing Staff endpoint... >> "%LOG%"
powershell -Command "$t = Get-Content E:\TTS\Projects\Desktop_Apps\E6_car_care\deploy\token.txt; $h = @{Authorization=\"Bearer $t\"}; $s = Invoke-RestMethod -Uri 'http://localhost:5080/api/staffadvances/staff' -Method GET -Headers $h; $s | ConvertTo-Json -Depth 3" >> "%LOG%" 2>&1

echo. >> "%LOG%"
echo Testing Summary endpoint... >> "%LOG%"
powershell -Command "$t = Get-Content E:\TTS\Projects\Desktop_Apps\E6_car_care\deploy\token.txt; $h = @{Authorization=\"Bearer $t\"}; $s = Invoke-RestMethod -Uri 'http://localhost:5080/api/staffadvances/summary' -Method GET -Headers $h; $s | ConvertTo-Json -Depth 3" >> "%LOG%" 2>&1

type "%LOG%"
