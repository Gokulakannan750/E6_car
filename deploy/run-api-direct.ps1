$api = 'C:\Program Files\E6 Car Spa\Api'
$log = 'C:\Program Files\E6 Car Spa\Api\startup-log.txt'

Set-Location $api
Write-Host "Running API directly (will show crash output)..."

# Redirect all output to a log file
$psi = New-Object System.Diagnostics.ProcessStartInfo
$psi.FileName = "$api\E6CarSpa.Api.exe"
$psi.WorkingDirectory = $api
$psi.UseShellExecute = $false
$psi.RedirectStandardOutput = $true
$psi.RedirectStandardError = $true
$psi.CreateNoWindow = $true

$proc = [System.Diagnostics.Process]::Start($psi)
Start-Sleep 8

if (-not $proc.HasExited) {
    Write-Host "Process still running after 8s - likely waiting for DB or listening OK"
    Write-Host "Checking port 5080..."
    try {
        $r = Invoke-WebRequest -Uri "http://localhost:5080/" -UseBasicParsing -TimeoutSec 5
        Write-Host "API is running! Status: $($r.StatusCode)"
    } catch {
        Write-Host "Not responding yet"
    }
    $proc.Kill()
} else {
    $out = $proc.StandardOutput.ReadToEnd()
    $err = $proc.StandardError.ReadToEnd()
    Write-Host "Process exited with code: $($proc.ExitCode)"
    Write-Host "--- STDOUT ---"
    Write-Host $out
    Write-Host "--- STDERR ---"
    Write-Host $err
}
