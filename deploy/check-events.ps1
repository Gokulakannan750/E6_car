$out = "E:\TTS\Projects\Desktop_Apps\E6_car_care\deploy\event-log.txt"
$lines = @()
$cutoff = (Get-Date).AddMinutes(-10)

$lines += "=== Application Log (E6CarSpa) ==="
$evts = Get-EventLog -LogName Application -Source 'E6CarSpa*' -Newest 10 -ErrorAction SilentlyContinue
foreach ($e in $evts) {
    if ($e.TimeGenerated -gt $cutoff) {
        $lines += ""
        $lines += "$($e.TimeGenerated) [$($e.EntryType)]"
        $lines += $e.Message.Substring(0, [Math]::Min(800, $e.Message.Length))
    }
}

$lines += ""
$lines += "=== System Log (service) ==="
$evts2 = Get-EventLog -LogName System -Newest 5 -ErrorAction SilentlyContinue
foreach ($e in $evts2) {
    if ($e.TimeGenerated -gt $cutoff) {
        $lines += ""
        $lines += "$($e.TimeGenerated) [$($e.EntryType)]"
        $lines += $e.Message.Substring(0, [Math]::Min(400, $e.Message.Length))
    }
}

$lines += ""
$lines += "=== Listening ports ==="
Get-NetTCPConnection -State Listen -ErrorAction SilentlyContinue | Where-Object { $_.OwningProcess -eq 3024 } | ForEach-Object {
    $lines += "  Port $($_.LocalPort) ($($_.OwningProcess))"
}

$lines | ForEach-Object { Write-Host $_ }
$lines | Out-File $out -Encoding UTF8
