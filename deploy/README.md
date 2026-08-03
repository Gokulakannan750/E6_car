# E6 Car Spa — Deployment

Two ways to deploy the API as an auto-starting Windows Service:

- **A. One-click installer** (recommended) — an Inno Setup `.exe` that installs the API service
  *and* the desktop app with shortcuts.
- **B. PowerShell scripts** — register the service manually (good for a headless server).

Both assume the publish output exists in `..\dist\` (see *Building the publish output* below).

---

## Building the publish output

From the repo root:

```powershell
dotnet publish src/E6CarSpa.Api     -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o dist/api
dotnet publish src/E6CarSpa.Desktop -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o dist/desktop
```

---

## A. One-click installer (Inno Setup)

1. Install **[Inno Setup 6](https://jrsoftware.org/isdl.php)** (free) on your build machine.
2. Compile the installer:
   ```powershell
   & "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" deploy\E6CarSpa.iss
   ```
   This produces **`deploy\Output\E6CarSpa-Setup.exe`**.
3. Copy `E6CarSpa-Setup.exe` to the shop PC and run it (it requests admin). It will:
   - install the API to `C:\Program Files\E6 Car Spa\Api` and register the **`E6CarSpaApi`**
     service (auto-start, auto-restart on crash);
   - install the desktop app to `…\Desktop` with Start-menu and (optional) desktop shortcuts;
   - add a firewall rule for port 5080;
   - open `appsettings.json` so you can enter the database connection.
4. **Edit the database connection** in `C:\Program Files\E6 Car Spa\Api\appsettings.json`
   (see *Configuration* below), then restart the service:
   ```powershell
   Restart-Service E6CarSpaApi
   ```

> The installer never overwrites an existing `appsettings.json`, so your connection string and
> secrets survive upgrades. To upgrade, just run a newer `E6CarSpa-Setup.exe`.

---

## B. PowerShell scripts (manual service)

Put `install-service.ps1`, `uninstall-service.ps1` next to the `api\` publish folder, then in an
**elevated** PowerShell:

```powershell
./install-service.ps1            # creates + starts the E6CarSpaApi service
./uninstall-service.ps1          # stops + removes it
```

---

## Updating an existing install (bug fixes / new features)

**Do NOT uninstall — that wipes `appsettings.json` (your DB connection).** Instead do an in-place upgrade:

1. On your machine, build the new release:
   ```powershell
   powershell -ExecutionPolicy Bypass -File deploy\build-release.ps1
   ```
2. (Optional) bump `MyAppVersion` in `E6CarSpa.iss` first, so the version shows in Add/Remove Programs.
3. **If the database schema changed**, take a backup first (run `backup-db.ps1` or the scheduled task).
4. Send `deploy\Output\E6CarSpa-Setup.exe` to the client (Google Drive / AnyDesk / USB).
5. The client just **runs it over the existing install** — no uninstall. The installer will:
   - close the running desktop app and stop the API service,
   - replace the program files,
   - recreate + restart the service,
   - **keep `appsettings.json` and the database untouched.**
   On next start the API auto-applies any new EF migrations to the database.

### Even lighter updates
- **Desktop-only fix:** copy the new `E6CarSpa.Desktop.exe` over `C:\Program Files\E6 Car Spa\Desktop\`. Nothing else.
- **API-only fix:** `Stop-Service E6CarSpaApi` → replace `Api\E6CarSpa.Api.exe` → `Start-Service E6CarSpaApi`.

## Configuration security (automatic since 2026-07-31)

`appsettings.json` holds the JWT signing key, the database password and the WhatsApp token.
**Program Files is world-*readable*** — only writing is restricted — so until this release any
Windows user on the shop PC could read all three. With the signing key, issuer and audience in
one file, that is enough to mint a token for any user and role and walk past the whole
permission system.

The installer now runs `secure-config.ps1` before starting the service. It:

1. **Generates a unique JWT signing key** for this install if the file still holds the shipped
   placeholder. An existing real key is never touched — regenerating one signs every user out.
2. **Removes inherited permissions** on `appsettings*.json` and grants only
   `SYSTEM` (read — the service identity) and `Administrators` (full).

Both steps are idempotent, so upgrades re-apply them safely.

**Editing the config afterwards.** The Start-menu shortcut *Edit API Configuration* now opens
Notepad elevated (UAC prompt). Editing in place keeps the permissions; if you ever replace the
file wholesale, re-run:

```powershell
powershell -ExecutionPolicy Bypass -File "C:\Program Files\E6 Car Spa\Api\secure-config.ps1" -ApiFolder "C:\Program Files\E6 Car Spa\Api"
```

**On an existing install, treat the old key as disclosed.** Any PC that ran an earlier build had
its signing key readable by every local user. After upgrading, rotate it once:

```powershell
# Elevated PowerShell. Blanks the key so secure-config regenerates it, then re-secures + restarts.
$p = "C:\Program Files\E6 Car Spa\Api\appsettings.json"
$j = Get-Content $p -Raw | ConvertFrom-Json
$j.Jwt.Key = "REPLACE_WITH_A_LONG_RANDOM_SECRET_AT_LEAST_32_CHARACTERS"
$j | ConvertTo-Json -Depth 10 | Set-Content $p -Encoding UTF8
powershell -ExecutionPolicy Bypass -File "C:\Program Files\E6 Car Spa\Api\secure-config.ps1" -ApiFolder "C:\Program Files\E6 Car Spa\Api"
Restart-Service E6CarSpaApi
```

Everyone is signed out once and logs back in — that is the rotation working. **Also change the
PostgreSQL password** (it was readable too) and update the connection string in the same file.

## ⚠️ Admin password: first install AND the upgrade past 2026-07-30

The app no longer ships a known admin password. Nobody can sign in — and since both apps are
login-first, **nobody can bill** — until someone completes this once. Do it *before* the shop opens,
not during trade.

**What happens.** On startup the API either creates the admin with a random password (first install)
or, if the account is still on the old shipped default `admin@123`, rotates it to a random one. Either
way the password is written to:

```
C:\Program Files\E6 Car Spa\Api\FIRST-RUN-ADMIN-PASSWORD.txt
```

(also in the service log — `Get-EventLog` / the API's `run.log`).

**Steps on the shop PC:**

1. Open that file and copy the password.
2. Sign in to the desktop app as `admin` with it.
3. It will immediately demand a new password — set your own (min 8 characters).
4. Sign in again with the new password. Normal use resumes.
5. **Delete `FIRST-RUN-ADMIN-PASSWORD.txt`.**

**Why it works this way.** The temporary password can do exactly one thing — set a new password.
Every other API call is refused while it is in force, so even if the file leaked, nobody could read
customer data with it, and nobody can take the account over (changing the password requires the old
one). If you skip step 3, the account stays inert.

If the file is missing (e.g. the install directory was read-only), get the password from the service
log instead. If both are gone, reset it directly in the database (`UPDATE "Users" SET ...` with a
BCrypt hash) — there is deliberately no back door.

### Delivering remotely
For the first few updates, remote in with **AnyDesk/TeamViewer** and run the installer yourself so you can confirm `Get-Service E6CarSpaApi` shows **Running** afterwards.

## Configuration (`appsettings.json`)

Edit these before going live:

| Key | What to set |
|-----|-------------|
| `ConnectionStrings:Default` | Your **Supabase** (or other) PostgreSQL connection string, with `SSL Mode=Require;Trust Server Certificate=true`. |
| `Jwt:Key` | A long random secret (≥ 32 chars). |
| `Urls` | `http://localhost:5080` if the desktop runs on the same PC; `http://0.0.0.0:5080` to allow other PCs on the LAN. |
| `WhatsApp` | Set `Enabled: true` and fill in `ApiUrl` / `AccessToken` after WhatsApp Business onboarding. |

