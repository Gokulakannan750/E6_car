@echo off
echo "=== %date% %time% ===" > "E:\TTS\Projects\Desktop_Apps\E6_car_care\deploy\event-log.txt"
echo Reading event log...
wevtutil qe Application /c:20 /f:text /q:"*[System[Provider[@Name='E6CarSpa'] or Provider[@Name='.NET Runtime'] or Provider[@Name='Microsoft-Windows-IIS-Configuration']]]" >> "E:\TTS\Projects\Desktop_Apps\E6_car_care\deploy\event-log.txt" 2>&1
echo Done.
