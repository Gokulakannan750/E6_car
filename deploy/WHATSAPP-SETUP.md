# E6 Car Spa — WhatsApp Payment Confirmation Setup

The app already sends an automatic WhatsApp thank-you the moment an invoice is **fully paid**
(balance hits ₹0). The code is done and every attempt is recorded in the `NotificationLogs`
table — what's missing is a WhatsApp Business API account for E6 Car Spa. This runbook goes
from zero to messages arriving on customers' phones.

**Timeline:** ~1–2 hours of actual work, spread over 2–5 days of waiting on Meta approvals.
**Cost:** no subscription (direct Meta route). ~₹0.12–0.15 per message (India, Utility
category), billed to a card. 500 payments/month ≈ under ₹100.

---

## Phase 0 — Gather these before starting

| Item | Why |
|---|---|
| A **dedicated phone number** for E6 Car Spa | The API number **cannot** also be used in the normal WhatsApp app. New SIM, or the shop landline (verification can arrive as a voice call). Do NOT use the owner's personal number. |
| E6 Car Spa **logo** (square, ≥ 400×400 px, JPG/PNG) | Becomes the WhatsApp profile picture. |
| **GST certificate / Udyam / shop licence** PDF | For Meta business verification of "E6 Car Spa". |
| A **Facebook account** for the owner (or agency) | Everything hangs off Meta Business Manager. |
| A **credit/debit card** | Per-message billing. Added later, Phase 4. |

## Phase 1 — Meta Business Manager

1. Go to https://business.facebook.com → **Create a business portfolio** → name it
   "E6 Car Spa", add the shop's email and address.
2. Business Settings → **Security Centre → Start verification**. Upload the GST
   certificate; the legal name/address must match it. Approval: hours to a few days.
   (You can continue with Phases 2–3 while this is pending.)

## Phase 2 — Create the WhatsApp app and register the number

1. Go to https://developers.facebook.com → **My Apps → Create App** → type **Business**
   → name it e.g. "E6 CarSpa Notifications" → attach it to the E6 business portfolio.
2. In the app dashboard, **Add product → WhatsApp → Set up**.
3. Under **API Setup** you get a free **test number** — useful for a first trial, skip if
   you want. To go real: **Add phone number** → enter the dedicated number → verify via
   SMS **or voice call** (choose voice for a landline).
4. Note down two values shown on the API Setup page — you'll need them at the end:
   - **Phone Number ID** (a long numeric id — NOT the phone number itself)
   - **WhatsApp Business Account (WABA) ID**

## Phase 3 — Profile: the name and the logo

In https://business.facebook.com/wa/manage/ (WhatsApp Manager) → select the number →
**Profile**:

1. **Display name: `E6 Car Spa`** — this is what customers see as the sender. Meta reviews
   it; it must plausibly match the verified business. Usually approved in minutes–hours.
2. **Profile picture: upload the E6 logo.**
3. Category: *Automotive service*; add address, hours, website if available.

## Phase 4 — Billing

Business Settings → **Billing** (or WhatsApp Manager → Payment settings) → add the card
and set India as the country. Without this, sends fail once you're past the test tier.

## Phase 5 — Permanent access token (do NOT use the 24-hour one)

The token shown on the API Setup page expires in 24 hours — fine for testing, useless for
production. Create a permanent one:

1. Business Settings → **Users → System users → Add** → name "e6-api", role **Admin**.
2. Select the system user → **Add assets** → Apps → your WhatsApp app → full control.
3. **Generate new token** → choose the app → expiry **Never** → tick permissions:
   `whatsapp_business_messaging` and `whatsapp_business_management` → Generate.
4. **Copy it immediately and store it in the password manager** — it is shown once.
   Treat it like a password; anyone with it can send messages as E6 Car Spa.

## Phase 6 — Create the message template (must match the app exactly)

WhatsApp Manager → **Message templates → Create template**:

| Field | Value (must be exact) |
|---|---|
| Name | `payment_thank_you` |
| Category | **Utility** |
| Language | **English** (`en` — plain English, not English (US)) |
| Body | `Thank you {{1}}! Payment of Rs.{{2}} for {{3}} received. - E6 Car Spa` |

