$apiDir = 'C:\Program Files\E6 Car Spa\Api'

# Show current ACL on appsettings.json
Write-Host "Current ACL on appsettings.json:"
icacls "$apiDir\appsettings.json"

# Grant the service account read+execute on the entire API folder
Write-Host "`nGranting service account perms..."
& icacls.exe "$apiDir" /grant "NT SERVICE\E6CarSpaApi:(OI)(CI)RX" /T | Out-Null
& icacls.exe "$apiDir" /grant "NT SERVICE\E6CarSpaApi:(OI)(CI)R" /T | Out-Null

# Verify
Write-Host "Updated ACL on appsettings.json:"
icacls "$apiDir\appsettings.json"

# Start the service
Write-Host "`nStarting service..."
Start-Service E6CarSpaApi
Start-Sleep 3
$svc = Get-Service E6CarSpaApi
Write-Host "Service status: $($svc.Status)"