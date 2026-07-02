# E6 Car Spa — Billing & Workshop Management

A Windows desktop billing application for **E6 Car Spa, Erode** (car detailing, coatings, wash,
tinting & bodyshop). It covers the full counter workflow — customer intake → quotation → tax
invoice → payment → automatic WhatsApp thank-you — plus a service catalogue and inventory
management for the products consumed during jobs (polishes, coatings, tint film, consumables).

## Architecture

A 3-tier design so the database password is never embedded in the distributed `.exe` and customer
data lives in a durable, online PostgreSQL database:

```
┌──────────────────────────┐     HTTPS/JSON     ┌─────────────────────┐      ┌──────────────┐
│  WPF Desktop client (.exe)│ ─────────────────► │ ASP.NET Core Web API│ ───► │  PostgreSQL  │
│  shop billing counter     │   JWT-authenticated │ business logic+auth │      │   (cloud)    │
└──────────────────────────┘                     └─────────────────────┘      └──────────────┘
```

### Projects

| Project | Type | Responsibility |
|---------|------|----------------|
| `src/E6CarSpa.Domain` | class lib | Entities, enums, GST calculation (no dependencies) |
| `src/E6CarSpa.Contracts` | class lib | DTOs shared by API and desktop |
| `src/E6CarSpa.Infrastructure` | class lib | EF Core `DbContext`, migrations, seed data |
| `src/E6CarSpa.Api` | ASP.NET Core | JWT auth, controllers, invoicing, inventory, PDF, WhatsApp |
| `src/E6CarSpa.Desktop` | WPF (`.exe`) | The application the shop runs (MVVM) |

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- A PostgreSQL database — either local (PostgreSQL 14+) or cloud (**Supabase**, **Neon**, **Railway**, AWS RDS, etc.)
- `dotnet-ef` tool (only needed if you change the schema): `dotnet tool install --global dotnet-ef`

## Configuration

Edit `src/E6CarSpa.Api/appsettings.json` (or override via environment variables in production):

```jsonc
{
  "ConnectionStrings": {
    // Local example:
    "Default": "Host=localhost;Port=5432;Database=e6carspa;Username=postgres;Password=YOUR_PASSWORD"
    // Cloud (Supabase) example:
    // "Default": "Host=db.xxxx.supabase.co;Port=5432;Database=postgres;Username=postgres;Password=...;SSL Mode=Require;Trust Server Certificate=true"
  },
  "Jwt": {
    "Key": "REPLACE_WITH_A_LONG_RANDOM_SECRET_AT_LEAST_32_CHARACTERS"
  },
  "WhatsApp": { "Enabled": false }
}
```

> **Security:** set a real, random `Jwt:Key` and keep production secrets out of source control
> (use `appsettings.Production.json` or environment variables — both are git-ignored).

### Authorization model

The API is **deny-by-default**: every endpoint requires a valid JWT unless it explicitly opts
out with `[AllowAnonymous]`. The opted-out surface is the shop-floor billing workflow — the
desktop app deliberately runs **without a login** at the counter (dashboard, customer lookup,
service catalogue, quotations/invoices/payments/PDF), while Reports, Inventory, Catalogue
editing, Settings, user management, and the audit trail all require a login (role-gated:
Admin / Manager / Worker). This contract is pinned by integration tests
(`ProtectedGets_WithoutToken_ReturnUnauthorized`, `ShopFloorGets_WithoutToken_RemainOpen`).
Additional protections: per-IP rate limiting (5 login attempts/min), per-account lockout
(5 wrong passwords → 15 min), instant token revocation via security stamps (password/role
change, deactivation), and an immutable audit log of security- and money-relevant actions.

> ⚠️ Because billing endpoints are anonymous **by design**, the API must never be exposed
> directly to the public internet without weighing that trade-off: anyone who can reach it can
> read customer data and create invoices. Prefer a VPN/private network between the shop and the
> server, or IP-allowlist the shop's address at the reverse proxy.

## Running (development)

1. **Start the API** (creates the database, applies migrations, and seeds the catalogue + admin user on first run):
   ```bash
   dotnet run --project src/E6CarSpa.Api
   ```
   It listens on `http://localhost:5080`.

2. **Start the desktop app** (in another terminal):
   ```bash
   dotnet run --project src/E6CarSpa.Desktop
   ```
   If your API runs on a different URL, set `E6_API_URL` before launching, e.g.
   `set E6_API_URL=http://192.168.1.50:5080/`.

3. **Log in** with the seeded admin account:
   - **Username:** `admin`  **Password:** `admin@123`  *(change this immediately in production)*

## The billing workflow (mapped to the shop's process)

1. **New Job** → enter customer name, phone, car number. "Look up" auto-fills returning customers.
2. **Pick services** from the catalogue (Ceramic/Graphene coating, Teflon polishing, water wash,
   interior cleaning, tinting, etc.) with editable price/qty. GST is computed live.
