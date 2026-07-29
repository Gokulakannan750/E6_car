# E6 Car Spa — Production-Readiness & Security Audit

**Scope:** Windows desktop (.NET WPF), Android app (.NET MAUI), ASP.NET Core API, PostgreSQL, VPS deployment.
**Date:** 2026-07-29 · **Reviewer role:** combined architect / AppSec / DevSecOps / DBA / network-security review.
**Method:** direct source review of the current `main` branch. Not a runtime pentest — no live host was probed.

> **Verdict up front:** the internals are **well above average** for an SMB product — BCrypt, per-account
> lockout, timing-safe login, security-stamp token revocation, secure-by-default authorization, rate
> limiting, security headers, Kestrel hardening, a least-privilege prod DB role, and a real VPS runbook.
> The blockers to a safe internet launch are **not** sloppy code; they are a small number of **exposure-model**
> decisions: an anonymous billing surface, a seeded default admin, and cleartext HTTP on mobile. Fix those
> and this is a defensible production system.

---

## Scores

| Dimension | Score | One-line basis |
|---|---|---|
| **Production readiness** | **62 → 72 / 100** | Installer + VPS runbook + backups exist; C1 closed 2026-07-29. Still blocked by default admin (C2) and mobile cleartext (H1). |
| **Security** | **68 → 78 / 100** | Strong auth internals; C1 closed. Remaining drag: default admin, cleartext HTTP, no pinning. |
| **Scalability** | **55 / 100** | Fine for one shop; single API instance, direct `DbContext`, no cache/queue, not horizontally scalable yet. |
| **Maintainability** | **80 / 100** | Clean layering, DI, DTOs, generous comments, 75 passing tests. |
| **Overall enterprise readiness** | **63 / 100** | A strong small-business build; needs the exposure hardening + a few enterprise gaps closed. |

Scores are judgement calls explained in each section. They assume **internet exposure** — on a closed LAN the security score would be ~85.

---

## Severity summary

| # | Severity | Finding | Area |
|---|---|---|---|
| C1 | ✅ **FIXED 2026-07-29** | ~~Anonymous billing surface exposed to the Internet~~ — every endpoint except `/api/auth/login` now requires a token; desktop is login-first; 78 tests pin the closed posture. | API / AuthZ |
| C2 | 🔴 Critical | Seeded default admin `admin` / `admin@123`, warned but not enforced | Auth |
| H1 | 🟠 High | Android allows cleartext HTTP (`usesCleartextTraffic=true`) and defaults to `http://` | Mobile / Network |
| H2 | 🟠 High | Real DB password committed in `appsettings.Development.json` | Secrets |
| H3 | 🟠 High | No certificate pinning (desktop or mobile) → MITM via installed/rogue root CA | Network |
| M1 | 🟡 Medium | No refresh tokens + no MFA; 12h access token is the whole session | Auth |
| M2 | 🟡 Medium | `Trust Server Certificate=true` on the generic DB template (no DB-TLS validation) | DB / Network |
| M3 | 🟡 Medium | `ForwardedHeaders` has no pinned `KnownProxies` → per-IP limits/audit trust XFF loosely | API |
| M4 | 🟡 Medium | APK: no obfuscation, root/emulator/tamper detection, or Play Integrity | Mobile |
| M5 | 🟡 Medium | Auto-migrate + auto-seed on every startup (no gated prod migration) | Deployment / DB |
| L1–L8 | ⚪ Low | `AllowedHosts:"*"`, no API versioning, no HTTPS-redirect middleware, no CSP, generic template uses `postgres` superuser, no automated dependency scanning, weak default-password strength policy, no account-level audit UI. | Various |

---

## What is already done right (do not regress these)

These are real strengths verified in code — keep them:

