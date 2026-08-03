<#
.SYNOPSIS
  Locks down the API's configuration file and gives the install a unique signing key.

.DESCRIPTION
  Fixes audit finding D-1. Program Files is world-READABLE by default — only writing
  is restricted — so `appsettings.json` (JWT signing key, database password, WhatsApp
  token) could be read by any local user. With the signing key, issuer and audience all
  in one file, anyone could mint a token for any user and role, walking straight past
  the permission system.

  Two things happen here, both idempotent and safe to re-run on every upgrade:

    1. If the JWT key is still the shipped placeholder (or too weak), a fresh
       cryptographically random key is generated. Every install therefore has its own
       signing key, and an operator can no longer leave the default in place.
       An existing real key is never touched — that would sign everyone out.

    2. The file's inherited permissions are removed and replaced with SYSTEM (read) and
       Administrators (full). SYSTEM is the account the API service runs as. Ordinary
       users lose read access entirely.

  The key deliberately stays in the file rather than moving to a machine environment
  variable: the Service Control Manager inherits its environment at boot, so a freshly
  written machine variable is not reliably visible to a service started moments later —
  and the API fails fast on a placeholder key, so the service would refuse to start
  until the PC was rebooted.

.PARAMETER ApiFolder
  Folder holding appsettings.json — normally "<install dir>\Api".

.EXAMPLE
  powershell -ExecutionPolicy Bypass -File secure-config.ps1 -ApiFolder "C:\Program Files\E6 Car Spa\Api"
#>
param(
    [Parameter(Mandatory = $true)]
    [string]$ApiFolder
)

$ErrorActionPreference = 'Stop'

$configPath = Join-Path $ApiFolder 'appsettings.json'
if (-not (Test-Path $configPath)) {
    Write-Host "secure-config: $configPath not found - nothing to do."
    exit 0
}

# ---------- 1. Ensure this install has its own strong signing key ----------
try {
    $raw = Get-Content $configPath -Raw
    $json = $raw | ConvertFrom-Json

    $currentKey = [string]$json.Jwt.Key
    $isPlaceholder =
        [string]::IsNullOrWhiteSpace($currentKey) -or
        $currentKey.Length -lt 32 -or
        $currentKey -like '*REPLACE_WITH*' -or
        $currentKey -like '*CHANGE_ME*'

    if ($isPlaceholder) {
        # 48 bytes -> 64 base64 characters, from the OS CSPRNG.
        $bytes = [byte[]]::new(48)
        [System.Security.Cryptography.RandomNumberGenerator]::Fill($bytes)
        $json.Jwt.Key = [Convert]::ToBase64String($bytes)

        $json | ConvertTo-Json -Depth 10 | Set-Content $configPath -Encoding UTF8
        Write-Host "secure-config: generated a unique JWT signing key for this install."
    }
    else {
        Write-Host "secure-config: existing JWT key kept (regenerating would sign every user out)."
    }
}
catch {
    # A malformed or hand-edited config must not abort the install; the API's own
    # fail-fast check still refuses to start on a weak key, which is the real backstop.
    Write-Host "secure-config: could not read/update the key ($($_.Exception.Message)). Continuing to permissions."
}

# ---------- 2. Restrict who can read the file ----------
# Well-known SIDs, so this works regardless of Windows display language:
#   S-1-5-18      NT AUTHORITY\SYSTEM        (the service identity)
#   S-1-5-32-544  BUILTIN\Administrators
#   S-1-5-32-545  BUILTIN\Users              } removed explicitly: /inheritance:r drops
#   S-1-5-11      NT AUTHORITY\Authenticated } only INHERITED entries, not explicit ones
#
# /grant:r replaces any existing entry for that SID rather than merging with it, so the
# resulting ACL is exactly what is written here no matter what it looked like before.
foreach ($file in Get-ChildItem -Path $ApiFolder -Filter 'appsettings*.json' -File -ErrorAction SilentlyContinue) {
    try {
        & icacls.exe $file.FullName /inheritance:r | Out-Null
        & icacls.exe $file.FullName /remove:g '*S-1-5-32-545' '*S-1-5-11' | Out-Null
        & icacls.exe $file.FullName /grant:r '*S-1-5-18:(R)' '*S-1-5-32-544:(F)' | Out-Null
        if ($LASTEXITCODE -ne 0) { throw "icacls exited $LASTEXITCODE" }
        Write-Host "secure-config: locked $($file.Name) to SYSTEM + Administrators."
    }
    catch {
        Write-Warning "secure-config: could not secure $($file.Name): $($_.Exception.Message)"
    }
}

# ---------- 3. Report ----------
$readable = (Get-Acl $configPath).Access |
    Where-Object { $_.IdentityReference -match 'Users|Everyone|Authenticated' }
if ($readable) {
    Write-Warning 'secure-config: ordinary users can STILL read appsettings.json - review manually.'
    exit 1
}
Write-Host 'secure-config: done - appsettings.json is no longer readable by ordinary users.'
