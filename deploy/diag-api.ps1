# Find the API installation
$svc = Get-Service E6CarSpaApi -ErrorAction SilentlyContinue
if ($svc) {
    Write-Host "Service: $($svc.Status)"
    $path = (Get-CimInstance Win32_Service -Filter "Name='E6CarSpaApi'").PathName
    Write-Host "Path: $path"
} else {
    Write-Host "Service not found"
}

# Check common install locations
$loc = "C:\Program Files (x86)\E6 Car Spa"
if (Test-Path $loc) { Write-Host "Install found: $loc" } else { Write-Host "NOT at: $loc" }

# Check where E6CarSpa.Api.exe exists
Get-ChildItem 'C:\' -Filter 'E6CarSpa.Api.exe' -Recurse -ErrorAction SilentlyContinue -Depth 4 | ForEach-Object {
    Write-Host "Found: $($_.FullName)"
}
