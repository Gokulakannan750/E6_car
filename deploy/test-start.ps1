$logFile = "E:\TTS\Projects\Desktop_Apps\E6_car_care\deploy\startup-log.txt"
$errFile = "E:\TTS\Projects\Desktop_Apps\E6_car_care\deploy\startup-err.txt"

$psi = New-Object System.Diagnostics.ProcessStartInfo
$psi.FileName = "dotnet"
$psi.Arguments = '"C:\Program Files\E6 Car Spa\Api\E6CarSpa.Api.dll"'
$psi.WorkingDirectory = "C:\Program Files\E6 Car Spa\Api"
$psi.UseShellExecute = $false
$psi.RedirectStandardOutput = $true
$psi.RedirectStandardError = $true
$psi.CreateNoWindow = $true

$proc = [System.Diagnostics.Process]::Start($psi)
Start-Sleep -Seconds 12

if (!$proc.HasExited) {
    "Process running (PID: $($proc.Id))" | Out-File $logFile -Append
    try {
        $r = Invoke-RestMethod http://localhost:5000/health -TimeoutSec 5
        "API HEALTHY: $($r.status)" | Out-File $logFile -Append
    } catch {
        "API NOT RESPONDING after 12s" | Out-File $logFile -Append
    }
    $proc.Kill()
    $proc.WaitForExit
} else {
    "Process EXITED with code $($proc.ExitCode)" | Out-File $logFile -Append
}

$stdout = $proc.StandardOutput.ReadToEnd()
$stderr = $proc.StandardError.ReadToEnd()
$stdout | Out-File $logFile
$stderr | Out-File $errFile
"Exit code: $($proc.ExitCode)" | Out-File $logFile -Append