On first start the service auto-creates the database schema and seeds the catalogue + the
default `admin / admin@123` login. **Change that password** in the app under *Settings*.

## Pointing the desktop app at the API

- Same PC as the API → nothing to do (defaults to `http://localhost:5080`).
- Different PC → set a system environment variable on each counter PC:
  `E6_API_URL = http://<server-ip>:5080/`

## Automated encrypted backups (local database)

If you run PostgreSQL locally (no cloud DB), set up nightly backups that are **encrypted** and
copied **off the machine** — a password-protected folder on the same PC does NOT survive a disk
failure, theft, or ransomware, so the off-machine copy is what actually protects your data.

Scripts (in this folder):

| Script | Purpose |
|--------|---------|
| `backup-db.ps1` | `pg_dump` → AES-256 encrypt → copy to a local + off-site folder → delete backups older than 30 days. Reads the DB connection from `api\appsettings.json`. |
| `restore-db.ps1` | Decrypt + `pg_restore` a chosen backup (use `-CreateDb` for disaster recovery onto a clean PC). |
| `register-backup-task.ps1` | Register a nightly Scheduled Task (runs as SYSTEM, catches up if the PC was off). |

### One-time setup (elevated PowerShell)

```powershell
# Pick a strong backup password; it encrypts every backup file.
./register-backup-task.ps1 -BackupPassword "choose-a-strong-pass" -Time "21:00"
Start-ScheduledTask -TaskName "E6CarSpa Daily Backup"   # run once to test
```

By default backups also copy to **OneDrive** (`%OneDrive%\E6CarSpa-Backups`) if OneDrive is set up —
that's the off-machine copy. For Google Drive or a different folder, pass `-OffsiteDir` to
`backup-db.ps1` (edit the scheduled task's argument) pointing at your synced folder.

### Restore (disaster recovery)

```powershell
# Onto a clean PostgreSQL after a PC failure:
./restore-db.ps1 -BackupFile "C:\path\e6carspa-YYYYMMDD-HHMMSS.dump.enc" -Password "your-pass" -CreateDb
```

> **Test a restore every so often.** A backup you've never restored is a guess, not a backup.
> (This round-trip — dump → encrypt → decrypt → restore — has been verified.)

### Backup password

Stored as the `E6_BACKUP_PASSWORD` machine environment variable (set by `register-backup-task.ps1`),
so it isn't sitting in the task definition. Keep a copy of this password somewhere safe — **without it,
the encrypted backups cannot be restored.**

## Scaling from one to many billing PCs

You don't have to decide the number of counters up front:

- **One PC:** API + PostgreSQL + desktop all on it. Nothing extra to do.
- **More PCs later:** keep the DB + API on the "main" PC; set its `appsettings.json` `Urls` to
  `http://0.0.0.0:5080`, restart the service (the installer already opened the firewall). On each
  extra counter PC, install just the desktop app and set the `E6_API_URL` environment variable to
  `http://<main-pc-ip>:5080/`. No rebuild, no schema change.

## Service management cheatsheet

```powershell
Get-Service E6CarSpaApi
Restart-Service E6CarSpaApi
Get-Content "C:\Program Files\E6 Car Spa\Api\logs\*" # (if file logging is added later)
```
