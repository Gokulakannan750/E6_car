Write-Host "=== Service Status ==="
Get-Service E6CarSpaApi -ErrorAction SilentlyContinue | Format-List Name, Status, StartType
Write-Host "=== API Binary ==="
$exe = Get-Item 'C:\Program Files\E6 Car Spa\Api\E6CarSpa.Api.exe' -ErrorAction SilentlyContinue
if ($exe) { Write-Host "  Found: $($exe.LastWriteTime)" } else { Write-Host "  NOT FOUND" }
Write-Host "=== Health Check ==="
try {
    $r = Invoke-RestMethod http://localhost:5000/health -TimeoutSec 5
    Write-Host "  Status: $($r.status)"
} catch {
    Write-Host "  Not responding: $_"
}
