@echo off
chcp 65001 >nul
set LOG=E:\TTS\Projects\Desktop_Apps\E6_car_care\deploy\verify.txt
echo === %date% %time% === > "%LOG%"
set PGPASSWORD=E6CarSpa@2024
echo Checking indexes on Staff table... >> "%LOG%"
"C:\Program Files\PostgreSQL\18\bin\psql.exe" -U postgres -d e6carspa -c "\di *Staff*" >> "%LOG%" 2>&1
echo. >> "%LOG%"
echo Checking FullName column... >> "%LOG%"
"C:\Program Files\PostgreSQL\18\bin\psql.exe" -U postgres -d e6carspa -c "SELECT column_name, data_type, is_nullable FROM information_schema.columns WHERE table_name='Staff' AND column_name='FullName';" >> "%LOG%" 2>&1
echo. >> "%LOG%"
echo Staff rows: >> "%LOG%"
"C:\Program Files\PostgreSQL\18\bin\psql.exe" -U postgres -d e6carspa -c "SELECT id, fullname, isactive FROM \"Staff\";" >> "%LOG%" 2>&1
type "%LOG%"