The app fills `{{1}}` = customer name, `{{2}}` = amount, `{{3}}` = car number — three body
parameters, no header/footer/buttons needed. Add sample values when prompted (e.g. *Gokul*,
*5,900*, *TN33 AB 1234*). Utility templates usually approve within minutes to a day.

> If you change the name or language here, mirror it in config (`PaymentTemplateName`,
> `TemplateLanguage`) — otherwise sends fail with error 132001 (template not found).

## Phase 7 — Prove it works with one curl (from any machine)

```bash
curl -X POST "https://graph.facebook.com/v21.0/<PHONE_NUMBER_ID>/messages" \
  -H "Authorization: Bearer <PERMANENT_TOKEN>" -H "Content-Type: application/json" \
  -d '{"messaging_product":"whatsapp","to":"91XXXXXXXXXX","type":"template",
       "template":{"name":"payment_thank_you","language":{"code":"en"},
       "components":[{"type":"body","parameters":[
         {"type":"text","text":"Test Customer"},
         {"type":"text","text":"1,000"},
         {"type":"text","text":"TN33 AB 1234"}]}]}}'
```

Send it to the owner's own number first. A JSON response containing `"messages":[{"id":...` 
and the message arriving from **E6 Car Spa with the logo** = everything upstream is done.

## Phase 8 — Configure the VPS (when the server is online)

The secrets belong in the root-only env file, not in appsettings.json. On the VPS:

```bash
sudo nano /etc/e6carspa/api.env
```

Add these three lines (double underscore is how .NET reads nested config from env vars):

```
WhatsApp__Enabled=true
WhatsApp__ApiUrl=https://graph.facebook.com/v21.0/<PHONE_NUMBER_ID>/messages
WhatsApp__AccessToken=<PERMANENT_TOKEN>
```

Then restart and watch the log:

```bash
sudo systemctl restart e6carspa-api
journalctl -u e6carspa-api -f
```

(Template name, language and country code default to `payment_thank_you` / `en` / `91`
in the app — only override them if you changed something in Phase 6.)

**Local/Windows install instead of VPS?** Edit the `WhatsApp` section of
`C:\Program Files\E6CarSpa\api\appsettings.json` (`"Enabled": true`, real `ApiUrl` and
`AccessToken`) and `Restart-Service E6CarSpaApi`.

## Phase 9 — End-to-end verification

1. Create a test job in the app with the **owner's phone number** as the customer.
2. Finalise the invoice and record **full payment** → balance ₹0.
3. The thank-you should arrive on WhatsApp within seconds.
4. Verify the audit trail in the database:
   ```sql
   SELECT "CreatedAt","ToPhone","Status","ProviderReference"
   FROM "NotificationLogs" ORDER BY "CreatedAt" DESC LIMIT 5;
   ```
   `Status = 1` (Sent) with a `wamid...` reference = success. `Status = 2` (Failed) stores
   the full error from Meta in `ProviderReference` — read it, it names the exact problem.
5. Cancel/adjust the test invoice afterwards if you don't want it in the real books.

## Troubleshooting

| Symptom | Cause / fix |
|---|---|
| Log row `Pending` — "disabled or not configured" | `WhatsApp__Enabled` not `true`, or the env file line has a typo. Restart the service after edits. |
| Error `(#132001) template name does not exist` | Template name/language mismatch — must be `payment_thank_you` + `en`, or override config to match. |
| Error `(#190)` / OAuth exception | Token expired (you used the 24-h one) or was regenerated. Create the permanent system-user token (Phase 5). |
| Error about payment / message limit | Card not added (Phase 4), or the new number is still in the low messaging tier — tiers rise automatically with clean sending. |
| Message sent but never delivered | The customer's number isn't on WhatsApp, or was stored wrong. The app prefixes `91` to 10-digit numbers automatically. |
| Worked in testing, stops after some days | Almost always the 24-hour token. Phase 5 fixes it permanently. |

## If Meta verification gets stuck: the BSP fallback

Providers like **AiSensy, Interakt or Gupshup** resell the same API with hand-holding
(₹1,000–2,500/month + per-message). The app works with them unchanged — set `ApiUrl` to
the provider's Graph-style send endpoint and `AccessToken` to their key. Use this only if
direct Meta onboarding fails; otherwise the direct route has no monthly fee.