- **Password storage:** BCrypt with per-password salt (`AuthController`, `DbInitializer`). ✔
- **Login hardening:** per-account lockout (5 tries / 15 min), per-IP `login` rate limit (5/min), oversized-input rejection before BCrypt (CPU-DoS guard), and a `DummyHash` verify on unknown users so timing doesn't leak valid usernames. ✔
- **Token revocation:** `SecurityStamp` checked on every request in `OnTokenValidated`; password/role/active changes rotate it → instant force-logout. Better than most JWT setups. ✔
- **JWT validation:** issuer, audience, lifetime, signing key all validated; 5-min clock skew. ✔
- **Secure-by-default authZ:** global `FallbackPolicy` requires auth; new endpoints are closed unless they opt out. ✔
- **Secrets hygiene:** `appsettings.json` / `.Production` / `.Local` gitignored; **fail-fast in Production** if `Jwt:Key` is a placeholder or <32 chars; DB password and JWT key overridable via `E6_DB_PASSWORD` / `E6_JWT_KEY`. ✔
- **Prod DB least privilege:** `deploy/linux/appsettings.vps.json` uses a dedicated `e6api` role, not `postgres`. ✔
- **Transport hardening:** `AddServerHeader=false`, 5 MB body cap, `X-Content-Type-Options`, `X-Frame-Options: DENY`, `Referrer-Policy`, HSTS in Production. ✔
- **Error handling:** global handler returns generic 500s (no stack/detail leak) and maps domain `InvalidOperationException` → 400. ✔
- **SQL injection:** data access is EF Core / LINQ; the single `FromSqlRaw` (`InvoiceService.cs:91`) is a constant `SELECT … FOR UPDATE` with no interpolation. No dynamic SQL. ✔
- **Client token at rest:** the JWT is held **in memory only** (`ApiClient` sets the `Authorization` header; nothing writes it to `Preferences`/disk) — no token-at-rest exposure on mobile or desktop. ✔
- **VPS runbook:** systemd loopback-only service, Caddy auto-HTTPS, firewall 22/80/443, off-site encrypted backups, and an explicit "anonymous billing surface" section with a Caddy IP-allowlist option. ✔

---

## Critical findings

### C1 — Anonymous billing surface exposed to the Internet — ✅ FIXED (2026-07-29)
> **Resolution:** `[AllowAnonymous]` was removed from every controller except the login endpoint,
> so the global fallback policy now closes the whole API. The desktop app was made **login-first**
> (login window gates startup; logout and the 5-minute inactivity timeout return to it rather than
> leaving a signed-out shell). Android was already login-first (Splash → Login → Shell). The six
> tests that pinned the anonymous contract were inverted to pin the closed posture; suite is green
> at **78 passing**. The Caddy IP-allowlist is now defence-in-depth rather than the only control.
>
> Original finding below, for the record.


**Where:** `CustomersController`, `InvoicesController`, `ServicesController`, `StaffAdvancesController` are class-level `[AllowAnonymous]` (`Program.cs:108-111` documents this is deliberate for the walk-up counter).
**Explanation:** On the shop LAN, anonymous counter access is a reasonable convenience. The moment the API has a public IP, **every** anonymous endpoint is world-reachable with no credential.
**Business impact:** Full customer PII dump (names, phone numbers, vehicles), fabricated or cancelled invoices, and **deletion of staff-advance records** — by anyone on the Internet. Reputational and possibly legal (data-protection) exposure.
**Technical impact:** Unauthenticated read **and write**. `StaffAdvancesController` exposes read + delete; `InvoicesController` exposes create/cancel/payment.
**Exploitation scenario:** Attacker finds the host (Shodan/DNS), calls `GET /api/customers` → exfiltrates the phone list; calls `DELETE /api/staffadvances/{id}` in a loop → wipes wage records; posts bogus payments to skew books.
**IDOR note:** invoices are keyed by GUIDv4 (unguessable), so this is not classic enumeration — but "anonymous + direct object reference" means any leaked/guessed ID is fully usable.
**Recommended fix (in priority order):**
1. **Network-scope it now (fastest):** enable the documented Caddy `@anonSurface` allowlist so only the shop's egress IP(s) reach those routes. This is the go-live gate.
2. **Fix properly (correct):** require auth on these controllers and give the counter a **device/service credential** (a low-privilege API key or a "counter" user auto-logged-in on the shop machines), so the app is authenticated even when the human isn't.
3. **Best:** put the whole API behind a WireGuard/Tailscale tunnel and don't expose it publicly at all — clients dial in; the anonymous design stays safe.
**Sample (option 2 — per-endpoint key):**
```csharp
// Program.cs — a minimal device-key gate for the "anonymous" surface
builder.Services.AddAuthentication().AddScheme<AuthenticationSchemeOptions, DeviceKeyHandler>("DeviceKey", null);
// Controllers: [Authorize(AuthenticationSchemes = "DeviceKey,Bearer")] instead of [AllowAnonymous]
// DeviceKeyHandler compares a header against a hashed key in config; rotate per client PC.
```
**Best practice:** OWASP API1:2023 (Broken Object Level Auth) + API5 (Broken Function Level Auth). Never expose mutating endpoints anonymously on the Internet.
**Priority:** P0 — **blocks internet launch.** **Effort:** allowlist ~1h; proper auth ~1–2 days.

