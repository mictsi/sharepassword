# sekura (.NET 10)

![.NET](https://img.shields.io/badge/.NET-10-512BD4?logo=dotnet)
[![Build](https://github.com/mictsi/sekura/actions/workflows/build.yml/badge.svg)](https://github.com/mictsi/sekura/actions/workflows/build.yml)

Latest release: `0.5.1` (2026-05-03). See `../docs/RELEASE_NOTES.md`.

Secure exchange for external users with:

- Secure shares: send a secret via unique link (`/s/{token}`) + email + 10-character access code
- Information requests: collect partner input via unique link (`/r/{token}`) + email + 15-character access code, with submit/update until expiry
- Local admin login interface plus admin-managed local users and optional OIDC/Entra ID SSO
- Selectable storage backend via configuration
- EF Core-backed storage for SQLite, SQL Server, or PostgreSQL
- Azure backend using Key Vault for shares and Azure Table Storage for audit logs
- Automatic expiration and cleanup for shares and information requests (default 4 hours)
- Second factor for local accounts: authenticator app (TOTP) and passkeys (WebAuthn/FIDO2)
- `Admin`, `User`, and built-in `Auditor` roles
- Per-share and per-request failed access attempt pause controls
- Optional browser-side secret/response encryption with an extra password
- Audit logging (with filtering and JSON export) for admin, user, and system operations

## Run

```bash
dotnet restore
dotnet run
```

## Startup scripts (workspace root)

From `e:/KTH/passwordManagerAzureKeyvault`:

- Windows (PowerShell):

```powershell
./start-win.ps1
```

- Linux/macOS (bash):

```bash
chmod +x ./start-linux.sh
./start-linux.sh
```

By default, the start scripts load `.env.dev` (create it with `cp .env.template .env.dev`) and use its `ASPNETCORE_URLS` for the listen URL.

Optional overrides:

- Windows: `./start-win.ps1 -EnvFile .env.prod -Urls https://localhost:7099 -Configuration Release`
- Linux: `./start-linux.sh .env.prod https://localhost:7099 Release`

## Configuration

All settings live in `.env` files using the ASP.NET environment format (`Section__Key=value`, arrays as `Section__0`):

- `.env.template` — checked-in template with every key
- `.env.dev` — development values (loaded by the start scripts)
- `.env.prod` — production values (docker compose, App Service deploy)

`.env.dev` and `.env.prod` are gitignored; never commit them once secrets are filled in.

Production hardening guide: `sekura/CONFIGURATION.md`

### Configuration reference

Keys below are written as configuration paths (`Section:Key`); in `.env` files replace `:` with `__` (for example `Application:Name` → `Application__Name`).

- `Application:Name`: app name shown in config/operations.
- `Application:EnableHttpsRedirection`: set `true` when HTTPS endpoint/certificate is configured.
- `Application:PathBase`: base path when the app is published under a sub-URI (default `/`).
- `Application:TimeZoneId`: timezone used for displayed/admin-facing times. Accepts Windows or IANA IDs, default `UTC`.
- `Application:AuthenticationSessionTimeoutMinutes`: idle timeout for the authentication session cookie (default `60`).
- `Application:AuthenticationSlidingExpiration`: when `true`, refreshes the session timeout while the user remains active.
- `Kestrel:Endpoints:Http:Url`: HTTP host+port (example: `http://0.0.0.0:5099`).
- `Storage:Backend`: selected storage backend (`sqlite`, `sqlserver`, `postgresql`, `azure`).
- `DatabaseResilience:MaxAttempts`: total database attempts before failing a request or startup check (default `3`).
- `DatabaseResilience:DelayMilliseconds`: wait time between database retry attempts (default `1000`).
- `SqliteStorage:ConnectionString`: SQLite connection string.
- `SqliteStorage:ApplyMigrationsOnStartup`: applies pending EF Core migrations when `Storage:Backend=sqlite`. SQLite uses EF Core migration locking; if startup migration is interrupted, clear the `__EFMigrationsLock` table before retrying or apply migrations out of process.
- `SqlServerStorage:ConnectionString`: SQL Server connection string.
- `SqlServerStorage:ApplyMigrationsOnStartup`: applies pending EF Core migrations when `Storage:Backend=sqlserver`.
- `PostgresqlStorage:ConnectionString`: PostgreSQL connection string.
- `PostgresqlStorage:ApplyMigrationsOnStartup`: applies pending EF Core migrations when `Storage:Backend=postgresql`.
- `AzureStorage:KeyVault:*`: Azure Key Vault settings used when `Storage:Backend=azure`.
- `AzureStorage:TableAudit:*`: Azure Table Storage audit settings used when `Storage:Backend=azure`.
- `AdminAuth:Username`: local admin username.
- `AdminAuth:PasswordHash`: required password hash. New hashes use Argon2id and fall back to scrypt if Argon2id is unavailable. Legacy PBKDF2-SHA256 hashes are still accepted.
- `OidcAuth:Enabled`: enable OIDC as alternative admin login.
- `OidcAuth:Authority`: OIDC authority/issuer URL.
- `OidcAuth:ClientId`: OIDC client ID.
- `OidcAuth:ClientSecret`: OIDC client secret.
- `OidcAuth:LogTokensForTroubleshooting`: when `true`, writes OIDC tokens to audit logs for troubleshooting.
- `OidcAuth:CallbackPath`: OIDC callback path (default `/signin-oidc`).
- `OidcAuth:SignedOutCallbackPath`: post-logout callback path.
- `OidcAuth:RequireHttpsMetadata`: should be `true` in production.
- `OidcAuth:Scopes`: scopes requested during login.
- `OidcAuth:GroupClaimType`: claim type used for incoming OIDC groups (default `groups`).
- `OidcAuth:AdminRoleName`: app role name used for administrators.
- `OidcAuth:UserRoleName`: app role name used for standard users.
- `OidcAuth:AdminGroups`: OIDC group IDs/names that map to admin role.
- `OidcAuth:UserGroups`: OIDC group IDs/names that map to user role.
- `Encryption:Passphrase`: required secret used for AES encryption at rest.
- `Share:DefaultExpiryHours`: default share expiration in hours.
- `Share:CleanupIntervalSeconds`: frequency for expired-share cleanup service.
- `ConsoleAuditLogging:Enabled`: enable/disable writing audit events to console logs.
- `ConsoleAuditLogging:Level`: console audit level (`DEBUG`, `INFO`, `ERROR`).
- `Logging:LogLevel:*`: standard ASP.NET logging levels.
- `Logging:LogLevel:Microsoft.EntityFrameworkCore`: EF Core logging level, default `Error`.
- `AllowedHosts`: allowed hostnames.

### Generate an admin password hash

The app accepts admin password hashes in these formats:

```text
ARGON2ID$v=19$m=<memory-kib>,t=<iterations>,p=<parallelism>$<salt-base64>$<hash-base64>
SCRYPT$N=<cost>,r=<block-size>,p=<parallelism>$<salt-base64>$<hash-base64>
PBKDF2$SHA256$<iterations>$<salt-base64>$<hash-base64>  (legacy)
```

Generate a new hash from the repository root with the included PowerShell script:

```powershell
./scripts/new-admin-password-hash.ps1
```

The script prompts for the password and prints a value you can paste into `AdminAuth:PasswordHash`. It prefers Argon2id and falls back to scrypt only if Argon2id cannot be used on the current runtime.

If you need to pass the password non-interactively:

```powershell
./scripts/new-admin-password-hash.ps1 -Password "use-a-long-random-password"
```

Update your config to use the generated hash:

```json
"AdminAuth": {
  "Username": "admin",
  "PasswordHash": "ARGON2ID$v=19$m=65536,t=3,p=1$<salt>$<hash>"
}
```

Cleartext `AdminAuth:Password` is no longer supported. The app fails startup if `AdminAuth:PasswordHash` is missing or invalid.

### Using Storage Backends

For SQLite, SQL Server, or PostgreSQL:

1. Set `Storage:Backend` to `sqlite`, `sqlserver`, or `postgresql`.
2. Fill the matching `*Storage` section connection string.
3. Set `ApplyMigrationsOnStartup=true` in that section if you want startup migration execution. For SQLite, prefer applying migrations during maintenance or with a single app instance to avoid SQLite migration-lock waits.

For Azure:

1. Set `Storage:Backend` to `azure`.
2. Fill `AzureStorage:KeyVault` for secure-share secrets.
3. Fill `AzureStorage:TableAudit` for audit logs.

In all cases:

1. Change `AdminAuth:Username`, configure `AdminAuth:PasswordHash`, and change `Encryption:Passphrase`.
2. If the app is published below the site root, set `Application:PathBase` to that subpath such as `/sekura`.
3. Set `Application:TimeZoneId` if admin-facing times should use a timezone other than `UTC`, for example `Europe/Stockholm` or `W. Europe Standard Time`.
4. Adjust `Application:AuthenticationSessionTimeoutMinutes` and `Application:AuthenticationSlidingExpiration` if the default 60-minute session policy is not what you want.
5. Start the app.

The `/health` endpoint now performs a real database connectivity check for the database-backed storage modes.

Timezone examples for `Application:TimeZoneId`:

- Stockholm: `Europe/Stockholm` or `W. Europe Standard Time`
- London: `Europe/London` or `GMT Standard Time`
- San Francisco: `America/Los_Angeles` or `Pacific Standard Time`
- Tokyo: `Asia/Tokyo` or `Tokyo Standard Time`

### OIDC login (alternative to local login)

- Enable in config: `OidcAuth:Enabled=true`.
- Fill `OidcAuth:Authority`, `OidcAuth:ClientId`, and `OidcAuth:ClientSecret`.
- Keep `OidcAuth:RequireHttpsMetadata=true` in production.
- When OIDC is enabled, users can sign in via `/account/externallogin`.
- Local admin login remains available only from localhost.
- Group claims are mapped to app roles using `OidcAuth:AdminGroups` and `OidcAuth:UserGroups`.
- If you use `scripts/provision-azure.ps1`, the created OIDC app is configured with `groupMembershipClaims=SecurityGroup` by default so `groups` claims are emitted in tokens.
- Shares can optionally require OIDC login (`Require Entra ID login to access`), in which case only the configured recipient email can access the secret.

### Local account authenticator codes

Database-backed storage modes support authenticator app codes for local accounts.

- Admins can require authenticator setup per user from `/users`.
- Users with required authenticator setup are taken through onboarding after password sign-in.
- The onboarding page shows a QR code, manual key, and provisioning URI and can be printed for archival storage.
- Confirmed authenticator setup details are never shown again. If a user changes their authenticator, a new setup is generated and the old setup remains active until the new code is verified.
- Admins can reset a user's authenticator setup from the user edit page when the current authenticator is lost. If the account still requires authenticator codes, the user must onboard again at next sign-in.

### Passkeys (second factor)

Local accounts can complete second-factor sign-in with a passkey (WebAuthn/FIDO2) instead of an authenticator code.

- Enable and configure the `Passkey` section (`Enabled`, `ServerDomain`, `ServerName`, `Origins`). `ServerDomain` is the WebAuthn relying-party ID (the site domain) and `Origins` lists the exact HTTPS origins users sign in from; startup fails if the section is enabled without them.
- Users register passkeys from their profile (up to 10 each); admins can remove a user's passkeys from the user edit page.
- A user with both a passkey and a confirmed authenticator gets a chooser at sign-in; either completes the second factor.

See `sekura/CONFIGURATION.md` for the full passkey configuration.

### Sign-in hardening

- `LoginThrottle:*` throttles repeated failed username/password attempts per account (audit operation `login.paused`).
- `OidcAuth:LocalLoginFallback` (`LoopbackOnly` default / `Always` / `Never`) controls whether the local login form stays reachable when OIDC is enabled.
- `ForwardedHeaders:*` enables trusted-proxy `X-Forwarded-For`/`X-Forwarded-Proto` handling so audit logs and IP checks see the real client.

### Auditor role

The built-in `Auditor` role grants read access to the audit log (`AuditAccess` policy) without full admin rights. Assign it to a local user from the user editor, or emit an OIDC app role claim named `Auditor` from the identity provider (group-to-role mapping in `OidcAuth` only covers the admin and user roles).

### Share access failed-attempt pause

The application settings page includes per-share lockout controls:

- Failed attempts before pause
- Pause duration in minutes

Failed recipient email or access code attempts are counted against that specific share. After the threshold is reached, attempts for that share are paused until the configured time has elapsed; successful access clears the failed-attempt state.

### Browser-side extra password protection

When creating a share, enable `Protect with extra password` to encrypt the `Password or secret` field in the browser before the form is submitted. The app stores only the encrypted browser payload and the recipient must enter the extra password in their browser after normal link, email/OIDC, and access-code checks pass.

The extra password is never sent to the server and is not recoverable. This protects stored secrets from database readers, backups, and application operators who cannot alter the delivered client code. It does not protect against an administrator who can change the JavaScript served to users; for that stronger threat model, serve the decrypting client from a separately trusted and integrity-controlled origin.

### Environment variables (Docker / Azure App Service)

Configuration is read from JSON files and environment variables. Use `__` for nested keys.

Examples:

- `Kestrel__Endpoints__Https__Url=https://localhost:7099`
- `Application__PathBase=/`
- `Application__TimeZoneId=UTC`
- `Application__AuthenticationSessionTimeoutMinutes=60`
- `Application__AuthenticationSlidingExpiration=true`
- `Storage__Backend=sqlite`
- `DatabaseResilience__MaxAttempts=3`
- `DatabaseResilience__DelayMilliseconds=1000`
- `SqliteStorage__ConnectionString=Data Source=App_Data/sekura.db`
- `SqliteStorage__ApplyMigrationsOnStartup=true`
- `SqlServerStorage__ConnectionString=Server=tcp:sql.example.com,1433;Database=Sekura;Encrypt=True;TrustServerCertificate=False;User ID=sekura_app;Password=<password>`
- `PostgresqlStorage__ConnectionString=Host=db.example.com;Port=5432;Database=sekura;Username=sekura_app;Password=<password>;SSL Mode=Require;Trust Server Certificate=false`
- `AzureStorage__KeyVault__VaultUri=https://myvault.vault.azure.net/`
- `AzureStorage__TableAudit__ServiceSasUrl=<table-service-sas-url>`
- `AdminAuth__Username=admin`
- `AdminAuth__PasswordHash=<argon2id-or-scrypt-hash>`
- `Encryption__Passphrase=<long-random-passphrase>`
- `OidcAuth__Enabled=true`
- `OidcAuth__Authority=https://login.microsoftonline.com/<tenant-id>/v2.0`
- `OidcAuth__ClientId=<client-id>`
- `OidcAuth__ClientSecret=<client-secret>`
- `OidcAuth__LogTokensForTroubleshooting=false`
- `OidcAuth__GroupClaimType=groups`
- `OidcAuth__AdminRoleName=Admin`
- `OidcAuth__UserRoleName=User`
- `ConsoleAuditLogging__Enabled=false`
- `ConsoleAuditLogging__Level=INFO`

For array values (for example scopes), use indexed variables:

- `OidcAuth__Scopes__0=openid`
- `OidcAuth__Scopes__1=profile`
- `OidcAuth__Scopes__2=email`
- `OidcAuth__AdminGroups__0=<entra-group-id-for-admins>`
- `OidcAuth__UserGroups__0=<entra-group-id-for-users>`

#### Generate Docker start script + compose from the env file

To generate Docker assets from the current `.env.prod` (override with `-EnvFile`), run:

```powershell
pwsh ./scripts/generate-docker-assets.ps1
```

The generator writes these files under `artifacts/docker/`:

- `start.sh`
- `docker-compose.generated.yml`

The generated files contain literal values from the env file, including any configured secrets, so review them before sharing.

The generator preserves the current application settings and applies container-safe overrides for `Application__EnableHttpsRedirection`, `ASPNETCORE_ENVIRONMENT`, `ASPNETCORE_URLS`, `Kestrel__Endpoints__Http__Url`, and the SQLite connection string when `Storage__Backend=sqlite`.

The generated Compose file intentionally omits the environment variables. Use the generated `start.sh` launcher to build the image and run the container with `docker run --env ...` arguments derived from the env file.

Start the app with the generated launcher:

```bash
bash ./artifacts/docker/start.sh
```

The generated Compose file is still written as a minimal reference for image, port, and volume settings:

```powershell
docker compose -f ./artifacts/docker/docker-compose.generated.yml config
```

## Usage

1. Open `/account/login` and sign in as admin.
2. Create share with recipient email + username + secret text + instructions + expiry.
3. Send recipient email, unique link, and expiration time by email.
4. Send the 10-character one-time access code separately via SMS to recipient mobile phone.
5. Recipient opens the link and submits email + code to view credentials, secret text, and instructions.
6. Share is removed automatically after expiration.

Secret text notes:

- Max length is `10000` characters.
- Multiline content is supported.
- Plain text, special characters, YAML, and JSON formatting are preserved end-to-end.

Instructions notes:

- Max length is `10000` characters.
- Multiline content is supported.
- Plain text formatting and line breaks are preserved end-to-end.

Access code notes:

- Access codes are exactly `10` characters long.
- Allowed characters are uppercase letters, lowercase letters, numbers, `#`, and `-`.
- Access codes are case-sensitive.

### Information requests

1. Open **Admin console - Information requests** and create a request with the partner email, instructions, and expiry (optionally requiring Entra ID sign-in).
2. Send the partner email, unique link (`/r/{token}`), and expiration time by email.
3. Send the `15`-character one-time access code separately (for example by SMS).
4. The partner opens the link, proves access with email + code (or Entra ID + code), and submits their information.
5. Reopening the link lets the partner update the submitted information until expiration.
6. The requester can review the response, extend expiration (1–168 hours), or revoke the request; expired requests are removed automatically.

Information request response notes:

- Responses support the same browser-side extra-password encryption option as shares.
- Failed email/code attempts are throttled per request using the same application settings as share access.

## Audit logs

Audit log entries are stored in the selected backend and are visible in admin UI (`Audit Logs`).

- Login/logout attempts
- Share creation and revoke
- External access attempts (success/failure)
- Automatic cleanup of expired shares
