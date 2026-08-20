$src = 'E:\TTS\Projects\Desktop_Apps\E6_car_care\dist\api'
$dst = 'C:\Program Files\E6 Car Spa\Api'

$topFiles = Get-ChildItem -Path $src -File
foreach ($f in $topFiles) {
    $target = Join-Path $dst $f.Name
    Copy-Item -Path $f.FullName -Destination $target -Force -ErrorAction SilentlyContinue
    Write-Host "Copied: $($f.Name)"
}

# Copy subdirectories (LatoFont etc.)
$dirs = Get-ChildItem -Path $src -Directory
foreach ($d in $dirs) {
    $targetDir = Join-Path $dst $d.Name
    if (!(Test-Path $targetDir)) { New-Item -ItemType Directory -Path $targetDir -Force | Out-Null }
    $subFiles = Get-ChildItem -Path $d.FullName -File -Recurse
    foreach ($f in $subFiles) {
        $rel = $f.FullName.Substring($d.FullName.Length + 1)
        $target = Join-Path $targetDir $rel
        $td = Split-Path $target -Parent
        if (!(Test-Path $td)) { New-Item -ItemType Directory -Path $td -Force | Out-Null }
        Copy-Item -Path $f.FullName -Destination $target -Force -ErrorAction SilentlyContinue
    }
    Write-Host "Copied dir: $($d.Name) ($($subFiles.Count) files)"
}

Write-Host "All done."