### C2 — Default admin `admin` / `admin@123`, warned but not enforced 🔴
**Where:** `DbInitializer.SeedAsync` seeds `admin` / `admin@123` on first run and logs a warning each startup if unchanged.
**Explanation:** A known default credential on an internet-facing app is trivially exploitable; the log warning is invisible to an operator who never reads journald.
**Business impact:** Full administrative takeover — user management, settings, reports, price lists.
**Technical impact:** `admin@123` is 8 chars and passes the ≥8 policy; combined with C1's exposure or any exposed login, it's a one-request compromise.
**Exploitation scenario:** Attacker POSTs `admin` / `admin@123` to `/api/auth/login` → admin JWT → creates their own admin user → persistent access.
**Recommended fix:** Do not ship a usable default password. Either (a) generate a random password at seed time and print it **once** to the console/setup log for the installer to capture, or (b) seed the admin **inactive** and force a first-run password set, or (c) make Production refuse to start while the admin still matches the default (you already detect it — escalate the warning to a hard fail in Production).
**Sample (escalate the existing check):**
```csharp
if (!isDevelopment && adminUser is not null &&
    BCrypt.Net.BCrypt.Verify("admin@123", adminUser.PasswordHash))
    throw new InvalidOperationException(
        "Refusing to start: admin still uses the default password. Reset it before exposing the API.");
```
**Best practice:** OWASP A07:2021 (Identification & Auth Failures); CIS "no default credentials".
**Priority:** P0. **Effort:** ~2h.

---

## High findings

### H1 — Android permits cleartext HTTP 🟠
**Where:** `AndroidManifest.xml` → `android:usesCleartextTraffic="true"`; `Settings.cs` default `http://192.168.1.6:5080`.
**Impact:** Even with Caddy HTTPS available, the app will happily talk plain HTTP if pointed at an `http://` URL, sending **JWT and customer data in cleartext** — trivially sniffable on public Wi-Fi, and MITM-able.
**Exploitation:** Attacker on the same network runs a transparent proxy; captures the login response (JWT) and every request.
**Fix:** For production builds, disable cleartext and require HTTPS. Keep a debug-only exception for the emulator/LAN if needed via a network-security-config that only whitelists `10.0.2.2`/LAN in debug.
```xml
<!-- Release manifest -->
<application android:usesCleartextTraffic="false" ... />
```
Also validate the server URL is `https://` in `Settings.ApiUrl` for release, and ship the default pointing at the real domain.
**Priority:** P1 (before public APK). **Effort:** ~half day incl. a debug/release network-security-config split.

### H2 — Real DB password committed to git 🟠
**Where:** `src/E6CarSpa.Api/appsettings.Development.json` (tracked) → `Password=Gokulakannan750`.
**Impact:** A real credential is in the repository and its history. If that password is reused anywhere reachable (prod DB, another service, the OS account), it's a direct compromise path; even dev-only, it trains bad habits and leaks a likely-reused secret.
**Exploitation:** Anyone with repo/clone access (or a future public mirror) reads it from history.
**Fix:** Rotate that Postgres password now. Replace the value with `SET_IN_ENV` and load dev secrets via `dotnet user-secrets` or an env var, matching the prod pattern you already use. Consider scrubbing history (`git filter-repo`) if the repo will ever be shared/published — and treat the password as burned regardless.
**Priority:** P1. **Effort:** ~1h (rotate + edit); +1h if scrubbing history.

