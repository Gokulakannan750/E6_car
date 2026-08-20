$out = "E:\TTS\Projects\Desktop_Apps\E6_car_care\deploy\event-check.txt"
$log = @()
$log += "=== $(Get-Date) ==="

# Check Windows Event Log for .NET Runtime errors
$cutoff = (Get-Date).AddMinutes(-30)
$evts = Get-EventLog -LogName Application -Source '.NET Runtime' -Newest 5 -ErrorAction SilentlyContinue
if ($evts) {
    foreach ($e in $evts) {
        if ($e.TimeGenerated -gt $cutoff) {
            $log += ""
            $log += "$($e.TimeGenerated) [$($e.EntryType)]"
            $log += $e.Message
        }
    }
} else {
    $log += "No .NET Runtime events found"
}

# Also check the API's own error log file
$logPath = "C:\Program Files\E6 Car Spa\Api\error.log"
if (Test-Path $logPath) {
    $log += ""
    $log += "=== API error.log ==="
    $log += (Get-Content $logPath -Tail 30)
}

$logPath2 = "C:\Program Files\E6 Car Spa\Api\e6carspa.log"
if (Test-Path $logPath2) {
    $log += ""
    $log += "=== e6carspa.log (last 30 lines) ==="
    $log += (Get-Content $logPath2 -Tail 30)
}

$log | ForEach-Object { Write-Host $_ }
$log | Out-File $out -Encoding UTF8
