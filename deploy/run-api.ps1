$apiDir = 'C:\Program Files\E6 Car Spa\Api'
Set-Location $apiDir
Write-Host "Running API directly..."
$p = Start-Process -FilePath "$apiDir\E6CarSpa.Api.exe" -PassThru -NoNewWindow -Wait
Write-Host "Exit: $($p.ExitCode)"
