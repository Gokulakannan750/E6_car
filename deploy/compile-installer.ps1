$iscc = 'C:\Users\gokul\AppData\Local\Programs\Inno Setup 6\ISCC.exe'
$iss = 'E:\TTS\Projects\Desktop_Apps\E6_car_care\deploy\E6CarSpa.iss'
Write-Host "Compiling installer..."
& $iscc $iss
Write-Host "ISCC exit: $LASTEXITCODE"
if ($LASTEXITCODE -eq 0) {
    $out = Get-Item 'E:\TTS\Projects\Desktop_Apps\E6_car_care\deploy\Output\E6CarSpa-Setup.exe'
    $mb = [math]::Round($out.Length / 1MB, 1)
    Write-Host "Installer ready: $($out.FullName) ($mb MB)"
} else {
    Write-Host "Build FAILED"
}
