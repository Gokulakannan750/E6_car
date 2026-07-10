# E6 Car Spa — Architecture

A billing / workshop-management system for a car detailing business. Two native client apps
talk to one ASP.NET Core Web API backed by PostgreSQL.

## Why 3-tier

A desktop `.exe` must never hold the database password, so the API sits between the clients and
Postgres. Both clients are thin REST clients; **all business logic, tax maths, and data access live
server-side.** This also means the phone and desktop are always consistent — there is a single
source of truth and no client-side cache to go stale (see *Synchronization*).

```
┌────────────┐        ┌────────────┐
│  Desktop   │        │  Android    │      HTTPS (JWT bearer)
│  (WPF)     │        │  (MAUI)     │  ─────────────────────────┐
└─────┬──────┘        └──────┬─────┘                            │
      │  E6CarSpa.Client (shared ApiClient + IApiClient)        │
      └───────────────┬───────────────────────────────────────┘
                      ▼
              ┌───────────────┐        ┌──────────────┐
              │ ASP.NET Core  │◀──────▶│  PostgreSQL   │
              │   Web API     │  EF    │              │
              └───────────────┘  Core  └──────────────┘
```

## Projects

| Project | Responsibility | Depends on |
|---|---|---|
| **E6CarSpa.Domain** | Entities, enums, and pure domain logic (`GstCalculator`). No framework deps. | — |
| **E6CarSpa.Contracts** | DTOs / request-response records shared across the wire. | — |
| **E6CarSpa.Infrastructure** | EF Core `AppDbContext`, migrations, DB seeding. | Domain |
| **E6CarSpa.Api** | Controllers, application services, auth, PDF rendering, WhatsApp. | Domain, Contracts, Infrastructure |
| **E6CarSpa.Client** | Shared typed API client (`ApiClient`/`IApiClient`) used by both UIs. | Contracts |
| **E6CarSpa.Desktop** | WPF app (MVVM, CommunityToolkit.Mvvm). Shop-counter billing. | Client, Contracts |
| **E6CarSpa.Mobile** | .NET MAUI app (Android). Owner's billing terminal. | Client, Contracts |
| **E6CarSpa.Tests** | xUnit unit + integration tests (WebApplicationFactory). | Api + others |

Dependencies point inward (UI → Client → Contracts; Api → Infrastructure → Domain). Domain has no
outward dependencies — the clean-architecture core.

## Billing workflow

`Intake → Quotation → (edit / add jobs) → Finalise (assigns sequential invoice number) →
Print PDF → Payment (Cash/Card/UPI) → auto WhatsApp on full payment.`

A quotation and an invoice are **one record** advanced by a `Status` enum
(`Quotation → Invoiced → Paid`, or `Cancelled`). Invoice numbers are sequential and never reset;
they are assigned under a `SELECT … FOR UPDATE` row lock so two concurrent finalisations (desktop +
phone) can't collide.

## GST

`GstCalculator` (pure, in Domain) recomputes every line and the invoice totals. Header-level
discount is distributed proportionally across lines **before** tax; GST is charged **once** on the
final taxable value at a single rate (the shop uses one rate for all services). Intra-state splits
into CGST+SGST; inter-state uses IGST. Money is stored as `numeric(12,2)`; rounding is
`MidpointRounding.AwayFromZero` throughout.

## Authentication & authorization

- **JWT bearer** tokens issued on login; the shared client attaches them to every request.
- **Deny-by-default**: a global `FallbackPolicy` requires an authenticated user; shop-floor
  endpoints (dashboard, customer lookup, quotation/invoice/payment) opt out with `[AllowAnonymous]`
  because the desktop runs without a login at the counter. Anything added in future is closed until
  explicitly opened.
- **Roles**: Admin / Manager / Worker.
- **Instant revocation**: each user has a `SecurityStamp` embedded in the token; `OnTokenValidated`
  reloads the user every request and rejects the token if the user is inactive or the stamp changed
  (password reset, role change, deactivation). Force-logout without waiting for expiry.
- **Brute-force defence**: per-IP rate limiter (global 300/60s, login 5/60s) **plus** per-account
  lockout (5 failures → 15-min lock). Login uses a dummy BCrypt verify on unknown usernames so
  timing doesn't leak valid usernames.
- **Hardening**: no `Server` header, 5 MB body cap, security headers (nosniff, DENY, HSTS in prod),
  `ForwardedHeaders` so the real client IP is seen behind Caddy, fail-fast if the JWT key is weak in
  production, 500s never leak internal details.

## Synchronization

There is **no offline store and no client-side cache of record** — every read hits the API live and
every write is server-authoritative. Consequently there is no sync-conflict logic to get wrong: the
database is the single source of truth, and a change made on one device is visible to the other on
its next read. Lists refresh on navigation / pull-to-refresh. Client-side total previews mirror
`GstCalculator` exactly, but the server figure is always authoritative on save.

## Deployment

- **Local (single shop PC)**: API runs as a Windows Service (`E6CarSpaApi`) on `:5080`, Postgres on
  the same machine, desktop reads the server URL from the `E6_API_URL` env var; the phone points at
  the PC's LAN IP. Installer: `deploy/E6CarSpa.iss` → `E6CarSpa-Setup.exe`.
- **Cloud (phone works anywhere)**: `deploy/linux/` kit — any Ubuntu 22/24 VPS: Postgres + Caddy
  (auto-HTTPS) + systemd service. Provider-agnostic.
- **Backups**: encrypted, off-site (`deploy/backup-db.ps1` on Windows, `deploy/linux/e6-backup.sh`).
  Secrets (`E6_DB_PASSWORD`, `E6_JWT_KEY`) come from environment variables, never committed.

## Testing

`dotnet test src/E6CarSpa.Tests` — unit tests for the money/business logic (GstCalculator,
InvoiceService) and integration tests that boot the real `Program` over an in-memory database
(auth, role gating, rate limiting, security headers).
