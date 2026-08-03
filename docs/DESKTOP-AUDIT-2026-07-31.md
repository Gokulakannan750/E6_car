# E6 Car Spa — Windows Desktop Application: Enterprise Audit

**Audit date:** 31 July 2026
**Commit reviewed:** `ada6b83` (clean working tree, `main`)
**Roles applied:** Software Architect · .NET Desktop Developer · Database Architect · Cybersecurity · DevSecOps · QA Lead · Enterprise Auditor
**Scope:** the WPF desktop client and everything it depends on to run on a shop PC — project settings, ViewModels, views, local file handling, the installer, the Windows service it registers, and the on-disk configuration.

> **Two corrections to the brief.**
>
> 1. The brief describes *"inventory management … purchases, stock movements, barcode labels, maintenance
>    records"*. That is a different product (Erode Rifles). **E6 Car Spa is a billing/workshop application**
>    for car detailing: quotations → invoices, job cards for the floor, customers and vehicles, a service
>    catalogue, staff advances and reports, with a secondary products/stock module. Audited as built.
> 2. The ".NET desktop" framing **is** correct here: WPF on .NET 10, MVVM via CommunityToolkit, talking to a
>    local ASP.NET Core API over HTTP.
>
> This complements `PRODUCTION-READINESS-AUDIT.md` (system/API-wide, same session). Findings there are not
> repeated; its two criticals were re-verified as still fixed at this commit.

**Verification method.** Source review plus **live inspection of the installed system** — file ACLs, service
account and firewall rules were read off this machine, not inferred. Findings marked *verified* were observed
directly.

---

## Scorecard

| Dimension | Score | Basis |
|---|---|---|
| **Desktop architecture / MVVM** | **82 / 100** | Clean VM/View separation, DI throughout, correct UI-thread affinity |
| **Desktop code quality** | **80 / 100** | Consistent, nullable-enabled, well commented; a few unguarded paths |
| **Client security** | **58 / 100** | Token held in memory only (good) — undermined by on-disk secrets and a SYSTEM service |
| **Deployment / DevSecOps** | **45 / 100** | Signed installer, but world-readable secrets, LocalSystem, unscoped firewall, non-idempotent |
| **Reliability / supportability** | **40 / 100** | No logging, no crash handling, no version stamp |
| **Overall desktop readiness** | **59 / 100** | Functionally strong; deployment hardening is the gap |

---

## CRITICAL

### D-1 · JWT signing key and database password are readable by every local user — *verified*

**Where:** `C:\Program Files\E6 Car Spa\Api\appsettings.json`, produced from `deploy/appsettings.template.json`
by the installer. No `icacls` hardening anywhere in `E6CarSpa.iss`.

Observed on this machine:

```
IdentityReference   FileSystemRights              AccessControlType
BUILTIN\Users       ReadAndExecute, Synchronize   Allow

Contains Jwt:Key     : True
Contains DB password : True
```

Program Files is world-**readable** by design; only writing is restricted. The file holds `Jwt:Key`,
`Jwt:Issuer`, `Jwt:Audience` and the PostgreSQL password together.

**Why this is critical.** With the signing key plus issuer and audience — all in the same file — any local
user can **mint a valid token for any user id, role and permission set** without knowing a password. That
defeats, in one step, the entire authorisation model: per-user permissions, role gating, account lockout,
and the security-stamp revocation. The same file also yields direct PostgreSQL access, bypassing the
application altogether.

**Exploitation.** A staff member with a Worker login (or any standard Windows account) opens the file in
Notepad, forges an Admin token, and reads reports, edits settings or creates users. The security-stamp check
is not a barrier — the DB password in the same file lets them read a real stamp.

**Business impact.** The permission work is decorative on any PC where more than one person has a Windows
login. Customer PII and full financial history are exposed to anyone with local access.

**Fix — two layers, both cheap:**

