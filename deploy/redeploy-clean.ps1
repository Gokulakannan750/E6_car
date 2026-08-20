$logFile = "E:\TTS\Projects\Desktop_Apps\E6_car_care\deploy\redeploy-result.txt"

function Log($msg) { $msg | Out-File $logFile -Append -Encoding UTF8; Write-Host $msg }

Log "=== $(Get-Date) ==="

Log "[1] Stopping service..."
Stop-Service E6CarSpaApi -ErrorAction SilentlyContinue
Start-Sleep -Seconds 3

Log "[2] Killing dotnet..."
taskkill /F /IM dotnet.exe 2>&1 | Out-Null
Start-Sleep -Seconds 3

Log "[3] Copying API..."
Copy-Item "E:\TTS\Projects\Desktop_Apps\E6_car_care\dist\api\*" "C:\Program Files\E6 Car Spa\Api\" -Recurse -Force
Log "   Done."

Log "[4] Starting service..."
Start-Service E6CarSpaApi -ErrorAction SilentlyContinue
Start-Sleep -Seconds 15

Log "[5] Checking..."
$svc = Get-Service E6CarSpaApi
Log "   Service: $($svc.Status)"

try {
    $r = Invoke-RestMethod http://localhost:5000/health -TimeoutSec 5
    Log "   Health 5000: $($r.status)"
} catch { Log "   Health 5000: not responding" }

try {
    $r = Invoke-RestMethod http://localhost:5080/health -TimeoutSec 5
    Log "   Health 5080: $($r.status)"
} catch { Log "   Health 5080: not responding" }

Log "=== Done ==="
