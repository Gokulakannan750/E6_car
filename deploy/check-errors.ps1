$entries = Get-EventLog -LogName Application -Newest 5 -Source 'E6CarSpa*' -ErrorAction SilentlyContinue
foreach ($e in $entries) {
 $msg = if ($e.Message.Length -gt 300) { $e.Message.Substring(0, 300) + '...' } else { $e.Message }
 Write-Host "[$($e.TimeGenerated)] $($e.EntryType): $msg"
}
