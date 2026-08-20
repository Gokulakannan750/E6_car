$paths = @(
    'C:\Program Files (x86)\Inno Setup 6',
    'C:\Program Files\Inno Setup 6',
    'C:\Program Files\Inno Setup',
    "$env:LOCALAPPDATA\Programs\Inno Setup 6",
    "${env:ProgramFiles(x86)}\Inno Setup 6",
    "${env:ProgramFiles}\Inno Setup 6"
)
foreach ($p in $paths) {
    if (Test-Path $p) { Write-Host "EXISTS: $p" }
    $iscc = Join-Path $p 'ISCC.exe'
    if (Test-Path $iscc) { Write-Host "ISCC: $iscc" }
}

# Also search broader
Write-Host "--- Searching ---"
Get-ChildItem 'C:\' -Filter 'ISCC.exe' -Recurse -ErrorAction SilentlyContinue -Depth 4 | ForEach-Object { Write-Host $_.FullName }
