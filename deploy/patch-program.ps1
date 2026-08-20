$path = "E:\TTS\Projects\Desktop_Apps\E6_car_care\src\E6CarSpa.Api\Program.cs"
$content = Get-Content $path -Raw
$old = "        // Don't leak internal details to the client for 500s.`r`n        Detail = isBadOp ? ex?.Message : `"An unexpected error occurred. Please try again or contact support.`""
$new = "        // DEBUG - temporarily expose error details`r`n        Detail = isBadOp ? ex?.Message : `$"Error: {ex?.GetType().Name}: {ex?.Message}. {ex?.InnerException?.Message ?? `"`"}`""
if ($content -like "*$old*") {
    $content = $content.Replace($old, $new)
    Set-Content $path -Value $content -NoNewline
    Write-Host "Patched successfully"
} else {
    Write-Host "Old string not found"
}