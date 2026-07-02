# E6 Car Spa — VPS Go-Live Runbook (MilesWeb / any Ubuntu VPS)

Hosting the API + PostgreSQL on a VPS so the desktop at the shop **and** the owner's phone
anywhere in India talk to the same server:

```
Shop PC (desktop .exe) ──┐
                         ├── HTTPS ──> Caddy ──> API (localhost:5080) ──> PostgreSQL
Owner's phone (APK) ─────┘            (VPS, one box)
```

## What you need before starting

1. **A VPS** — Ubuntu 24.04 (or 22.04), e.g. MilesWeb SM-L2. You get a root password + IP.
2. **A domain name** — e.g. `api.e6carspa.in`. Use a domain, **not the raw IP**: if you ever
   switch hosts, you repoint DNS and no app needs reconfiguring.
3. **DNS A record** pointing the domain at the VPS IP (do this first — HTTPS needs it).
4. An SSH client on your PC (Windows 10/11 has `ssh`/`scp` built in).

## Step 1 — Build the Linux API on the dev PC

```powershell
./deploy/linux/publish-linux.ps1        # -> dist\e6carspa-api-linux.tar.gz (~100 MB)
```

## Step 2 — Upload to the VPS

```powershell
scp dist/e6carspa-api-linux.tar.gz root@VPS_IP:/root/
scp -r deploy/linux root@VPS_IP:/root/e6-deploy
```

## Step 3 — Run the setup script on the VPS

```bash
ssh root@VPS_IP
cd /root/e6-deploy && chmod +x setup-vps.sh
./setup-vps.sh api.yourdomain.com
```

The script installs PostgreSQL + Caddy, creates the `e6carspa_prod` database with a random
password, generates a random JWT key (both stored root-only in `/etc/e6carspa/api.env`),
installs the API as the `e6carspa-api` systemd service (loopback-only), configures Caddy
with automatic HTTPS, and locks the firewall to ports 22/80/443. The database schema and
seed data (admin login, service catalogue) are created automatically on first start.

**Save `/etc/e6carspa/api.env` in a password manager** — it holds the DB password + JWT key.

## Step 4 — Verify

```bash
curl https://api.yourdomain.com/api/services      # -> JSON list of services
journalctl -u e6carspa-api -n 30                  # API logs (look for the admin-password warning)
```

## Step 5 — Point the clients at the VPS

- **Desktop (shop PC)** — admin PowerShell, then restart the app:
  ```powershell
  [Environment]::SetEnvironmentVariable('E6_API_URL', 'https://api.yourdomain.com/', 'Machine')
  ```
  With the API on the VPS, the shop PC does **not** need PostgreSQL or the local API service —
  when installing with `E6CarSpa-Setup.exe`, you can stop/disable the `E6CarSpaApi` Windows
  service afterwards (`Stop-Service E6CarSpaApi; Set-Service E6CarSpaApi -StartupType Disabled`).
- **Phone** — type `https://api.yourdomain.com` in the **Server** field on the login screen.
- **Immediately** log in as `admin` / `admin@123` and change the password (Settings).

## Migrating data from a local install (if the shop ran locally first)

```powershell
# On the shop PC (adjust the postgres password/paths):
pg_dump -h localhost -U postgres -d e6carspa -Fc -f e6.dump
scp e6.dump root@VPS_IP:/root/
```
```bash
# On the VPS (restore BEFORE the first real use of the VPS API):
systemctl stop e6carspa-api
sudo -u postgres pg_restore --clean --if-exists -d e6carspa_prod /root/e6.dump
sudo -u postgres psql -d e6carspa_prod -c 'REASSIGN OWNED BY postgres TO e6api'
systemctl start e6carspa-api && rm /root/e6.dump
```

## Upgrading the API later

Re-run Step 1 + 2, then on the VPS: `cd /root/e6-deploy && ./setup-vps.sh api.yourdomain.com`
— the script refreshes the binaries and **keeps** the existing database, appsettings, and
secrets. (Remember: everyone must log in again after upgrades that change auth internals.)

## Backups (do not skip)

Nightly encrypted off-site backups to Google Drive: follow `README.md` in this folder
(`e6-backup.sh` + cron). Client billing data with no off-site backup is a business risk.

## Security options for the anonymous billing surface

The desktop deliberately works **without a login** at the counter, so the billing endpoints
(quotations, invoices, payments, customer lookup, dashboard) accept anonymous requests. On a
public VPS that means anyone who discovers the domain (TLS certificates are public record)
could read customer names/phones and create junk invoices. Ordered by strength:

1. **Do nothing extra** — HTTPS + rate limiting (300 req/min/IP) + account lockout are active.
   Fine for a soft launch; weakest against a targeted person.
2. **Caddy allowlist** — uncomment the `@anonSurface` block in `/etc/caddy/Caddyfile` and set
   the shop's public IP. Stops scanners/casual abuse; a determined attacker can bypass the
   header check, and a shop IP change (common on Indian broadband) needs a Caddyfile edit.
3. **Shop key (recommended, needs an app change)** — the API requires a shared secret header
   on anonymous endpoints; desktop reads it from an env var, phone stores it once in Settings.
   Invisible to staff after setup. Not built yet — ask for it when you're ready (also needs a
   new desktop installer + APK).
4. **Require login on the desktop too** — strongest, but changes the counter workflow the
   shop chose. A future toggle if the client ever wants it.

## Troubleshooting

| Symptom | Check |
|---|---|
| API service won't start | `journalctl -u e6carspa-api -n 50` — usually DB password or a missing `/etc/e6carspa/api.env` |
| HTTP 400 on every request | `AllowedHosts` in `/opt/e6carspa/api/appsettings.json` must include the domain |
| No HTTPS / cert errors | DNS A record not propagated yet, or port 80/443 blocked; `journalctl -u caddy -n 50` |
| Desktop says "Cannot reach the server" | `E6_API_URL` typo (needs `https://` and trailing `/` is fine), or PC offline |
| Works on WiFi, not on mobile data | Almost always DNS still propagating — wait, or test `https://DOMAIN/api/services` in the phone browser |
