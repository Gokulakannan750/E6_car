#!/usr/bin/env bash
# =============================================================================
# E6 Car Spa — one-time VPS setup (Ubuntu 22.04 / 24.04, run as root).
#
# Installs PostgreSQL + Caddy, creates the database and a locked-down service
# user, unpacks the API, wires it up as a systemd service behind Caddy (HTTPS),
# and opens only ports 22/80/443.
#
# Usage:
#   1. Upload from the dev PC (after running deploy/linux/publish-linux.ps1):
#        scp dist/e6carspa-api-linux.tar.gz  root@VPS_IP:/root/
#        scp -r deploy/linux                 root@VPS_IP:/root/e6-deploy
#   2. On the VPS:
#        cd /root/e6-deploy && chmod +x setup-vps.sh
#        ./setup-vps.sh api.yourdomain.com
#
# Safe to re-run: existing secrets/config/db are kept; the API binaries are
# refreshed from the tarball (so re-running is also the UPGRADE procedure).
# =============================================================================
set -euo pipefail

DOMAIN="${1:-}"
TARBALL="${2:-/root/e6carspa-api-linux.tar.gz}"
APP_DIR=/opt/e6carspa/api
ENV_FILE=/etc/e6carspa/api.env
DB_NAME=e6carspa_prod
DB_USER=e6api
HERE="$(cd "$(dirname "$0")" && pwd)"

[[ $EUID -eq 0 ]] || { echo "Run as root (sudo ./setup-vps.sh ...)"; exit 1; }
[[ -n "$DOMAIN" ]] || { echo "Usage: ./setup-vps.sh api.yourdomain.com [tarball]"; exit 1; }
[[ -f "$TARBALL" ]] || { echo "API tarball not found: $TARBALL (upload it first — see header)"; exit 1; }
for f in e6carspa-api.service Caddyfile appsettings.vps.json; do
    [[ -f "$HERE/$f" ]] || { echo "Missing $HERE/$f — upload the whole deploy/linux folder."; exit 1; }
done

echo "== [1/8] Installing packages =="
export DEBIAN_FRONTEND=noninteractive
apt-get update -qq
apt-get install -y -qq postgresql ufw curl tzdata libfontconfig1 \
    debian-keyring debian-archive-keyring apt-transport-https gnupg
# .NET needs ICU for cultures; the package name tracks the Ubuntu release.
apt-get install -y -qq libicu74 2>/dev/null || apt-get install -y -qq libicu72 2>/dev/null || apt-get install -y -qq libicu70

if ! command -v caddy >/dev/null; then
    echo "== Installing Caddy (official repo) =="
    curl -1sLf 'https://dl.cloudsmith.io/public/caddy/stable/gpg.key' \
        | gpg --dearmor -o /usr/share/keyrings/caddy-stable-archive-keyring.gpg
    curl -1sLf 'https://dl.cloudsmith.io/public/caddy/stable/debian.deb.txt' \
        > /etc/apt/sources.list.d/caddy-stable.list
    apt-get update -qq && apt-get install -y -qq caddy
fi

echo "== [2/8] PostgreSQL database + service role =="
systemctl enable --now postgresql
if sudo -u postgres psql -tAc "SELECT 1 FROM pg_roles WHERE rolname='$DB_USER'" | grep -q 1; then
    echo "   role $DB_USER already exists — keeping its password"
    DB_PASSWORD="(unchanged)"
else
    DB_PASSWORD="$(openssl rand -base64 24 | tr -d '/+=')"
    sudo -u postgres psql -c "CREATE ROLE $DB_USER LOGIN PASSWORD '$DB_PASSWORD'"
fi
sudo -u postgres psql -tAc "SELECT 1 FROM pg_database WHERE datname='$DB_NAME'" | grep -q 1 \
    || sudo -u postgres psql -c "CREATE DATABASE $DB_NAME OWNER $DB_USER"

echo "== [3/8] System user + directories =="
id -u e6api &>/dev/null || useradd --system --home /opt/e6carspa --shell /usr/sbin/nologin e6api
# 'state' is the only place the API may write at runtime (ProtectSystem=strict makes the install
# directory read-only) — it holds the one-time generated admin password.
mkdir -p "$APP_DIR" /opt/e6carspa/extract /opt/e6carspa/state /etc/e6carspa

echo "== [4/8] Unpacking the API =="
systemctl stop e6carspa-api 2>/dev/null || true
tar -xzf "$TARBALL" -C "$APP_DIR"
chmod +x "$APP_DIR/E6CarSpa.Api"

echo "== [5/8] Configuration =="
if [[ ! -f "$APP_DIR/appsettings.json" ]]; then
    sed "s/YOUR_DOMAIN/$DOMAIN/g" "$HERE/appsettings.vps.json" > "$APP_DIR/appsettings.json"
    echo "   wrote $APP_DIR/appsettings.json"
else
    echo "   appsettings.json exists — kept (delete it and re-run to regenerate)"
fi
if [[ ! -f "$ENV_FILE" ]]; then
    [[ "$DB_PASSWORD" == "(unchanged)" ]] && { echo "ERROR: role exists but $ENV_FILE is missing — reset the role password manually"; exit 1; }
    JWT_KEY="$(openssl rand -base64 48)"
    printf 'E6_DB_PASSWORD=%s\nE6_JWT_KEY=%s\n' "$DB_PASSWORD" "$JWT_KEY" > "$ENV_FILE"
    chmod 600 "$ENV_FILE"
    echo "   wrote $ENV_FILE (db + jwt secrets)"
else
    echo "   $ENV_FILE exists — kept"
fi
chown -R e6api:e6api /opt/e6carspa

echo "== [6/8] systemd service =="
cp "$HERE/e6carspa-api.service" /etc/systemd/system/
systemctl daemon-reload
systemctl enable --now e6carspa-api

echo "== [7/8] Caddy (HTTPS reverse proxy) =="
sed "s/YOUR_DOMAIN/$DOMAIN/g" "$HERE/Caddyfile" > /etc/caddy/Caddyfile
systemctl enable caddy
systemctl restart caddy

echo "== [8/8] Firewall =="
ufw allow OpenSSH >/dev/null
ufw allow 80/tcp  >/dev/null
ufw allow 443/tcp >/dev/null
ufw --force enable >/dev/null
echo "   open: 22, 80, 443 (the API on :5080 stays loopback-only)"

echo
echo "== Health check =="
sleep 3
if curl -fsS http://localhost:5080/api/services >/dev/null; then
    echo "   API is up on localhost:5080 ✓"
else
    echo "   API not answering yet — check:  journalctl -u e6carspa-api -n 50"
fi
echo "   Public check (DNS + TLS must be ready):  curl https://$DOMAIN/api/services"
echo
echo "=============================================================="
echo " DONE. Next steps:"
echo "   1. Log in from the desktop/phone as admin/admin@123 and CHANGE THE PASSWORD."
echo "   2. Shop PC:   [Environment]::SetEnvironmentVariable('E6_API_URL','https://$DOMAIN/','Machine')"
echo "   3. Phone:     enter https://$DOMAIN in the Server field on the login screen."
echo "   4. Set up nightly encrypted backups: see README.md in this folder."
echo "   5. SAVE /etc/e6carspa/api.env somewhere safe (password manager)."
echo "=============================================================="
