# SharePassword Design

This document describes the current design of the SharePassword application. It focuses on the runtime architecture, data boundaries, core request flows, and the security decisions that shape the implementation.

## Purpose

SharePassword is an ASP.NET Core MVC application for sharing temporary secrets without putting the secret directly in email, chat, or tickets. A signed-in sender creates a share for a recipient. The recipient opens a unique link and proves access with their email address plus a separate one-time access code. Shares expire automatically and can also be revoked or deleted after retrieval.

For higher-sensitivity shares, the sender can enable browser-side encryption with an extra password. In that mode, the browser encrypts the secret before form submission, the server stores only the encrypted client payload, and the recipient must know the extra password to decrypt the secret in their browser.

## Technology Stack

- ASP.NET Core MVC on .NET 10.
- Razor views with Bootstrap, jQuery validation, and small project-specific JavaScript.
- Cookie authentication for application sessions.
- Optional OpenID Connect login, intended for Microsoft Entra ID or compatible providers.
- EF Core for SQLite, SQL Server, and PostgreSQL storage.
- Azure Key Vault and Azure Table Storage for the Azure storage mode.
- Background hosted service for expired-share cleanup.

## Repository Layout

- `sharepassword/` contains the web application.
- `sharepassword/Controllers/` contains MVC request handlers.
- `sharepassword/Views/` contains Razor UI.
- `sharepassword/Services/` contains storage, crypto, audit, notification, authentication support, metrics, and background services.
- `sharepassword/Data/` contains EF Core context setup, migrations, and storage registration.
- `sharepassword/Models/` contains persisted domain models.
- `sharepassword/ViewModels/` contains form and page models.
- `sharepassword/wwwroot/` contains static assets and client-side encryption logic.
- `sharepassword.Tests/` contains unit and integration tests.
- `scripts/` contains local helper, Azure provisioning, deployment, and Docker asset generation scripts.
- `docs/` contains user-facing overview, flow, changelog, and release notes.

## Runtime Architecture

`Program.cs` is the composition root. It validates critical options, registers storage based on configuration, configures authentication and authorization policies, runs storage startup checks or migrations, initializes platform data, and wires the MVC routes.

The main service boundaries are:

- `IShareStore`: creates, loads, updates, and deletes password shares.
- `IAuditLogSink` and `IAuditLogReader`: persist and read audit events.
- `IPasswordCryptoService`: encrypts and decrypts server-managed secrets at rest.
- `IAccessCodeService`: generates and verifies access codes.
- `ILocalUserService`: manages local database-backed users and their password/TOTP state.
- `ISystemConfigurationService`: reads and updates runtime settings stored in the database-backed modes.
- `IUsageMetricsService`: records dashboard metrics and event counters.
- `INotificationEmailService`: sends share-access notifications when configured.
- `IApplicationTime`: centralizes UTC time and display timezone behavior.

Controllers depend on these interfaces instead of concrete storage implementations. Storage-specific behavior is selected once during application startup by `AddConfiguredStorageBackend`.

## Storage Design

The app supports four configured storage backends through `Storage:Backend`:

- `sqlite`
- `sqlserver`
- `postgresql`
- `azure`

The database-backed modes use EF Core and share the same logical schema:

- `PasswordShares`
- `AuditLogs`
- `LocalUsers`
- `SystemConfigurations`
- `UsageMetricCounters`
- `UsageMetricEvents`

Each database provider has its own migration set under `sharepassword/Data/Migrations/`. Startup either applies migrations or performs a connectivity check depending on the provider-specific `ApplyMigrationsOnStartup` setting.

The Azure mode stores shares as JSON secrets in Azure Key Vault and audit logs in Azure Table Storage. Database-backed platform features such as local user management, editable mail configuration, runtime timezone settings, and persisted KPI counters are not available in Azure mode unless separate implementations are added.

Database-backed operations flow through `IDatabaseOperationRunner`, which centralizes retry behavior and maps provider failures into `DatabaseOperationException` for user-facing error handling and diagnostics.

## Data Model

`PasswordShare` is the central business entity. It contains:

- recipient email
- shared username or account name
- encrypted secret payload
- server-managed or client-encrypted secret mode
- optional instructions
- hashed access code
- random access token for the share URL
- creation and expiration timestamps
- creator identifier
- optional OIDC requirement
- failed-attempt pause state

Access tokens are random hex strings and are unique in database-backed storage. Access codes are generated separately and stored only as hashes.

Audit logs capture operation, actor, success flag, optional target, IP address, user agent, correlation ID, and bounded details. Audit logging is best-effort: persistence failures are logged through ASP.NET logging but do not block the main request.

All persisted `DateTime` values in EF Core are normalized to UTC through model converters.

## Main User Flows

### Create Share

1. A local or OIDC-authenticated user opens the admin dashboard.
2. The sender creates a share with recipient email, shared username, secret, instructions, expiry, and optional OIDC/client-encryption controls.
3. The app generates a share token and access code.
4. For server-managed shares, `PasswordCryptoService` encrypts the secret before persistence.
5. For client-encrypted shares, the browser writes an encrypted payload and clears the plaintext before form submission.
6. `IShareStore` persists the share.
7. Audit and metric events are recorded.
8. The created page shows the share link and access code so they can be delivered over separate channels.