1. **Restrict the file now** (add to the installer's `[Run]`, after files are copied):
   ```
   Filename: "{sys}\icacls.exe"; \
     Parameters: """{app}\Api\appsettings.json"" /inheritance:r /grant ""*S-1-5-18:(R)"" /grant ""*S-1-5-32-544:(F)"""; \
     Flags: runhidden
   ```
   (`S-1-5-18` = LocalSystem, the service identity; `S-1-5-32-544` = Administrators. Locale-independent SIDs.)
2. **Prefer secrets out of the file entirely.** The API already reads `E6_JWT_KEY` and `E6_DB_PASSWORD`
   environment variables — set them as **machine-scoped** variables during install and leave the placeholders
   in the file. Then the file is worthless even if the ACL is later reset by a repair install.

**Also rotate** the current JWT key and DB password after fixing — both must be considered disclosed on any
machine where this build has run. Rotating the JWT key signs everyone out once, which is acceptable.

**Priority P0. Effort ~3h including rotation.**

---

## HIGH

### D-2 · A network-facing service runs as LocalSystem — *verified*

```
Service  : E6CarSpaApi
RunsAs   : LocalSystem
PathName : C:\Program Files\E6 Car Spa\Api\E6CarSpa.Api.exe
```

`E6CarSpa.iss` creates the service with `sc create … binPath= … start= auto` and **no `obj=`**, so it defaults
to LocalSystem — the highest-privilege account on the machine. That process listens on TCP 5080 and parses
untrusted input from every client.

Any remote-code-execution flaw in ASP.NET Core, Npgsql, QuestPDF or any other dependency therefore yields
**SYSTEM**, not a sandboxed service account. Nothing about this application needs SYSTEM: it needs to read
its own folder, listen on a port, and reach PostgreSQL.

**Fix.** Run under the per-service virtual account, which Windows creates automatically and which has no
rights beyond what you grant:

```
sc create E6CarSpaApi binPath= "…" start= auto obj= "NT SERVICE\E6CarSpaApi"
```
Then grant that identity read access to the Api folder (and write access only to the log path). Verify the
service starts before shipping — a service account change is the kind of thing that fails at the customer site.
**Priority P1. Effort ~half day including testing.**

### D-3 · Firewall rule is open to every network, and duplicates on every install — *verified*

The installer runs:

```
netsh advfirewall firewall add rule name="E6 Car Spa API" dir=in action=allow protocol=TCP localport=5080
```

with no `profile=` and no `remoteip=`. Observed on this machine:

```
Profile=Any  Port=5080  RemoteAddress=Any     ← × 34 identical rules
```

Two distinct problems:

1. **Scope.** The billing API is reachable from *any* address on *any* network profile — including Public.
   Take the shop laptop to a café and the API is exposed to that network. The rule is added unconditionally
   even when `Urls` is `http://localhost:5080`, where no rule is needed at all.
2. **Non-idempotent installation.** Every install/upgrade adds another copy; `[UninstallRun]` deletes by
   name but only on uninstall. **34 duplicate rules** have accumulated here from repeated upgrades. Harmless
   individually, but it is a clear signal the installer's system changes are not idempotent — worth checking
   the same pattern elsewhere.

**Fix.** Delete before adding, and scope it:
```
netsh advfirewall firewall delete rule name="E6 Car Spa API"
netsh advfirewall firewall add rule name="E6 Car Spa API" dir=in action=allow protocol=TCP \
  localport=5080 profile=private remoteip=localsubnet
```
Skip the rule entirely when the API is bound to localhost. **Priority P1. Effort ~1h.**

### D-4 · The desktop client has no logging and no crash handling — *verified by search*

Neither exists anywhere in `E6CarSpa.Desktop`:

- No `ILogger`, Serilog, `Trace` or EventLog usage — **zero** log output.
- No `DispatcherUnhandledException`, no `AppDomain.CurrentDomain.UnhandledException`, no
  `TaskScheduler.UnobservedTaskException`.

Any unhandled exception on the UI thread therefore terminates the application with the default Windows crash
dialog, mid-transaction, losing whatever the counter operator was entering — and leaves **nothing** to
diagnose from. When the shop reports "it closed by itself", there is no artefact to inspect. That is the
difference between a ten-minute fix and an unreproducible ghost.

This compounds every other robustness finding below (D-7, D-8): each is a crash path with no net beneath it.

**Fix.**
```csharp
DispatcherUnhandledException += (_, e) => { Log(e.Exception); ShowFriendlyDialog(); e.Handled = true; };
AppDomain.CurrentDomain.UnhandledException += (_, e) => Log(e.ExceptionObject as Exception);
TaskScheduler.UnobservedTaskException  += (_, e) => { Log(e.Exception); e.SetObserved(); };
```
Write to a rolling file under `%LOCALAPPDATA%\E6CarSpa\logs\` (never Program Files — not writable), cap
retention, and **never log tokens, passwords or full customer records**. **Priority P1. Effort ~1 day.**

---

## MEDIUM

### D-5 · PDF filenames are built from user-typed text — path traversal

**Where:** `ViewModels/InvoiceDetailViewModel.cs:216` and `:234`

```csharp
var car  = (Invoice.CarNumber ?? …).Replace("/", "-").Replace(" ", "");
var file = Path.Combine(Path.GetTempPath(), $"E6_JobCard_{car}.pdf");
await File.WriteAllBytesAsync(file, bytes);
```

Only `/` (and space) are removed. On Windows `\` is equally a separator and `..` is untouched, so a car
number of `..\..\Users\Public\x` writes the PDF outside the temp directory — anywhere the signed-in user can
write. The car number is free text typed at the counter and stored server-side, so this is reachable by any
user who can create a job. The `E6_JobCard_` prefix prevents a fully-rooted path, and invalid characters
throw (caught, shown as an error), which limits it to relative traversal — a file-overwrite primitive rather
than code execution.

**Fix.** Never build a path from data. Sanitise and anchor:
```csharp
var safe = string.Concat(car.Where(c => !Path.GetInvalidFileNameChars().Contains(c)));
var file = Path.Combine(dir, Path.GetFileName($"E6_JobCard_{safe}.pdf"));
if (!file.StartsWith(dir, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException();
```
**Priority P2. Effort ~1h.**

### D-6 · Invoices and job cards sit unencrypted in %TEMP% for 24 hours

`PrintAsync` / `PrintJobCardAsync` write the PDF to `Path.GetTempPath()` and open it with the shell handler;
`App.CleanupOldPdfs()` deletes `E6_*.pdf` older than one day at startup. Those PDFs contain customer names,
phone numbers, vehicle details, prices and GST figures.

`%TEMP%` is per-user, so this is not world-readable — but on a shop PC where staff share one Windows login
(the common case, and the reason the app has its own user accounts), every member of staff can read every
invoice printed in the last day, regardless of their in-app permissions. Deleting only *at startup* means a
terminal left on for a week never cleans up.

**Fix.** Write to a dedicated subfolder, delete the file once the viewer closes (or on app exit as well as
startup), and shorten retention. **Priority P2. Effort ~2h.**

### D-7 · Concurrent 401s stack multiple login dialogs

`ApiClient.cs:253` raises `OnUnauthorized` on **every** 401, and `App.xaml.cs:54` responds by showing a modal
`LoginWindow` — with no guard. A screen that issues several calls in parallel (the dashboard does) will, on an
expired or revoked token, raise several 401s and queue several dialogs; the operator must dismiss each in
turn. A revoked security stamp — which the permissions feature triggers deliberately — is exactly when this
fires.

**Fix.** Gate re-authentication with a flag or `SemaphoreSlim(1,1)`, ignore further 401s while a prompt is
open, and drop the queued ones once a new token is obtained. **Priority P2. Effort ~2h.**

### D-8 · Logo upload can crash the application

`Views/SettingsView.xaml.cs:80` — inside `async void`:
```csharp
var bytes = await System.IO.File.ReadAllBytesAsync(dlg.FileName);   // no try/catch
```
A locked, deleted-since-picked, or permission-denied file throws inside `async void`, which cannot be caught
by the caller and — given D-4 — terminates the app. There is also no client-side size check before reading
the whole file into memory (the API caps at 2 MB, but only after the read).

**Fix.** Wrap in try/catch, surface through the existing `Error` property, and reject oversized files before
reading. **Priority P2. Effort ~1h.**

### D-9 · Navigation is not transactional and swallows failures

`ShellViewModel.NavigateAsync` sets `ActiveNav` *before* awaiting `InitializeAsync()`, so a failed load leaves
the sidebar highlighting a screen the content area never switched to. The commands are `AsyncRelayCommand`,
whose faults are captured in a Task nobody observes — with no `UnobservedTaskException` handler (D-4), a
throwing initialiser fails **silently**. There is also no re-entrancy guard: two quick clicks race, and the
slower load wins.

**Fix.** Set `ActiveNav` only after a successful load, catch and surface initialiser failures, and ignore
navigation while one is in flight. **Priority P2. Effort ~3h.**

### D-10 · The binary carries no version — you cannot tell which build a shop is running

`E6CarSpa.Desktop.csproj` sets no `Version`, `AssemblyVersion` or `FileVersion`, so the exe reports 1.0.0.0
forever; the installer's version lives only in `E6CarSpa.iss`. Supporting a remote site starts with "which
build are you on?", and today there is no reliable answer. (The sibling project solved exactly this.)

**Fix.** Set `<Version>` in the csproj, drive `MyAppVersion` from it, and show it in Settings.
**Priority P2. Effort ~2h.**

---

## LOW

| ID | Finding | Note |
|---|---|---|
| D-11 | Install directory is user-changeable | `DefaultDirName={autopf}` with `PrivilegesRequired=admin` is correct, but if an operator redirects the install to e.g. `C:\E6`, the service binary may sit in a user-writable folder — and the service runs as LocalSystem (D-2), making that a privilege-escalation path. Pin the directory, or `icacls` it after install |
| D-12 | Shipped template weakens two defaults | `AllowedHosts: "*"` and `Trust Server Certificate=true` (DB TLS not validated). Harmless while PostgreSQL is local; wrong the moment it is not |
| D-13 | No DPI manifest | WPF defaults to system DPI awareness; on a 4K counter monitor the UI renders blurry. Add PerMonitorV2 via `app.manifest` |
| D-14 | Single-file self-extracting publish | `IncludeNativeLibrariesForSelfExtract` unpacks native libraries to a temp directory at first run — a DLL side-loading surface — and `EnableCompressionInSingleFile` measurably slows cold start |
| D-15 | Server URL is env-var only | `E6_API_URL` must be set with PowerShell; there is no in-app field (the Android app has one). Friction at VPS cut-over, and easy to get wrong under time pressure |
| D-16 | No idle lock on the desktop | 30-minute inactivity returns to login (added this session) — worth confirming it survives the VPS move, since a stale token then fails differently |

---

## What is well built (do not regress)

- **The access token is never persisted.** `ApiClient` holds it in memory and sets the header per client; nothing
  writes it to disk or the registry. Signing out or closing the app genuinely ends the session.
- **Correct async discipline.** No `ConfigureAwait(false)` anywhere in the client, so continuations resume on
  the UI thread — the right choice for WPF, and the reason there are no cross-thread collection bugs.
- **`async void` is confined to event handlers**, which is its one legitimate use; all but one delegate
  straight to a guarded `Task` method.
- **Clean MVVM and DI.** Views hold no business logic, ViewModels hold no `HttpClient`, and the shared
  `IApiClient` is the single boundary — which is why the phone and desktop cannot drift apart.
- **Only two empty catch blocks**, both deliberate and commented (optional watermark, best-effort temp cleanup).
- **The installer preserves configuration and the database on upgrade**, stops the service before replacing
  files, and registers automatic restart on failure.
- **Signed releases** with the certificate held in the Windows store — no key material in the repository.

---

## Recommended order of work

**P0 — before the next shop deployment**
1. D-1 — ACL `appsettings.json`, move secrets to machine environment variables, **rotate the JWT key and DB password**.

**P1 — this cycle**
2. D-2 — run the service as `NT SERVICE\E6CarSpaApi` instead of LocalSystem.
3. D-3 — scope the firewall rule to the private profile and local subnet; delete-before-add.
4. D-4 — global exception handlers plus rolling file logging under `%LOCALAPPDATA%`.

**P2 — next**
5. D-5 path sanitising · D-7 re-auth guard · D-8 logo crash · D-9 navigation.
6. D-6 PDF retention · D-10 version stamp.

**P3**
7. D-11 – D-16, and revisit D-12 when PostgreSQL moves off the shop PC.

---

## Limits of this audit

Static review plus live inspection of the installed service, its ACLs and firewall rules on **this** machine.
Not performed: dynamic testing of the packaged desktop binary, fuzzing of API inputs from a hostile client, a
DLL side-loading test against the self-extracted native libraries (D-14), verification that the shop PC's
disk is BitLocker-encrypted, or any review of the physical security and Windows account model at the site —
which D-1 and D-6 both depend on, and which no amount of code review can establish.
