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

Every endpoint except login now requires a token, so an unauthenticated call **should** be refused —
a 401 here means the API is up and correctly closed:

```bash
curl -o /dev/null -w '%{http_code}\n' https://api.yourdomain.com/api/services   # -> 401 (expected)
journalctl -u e6carspa-api -n 30                                                # look for the generated admin password
```

## Step 4b — Retrieve the admin password (required, once)

No admin password ships with the app. On first start the API generates a random one; upgrading from a
build that used the old `admin@123` default rotates that away automatically. Until someone sets a real
password the account can do **nothing except change its own password** — and nobody can bill, because
both clients are login-first. So do this before handing the system over.

```bash
sudo cat /opt/e6carspa/state/FIRST-RUN-ADMIN-PASSWORD.txt
# or, if the file is missing:
journalctl -u e6carspa-api | grep -i 'password for'
```

Sign in from the desktop or Android app as `admin`, set your own password when prompted, then delete
the file:

```bash
sudo rm -f /opt/e6carspa/state/FIRST-RUN-ADMIN-PASSWORD.txt
```

End-to-end check once you have the password:

```bash
curl -sS -X POST https://api.yourdomain.com/api/auth/login \
  -H 'Content-Type: application/json' \
  -d '{"username":"admin","password":"<the password>"}'    # -> token + "mustChangePassword": true
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

## Securing the billing surface — status

**Resolved 2026-07-29/30.** This section previously described a deliberately anonymous counter
surface and ways to shield it. That surface no longer exists:

- **Every endpoint except `/api/auth/login` requires a token.** The desktop app is login-first, as
  the Android app already was. Option 4 below ("require login on the desktop too") is what was done.
- **No default credential.** See Step 4b — the admin password is generated, and a machine-generated
  password can do nothing but replace itself.

Still worth layering on for a public VPS:

1. **Caddy allowlist (defence in depth)** — uncomment `@anonSurface` in `/etc/caddy/Caddyfile` and
   set the shop's public IP. No longer the only control, so an IP change on Indian broadband is now
   an inconvenience rather than an outage risk. Recommended if the shop has a static IP.
2. **Private networking (strongest)** — put the API behind WireGuard/Tailscale and don't expose it
   publicly at all. Clients dial in; nothing is internet-reachable to attack.
3. **Still open (see `docs/PRODUCTION-READINESS-AUDIT.md`)** — Android permits cleartext HTTP (H1)
   and neither client pins the TLS certificate (H3). Close H1 before a public APK.

## Troubleshooting

| Symptom | Check |
|---|---|
| API service won't start | `journalctl -u e6carspa-api -n 50` — usually DB password or a missing `/etc/e6carspa/api.env` |
| HTTP 400 on every request | `AllowedHosts` in `/opt/e6carspa/api/appsettings.json` must include the domain |
| No HTTPS / cert errors | DNS A record not propagated yet, or port 80/443 blocked; `journalctl -u caddy -n 50` |
| Desktop says "Cannot reach the server" | `E6_API_URL` typo (needs `https://` and trailing `/` is fine), or PC offline |
| Works on WiFi, not on mobile data | Almost always DNS still propagating — wait, or test `https://DOMAIN/api/services` in the phone browser |
