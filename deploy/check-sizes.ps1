$files = Get-ChildItem 'E:\TTS\Projects\Desktop_Apps\E6_car_care\src\E6CarSpa.Api\bin' -Recurse -Filter 'E6CarSpa.Api.dll'
foreach ($f in $files) {
 $sizeKb = [math]::Round($f.Length / 1KB, 1)
 Write-Host "$($f.FullName) - $($f.LastWriteTime) - $sizeKb KB"
}
