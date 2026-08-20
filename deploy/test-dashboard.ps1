$ErrorActionPreference = 'Stop'

# Login
$login = Invoke-RestMethod -Uri 'http://localhost:5080/api/auth/login' `
 -Method Post -ContentType 'application/json' `
 -Body '{"username":"admin","password":"admin123"}'

Write-Host "Login OK - Role: $($login.user.role) MustChangePassword: $($login.mustChangePassword)"

$token = $login.token
$headers = @{ Authorization = "Bearer $token" }

# Dashboard
Write-Host "`n--- Dashboard ---"
try {
 $dash = Invoke-RestMethod -Uri 'http://localhost:5080/api/dashboard' -Headers $headers
 $dash | ConvertTo-Json -Depth 5
} catch {
 Write-Host "Dashboard FAILED: $($_.Exception.Message)"
}

# Collections today
Write-Host "`n--- Collections Today ---"
try {
 $col = Invoke-RestMethod -Uri 'http://localhost:5080/api/income?includeDeleted=false' -Headers $headers
 Write-Host "Income records: $($col.Count)"
} catch {
 Write-Host "Income FAILED: $($_.Exception.Message)"
}

# Low stock
Write-Host "`n--- Products ---"
try {
 $prods = Invoke-RestMethod -Uri 'http://localhost:5080/api/products?lowStockOnly=true' -Headers $headers
 Write-Host "Low stock items: $($prods.Count)"
} catch {
 Write-Host "Products FAILED: $($_.Exception.Message)"
}

# Showrooms
Write-Host "`n--- Showrooms ---"
try {
 $rooms = Invoke-RestMethod -Uri 'http://localhost:5080/api/showrooms' -Headers $headers
 Write-Host "Showrooms: $($rooms.Count)"
} catch {
 Write-Host "Showrooms FAILED: $($_.Exception.Message)"
}

Write-Host "`nDone."
