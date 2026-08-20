$logFile = "E:\TTS\Projects\Desktop_Apps\E6_car_care\deploy\direct-start-log.txt"
$proc = Start-Process -FilePath "dotnet" -ArgumentList '"C:\Program Files\E6 Car Spa\Api\E6CarSpa.Api.dll"' -WorkingDirectory "C:\Program Files\E6 Car Spa\Api" -NoNewWindow -PassThru -RedirectStandardError "$logFile.err" -RedirectStandardOutput "$logFile.out"
Start-Sleep -Seconds 8

if (!$proc.HasExited) {
    Write-Host "Process running (PID: $($proc.Id))"
    try {
        $r = Invoke-RestMethod http://localhost:5000/health -TimeoutSec 5
        Write-Host "API is UP: $($r.status)"
    } catch {
        Write-Host "API not responding after 8s"
    }
    Stop-Process -Id $proc.Id -Force
} else {
    Write-Host "Process exited with code $($proc.ExitCode)"
    if (Test-Path "$logFile.err") {
        Write-Host "=== STDERR ==="
        Get-Content "$logFile.err" -ErrorAction SilentlyContinue | Select-Object -First 50
    }
}
