$ErrorActionPreference = 'Continue'
$out = "E:\TTS\Projects\Desktop_Apps\E6_car_care\deploy\svc-start.txt"
"=== $(Get-Date) ===" | Out-File $out

try {
    $svc = Get-Service E6CarSpaApi -ErrorAction Stop
    "Found service: $($svc.Status)" | Out-File $out -Append
    Start-Service E6CarSpaApi -ErrorAction Stop
    "Start command sent" | Out-File $out -Append
    Start-Sleep -Seconds 20
    $svc2 = Get-Service E6CarSpaApi
    "Status after 20s: $($svc2.Status)" | Out-File $out -Append
} catch {
    "ERROR: $_" | Out-File $out -Append
}
