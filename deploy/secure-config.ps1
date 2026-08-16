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
    [string]$ApiFolder,

    # Identity the API service runs as. It must be able to READ its own configuration, so it is
    # granted explicitly — the service no longer runs as SYSTEM (audit D-2).
    [string]$ServiceAccount = 'NT SERVICE\E6CarSpaApi',

    # Machine-wide folder the API writes to (the one-time generated admin password). Restricted
    # to the service account and administrators, because that file is a live credential.
    [string]$StateFolder = "$env:ProgramData\E6CarSpa"
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
        # Use GetBytes() instead of Fill() — Fill() was added in .NET 6 / PowerShell 7+.
        $bytes = [byte[]]::new(48)
        $rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
        try {
            $rng.GetBytes($bytes)
        }
        finally {
            $rng.Dispose()
        }
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
        # Each step is checked. Reporting success when icacls was denied — which is exactly what
        # happens if this is run without elevation — would leave the operator believing the file
        # is protected when it is still world-readable.
        $failed = $false
        foreach ($args in @(
                @('/inheritance:r'),
                @('/remove:g', '*S-1-5-32-545', '*S-1-5-11'),
                @('/grant:r', '*S-1-5-18:(R)', '*S-1-5-32-544:(F)'),
                # The service identity must still read its own configuration. Granting it
                # explicitly is what makes running as anything other than SYSTEM possible.
                @('/grant:r', "$($ServiceAccount):(R)"))) {
            & icacls.exe $file.FullName @args 2>&1 | Out-Null
            if ($LASTEXITCODE -ne 0) { $failed = $true }
        }

        if ($failed) {
            Write-Warning "secure-config: could not fully secure $($file.Name) - run this elevated."
        }
        else {
            Write-Host "secure-config: locked $($file.Name) to SYSTEM, Administrators and the service account."
        }
    }
    catch {
        Write-Warning "secure-config: could not secure $($file.Name): $($_.Exception.Message)"
    }
}

# ---------- 2b. Writable state folder for the generated admin password ----------
# The service cannot write into Program Files any more, so it uses this instead. The file it
# writes is a temporary admin credential, so ordinary users must not be able to read it.
try {
    New-Item -ItemType Directory -Path $StateFolder -Force | Out-Null
    $failed = $false
    foreach ($args in @(
            @('/inheritance:r'),
            @('/remove:g', '*S-1-5-32-545', '*S-1-5-11'),
            @('/grant:r', '*S-1-5-18:(F)', '*S-1-5-32-544:(F)'),
            @('/grant:r', "$($ServiceAccount):(OI)(CI)(M)"))) {
        & icacls.exe $StateFolder @args 2>&1 | Out-Null
        if ($LASTEXITCODE -ne 0) { $failed = $true }
    }
    if ($failed) {
        Write-Warning "secure-config: could not fully secure $StateFolder - run this elevated."
    }
    else {
        Write-Host "secure-config: state folder ready at $StateFolder (service + administrators only)."
    }
}
catch {
    Write-Warning "secure-config: could not prepare $StateFolder : $($_.Exception.Message)"
}

# ---------- 3. Report ----------
$readable = (Get-Acl $configPath).Access |
    Where-Object { $_.IdentityReference -match 'Users|Everyone|Authenticated' }
if ($readable) {
    Write-Warning 'secure-config: ordinary users can STILL read appsettings.json - review manually.'
    exit 1
}
Write-Host 'secure-config: done - appsettings.json is no longer readable by ordinary users.'
