$result = @()
$svc = Get-Service E6CarSpaApi -ErrorAction SilentlyContinue
$result += "Before: Status=$($svc.Status) StartType=$($svc.StartType)"

try {
    Set-Service -Name E6CarSpaApi -StartupType Automatic -ErrorAction Stop
    $result += "Set-Service OK"
} catch {
    $result += "Set-Service FAILED: $($_.Exception.Message)"
}

try {
    Start-Service E6CarSpaApi -ErrorAction Stop
    $result += "Start-Service OK"
} catch {
    $result += "Start-Service FAILED: $($_.Exception.Message)"
}

Start-Sleep -Seconds 3
$svc = Get-Service E6CarSpaApi
$result += "After: Status=$($svc.Status) StartType=$($svc.StartType)"

try {
    $r = Invoke-RestMethod http://localhost:5080/health -TimeoutSec 5
    $result += "Health 5080: $($r.status)"
} catch {
    $result += "Health 5080: NOT RESPONDING"
}

try {
    $r2 = Invoke-RestMethod http://localhost:5000/health -TimeoutSec 5
    $result += "Health 5000: $($r2.status)"
} catch {
    $result += "Health 5000: NOT RESPONDING"
}

$result | ForEach-Object { Write-Host $_ }
$result | Out-File "E:\TTS\Projects\Desktop_Apps\E6_car_care\deploy\fix-result.txt" -Encoding UTF8
