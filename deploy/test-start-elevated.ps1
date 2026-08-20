$logFile = "E:\TTS\Projects\Desktop_Apps\E6_car_care\deploy\direct-start-result.txt"

$proc = Start-Process -FilePath "dotnet" `
    -ArgumentList '"C:\Program Files\E6 Car Spa\Api\E6CarSpa.Api.dll"' `
    -WorkingDirectory "C:\Program Files\E6 Car Spa\Api" `
    -NoNewWindow -PassThru `
    -RedirectStandardOutput "E:\TTS\Projects\Desktop_Apps\E6_car_care\deploy\direct-stdout.txt" `
    -RedirectStandardError "E:\TTS\Projects\Desktop_Apps\E6_car_care\deploy\direct-stderr.txt"

Start-Sleep -Seconds 12

if (!$proc.HasExited) {
    "STATUS: RUNNING (PID $($proc.Id))" | Out-File $logFile
    try {
        $r = Invoke-RestMethod http://localhost:5000/health -TimeoutSec 5
        "HEALTH: $($r.status)" | Out-File $logFile -Append
    } catch {
        "HEALTH: NOT RESPONDING" | Out-File $logFile -Append
    }
    Stop-Process -Id $proc.Id -Force
} else {
    "STATUS: CRASHED (exit code $($proc.ExitCode))" | Out-File $logFile
}

$out = Get-Content "E:\TTS\Projects\Desktop_Apps\E6_car_care\deploy\direct-stdout.txt" -Raw -ErrorAction SilentlyContinue
$err = Get-Content "E:\TTS\Projects\Desktop_Apps\E6_car_care\deploy\direct-stderr.txt" -Raw -ErrorAction SilentlyContinue

"--- STDOUT (last 2000 chars) ---" | Out-File $logFile -Append
$out.Substring([Math]::Max(0, $out.Length - 2000)) | Out-File $logFile -Append
"--- STDERR (last 2000 chars) ---" | Out-File $logFile -Append
$err.Substring([Math]::Max(0, $err.Length - 2000)) | Out-File $logFile -Append
