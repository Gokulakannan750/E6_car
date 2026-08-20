$logFile = "E:\TTS\Projects\Desktop_Apps\E6_car_care\deploy\full-log.txt"
$errFile = "E:\TTS\Projects\Desktop_Apps\E6_car_care\deploy\full-err.txt"

$proc = Start-Process -FilePath "dotnet" `
    -ArgumentList '"C:\Program Files\E6 Car Spa\Api\E6CarSpa.Api.dll"' `
    -WorkingDirectory "C:\Program Files\E6 Car Spa\Api" `
    -NoNewWindow -PassThru `
    -RedirectStandardOutput $logFile `
    -RedirectStandardError $errFile

Start-Sleep -Seconds 15

$log = @()
$log += "PID: $($proc.Id) Exited: $($proc.HasExited)"

if (!$proc.HasExited) {
    $ports = Get-NetTCPConnection -OwningProcess $proc.Id -ErrorAction SilentlyContinue
    foreach ($p in $ports) { $log += "Port: $($p.LocalPort) State: $($p.State)" }

    try { $r = Invoke-RestMethod http://localhost:5000/health -TimeoutSec 5; $log += "5000: $($r.status)" } catch { $log += "5000: no" }
    try { $r = Invoke-RestMethod http://localhost:5080/health -TimeoutSec 5; $log += "5080: $($r.status)" } catch { $log += "5080: no" }

    Stop-Process -Id $proc.Id -Force
} else {
    $log += "EXITED code: $($proc.ExitCode)"
}

$log | ForEach-Object { Write-Host $_ }
