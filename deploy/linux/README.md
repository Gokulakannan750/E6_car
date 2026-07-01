# E6 Car Spa — Linux VPS Backup Setup

One-time setup on the Ubuntu VPS. After this, `e6-backup.sh` runs automatically every night —
no manual steps, with an email report either way.

## 0. Install required tools

```bash
sudo apt update
sudo apt install -y postgresql-client gnupg tar msmtp msmtp-mta mailutils
curl https://rclone.org/install.sh | sudo bash
```

## 1. Database access (dedicated, read-only backup user)

Don't reuse the app's main DB user — create one that can only read:

```sql
CREATE USER e6backup WITH PASSWORD 'a-strong-random-password';
GRANT CONNECT ON DATABASE e6carspa_prod TO e6backup;
GRANT USAGE ON SCHEMA public TO e6backup;
GRANT SELECT ON ALL TABLES IN SCHEMA public TO e6backup;
```

Store its password in root's `~/.pgpass` so the script never types or hardcodes it
(the cron job runs as root, matching this file):

```bash
echo "localhost:5432:e6carspa_prod:e6backup:a-strong-random-password" >> ~/.pgpass
chmod 600 ~/.pgpass
```

## 2. GPG encryption passphrase

```bash
openssl rand -base64 32 > /root/.e6backup_gpg_pass
chmod 600 /root/.e6backup_gpg_pass
```

**Save a copy of this passphrase outside the server** (password manager). If the server is
ever lost, this passphrase is the *only* way to decrypt the Google Drive backups — losing it
means losing the backups too.

To restore/decrypt a backup later:

```bash
gpg --batch --yes --pinentry-mode loopback \
    --passphrase-file /root/.e6backup_gpg_pass \
    -d e6carspa_backup_2026-07-01_02-00-00.tar.gpg > restored.tar
tar xf restored.tar
pg_restore -d test_restore_db db_2026-07-01_02-00-00.dump
```

## 3. Google Drive connection (rclone)

```bash
rclone config
```

- `n` for new remote, name it exactly **`gdrive`** (must match `RCLONE_REMOTE` in the script)
- Storage type: `drive`
- Scope: `drive.file` (rclone can only see files it creates — more restrictive, recommended)
- Headless VPS: answer `N` to "Auto config", then either run `rclone authorize "drive"` on your
  laptop and paste the token back, or follow the `--headless` flow it prints

Test it:

```bash
echo "test" > /tmp/test.txt
rclone copy /tmp/test.txt gdrive:E6CarSpa/DailyBackups/
rclone ls gdrive:E6CarSpa/DailyBackups/     # confirm it's there, then delete it from Drive
```

## 4. Email reporting (msmtp)

Port 25 (raw SMTP) is blocked by most VPS providers, MilesWeb included — relay through an
existing mailbox instead (a Gmail App Password, or a transactional service like Brevo/Resend).

`/root/.msmtprc`:

```
defaults
auth           on
tls            on
tls_trust_file /etc/ssl/certs/ca-certificates.crt
logfile        /var/log/msmtp.log

account        default
host           smtp.gmail.com
port           587
from           backups@trovotechsolutions.in
user           your-gmail-address@gmail.com
password       your-16-char-app-password
```

```bash
chmod 600 /root/.msmtprc
echo -e "Subject: Test\n\nHello from E6 backup script" | msmtp -a default you@trovotechsolutions.in
```

## 5. Deploy the script

```bash
sudo mkdir -p /var/backups/e6carspa/{tmp,archive}
sudo cp e6-backup.sh /usr/local/bin/e6-backup.sh
sudo chmod 700 /usr/local/bin/e6-backup.sh
```

Open `/usr/local/bin/e6-backup.sh` and check the **CONFIG** section — `DB_NAME`,
`FILES_TO_BACKUP`, `EMAIL_TO` must match your actual server.

Run it once by hand to confirm the whole chain works:

```bash
sudo /usr/local/bin/e6-backup.sh
```

Check `/var/log/e6carspa-backup.log`, your inbox, and `rclone ls gdrive:E6CarSpa/DailyBackups/`.

## 6. Schedule it

```bash
sudo crontab -e
```

```
0 2 * * * /usr/local/bin/e6-backup.sh
```

(2 AM, off-peak. The script logs to `/var/log/e6carspa-backup.log` itself, so no `>>` redirect needed.)

## 7. Test a restore periodically

**An untested backup is not a backup.** Once a month:

1. Download the latest `.gpg` file from Drive.
2. Decrypt + extract it (step 2 commands above).
3. `pg_restore -d test_restore_db db_TIMESTAMP.dump` into a throwaway database.
4. Confirm the data looks right.

## Retention

- **Google Drive**: kept indefinitely — this is client production data, so we lean toward keeping
  everything and watching Drive storage rather than auto-pruning. Add
  `rclone delete --min-age 90d gdrive:E6CarSpa/DailyBackups/` as a cron step later if you want
  automatic cleanup.
- **Local** (`/var/backups/e6carspa/archive/`): auto-pruned after `RETAIN_LOCAL_DAYS` (default 3) —
  it's just a fast local fallback; Drive is the real backup.
