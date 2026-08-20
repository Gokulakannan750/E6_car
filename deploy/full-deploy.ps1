$ErrorActionPreference = 'Stop'
$log = @("=== $(Get-Date) ===")

# 1. Stop service
Stop-Service E6CarSpaApi -ErrorAction SilentlyContinue
Start-Sleep -Seconds 3
taskkill /F /IM dotnet.exe 2>&1 | Out-Null
Start-Sleep -Seconds 3
$log += "Service stopped."

# 2. Copy files
Copy-Item "E:\TTS\Projects\Desktop_Apps\E6_car_care\dist\api\*" "C:\Program Files\E6 Car Spa\Api\" -Recurse -Force
$log += "Files copied."

# 3. Grant service account read access to the Api directory
$svc = Get-Service E6CarSpaApi
$acct = $svc.Properties["ObjectName"].Value
$dir = "C:\Program Files\E6 Car Spa\Api"
$acl = Get-Acl $dir
$rule = New-Object System.Security.AccessControl.FileSystemAccessRule($acct, "ReadAndExecute,ListDirectory", "ContainerInherit,ObjectInherit", "None", "Allow")
$acl.AddAccessRule($rule)
Set-Acl $dir $acl
$log += "Granted read access to $acct."

# 4. Drop the bad unique index on Staff.FullName (fixes startup crash)
$env:PGPASSWORD = "E6CarSpa@2024"
& "C:\Program Files\PostgreSQL\17\bin\psql.exe" -U postgres -d e6carspa -c "DROP INDEX IF EXISTS \"IX_Staff_FullName\";" 2>&1 | Out-Null
$log += "Dropped bad unique index (if it existed)."

# 5. Start service
Start-Service E6CarSpaApi -ErrorAction Stop
Start-Sleep -Seconds 20

$svc2 = Get-Service E6CarSpaApi
$log += "Service status: $($svc2.Status)"

# 6. Test API
$body = @{username="admin"; password="admin123"} | ConvertTo-Json
$resp = Invoke-RestMethod -Uri "http://localhost:5080/api/auth/login" -Method POST -Body $body -ContentType "application/json"
$token = $resp.token
$headers = @{Authorization = "Bearer $token"}

try {
    $staff = Invoke-RestMethod -Uri "http://localhost:5080/api/staffadvances/staff" -Method GET -Headers $headers
    $log += "Staff OK: $($staff.Count) rows"
    foreach ($s in $staff) { $log += "  $($s.id) $($s.fullName) active=$($s.isActive)" }
} catch {
    $log += "Staff ERROR: $($_.Exception.Message)"
}

try {
    $sum = Invoke-RestMethod -Uri "http://localhost:5080/api/staffadvances/summary" -Method GET -Headers $headers
    $log += "Summary OK: $($sum.Count) rows"
    foreach ($r in $sum) { $log += "  $($r.staffId) $($r.staffName) total=$($r.totalAdvanced) count=$($r.advanceCount)" }
} catch {
    $log += "Summary ERROR: $($_.Exception.Message)"
}

$log | ForEach-Object { Write-Host $_ }
$log | Out-File "E:\TTS\Projects\Desktop_Apps\E6_car_care\deploy\full-result.txt" -Encoding UTF8