3. **Save Quotation** — stored with status `Quotation`; can be printed for the customer.
4. **Reopen the job** when work is done, **add any extra jobs**, then **Generate Invoice** — this
   assigns a permanent invoice number (sequential, never resets), sets status `Invoiced`, and
   **auto-deducts inventory** per each service's bill-of-materials. **Print** produces a GST PDF.
5. **Collect Payment** (Cash / Card / UPI). When fully paid, an **automatic WhatsApp thank-you**
   is sent to the customer and the invoice is marked `Paid`.

## GST

Each line carries its own HSN/SAC and GST rate. Intra-state jobs split tax into **CGST + SGST**;
inter-state supply uses **IGST**. Company GSTIN and the invoice-number prefix are set in
**Settings** (seeded from E6's details). Invoice numbers do **not** reset yearly.

## Inventory

Products (polishes, coatings, tint film by the metre, shampoos, pads, paint, etc.) track stock in
their natural unit (ml, litre, metre, piece). Every change is an auditable `StockMovement`
(purchase / consumption / adjustment). Finishing a service automatically consumes stock based on
its bill-of-materials. The dashboard and Inventory tab flag low-stock items.

## Reports & administration

- **Catalogue** (Admin/Manager): add/edit **services** (name, category, price, HSN/SAC, GST %, active)
  and **products**, and set each service's **recipe / bill-of-materials** — the products and quantities
  it consumes, which are auto-deducted from stock when the job is finalised. No code or DB access needed.
- **Reports** (Admin/Manager): pick a date range for a **sales report** (billed, collected, cash/card/UPI
  split, outstanding), a **daily breakdown**, **top services**, and a **GST summary by rate** for filing;
  plus **customer history** lookup by phone (visits, lifetime value, outstanding).
- **Settings** (Admin only): change your own password, **manage staff users** (add, edit role, activate/
  deactivate, reset password), and edit the **company / GST details** and invoice prefix.

> Change the default `admin@123` password under **Settings → Change My Password** before real use.

## WhatsApp setup (the automatic post-payment message)

Messaging uses the **official WhatsApp Business Cloud API** (Meta directly, or a provider such as
**AiSensy / Interakt / Gupshup** that exposes the same `/messages` endpoint). Automating WhatsApp
Web is **not** used — it gets numbers banned.

1. Complete WhatsApp Business onboarding (needs the shop's business proof) and get an approved
   **message template** with 3 body variables: `{{1}}` name, `{{2}}` amount, `{{3}}` car number.
2. Fill in the `WhatsApp` section of `appsettings.json`:
   ```jsonc
   "WhatsApp": {
     "Enabled": true,
     "ApiUrl": "https://graph.facebook.com/v21.0/<PHONE_NUMBER_ID>/messages",
     "AccessToken": "<token>",
     "PaymentTemplateName": "payment_thank_you",
     "TemplateLanguage": "en",
     "DefaultCountryCode": "91"
   }
   ```
   While `Enabled` is `false`, payments still work and the intended message is logged
   (`NotificationLogs`) without sending — useful before onboarding is complete.

## Building the desktop `.exe`

Produce a self-contained single-file executable (no .NET install needed on the shop PC):

```bash
dotnet publish src/E6CarSpa.Desktop -c Release -r win-x64 --self-contained true ^
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```

The `E6CarSpa.Desktop.exe` appears in
`src/E6CarSpa.Desktop/bin/Release/net10.0-windows/win-x64/publish/`.
For a proper installer (Start-menu shortcut, etc.), wrap that folder with
[Inno Setup](https://jrsoftware.org/isinfo.php).

## One-click install (Windows Service + desktop)

For a clean shop deployment, use the installer in [`deploy/`](deploy/README.md):

- **`deploy/E6CarSpa.iss`** — an Inno Setup script that builds `E6CarSpa-Setup.exe`, which installs
  the API as an **auto-starting Windows Service** (`E6CarSpaApi`, auto-restart on crash) *and* the
  desktop app with shortcuts, adds a firewall rule, and opens `appsettings.json` to enter the DB
  connection. It never overwrites your config on upgrade.
- **`deploy/install-service.ps1` / `uninstall-service.ps1`** — register/remove the service manually
  (run elevated) without the GUI installer — handy for a headless server.

The API uses `Microsoft.Extensions.Hosting.WindowsServices`, so the same exe runs as a console app
(dev) or a Windows Service (SCM) with no code change. See [deploy/README.md](deploy/README.md).

## Hosting the API & PostgreSQL (production)

- Host the API on a small cloud VM / app service, or on a shop PC reachable on the LAN.
- Use a **managed PostgreSQL** (Supabase / Neon / RDS) so customer data is durable and backed up.
- Point every desktop install at the API via the `E6_API_URL` environment variable.

## Default seeded data

- **Admin:** `admin` / `admin@123`
- **Services:** Ceramic/Graphene/Underbody coating, Teflon polishing, Ceramic & Foam wash,
  Interior cleaning, Rain repellent, Window tinting, Tinkering & painting, Headlight restoration,
  Engine bay cleaning, AMC package.
- **Products + bill-of-materials** for the above.
- **Company settings** pre-filled with E6 Car Spa, Erode.
