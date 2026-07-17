# Sekura Feature Inventory

This document is a complete inventory of what the application does today. It is grouped by area. For configuration details see [`../sekura/CONFIGURATION.md`](../sekura/CONFIGURATION.md) and [`../sekura/README.md`](../sekura/README.md); for architecture see [`../DESIGN.md`](../DESIGN.md).

The app ships two secure external-facing workflows behind one authenticated admin console:

1. **Secure shares** — hand a secret to a recipient for a limited time.
2. **Information requests** — collect information back from an external partner for a limited time.

After sign-in, `/Dashboard` opens a two-card start page that links to the **Admin console - Secure shares** and **Admin console - Information requests**.

---

## 1. Secure shares

Route prefix: `/Admin`, recipient link `/s/{token}`.

- Create a share with recipient email, shared username/account name, secret text, optional instructions, and an expiration time.
- Secret text and instructions each support up to `10000` characters, multiline, with plain text / special characters / YAML / JSON preserved end to end.
- The app generates a unique access link (`/s/{token}`) and a separate `10`-character, case-sensitive one-time access code so link and code can be delivered over different channels.
- **Access modes:**
  - `Email + Code` — recipient confirms their email address and enters the access code.
  - `Entra ID Required + Email + Code` — recipient must additionally sign in with Microsoft Entra ID (OIDC), and only the configured recipient email can open the secret.
- **Optional browser-side extra-password protection** — the secret is encrypted in the browser (AES-GCM) before submission; the server stores only the encrypted payload and the extra password is never sent to the server.
- **Per-share failed-attempt pause** — repeated wrong email/code attempts on a share pause further attempts for that share for a configured time.
- **Delete after retrieve** — the recipient can delete the share once they have copied the secret, with a confirmation step and a deleted-state confirmation page.
- **Automatic expiration and cleanup** — a background service removes expired shares on an interval.
- **Dashboard** — search and status filters (active, expiring soon, opened/used, expired), per-share metadata (created/expires, access mode), and manual revocation.
- **Ownership scoping** — standard users see and revoke only shares they created; admins see all shares.
- **Access notification email** — optional SMTP notification to admins and/or the share creator when a share is opened, using configurable subject/body templates.

## 2. Information requests

Route prefix: `/InformationRequests` (admin) and `/InformationRequest` (partner), partner link `/r/{token}`.

- App users create a request for an external partner with partner email, request instructions, and an expiration time; optionally require Microsoft Entra ID sign-in.
- The app generates a unique request link (`/r/{token}`) and a separate `15`-character access code, delivered over different channels.
- **Partner submit / update flow** — the partner proves access with email + access code (or Entra ID + access code when required), then submits their information. Reopening an already-submitted request prompts the partner to update the previously submitted response until expiration.
- **Optional browser-side extra-password protection of the response** — the partner can encrypt their response in the browser (AES-GCM). The server stores only the opaque payload. When a protected response is opened again, it is decrypted once in the browser and the extra password is held only in page memory so it can be re-saved during that page session without re-entering it.
- **Per-request failed-attempt pause** — same throttling model as shares, driven by the same application settings.
- **Expiration extension** — the owner can extend a request's expiration (1–168 hours) from the details page.
- **Revoke** — the owner or an admin can delete a request.
- **Automatic expiration and cleanup** — the same background service removes expired information requests and, when a partner opens an expired link, deletes it on access.
- **Dashboard** — counts for active, expiring soon, submitted, and revoked; search and status filters; ownership scoping identical to shares.
- **Audit and usage metrics** — create, access, response update, revoke, extend, and cleanup events are recorded.

## 3. Admin console and dashboard

- `/Dashboard` two-card start page routes to the Secure shares and Information requests consoles.
- Persistent top navigation exposes both consoles, create actions, profile, and (for admins) Users, Mail configuration, Settings, and Audit logs.
- Default route lands on the dashboard after sign-in.

## 4. Authentication and accounts

- **Identity sources:**
  - Configured local admin (`AdminAuth:Username` + `AdminAuth:PasswordHash`), available from localhost as break-glass access.
  - Database-backed local users, managed by admins (create, edit, reset password, delete).
  - Optional OIDC / Microsoft Entra ID single sign-on, with group-to-role mapping.
- **Roles:** `Admin`, `User`, and a built-in `Auditor` role.
- **Authorization policies:** `AdminOnly`, `UserOrAdmin`, and `AuditAccess` (admins plus auditors).
- **Second-factor sign-in for local accounts** (database-backed storage modes):
  - **Authenticator app (TOTP)** — onboarding shows a QR code, manual key, and provisioning URI, and can be printed; confirmed setup is never shown again.
  - **Passkeys (WebAuthn / FIDO2)** — users register passkeys from their profile (up to 10 per user); a passkey can complete the second factor instead of an authenticator code.
  - **Method chooser** — a user with both TOTP and passkeys chooses either at sign-in; a user required to enroll picks a method when nothing is registered yet.
  - **Admin recovery** — admins can reset a user's authenticator or remove a user's passkeys from the user editor.
  - Enrollment is required per user via the "require second factor" setting; any registered passkey or confirmed TOTP always enforces the second-factor prompt.
