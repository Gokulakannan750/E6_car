$entries = Get-EventLog -LogName Application -Newest 3 -EntryType Error -Source 'E6CarSpa*' -ErrorAction SilentlyContinue
foreach ($e in $entries) {
 Write-Host "=== [$($e.TimeGenerated)] $($e.EntryType) ==="
 Write-Host $e.Message
 Write-Host ""
}
