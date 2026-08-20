@echo off
chcp 65001 >nul
set LOG=E:\TTS\Projects\Desktop_Apps\E6_car_care\deploy\backfill-log.txt
echo === %date% %time% === > "%LOG%"

set PGPASSWORD=Gokulakannan750

echo Checking Staff columns... >> "%LOG%"
"C:\Program Files\PostgreSQL\18\bin\psql.exe" -U postgres -d e6carspa -c "SELECT column_name FROM information_schema.columns WHERE table_name='Staff' ORDER BY ordinal_position;" >> "%LOG%" 2>&1

echo. >> "%LOG%"
echo Checking Users columns... >> "%LOG%"
"C:\Program Files\PostgreSQL\18\bin\psql.exe" -U postgres -d e6carspa -c "SELECT column_name FROM information_schema.columns WHERE table_name='Users' ORDER BY ordinal_position;" >> "%LOG%" 2>&1

echo. >> "%LOG%"
echo Current Staff rows... >> "%LOG%"
"C:\Program Files\PostgreSQL\18\bin\psql.exe" -U postgres -d e6carspa -c "SELECT * FROM ""Staff"";" >> "%LOG%" 2>&1

echo. >> "%LOG%"
echo Current Users rows... >> "%LOG%"
"C:\Program Files\PostgreSQL\18\bin\psql.exe" -U postgres -d e6carspa -c "SELECT ""Id"", ""FullName"" FROM ""Users"" LIMIT 5;" >> "%LOG%" 2>&1

echo. >> "%LOG%"
echo === DONE === >> "%LOG%"
type "%LOG%"