### Access Share

1. The recipient opens `/s/{token}`.
2. The app validates the token format and loads the share.
3. If the share requires OIDC, the recipient is challenged through OIDC and their authenticated email must match the share recipient.
4. Otherwise, the recipient submits their email and access code.
5. The app checks expiry, per-share pause state, recipient email, access code format, and access code hash.
6. Failed attempts increment the share's failure counter and can pause further attempts for that specific share.
7. Successful access clears failed-attempt state, records last access, writes audit and metrics, and attempts notification email.
8. Server-managed shares are decrypted on the server before rendering. Client-encrypted shares return the encrypted payload for browser-side decryption.
9. The recipient can delete the share after retrieval.

### Cleanup

`ExpiredShareCleanupService` runs on an interval from `Share:CleanupIntervalSeconds`, with a minimum of 15 seconds. It deletes expired shares through `IShareStore`, records audit events when deletions happen, and updates metrics for expired and expired-unused shares.

## Authentication and Authorization

The application uses cookie authentication for sessions. Cookies are HTTP-only, `SameSite=Lax`, and configured with a secure policy of `Always`. Session lifetime and sliding expiration are configured through the `Application` section.

The supported identity paths are:

- Configured local admin from `AdminAuth:Username` and `AdminAuth:PasswordHash`.
- Database-backed local users managed by administrators.
- Optional OIDC users when `OidcAuth:Enabled=true`.

OIDC claims are normalized into application role claims. Configured admin and user groups map to role names from `OidcAuth:AdminRoleName` and `OidcAuth:UserRoleName`, defaulting to `Admin` and `User`.

Authorization policies are:

- `AdminOnly`: requires the admin role.
- `UserOrAdmin`: allows users and admins to create/manage their permitted shares.
- `AuditAccess`: allows admins and users with the built-in auditor role to access audit data.

When OIDC is enabled, non-local visits to the local login page are redirected to external login. Local admin login remains available only from local requests.

## Secret Protection

Server-managed share secrets are encrypted at rest using AES-GCM. `PasswordCryptoService` derives a 256-bit key from `Encryption:Passphrase` and a per-secret random salt with PBKDF2-SHA256. Each encryption uses a random nonce and stores salt, nonce, tag, and ciphertext as one base64 payload.

Client-encrypted shares use Web Crypto in `wwwroot/js/share-client-encryption.js`. The browser derives an AES-GCM key from the extra password with PBKDF2-SHA256, encrypts the secret locally, and submits a JSON payload containing version, algorithm, KDF metadata, salt, nonce, and ciphertext. The extra password is never sent to the server.

Client-side encryption protects stored data from database readers, backups, and operators who cannot alter delivered client code. It does not protect against a server or administrator that can change the JavaScript served to the browser.

Admin and local-user passwords are hashed independently from share encryption. Current admin password hashes prefer Argon2id and fall back to scrypt if Argon2id is unavailable. Legacy PBKDF2-SHA256 hashes remain accepted for compatibility.

## Operational Concerns

Startup fails fast for validated options such as `AdminAuth:PasswordHash`, application path base, timezone, and database resilience settings. `Encryption:Passphrase` is required by `PasswordCryptoService` and must be configured before server-managed secret encryption is used. For database-backed modes, startup also verifies connectivity or applies migrations before serving requests.

The `/health` endpoint performs a live database connectivity check for database-backed modes. The Azure mode does not use EF Core migrations.

The middleware pipeline adds these security headers to every response:

- `Content-Security-Policy`
- `X-Frame-Options`
- `X-Content-Type-Options`
- `Referrer-Policy`

HTTPS redirection is controlled by `Application:EnableHttpsRedirection`, allowing deployments behind a TLS-terminating reverse proxy to choose the appropriate setting.

For production hardening and deployment details, see `sharepassword/CONFIGURATION.md`.

## Extension Points

Common changes should fit these existing boundaries:

- Add a storage provider by implementing `IShareStore`, audit sink/reader support, and registration in `DatabaseRegistrationExtensions`.
- Add new persisted database-backed behavior through `SharePasswordDbContext`, provider-specific migrations, and focused service interfaces.
- Add new audit-producing behavior by calling `IAuditLogger` with a stable operation name.
- Add new user-facing workflows through MVC controllers, view models, and Razor views while keeping secrets out of logs and model errors.
- Add new runtime settings through options classes for static configuration, or through `SystemConfiguration` when the setting must be editable at runtime.

## Non-Goals

The app is not a general password manager. It is designed for temporary, recipient-bound secret transfer with expiry, auditability, and revocation.

The browser-side encryption mode is not an end-to-end security boundary against a malicious or compromised web server. Its design goal is reducing exposure of stored secrets in the database, backups, and normal server-side storage paths.
