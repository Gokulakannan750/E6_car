$ErrorActionPreference = 'Stop'
Set-Content "E:\TTS\Projects\Desktop_Apps\E6_car_care\deploy\svc-log.txt" "=== $(Get-Date) ==="
Add-Content "E:\TTS\Projects\Desktop_Apps\E6_car_care\deploy\svc-log.txt" "Starting service..."
try {
    Start-Service E6CarSpaApi -ErrorAction Stop
    Add-Content "E:\TTS\Projects\Desktop_Apps\E6_car_care\deploy\svc-log.txt" "Service start command succeeded."
    Start-Sleep -Seconds 20
    $svc = Get-Service E6CarSpaApi
    Add-Content "E:\TTS\Projects\Desktop_Apps\E6_car_care\deploy\svc-log.txt" "Status: $($svc.Status)"
} catch {
    Add-Content "E:\TTS\Projects\Desktop_Apps\E6_car_care\deploy\svc-log.txt" "ERROR: $_"
}
