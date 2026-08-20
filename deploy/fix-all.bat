@echo off
chcp 65001 >nul
echo === %date% %time% === > "E:\TTS\Projects\Desktop_Apps\E6_car_care\deploy\fix-log.txt"

set PGPASSWORD=E6CarSpa@2024
echo Step 1: Drop bad index... >> "E:\TTS\Projects\Desktop_Apps\E6_car_care\deploy\fix-log.txt"
"C:\Program Files\PostgreSQL\18\bin\psql.exe" -U postgres -d e6carspa -c "DROP INDEX IF EXISTS ""IX_Staff_FullName"";" >> "E:\TTS\Projects\Desktop_Apps\E6_car_care\deploy\fix-log.txt" 2>&1
echo Step 2: Start service... >> "E:\TTS\Projects\Desktop_Apps\E6_car_care\deploy\fix-log.txt"
net start E6CarSpaApi >> "E:\TTS\Projects\Desktop_Apps\E6_car_care\deploy\fix-log.txt" 2>&1
echo Waiting 25s... >> "E:\TTS\Projects\Desktop_Apps\E6_car_care\deploy\fix-log.txt"
ping -n 26 127.0.0.1 >nul
sc query E6CarSpaApi >> "E:\TTS\Projects\Desktop_Apps\E6_car_care\deploy\fix-log.txt" 2>&1
echo === END === >> "E:\TTS\Projects\Desktop_Apps\E6_car_care\deploy\fix-log.txt"
