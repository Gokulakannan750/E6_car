$outFile = "E:\TTS\Projects\Desktop_Apps\E6_car_care\deploy\diag-result.txt"
$log = @()
$log += "=== $(Get-Date) ==="
$svc = Get-Service E6CarSpaApi -ErrorAction SilentlyContinue
$log += "Before start: Status=$($svc.Status) StartType=$($svc.StartType)"

try {
    Start-Service E6CarSpaApi -ErrorAction Stop
    Start-Sleep -Seconds 10
    $svc = Get-Service E6CarSpaApi
    $log += "After start: Status=$($svc.Status) StartType=$($svc.StartType)"
} catch {
    $log += "Start failed: $($_.Exception.Message)"
}

$events = Get-EventLog -LogName Application -Source 'E6CarSpa*' -Newest 5 -ErrorAction SilentlyContinue
foreach ($e in $events) {
    $log += ""
    $log += "$($e.TimeGenerated) [$($e.EntryType)]"
    $log += $e.Message.Substring(0, [Math]::Min(500, $e.Message.Length))
}

$health = try { Invoke-RestMethod http://localhost:5000/health -TimeoutSec 5 } catch { "Not responding: $_" }
$log += ""
$log += "Health: $health"

$log | Out-File $outFile -Encoding UTF8
Write-Host "Done. Results in $outFile"
