Set-Service -Name E6CarSpaApi -StartupType Automatic -ErrorAction SilentlyContinue
Start-Service E6CarSpaApi -ErrorAction SilentlyContinue
$svc = Get-Service E6CarSpaApi
"Status: $($svc.Status), StartType: $($svc.StartType)"

# Check health on port 5080
try {
    $r = Invoke-RestMethod http://localhost:5080/health -TimeoutSec 5
    "Health: $($r.status)"
} catch {
    "Health: not responding"
}
