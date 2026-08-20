# Check the service account and current ACL
$svc = Get-CimInstance Win32_Service -Filter "Name='E6CarSpaApi'" -ErrorAction SilentlyContinue
if ($svc) {
    Write-Host "Service: $($svc.Name)"
    Write-Host "Path: $($svc.PathName)"
    Write-Host "StartMode: $($svc.StartMode)"
    Write-Host "State: $($svc.State)"
    Write-Host "ErrorControl: $($svc.ErrorControl)"
}

Write-Host ""
Write-Host "=== ACL on appsettings.json ==="
$acl = Get-Acl 'C:\Program Files\E6 Car Spa\Api\appsettings.json'
foreach ($ace in $acl.Access) {
    $id = $ace.IdentityReference
    Write-Host "$id : $($ace.FileSystemRights) / $($ace.AccessControlType)"
}
