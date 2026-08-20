$api = 'C:\Program Files\E6 Car Spa\Api'

# NT SERVICE\E6CarSpaApi is S-1-5-80-... but using the name is fine for icacls
# First reset ACL to allow Administrators full + service account read
icacls "$api\appsettings.json" /reset /T
icacls "$api\appsettings.json" /inheritance:e /T
icacls "$api\appsettings.json" /grant "Administrators:(F)" /T
icacls "$api\appsettings.json" /grant "SYSTEM:(R)" /T
icacls "$api\appsettings.json" /grant "NT SERVICE\E6CarSpaApi:(R)" /T

# Make the folder itself traversable
icacls "$api" /grant "NT SERVICE\E6CarSpaApi:(OI)(CI)RX" /T

Write-Host "Permissions updated. Starting service..."
sc.exe start E6CarSpaApi
Start-Sleep 5
$svc = Get-CimInstance Win32_Service -Filter "Name='E6CarSpaApi'"
Write-Host "State: $($svc.State)"
Write-Host "Status: $($svc.Status)"

# Test the API
Write-Host "Testing API..."
try {
    $r = Invoke-WebRequest -Uri "http://localhost:5080/api/auth/health" -UseBasicParsing -TimeoutSec 5
    Write-Host "API responded: $($r.StatusCode)"
} catch {
    Write-Host "Health endpoint not available: $($_.Exception.Message)"
}
try {
    $r = Invoke-WebRequest -Uri "http://localhost:5080/" -UseBasicParsing -TimeoutSec 5
    Write-Host "Root: $($r.StatusCode)"
} catch {
    Write-Host "Root unavailable: $($_.Exception.Message)"
}
