<#
.SYNOPSIS
  Restore the E6 Car Spa database from an encrypted (.enc) or plain (.dump) backup.
.DESCRIPTION
  Decrypts the backup (if .enc) and runs pg_restore into the target database.
  Use -CreateDb to (re)create an empty target database first — for disaster recovery
  onto a fresh machine. TEST THIS occasionally so you know your backups actually work.
.EXAMPLE
  # Restore the latest backup into the existing DB (objects are dropped & recreated):
  ./restore-db.ps1 -BackupFile ".\backups\e6carspa-20260622-210000.dump.enc" -Password "MyStrongPass"

  # Disaster recovery onto a clean PostgreSQL:
  ./restore-db.ps1 -BackupFile "...\e6carspa-....dump.enc" -Password "..." -CreateDb
#>
param(
    [Parameter(Mandatory = $true)][string]$BackupFile,
    [string]$ConfigPath = (Join-Path $PSScriptRoot 'api\appsettings.json'),
    [string]$Password   = $env:E6_BACKUP_PASSWORD,
    [switch]$CreateDb
)

$ErrorActionPreference = 'Stop'

function Unprotect-File([string]$inPath, [string]$outPath, [string]$pwd) {
    $all = [System.IO.File]::ReadAllBytes($inPath)
    if ([System.Text.Encoding]::ASCII.GetString($all, 0, 5) -ne 'E6BK1') { throw 'Not an E6 encrypted backup (bad header).' }
    $salt = New-Object byte[] 16; [Array]::Copy($all, 5, $salt, 0, 16)
    $iv   = New-Object byte[] 16; [Array]::Copy($all, 21, $iv, 0, 16)
    $clen = $all.Length - 37
    $cipher = New-Object byte[] $clen; [Array]::Copy($all, 37, $cipher, 0, $clen)
    $kdf = New-Object System.Security.Cryptography.Rfc2898DeriveBytes($pwd, $salt, 200000, [System.Security.Cryptography.HashAlgorithmName]::SHA256)
    $aes = [System.Security.Cryptography.Aes]::Create(); $aes.KeySize = 256; $aes.Key = $kdf.GetBytes(32); $aes.IV = $iv
    try { $plain = $aes.CreateDecryptor().TransformFinalBlock($cipher, 0, $cipher.Length) }
    catch { throw 'Decryption failed — wrong password or corrupted file.' }
    [System.IO.File]::WriteAllBytes($outPath, $plain)
    $aes.Dispose(); $kdf.Dispose()
}

function Get-ConnInfo([string]$path) {
    $cs = (Get-Content $path -Raw | ConvertFrom-Json).ConnectionStrings.Default
    $h = @{}; foreach ($p in $cs -split ';') { $kv = $p -split '=', 2; if ($kv.Count -eq 2) { $h[$kv[0].Trim().ToLower()] = $kv[1].Trim() } }
    [PSCustomObject]@{
        DbHost=$(if($h.host){$h.host}else{'localhost'}); Port=$(if($h.port){$h.port}else{'5432'})
        Db=$(if($h.database){$h.database}else{'e6carspa'}); User=$(if($h.username){$h.username}else{'postgres'}); Pass=$h.password
    }
}

if (-not (Test-Path $BackupFile)) { throw "Backup file not found: $BackupFile" }
$conn = Get-ConnInfo $ConfigPath
$bin = (Get-ChildItem 'C:\Program Files\PostgreSQL\*\bin' -ErrorAction SilentlyContinue | Sort-Object FullName -Descending | Select-Object -First 1).FullName
if (-not $bin) { throw 'PostgreSQL bin folder not found.' }

# Decrypt if needed.
$dump = $BackupFile
if ($BackupFile.EndsWith('.enc')) {
    if ([string]::IsNullOrWhiteSpace($Password)) { throw 'Backup is encrypted — provide -Password.' }
    $dump = Join-Path $env:TEMP ("e6restore-" + [guid]::NewGuid().ToString('N') + '.dump')
    Unprotect-File $BackupFile $dump $Password
    Write-Host 'Decrypted backup to a temp file.'
}

$env:PGPASSWORD = $conn.Pass
try {
    if ($CreateDb) {
        Write-Host "Recreating database '$($conn.Db)'..."
        & "$bin\psql.exe" -h $conn.DbHost -p $conn.Port -U $conn.User -d postgres -c "DROP DATABASE IF EXISTS ""$($conn.Db)"";"
        & "$bin\psql.exe" -h $conn.DbHost -p $conn.Port -U $conn.User -d postgres -c "CREATE DATABASE ""$($conn.Db)"";"
    }
    Write-Host "Restoring into '$($conn.Db)'..."
    & "$bin\pg_restore.exe" -h $conn.DbHost -p $conn.Port -U $conn.User -d $conn.Db --clean --if-exists --no-owner $dump
    Write-Host 'Restore complete.' -ForegroundColor Green
}
finally {
    $env:PGPASSWORD = $null
    if ($dump -ne $BackupFile -and (Test-Path $dump)) { Remove-Item $dump -Force }
}
