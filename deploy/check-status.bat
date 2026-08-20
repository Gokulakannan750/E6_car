@echo off
chcp 65001 >nul
set LOG=E:\TTS\Projects\Desktop_Apps\E6_car_care\deploy\status.txt
echo === %date% %time% === > "%LOG%"

sc query E6CarSpaApi >> "%LOG%" 2>&1

echo. >> "%LOG%"
echo Testing API... >> "%LOG%"
powershell -Command "$body = '{\"username\":\"admin\",\"password\":\"admin123\"}'; $r = Invoke-RestMethod -Uri 'http://localhost:5080/api/auth/login' -Method POST -Body $body -ContentType 'application/json'; $r.token" > "E:\TTS\Projects\Desktop_Apps\E6_car_care\deploy\token.txt" 2>&1

powershell -Command "$t = Get-Content E:\TTS\Projects\Desktop_Apps\E6_car_care\deploy\token.txt; $h = @{Authorization=\"Bearer $t\"}; $s = Invoke-RestMethod -Uri 'http://localhost:5080/api/staffadvances/staff' -Method GET -Headers $h; echo Staff count: $($s.Count); $s | ForEach-Object { echo \"  ID: $($_.id) Name: [$($_.fullName)] Active: $($_.isActive)\" }" >> "%LOG%" 2>&1

powershell -Command "$t = Get-Content E:\TTS\Projects\Desktop_Apps\E6_car_care\deploy\token.txt; $h = @{Authorization=\"Bearer $t\"}; $s = Invoke-RestMethod -Uri 'http://localhost:5080/api/staffadvances/summary' -Method GET -Headers $h; echo Summary count: $($s.Count); $s | ForEach-Object { echo \"  Staff: [$($_.staffName)] Total: $($_.totalAdvanced) Count: $($_.advanceCount)\" }" >> "%LOG%" 2>&1

type "%LOG%"
