$outFile = "E:\TTS\Projects\Desktop_Apps\E6_car_care\deploy\appsettings-content.txt"
$ErrorActionPreference = 'Stop'
try {
    $raw = Get-Content "C:\Program Files\E6 Car Spa\Api\appsettings.json" -Raw -ErrorAction Stop
    $raw | Out-File $outFile -Encoding UTF8
    Write-Host "SUCCESS"
} catch {
    Write-Host "FAILED: $($_.Exception.Message)"
}
