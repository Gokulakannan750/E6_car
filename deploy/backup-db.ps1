<#
.SYNOPSIS
  Nightly encrypted backup of the E6 Car Spa PostgreSQL database.
.DESCRIPTION
  - Reads the DB connection from the API's appsettings.json (single source of truth).
  - Runs pg_dump (compressed custom format).
  - AES-256 encrypts the dump with a password (from -Password or the E6_BACKUP_PASSWORD
    environment variable) so the backup file is safe even if copied off the machine.
  - Copies the encrypted file to a local folder AND an off-machine folder (OneDrive/Google
    Drive synced folder by default) — the off-machine copy is what survives a disk failure.
  - Deletes backups older than -RetentionDays.
  Designed to be run by Task Scheduler (see register-backup-task.ps1).
.EXAMPLE
  ./backup-db.ps1 -Password "MyStrongPass"
#>
param(
    [string]$ConfigPath = (Join-Path $PSScriptRoot 'api\appsettings.json'),
    [string]$BackupDir  = (Join-Path $PSScriptRoot 'backups'),
    [string]$OffsiteDir = $(if ($env:OneDrive) { Join-Path $env:OneDrive 'E6CarSpa-Backups' } else { '' }),
    [int]$RetentionDays = 30,
    [string]$Password   = $env:E6_BACKUP_PASSWORD
)

$ErrorActionPreference = 'Stop'
New-Item -ItemType Directory -Force $BackupDir | Out-Null
$logFile = Join-Path $BackupDir 'backup.log'
function Log($m) { $line = "$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')  $m"; Add-Content $logFile $line; Write-Host $line }

function Protect-File([string]$inPath, [string]$outPath, [string]$pwd) {
    $salt = New-Object byte[] 16
    [System.Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($salt)
    $kdf  = New-Object System.Security.Cryptography.Rfc2898DeriveBytes($pwd, $salt, 200000, [System.Security.Cryptography.HashAlgorithmName]::SHA256)
    $aes  = [System.Security.Cryptography.Aes]::Create(); $aes.KeySize = 256; $aes.Key = $kdf.GetBytes(32); $aes.GenerateIV()
    $data = [System.IO.File]::ReadAllBytes($inPath)
    $cipher = $aes.CreateEncryptor().TransformFinalBlock($data, 0, $data.Length)
    $ms = New-Object System.IO.MemoryStream
    $ms.Write([System.Text.Encoding]::ASCII.GetBytes('E6BK1'), 0, 5)  # magic header
    $ms.Write($salt, 0, 16); $ms.Write($aes.IV, 0, 16); $ms.Write($cipher, 0, $cipher.Length)
    [System.IO.File]::WriteAllBytes($outPath, $ms.ToArray())
    $aes.Dispose(); $kdf.Dispose()
}

function Get-ConnInfo([string]$path) {
    if (-not (Test-Path $path)) { throw "appsettings.json not found at $path" }
    $cs = (Get-Content $path -Raw | ConvertFrom-Json).ConnectionStrings.Default
    $h = @{}
    foreach ($pair in $cs -split ';') {
        $kv = $pair -split '=', 2
        if ($kv.Count -eq 2) { $h[$kv[0].Trim().ToLower()] = $kv[1].Trim() }
    }
    [PSCustomObject]@{
        DbHost = $(if ($h.host) { $h.host } else { 'localhost' })
        Port   = $(if ($h.port) { $h.port } else { '5432' })
        Db     = $(if ($h.database) { $h.database } else { 'e6carspa' })
        User   = $(if ($h.username) { $h.username } else { 'postgres' })
        Pass   = $h.password
    }
}

try {
    $conn = Get-ConnInfo $ConfigPath
    $pgDump = Get-ChildItem 'C:\Program Files\PostgreSQL\*\bin\pg_dump.exe' -ErrorAction SilentlyContinue |
              Sort-Object FullName -Descending | Select-Object -First 1
    if (-not $pgDump) { throw 'pg_dump.exe not found under C:\Program Files\PostgreSQL\*\bin' }

    $stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
    $dump  = Join-Path $BackupDir "e6carspa-$stamp.dump"

    $env:PGPASSWORD = $conn.Pass
    Log "Dumping $($conn.Db) from $($conn.DbHost):$($conn.Port) ..."
    & $pgDump.FullName -h $conn.DbHost -p $conn.Port -U $conn.User -d $conn.Db -Fc -f $dump
    if ($LASTEXITCODE -ne 0) { throw "pg_dump failed (exit $LASTEXITCODE)" }
    $env:PGPASSWORD = $null

    # Encrypt (if a password is available) and remove the plaintext dump.
    $final = $dump
    if ([string]::IsNullOrWhiteSpace($Password)) {
        Log "WARNING: no backup password set (E6_BACKUP_PASSWORD) — storing UNENCRYPTED. Keep the backup folder restricted."
    } else {
        $final = "$dump.enc"
        Protect-File $dump $final $Password
        Remove-Item $dump -Force
        Log "Encrypted -> $(Split-Path $final -Leaf)"
    }

    # Copy off-machine.
    if ($OffsiteDir) {
        New-Item -ItemType Directory -Force $OffsiteDir | Out-Null
        Copy-Item $final $OffsiteDir -Force
        Log "Copied off-site -> $OffsiteDir"
    } else {
        Log "WARNING: no off-site folder (OneDrive not detected). Set -OffsiteDir to a cloud-synced folder so backups survive disk failure."
    }

    # Retention.
    $cutoff = (Get-Date).AddDays(-$RetentionDays)
    foreach ($dir in @($BackupDir, $OffsiteDir | Where-Object { $_ })) {
        Get-ChildItem $dir -Filter 'e6carspa-*' -ErrorAction SilentlyContinue |
            Where-Object { $_.LastWriteTime -lt $cutoff } | Remove-Item -Force -ErrorAction SilentlyContinue
    }
    Log "Backup complete: $(Split-Path $final -Leaf) ($([math]::Round((Get-Item $final).Length/1KB,1)) KB)"
}
catch {
    Log "ERROR: $($_.Exception.Message)"
    exit 1
}
