# Production Configuration Guide

This guide focuses on secure production settings for Sekura.

## 1) Secrets and credentials

Set strong values before deployment:

- `AdminAuth:Username`: non-default admin name.
- `AdminAuth:PasswordHash`: required password hash. New hashes use Argon2id and fall back to scrypt if Argon2id is unavailable. Legacy PBKDF2-SHA256 hashes are still accepted.
- `Encryption:Passphrase`: long random secret (32+ chars recommended). The app refuses to start when it is missing or shorter than 15 characters.

Recommendations:

- All configuration lives in `.env` files (`Section__Key=value`). Only `.env.template` belongs in source control — never commit `.env.prod`/`.env.dev` once secrets are filled in.
- Generate `AdminAuth:PasswordHash` with `./scripts/new-admin-password-hash.ps1`.
- The app fails startup if `AdminAuth:PasswordHash` is missing or invalid.
- Prefer environment variables or secret stores.
- Rotate admin credentials and `Encryption:Passphrase` regularly.

## 2) Network and TLS

For production, enable HTTPS redirection and bind the listen URL:

```dotenv
Application__EnableHttpsRedirection=true
```

Bind the endpoint behind a reverse proxy or directly with TLS certs. Example HTTP-only (reverse proxy handles TLS):

```dotenv
ASPNETCORE_URLS=http://0.0.0.0:5099
```

Leave `ASPNETCORE_URLS` unset for containers (the image binds port 8080) and App Service (the deploy script sets it).

If the app is hosted under a sub-URI on the web server, configure `Application__PathBase`.

Examples:

- Root deployment: `Application__PathBase=/`
- Subpath deployment: `Application__PathBase=/sekura`

Configure `Application__TimeZoneId` if admin and recipient pages should display times in a specific timezone instead of `UTC`.

Examples:

- IANA: `Application__TimeZoneId=Europe/Stockholm`
- Windows: `Application__TimeZoneId=W. Europe Standard Time`

Authentication cookies remain session-only, so closing the browser ends the session. The default idle timeout is 60 minutes and can be configured with:

- `Application__AuthenticationSessionTimeoutMinutes`
- `Application__AuthenticationSlidingExpiration`

If exposing directly on internet, terminate TLS at app or trusted ingress and restrict inbound ports.

### Reverse proxy and forwarded headers

When the app runs behind a reverse proxy, enable forwarded-header processing so audit logs record the real client IP and IP-based checks (such as the local login fallback) see the actual caller instead of the proxy:

```dotenv
ForwardedHeaders__Enabled=true
ForwardedHeaders__KnownProxies__0=127.0.0.1
ForwardedHeaders__KnownNetworks__0=10.0.0.0/8
```

`X-Forwarded-For` and `X-Forwarded-Proto` are only accepted from the listed proxies/networks; startup fails when the section is enabled without any trusted source configured.

### Local login fallback with OIDC

When OIDC is enabled, `OidcAuth:LocalLoginFallback` controls whether the built-in username/password form stays reachable:

- `LoopbackOnly` (default): only requests from the app host itself can use the form (break-glass access).
- `Never`: every sign-in goes through OIDC.
- `Always`: the form stays available to everyone (not recommended).

### Passkeys (WebAuthn)

Local users can complete second-factor sign-in with a passkey instead of an authenticator code once the feature is configured:

```dotenv
Passkey__Enabled=true
Passkey__ServerDomain=sekura.example.com
Passkey__ServerName=Sekura
Passkey__Origins__0=https://sekura.example.com
```

