$out = "E:\TTS\Projects\Desktop_Apps\E6_car_care\deploy\final-result.txt"
$log = @()
$log += "=== $(Get-Date) ==="

# Stop service
$log += "Stopping service..."
Stop-Service E6CarSpaApi -ErrorAction SilentlyContinue
Start-Sleep -Seconds 3

# Kill any dotnet process
taskkill /F /IM dotnet.exe 2>&1 | Out-Null
Start-Sleep -Seconds 3

# Copy new API files (force overwrite)
$log += "Copying API..."
Copy-Item "E:\TTS\Projects\Desktop_Apps\E6_car_care\dist\api\*" "C:\Program Files\E6 Car Spa\Api\" -Recurse -Force
$log += "Done."

# Verify the new file timestamp
$exe = Get-Item "C:\Program Files\E6 Car Spa\Api\E6CarSpa.Api.exe" -ErrorAction SilentlyContinue
$log += "API exe timestamp: $($exe.LastWriteTime)"

# Start service
$log += "Starting service..."
Start-Service E6CarSpaApi -ErrorAction SilentlyContinue
Start-Sleep -Seconds 15

# Check
$svc = Get-Service E6CarSpaApi
$log += "Service: $($svc.Status)"

$proc = Get-Process -Name "E6CarSpa.Api" -ErrorAction SilentlyContinue
if ($proc) {
    $log += "Process: PID $($proc.Id)"
    $conns = Get-NetTCPConnection -OwningProcess $proc.Id -State Listen -ErrorAction SilentlyContinue
    foreach ($c in $conns) { $log += "  Listening on port $($c.LocalPort)" }
}

try { $r = Invoke-RestMethod http://localhost:5000/health -TimeoutSec 5; $log += "5000: $($r.status)" } catch { $log += "5000: no" }
try { $r = Invoke-RestMethod http://localhost:5080/health -TimeoutSec 5; $log += "5080: $($r.status)" } catch { $log += "5080: no" }

$log | ForEach-Object { Write-Host $_ }
$log | Out-File $out -Encoding UTF8
