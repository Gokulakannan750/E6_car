$ErrorActionPreference = 'Stop'
$out = "E:\TTS\Projects\Desktop_Apps\E6_car_care\deploy\fix-log.txt"
"=== $(Get-Date) ===" | Out-File $out

# Step 1: Drop the bad unique index
$env:PGPASSWORD = "E6CarSpa@2024"
& "C:\Program Files\PostgreSQL\18\bin\psql.exe" -U postgres -d e6carspa -c "DROP INDEX IF EXISTS `"IX_Staff_FullName`";" 2>&1 | Out-File $out -Append
"Dropped bad index (if existed)" | Out-File $out -Append

# Step 2: Grant service account read access to Api directory
$svc = Get-Service E6CarSpaApi
$acct = $svc.Properties["ObjectName"].Value
$dir = "C:\Program Files\E6 Car Spa\Api"
$acl = Get-Acl $dir
$rule = New-Object System.Security.AccessControl.FileSystemAccessRule($acct, "ReadAndExecute,ListDirectory", "ContainerInherit,ObjectInherit", "None", "Allow")
$acl.AddAccessRule($rule)
Set-Acl $dir $acl
"Granted read access to $acct" | Out-File $out -Append

# Step 3: Start service
"Starting service..." | Out-File $out -Append
Start-Service E6CarSpaApi -ErrorAction Stop
Start-Sleep -Seconds 25
$svc2 = Get-Service E6CarSpaApi
"Status: $($svc2.Status)" | Out-File $out -Append
