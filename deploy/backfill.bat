@echo off
chcp 65001 >nul
set LOG=E:\TTS\Projects\Desktop_Apps\E6_car_care\deploy\backfill-log.txt
echo === %date% %time% === > "%LOG%"

set PGPASSWORD=Gokulakannan750

echo Current Staff data... >> "%LOG%"
"C:\Program Files\PostgreSQL\18\bin\psql.exe" -U postgres -d e6carspa -c "SELECT * FROM ""Staff"";" >> "%LOG%" 2>&1

echo. >> "%LOG%"
echo Current Users data... >> "%LOG%"
"C:\Program Files\PostgreSQL\18\bin\psql.exe" -U postgres -d e6carspa -c "SELECT * FROM ""Users"" LIMIT 5;" >> "%LOG%" 2>&1

echo. >> "%LOG%"
echo === DONE === >> "%LOG%"
type "%LOG%"