- `ServerDomain` is the WebAuthn relying-party ID (the site's domain). Registered passkeys are bound to it — changing the domain invalidates every passkey.
- `Origins` lists the exact web origins users sign in from; startup fails when the section is enabled without them.
- Browsers only offer WebAuthn in a secure context, so the site must be served over HTTPS (localhost is exempt for development).
- Users register passkeys from their profile; administrators can remove a user's passkeys from the user editor (audit operations `local-user.passkey.register`, `local-user.passkey.login`, `local-user.passkey.remove`, `local-user.passkey.reset`).
- A user with both TOTP and passkeys gets a chooser during sign-in; either method completes the second factor.

### Login throttling

Failed username/password sign-ins are throttled per account. Defaults (5 attempts, 15-minute pause, 60-minute failure window) can be tuned:

```dotenv
LoginThrottle__FailedAttemptLimit=5
LoginThrottle__PauseMinutes=15
LoginThrottle__FailureWindowMinutes=60
```

Throttle events appear in the audit log as `login.paused`.

## 3) Storage backend selection

Set:

- `Storage__Backend`: `sqlite`, `sqlserver`, `postgresql`, or `azure`

### SQLite

Use only for small/single-instance deployments. SQLite startup migrations should run with a single application instance; if a migration is interrupted, clear the EF Core `__EFMigrationsLock` table before retrying:

```dotenv
Storage__Backend=sqlite
SqliteStorage__ConnectionString=Data Source=/var/lib/sekura/sekura.db
SqliteStorage__ApplyMigrationsOnStartup=true
```

### SQL Server

Use encrypted transport and managed identity/least privilege where possible:

```dotenv
Storage__Backend=sqlserver
SqlServerStorage__ConnectionString=Server=tcp:sql.example.com,1433;Database=Sekura;Encrypt=True;TrustServerCertificate=False;User ID=...;Password=...
SqlServerStorage__ApplyMigrationsOnStartup=true
```

### PostgreSQL

Require SSL mode and least-privilege DB user:

```dotenv
Storage__Backend=postgresql
PostgresqlStorage__ConnectionString=Host=db.example.com;Port=5432;Database=sekura;Username=sekura_app;Password=...;SSL Mode=Require;Trust Server Certificate=false
PostgresqlStorage__ApplyMigrationsOnStartup=true
```

### Azure

For the Azure backend, shares are stored in Key Vault and audit logs are stored in Azure Table Storage:

```dotenv
Storage__Backend=azure
AzureStorage__KeyVault__VaultUri=https://sekura.vault.azure.net/
AzureStorage__KeyVault__TenantId=<tenant-id>
AzureStorage__KeyVault__ClientId=<client-id>
AzureStorage__KeyVault__ClientSecret=<client-secret>
AzureStorage__KeyVault__SecretPrefix=sekura
AzureStorage__TableAudit__ServiceSasUrl=https://account.table.core.windows.net/?<sas>
AzureStorage__TableAudit__TableName=auditlogs
AzureStorage__TableAudit__PartitionKey=audit
```

Database-backed platform features such as local user management, editable mail configuration, runtime timezone settings, and persisted KPI counters are only available when `Storage__Backend` is `sqlite`, `sqlserver`, or `postgresql`.

For the database-backed storage modes, the app now retries transient database failures before returning an error. Configure:

- `DatabaseResilience__MaxAttempts`: total database attempts before failing startup checks or a request. Default `3`.
- `DatabaseResilience__DelayMilliseconds`: wait time between attempts. Default `1000`.

The `/health` endpoint performs a live database connectivity probe for `sqlite`, `sqlserver`, and `postgresql`.

## 4) Share lifetime and cleanup

Security-related retention:

- `Share__DefaultExpiryHours`: keep short (example `1`–`4`).
- `Share__CleanupIntervalSeconds`: low enough to clear expired items quickly (example `30`–`60`).

Example:

```dotenv
Share__DefaultExpiryHours=2
Share__CleanupIntervalSeconds=30
```

## 5) Mail notifications

Configure the `Mail` section if administrators and share creators should receive an email when a share is opened:

```dotenv
Mail__SmtpHost=smtp.example.com
Mail__Port=587
Mail__Username=sekura
Mail__Password=<secret>
Mail__UseTls=true
Mail__SenderEmail=sekura@example.com
Mail__SenderDisplayName=Sekura
Mail__AdminNotificationRecipients=admin1@example.com;admin2@example.com
Mail__NotifyAdminsOnShareAccess=true
Mail__NotifyCreatorOnShareAccess=true
Mail__ShareAccessedSubjectTemplate=Share used: {{SharedUsername}} for {{RecipientEmail}}
```

The multi-line `Mail__ShareAccessedBodyTemplate` cannot be represented in a `.env` file; the built-in default applies, and both templates are editable in the admin mail configuration UI.

The SMTP password is encrypted at rest with `Encryption:Passphrase` when stored in a database backend, and the admin mail form never echoes it back — leave the password field blank to keep the stored value. Existing plaintext values are encrypted automatically at startup.

Template placeholders:

- `{{ShareId}}`
- `{{CreatedBy}}`
- `{{RecipientEmail}}`
- `{{SharedUsername}}`
- `{{AccessedBy}}`
- `{{AccessedAt}}`
- `{{ExpiresAt}}`
- `{{TimeZoneId}}`

## 6) Operational hardening

- Set `AllowedHosts` to known hostnames instead of `*` when possible.
- Run app with least-privileged OS account.
- Restrict database account to only required schema/table permissions.
- Store logs centrally and monitor audit events (`admin.login`, `share.access`, `share.create`, `share.revoke`).
- Back up database securely (encrypted backups).

## 7) Example production configuration

Create `.env.prod` from the template (`cp .env.template .env.prod`) and adjust, for example:

```dotenv
ASPNETCORE_ENVIRONMENT=Production
Application__EnableHttpsRedirection=true
Application__TimeZoneId=UTC
ASPNETCORE_URLS=http://0.0.0.0:5099
Storage__Backend=postgresql
PostgresqlStorage__ConnectionString=Host=db.example.com;Port=5432;Database=sekura;Username=sekura_app;Password=...;SSL Mode=Require;Trust Server Certificate=false
PostgresqlStorage__ApplyMigrationsOnStartup=true
Share__DefaultExpiryHours=2
Share__CleanupIntervalSeconds=30
```

The file is read by docker compose (`env_file`), the start scripts, and `scripts/deploy-appservice.ps1`. Keep secrets out of source control; inject the most sensitive values through the environment or a secret store when possible.
