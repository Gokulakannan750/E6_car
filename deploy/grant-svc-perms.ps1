$ErrorActionPreference = 'Stop'
$svcName = "E6CarSpaApi"
$svc = Get-Service $svcName -ErrorAction SilentlyContinue
if (-not $svc) { Write-Host "Service not found"; exit 1 }

$acct = $svc.Properties["ObjectName"].Value
Write-Host "Service account: $acct"

$path = "C:\Program Files\E6 Car Spa\Api\appsettings.json"
$acl = Get-Acl $path
$identity = New-Object System.Security.Principal.NTAccount $acct
Write-Host "Identity SID: $($identity.Translate([System.Security.Principal.SecurityIdentifier]).Value)"

$rule = New-Object System.Security.AccessControl.FileSystemAccessRule($acct, "Read", "Allow")
$acl.SetAccessRule($rule)
Set-Acl $path $acl
Write-Host "Granted read access to $acct"
