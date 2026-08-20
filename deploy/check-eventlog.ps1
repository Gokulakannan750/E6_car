Get-EventLog -LogName Application -Newest 30 -After '2026-08-19' |
 Where-Object { $_.Source -like '*E6CarSpa*' -or $_.Source -eq '.NET Runtime' -or $_.Source -eq 'Application Error' } |
 Select-Object TimeGenerated, Source,
 @{N='Message';E={ $_.Message.Substring(0, [Math]::Min(300, $_.Message.Length)) }} |
 Format-List