### H3 — No certificate pinning (desktop or mobile) 🟠
**Where:** `ApiClient` uses a default `HttpClient`; no `ServerCertificateCustomValidationCallback` / pin.
**Impact:** TLS trust relies on the device trust store. A malicious/miconfigured root CA (corporate MITM box, a user-installed cert, a compromised CA) lets an attacker transparently intercept HTTPS. For a payments/PII app this is worth closing.
**Fix:** Pin the leaf or intermediate public-key (SPKI) hash in `ApiClient` for release builds. Pin the **intermediate** (e.g. Let's Encrypt R-series) so leaf rotation via Caddy doesn't break clients, and ship a backup pin.
```csharp
handler.ServerCertificateCustomValidationCallback = (msg, cert, chain, errors) =>
    errors == SslPolicyErrors.None && chain is not null &&
    chain.ChainElements.Any(e => SpkiSha256(e.Certificate) is "<pin1>" or "<pin2-backup>");
```
**Priority:** P2 (P1 if handling card data later). **Effort:** ~1 day incl. a pin-rotation runbook.

---

## Medium findings

| ID | Finding | Fix / note | Effort |
|---|---|---|---|
| **M1** | No refresh tokens, no MFA. The 12h access token *is* the session; force-logout works via security stamp, but there's no step-up auth for admin actions. | Acceptable for a single shop. For enterprise: add refresh tokens (rotating, revocable) + optional TOTP MFA for Admin/Manager. | 2–4 days |
| **M2** | Generic template DB conn uses `Trust Server Certificate=true` → DB-TLS not validated. | Fine while Postgres is `localhost`. If the DB is ever remote, set `SSL Mode=VerifyFull` and trust a real CA. | 1h |
| **M3** | `ForwardedHeaders` sets no `KnownProxies`/`KnownNetworks`. Default trusts loopback only, so it's OK **iff** Caddy is same-host; misdeploy could let a client spoof `X-Forwarded-For` and evade per-IP rate limits / poison audit IPs. | Pin `KnownProxies` to Caddy's loopback address explicitly and `ForwardLimit = 1`. | 30 min |
| **M4** | APK has no obfuscation, root/emulator/tamper detection, or Play Integrity. Business logic and the API contract are readable by decompiling. | Enable R8/AOT trimming, add Play Integrity attestation on sensitive calls, and basic root/tamper checks. Note: client checks are defense-in-depth, not a substitute for server auth (C1). | 2–3 days |
| **M5** | `MigrateAsync()` + seed run on **every** startup. A bad migration auto-applies in prod; startup does schema changes with the app's DB role. | Gate prod migrations behind an explicit deploy step (`dotnet ef database update` in the upgrade script), and run the app with a role that can't DDL. | Half day |

---

## Low findings

| ID | Finding | Fix |
|---|---|---|
| L1 | `AllowedHosts: "*"` in templates | Set to the real domain to blunt Host-header attacks. |
| L2 | No API versioning (`/api/...` unversioned) | Add `/api/v1` now; cheap insurance for the remote-update future. |
| L3 | No `UseHttpsRedirection` (relies entirely on Caddy) | Fine behind Caddy; document the hard dependency so no one exposes 5080 directly. |
| L4 | No Content-Security-Policy header | Low value (no browser UI), but add a strict CSP if any web surface appears. |
| L5 | Generic `appsettings.template.json` uses `postgres` superuser | Align it with the good VPS file (`e6api`); the superuser default invites misuse. |
| L6 | No automated dependency/vuln scanning | Add `dotnet list package --vulnerable` + Dependabot/`gh` scanning in CI. |
| L7 | Default password policy is length-only (≥8) | Add complexity/breach-list check (e.g. HaveIBeenPwned k-anonymity) for admin accounts. |
| L8 | No in-app audit-log viewer | `AuditService` writes good events; surface them to Admin for tamper-evidence. |

---

## Section-by-section coverage

### 1. Architecture
Clean layered solution: `Domain` (entities/enums), `Contracts` (DTOs), `Infrastructure` (EF `AppDbContext`, migrations, seed), `Api` (controllers + services), `Client` (`ApiClient`/`IApiClient`), `Desktop`, `Mobile`. Separation of concerns is good; DI is used throughout; DTOs cleanly separate wire models from entities (mass-assignment risk is low because requests bind to explicit `*Request` records, not entities).
- **Service pattern:** present (`InvoiceService`, `InventoryService`, `ReportsService`, etc.). ✔
- **Repository / Unit of Work:** **not** abstracted — controllers/services use `AppDbContext` directly and rely on EF's built-in UoW (`SaveChangesAsync`). For this size that's a *pragmatic, defensible* choice, not a smell. Only introduce repositories if you need to swap the store or unit-test data logic in isolation — otherwise it's needless indirection.
- **Coupling:** projects form a clean acyclic dependency graph (Domain ← Infrastructure ← Api; Contracts shared). No circular dependencies found.
- **Microservice/event-driven readiness:** currently a well-structured monolith — correct for one shop. If multi-branch arrives, the seams (services, DTOs) make extracting an invoicing service feasible; a message bus (e.g. outbox → RabbitMQ) would be the first event-driven step. Don't pre-build it.
- **Scalability limit:** single instance, in-process rate limiter (per-instance, not shared), auto-migrate on boot — all fine for one node, all blockers for horizontal scale.

### 2–3. Security & VPS
Covered in findings above. VPS runbook is solid (systemd loopback, Caddy HTTPS, firewall 22/80/443, off-site backups). **Not verifiable from source (verify on the box):** actual `ufw` state, Fail2Ban/SSH-key-only + `PermitRootLogin no`, unattended-upgrades, the `e6api` role's real `GRANT`s (should be `CONNECT`+CRUD on the app schema, **no** superuser/DDL), Caddy security headers, and file permissions on `/etc/e6carspa/api.env` (must be `600`, root-owned). Add these to a go-live checklist.

### 4. Database
EF Core + Npgsql, parameterized throughout. **Not deeply reviewed (no schema dump/telemetry available in this pass):** index coverage, FK/constraint completeness, and query plans. Recommendations: ensure indexes on all FK columns and on `Invoice.CreatedAt`, `Customer.Phone`, `Vehicle.CarNumber` (the lookup/report hot paths), confirm money columns are `numeric(x,2)` not float, and add a covering index for the dashboard date-range aggregates. I can't name specific slow queries without runtime profiling — enable `pg_stat_statements` and capture the top 10 by total time.

### 5. Performance
- **Desktop:** self-contained single-file publish; startup is fine; nothing suspicious.
- **Mobile:** in-memory token (no disk I/O per call), `RefreshView` + `CollectionView` reuse; watch the `ThemeRowRefresher` null-then-reset pattern on very large lists. No obvious leaks/ANRs in reviewed code.
- **Backend:** `async` throughout; Npgsql pools connections; WhatsApp send is time-boxed (10s) and never blocks billing. **Gaps:** no response compression, no output caching for read-heavy endpoints (catalogue/reports), no `AsNoTracking()` audit (present in some queries — make it the default for read paths).

### 6. Code quality
Consistent naming, generous intent-revealing comments, good null handling, `try/finally` around busy-state, disposal via `using`. Some controller methods are long but readable. Async usage is correct. No thread-safety red flags (stateless scoped services). Duplication is low.

### 7. Deployment
Inno Setup installer (signed), Windows service via `sc.exe`, `build-release.ps1` one-command build+sign, Linux systemd path documented. **Gaps:** no CI/CD (all local/manual), no blue-green/zero-downtime (systemd restart = brief 5xx window — acceptable for one shop), rollback is "reinstall previous exe" (works, but keep the previous signed artifact). Debug vs Release is handled; secrets are env-driven in prod. Add a CI pipeline that builds, runs the 75 tests, scans dependencies, and archives signed artifacts.

### 8. Logging
`AuditService` records auth events (login success/fail/lockout, user CRUD, password change) with actor + IP — good security logging. **Gaps:** confirm log **rotation/retention** (journald caps or Serilog rolling file), and **PII masking** — audit details include `username=...`; ensure phone numbers / tokens are never logged in request logs. No evidence of sensitive data logging in reviewed code, but add an explicit "never log tokens/passwords/full PII" rule and a structured logger (Serilog) with a masking enricher.

### 9. Backup & DR
Runbook references nightly encrypted off-site backups (`e6-backup.sh` + cron to Google Drive). **Not verified:** that restores are **tested**, and defined **RPO/RTO**. Action: do a real restore drill into a scratch DB, document RPO (nightly → up to 24h loss; tighten with WAL archiving/PITR if that's too much) and RTO (VPS rebuild + restore time), and snapshot the VPS before each upgrade.

### 10. Pentest checklist (source-level)
| Class | Assessment |
|---|---|
| SQL injection | Not found — EF/LINQ; single constant `FromSqlRaw`. ✔ |
| Broken access control | **C1** — anonymous mutating endpoints. 🔴 |
| Auth bypass / default creds | **C2** — default admin. 🔴 |
| IDOR | Mitigated by GUIDv4 keys, but anonymous access makes any known ID usable (see C1). |
| Mass assignment | Low — binds to explicit `*Request` DTOs, not entities. ✔ |
| RCE / command injection | None found — no shell-out with user input in the API. ✔ |
| Insecure deserialization | System.Text.Json only; no `BinaryFormatter`/type-name handling. ✔ |
| Path traversal / file upload | Logo upload capped (2 MB app / 5 MB Kestrel); confirm content-type/extension validation and that stored bytes aren't served back with a user-controlled path. |
| SSRF | WhatsApp `ApiUrl` is config-set (not user input) → low; keep it non-user-controllable. |
| XXE | No XML parsing of untrusted input found. ✔ |
| CSRF | N/A for token-in-header native clients (no cookie auth). ✔ |
| XSS / clickjacking / open redirect | No server-rendered HTML; `X-Frame-Options: DENY` set. ✔ |
| Race conditions | Invoice number/settings use `SELECT … FOR UPDATE` — good; audit payment posting for double-submit idempotency. |
| CORS | None configured → browsers can't call cross-origin; fine for native clients. ✔ |

### 11. Client ↔ API communication
Bearer JWT in header; token in memory only; WhatsApp send time-boxed. **Gaps:** no cert pinning (**H3**), no request signing/nonce → **no replay protection** on mutating calls (a captured request over a broken TLS path could be replayed; low risk once H1/H3 fixed, but add an idempotency key on payments). No offline sync (mobile is online-only) — acceptable, but the desktop's LAN-first resilience is lost if you fully centralize to the VPS (flagged earlier as a business-continuity decision).

### 12. Update system (future)
- **Desktop:** you already sign installers (Trovotech cert). For auto-update, verify the downloaded package's **Authenticode signature before executing**, host the manifest over HTTPS with pinning, and keep the previous signed build for rollback. Never auto-run an unsigned/unverified payload.
- **Android:** prefer Play Store (handles integrity/rollout). For sideloaded APKs, host over HTTPS, verify the signing cert, and gate sensitive server calls behind **Play Integrity** attestation. Enforce a minimum-version check server-side so old clients can be cut off.

### 13. Compliance readiness
- **OWASP API Top 10:** main gaps are API1/API5 (C1) and API2 (default creds/no MFA). Others largely addressed.
- **OWASP Mobile Top 10:** M1 cleartext (H1), no pinning (H3), no anti-tamper (M4); secure-storage of token is fine (in-memory).
- **OWASP Web Top 10:** A01 (C1), A05 misconfig (defaults), A07 (C2) — rest reasonable.
- **CIS/GDPR-style:** you store customer PII (name/phone/vehicle). For data-protection readiness: encrypt backups (done), define retention, support "delete this customer's data," restrict PII in logs, and document a processing basis. Audit trail exists via `AuditService`.

---

## Prioritized remediation roadmap

**P0 — before any internet exposure**
1. C2: kill the default admin (hard-fail in prod or forced first-run reset).
2. C1: gate the anonymous surface — Caddy IP-allowlist immediately, then real device auth.
3. Verify on the box: `.env` is `600`/root-owned, `e6api` has no DDL/superuser, SSH is key-only + `PermitRootLogin no`, Fail2Ban + unattended-upgrades on, `ufw` limited to 22/80/443.

**P1 — before public APK / first month**
4. H1: disable cleartext + default to HTTPS in release.
5. H2: rotate + de-commit the dev DB password.
6. M3: pin `KnownProxies`. M5: gate prod migrations.

**P2 — hardening / enterprise**
7. H3 pinning; M1 refresh tokens + admin MFA; M4 APK hardening + Play Integrity; L2 API versioning; L6 dependency scanning; CI/CD with the 75 tests; restore drill + documented RPO/RTO.

---

## Honest limits of this audit
This is a **source-level** review of `main`. It did **not** probe a live host, so anything on the VPS itself (firewall state, SSH config, Postgres grants, Caddy config, backup-restore success, file permissions) is **assumed from the runbook and must be verified on the box** — those are called out inline. Database index/query performance needs runtime profiling (`pg_stat_statements`) that this pass couldn't do. Treat the checklist items marked "verify" as unproven until you confirm them on the server.