- **Login throttling** — failed username/password sign-ins are throttled per account (attempt limit, pause duration, failure window), audit-logged as `login.paused`.
- **Local login fallback with OIDC** — `OidcAuth:LocalLoginFallback` = `LoopbackOnly` (default), `Always`, or `Never`.
- **Session cookies** — HTTP-only, `SameSite=Lax`, secure-always, session-only (no persistence), with a configurable idle timeout and optional sliding expiration.
- **Self-service profile** — users manage their own passkeys and authenticator from `/account/profile`.

## 5. Security controls

- **Server-side encryption at rest** — share secrets and information-request responses are encrypted with AES-GCM; a 256-bit key is derived from `Encryption:Passphrase` and a per-secret salt via PBKDF2-SHA256, with a random nonce per encryption.
- **Client-side (browser) encryption** — optional AES-GCM encryption via Web Crypto for high-sensitivity shares and responses; only opaque payloads reach the server.
- **Password hashing** — admin and local-user passwords are hashed with Argon2id, falling back to scrypt; legacy PBKDF2-SHA256 hashes remain valid. Cleartext admin passwords are not supported.
- **Access codes** stored as keyed HMAC-SHA256 hashes (legacy bare SHA-256 still verifies).
- **Passphrase startup validation** — the app refuses to start when `Encryption:Passphrase` is missing or shorter than 15 characters (32+ recommended).
- **SMTP password encrypted at rest**; the admin mail form is write-only and never echoes the stored value.
- **Security headers** — `Content-Security-Policy` (`frame-ancestors 'none'`), `X-Frame-Options: DENY`, `X-Content-Type-Options: nosniff`, `Referrer-Policy: strict-origin-when-cross-origin`.
- **Anti-forgery tokens** on state-changing forms.
- **Audit-log sanitization** — user-derived values are newline-sanitized to prevent log forging.
- **Trusted forwarded headers** — optional `X-Forwarded-For`/`X-Forwarded-Proto` handling accepted only from configured proxies/networks.
- **Non-root container** — the Docker image runs as an unprivileged user.

## 6. Storage backends

Selectable via `Storage:Backend`, for both shares/requests and audit logs:

- **SQLite** — single-instance / small deployments (EF Core migrations with migration locking).
- **SQL Server** — EF Core provider + migrations.
- **PostgreSQL** — EF Core provider + migrations.
- **Azure** — Key Vault for share secrets + Azure Table Storage for audit logs.

Supporting behavior:

- Automatic EF Core migration on startup (opt-in per backend via `ApplyMigrationsOnStartup`).
- Database resilience: transient failures are retried (`DatabaseResilience:MaxAttempts`, `DelayMilliseconds`).
- `/health` endpoint performs a live database connectivity probe for the database-backed modes.
- Database-backed platform features (local users, editable mail config, runtime timezone/settings, KPI counters) require a database backend (`sqlite`, `sqlserver`, or `postgresql`).

## 7. Auditing and metrics

- **Audit log** (visible to admins and auditors) covers login/logout, admin and OIDC sign-in success/failure, login throttling, share create/access/revoke, information-request create/access/response-update/revoke/extend, and background cleanup events.
- **Audit filtering and export** — filter by date range, actor, operation, and success; free-text search; paging (default 100 rows); JSON export of filtered results.
- **Usage metric counters** — persisted KPI counters (shares created/accessed/revoked, information requests created/submitted/revoked, admin/user logins, expired and expired-unused cleanup).
- **Console audit logging** — optional mirror of audit events to console at `DEBUG`/`INFO`/`ERROR`.

## 8. Operations and configuration

- **Timezone display** — admin- and recipient-facing times use `Application:TimeZoneId` (Windows or IANA IDs).
- **Path base** — publish under a sub-URI with `Application:PathBase`.
- **Session policy** — configurable idle timeout and sliding expiration.
- **Mail configuration UI** — SMTP settings and notification templates managed from the admin console.
- **Application settings UI** — runtime timezone and per-share/-request failed-attempt pause controls.
- **Admin password hash CLI / script** — `hash-admin-password` command and `scripts/new-admin-password-hash.ps1`.
- **Docker lifecycle** — `start-docker.sh` (start/stop/clean) driving `docker-compose.yml`, with env-file generation from appsettings templates.
- **Azure automation** — `scripts/provision-azure.ps1` (resource group, storage + audit table, Key Vault, app registration) and `scripts/deploy-appservice.ps1` (App Service plan/web app + settings + deploy).

---

## Routes at a glance

| Path | Audience | Purpose |
| --- | --- | --- |
| `/Dashboard` | Admin/User | Console chooser start page |
| `/Admin` | Admin/User | Secure shares console (list, create, revoke) |
| `/Admin/Audit` | Admin/Auditor | Audit log (filter, search, JSON export) |
| `/InformationRequests` | Admin/User | Information requests console (list, create, details, extend, revoke) |
| `/Users` | Admin | Local user management + second-factor reset |
| `/Configuration/Mail` | Admin | SMTP + notification templates |
| `/Configuration/Settings` | Admin | Timezone + failed-attempt pause |
| `/account/login` | Local admin/user | Sign-in (with TOTP/passkey second factor) |
| `/account/profile` | Authenticated | Self-service passkey/authenticator management |
| `/s/{token}` | Recipient (external) | Open a secure share |
| `/r/{token}` | Partner (external) | Submit/update an information request |
| `/health` | Ops | Liveness + database connectivity probe |
</content>
</invoke>
