$logFile = "E:\TTS\Projects\Desktop_Apps\E6_car_care\deploy\oneshot-result.txt"

function Log($msg) {
    $msg | Out-File $logFile -Append -Encoding UTF8
    Write-Host $msg
}

Log "=== $(Get-Date) - Oneshot Deploy ==="

# Kill all dotnet to release handles
Log "[1] Killing dotnet processes..."
taskkill /F /IM dotnet.exe 2>&1 | Out-Null
Start-Sleep -Seconds 3

# Delete old service
Log "[2] Deleting old service..."
sc.exe stop E6CarSpaApi >$null 2>&1
Start-Sleep -Seconds 2
sc.exe delete E6CarSpaApi 2>&1 | Out-File $logFile -Append
Start-Sleep -Seconds 5

# Verify gone
$svcCheck = sc.exe query E6CarSpaApi 2>&1
if ($LASTEXITCODE -eq 0) {
    Log "   Service still exists, waiting more..."
    Start-Sleep -Seconds 5
    sc.exe delete E6CarSpaApi 2>&1 | Out-File $logFile -Append
    Start-Sleep -Seconds 5
}

# Create service
Log "[3] Creating service..."
sc.exe create E6CarSpaApi binPath= "C:\Program Files\E6 Car Spa\Api\E6CarSpa.Api.exe" DisplayName= "E6 Car Spa API" start= auto obj= "LocalSystem" 2>&1 | Out-File $logFile -Append
if ($LASTEXITCODE -ne 0) {
    Log "   CREATE FAILED"
}

sc.exe failure E6CarSpaApi reset= 86400 actions= restart/5000/restart/5000/restart/5000 2>&1 | Out-Null
netsh advfirewall firewall add rule name="E6 Car Spa API" dir=in action=allow protocol=TCP localport=5000 >$null 2>&1
netsh advfirewall firewall add rule name="E6 Car Spa API 5080" dir=in action=allow protocol=TCP localport=5080 >$null 2>&1

# Start service
Log "[4] Starting service..."
sc.exe start E6CarSpaApi 2>&1 | Out-File $logFile -Append

Log "[5] Waiting 15s for startup..."
Start-Sleep -Seconds 15

# Check status
Log ""
Log "[6] Service status:"
sc.exe query E6CarSpaApi | Out-File $logFile -Append

# Check health
Log "[7] Health checks:"
try {
    $r = Invoke-RestMethod http://localhost:5000/health -TimeoutSec 5
    Log "   Port 5000: $($r.status)"
} catch {
    Log "   Port 5000: not responding"
}
try {
    $r = Invoke-RestMethod http://localhost:5080/health -TimeoutSec 5
    Log "   Port 5080: $($r.status)"
} catch {
    Log "   Port 5080: not responding"
}

Log ""
Log "=== Deploy complete ==="
