# Sekura

[![Build](https://github.com/mictsi/sekura/actions/workflows/build.yml/badge.svg)](https://github.com/mictsi/sekura/actions/workflows/build.yml)


`sekura` is a secure exchange app built with ASP.NET Core (.NET 10). It runs two external-facing workflows behind one authenticated admin console:

- **Secure shares** — hand a password or other sensitive text to a recipient without leaving it in email, chat, or shared documents. An app user creates a share with a recipient, secret text, optional instructions, and an expiration time. The app generates a unique access link and a separate `10`-character one-time access code, so the link and code can be delivered through different channels. Recipients open the link, confirm their email, and enter the access code before the secret is shown. Shares can be deleted after retrieval and expire automatically.
- **Information requests** — collect information *back* from an external partner. App users create a request from the information-request console; the partner uses a secure link and a `15`-character access code to submit or update information until expiration. Reopening a submitted request prompts the partner to update it; a protected response is decrypted once in the browser and can be re-saved without re-entering the extra password during that page session.

After sign-in, `/Dashboard` opens a two-card start page for `Admin console - Secure shares` and `Admin console - Information requests`. For higher-sensitivity secrets or responses, optional browser-side extra-password encryption stores only an encrypted payload on the server.

Supported storage backends for both workflows and audit logs:

- SQLite
- SQL Server
- PostgreSQL
- Azure Key Vault + Azure Table Storage

## Features

A full inventory is in [docs/FEATURES.md](docs/FEATURES.md). In brief:

- **Two workflows** — one-time secure shares (`/s/{token}`) and information requests (`/r/{token}`), each with expiring links, separate access codes, per-item failed-attempt pause, automatic cleanup, and optional browser-side encryption.
- **Access modes** — `Email + Code` or `Entra ID Required + Email + Code` (OIDC recipient verification).
- **Accounts and roles** — configured local admin, database-backed local users, and optional OIDC/Microsoft Entra ID SSO mapped to `Admin`, `User`, and `Auditor` roles.
- **Second factor** — authenticator app (TOTP) and passkeys (WebAuthn/FIDO2) for local accounts, with a sign-in method chooser, enrollment chooser, and admin reset.
- **Encryption** — AES-GCM at rest for stored secrets/responses; Argon2id (scrypt fallback) password hashing; HMAC-SHA256 access-code hashes.
- **Auditing** — audit log with date/actor/operation filtering, search, paging, and JSON export, plus persisted usage-metric counters.
- **Operations** — Docker Compose lifecycle, Azure provisioning/deploy scripts, timezone and path-base support, mail notifications, and a `/health` database probe.

## Repository layout

- `sekura/` — web application project
- `sekura.Tests/` — test project
- `Dockerfile` — container image definition
- `docker-compose.yml` — compose definition (service, port, data volume)
- `start-docker.sh` — build and run the app via Docker Compose (start/stop/clean)
- `start-linux.sh` / `start-win.ps1` — run the app locally with the .NET SDK
- `.github/workflows/build.yml` — CI workflow
- `docs/CHANGELOG.md` — changelog
- `docs/RELEASE_NOTES.md` — consolidated release notes
- `docs/flowDiagram.md` — user flow diagram

## Quick start

```bash
dotnet restore ./sekura.sln
dotnet run --project ./sekura/sekura.csproj
```

## Run with Docker

`start-docker.sh` builds the image and manages the container through Docker Compose (`docker-compose.yml`). The container runs as a non-root user and stores the SQLite database in a named volume (`sekura-data`).

Generate a full configuration file from the appsettings templates, fill in the secrets, and start:

```bash
./scripts/generate-env-file.sh prod   # writes .env.prod from appsettings.json.in
./scripts/generate-env-file.sh dev    # writes .env.dev (base + Development overlay)

# Generate the admin password hash and put it in the file
dotnet run --project ./sekura -- hash-admin-password --password '<password>'

cp .env.prod .env.docker       # start-docker.sh and compose read .env.docker

./start-docker.sh start        # build image + run container on port 8080
./start-docker.sh stop         # stop the container
./start-docker.sh clean        # remove container + image, keep data volume
./start-docker.sh clean --all  # also remove the data volume (deletes the database)
```

The generator flattens the JSON templates to ASP.NET environment format (`Section__Key=value`), sets `ASPNETCORE_ENVIRONMENT`, omits `Kestrel__*` (the image binds port 8080 itself), and points the SQLite path at the data volume. It lists any placeholders that still need real values. Alternatively, export just `AdminAuth__PasswordHash` and `Encryption__Passphrase` in the shell and skip the file — the container then runs on image defaults.

Set `SEKURA_PORT` to publish a different host port. When running behind a reverse proxy, configure the `ForwardedHeaders` section so audit logs record real client IPs (see [sekura/CONFIGURATION.md](sekura/CONFIGURATION.md)).

For full configuration and usage instructions, see:

- [docs/FEATURES.md](docs/FEATURES.md)
- [docs/app-overview.md](docs/app-overview.md)
- [DESIGN.md](DESIGN.md)
- [docs/flowDiagram.md](docs/flowDiagram.md)
- [docs/CHANGELOG.md](docs/CHANGELOG.md)
- [docs/RELEASE_NOTES.md](docs/RELEASE_NOTES.md)
- [sekura/README.md](sekura/README.md)
- [sekura/CONFIGURATION.md](sekura/CONFIGURATION.md)

## Admin password hash

Generate an admin password hash (Argon2id, with scrypt fallback; legacy PBKDF2-SHA256 hashes remain valid) from the repository root with:

```powershell
./scripts/new-admin-password-hash.ps1
```

or with the .NET CLI:

```bash
dotnet run --project ./sekura -- hash-admin-password --password '<password>'
```

Paste the output into `AdminAuth:PasswordHash`. Cleartext `AdminAuth:Password` is no longer supported.

The full admin authentication configuration is documented in `sekura/README.md`.

## Security hardening

Beyond the share access controls described above, the app ships with:

- Second-factor sign-in for local accounts: authenticator app (TOTP) and passkeys (WebAuthn/FIDO2), with method chooser and admin reset (`Passkey` section)
- Built-in `Auditor` role and `AuditAccess` policy for read access to audit data without full admin rights
- Per-account throttling of failed sign-ins (`LoginThrottle` section, audit-logged as `login.paused`)
- Startup validation of `Encryption:Passphrase` (minimum 15 characters, 32+ recommended)
- Access codes stored as keyed HMAC-SHA256 hashes
- SMTP password encrypted at rest; the admin mail form never echoes it back
- Optional trusted-proxy forwarded-header handling (`ForwardedHeaders` section)
- Configurable local login fallback when OIDC is enabled (`OidcAuth:LocalLoginFallback`: `LoopbackOnly`, `Always`, or `Never`)
- Container image runs as a non-root user

See [sekura/CONFIGURATION.md](sekura/CONFIGURATION.md) for details.

## Azure provisioning script

A helper script is available at `scripts/provision-azure.ps1` to create required Azure resources for this app:

- Resource group
- Storage account + audit table
- Table Service SAS URL with permissions `rwdlacu`
- Key Vault for application secret storage
- Key Vault secret permissions for the app principal (existing principal or newly created app registration)
- Direct output of app config values (including SAS URL unless `-NoSecretOutput` is used)

Example:

```powershell
./scripts/provision-azure.ps1 `
    -SubscriptionId "<subscription-id>" `
    -ResourceGroupName "rg-sekura-prod" `
    -Location "swedencentral" `
    -NamePrefix "sharepass"
```

The script prints JSON output with created resource names and app environment variable values.

## Azure App Service deployment script

A helper script is available at `scripts/deploy-appservice.ps1` to:

- Create/update resource group
- Create/update Linux App Service plan and Web App
- Flatten `appsettings.json` into App Service application settings
- Publish and deploy the app package

By default, the script reads `sekura/appsettings.json`, flattens every JSON setting into ASP.NET Core environment variable keys, and pushes those values to App Service. The script also sets App Service-specific runtime values such as the environment, port binding, and startup command.

Example:

```powershell
./scripts/deploy-appservice.ps1 `
    -SubscriptionId "<subscription-id>" `
    -ResourceGroupName "rg-sekura-prod" `
    -Location "swedencentral" `
    -AppServicePlanName "asp-sekura-prod" `
    -WebAppName "app-sekura-prod"
```

The script prints the deployed app URL and Azure Portal URL on success.

## Release documentation

See [docs/RELEASE_NOTES.md](docs/RELEASE_NOTES.md) for release summaries and [docs/CHANGELOG.md](docs/CHANGELOG.md) for the full changelog.

## User flow

See [docs/flowDiagram.md](docs/flowDiagram.md) for the current user flow diagram.
