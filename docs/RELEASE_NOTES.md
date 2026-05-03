# Release Notes

## Unreleased

Database storage modernization work in progress for `sharepassword`.

### Highlights

- Added EF Core-backed persistence for password shares and audit logs.
- Added selectable storage backends for SQLite, SQL Server, PostgreSQL, and Azure.
- Added per-backend configuration sections with an Azure-specific Key Vault + Table Storage layout.
- Added automatic EF Core migration application during app startup.
- Added optional browser-side encryption for high-sensitivity shares using an extra password.

## v0.5.1

Logo, login, TOTP setup, and deployment refinement release of `sharepassword`.

### Highlights

- Improved app brand logo sizing and visibility.
- Refined the login page UI and password visibility toggle behavior.
- Improved TOTP setup by encrypting authenticator secrets and updating QR code handling.
- Added remote-IP support for local login policy checks.
- Refactored database context creation and deployment script behavior.

### Notes

- Release date: 2026-05-03
- Tag: `0.5.1`

## v0.5.0

Authenticator app and account security release of `sharepassword`.

### Highlights

- Added time-based one-time password (TOTP) support for local accounts.
- Added authenticator onboarding, verification, reset, and profile management flows.
- Added database migrations for TOTP and account security metadata.
- Added settings for authenticator requirements and per-share failed-attempt pause behavior.
- Improved OIDC login requirements and user management views.
- Expanded integration test coverage for the new security flows.

### Notes

- Release date: 2026-05-02
- Tag: `0.5.0`

## v0.4.0

Platform, database resilience, and project rename release of `sharepassword`.

### Highlights

- Renamed the main project from `sharepasswordAzure` to `sharepassword`.
- Renamed the solution from `sharepasswordAzure.sln` to `sharepassword.sln`.
- Added platform features for local users, system configuration, usage metrics, and mail configuration.
- Added database resilience abstractions, connectivity health checks, and provider migrations for platform feature tables.
- Added SMTP notification email service support.
- Added local user management and profile views.
- Refactored admin UI, shared layout, and styling for the expanded platform experience.
- Updated package references and release artifact workflow validation.

### Notes

- Release date: 2026-04-30
- Tag: `0.4.0`

## v0.3.2

Audit filtering and export release of `sharepassword`.

### Highlights

- Added audit log filtering by date range.
- Added JSON export for filtered audit log results.
- Improved audit log page layout and filter controls.
- Added test coverage for audit filtering and export behavior.

### Notes

- Release date: 2026-04-19
- Tag: `0.3.2`

## v0.3.1

Timezone and access-code format release of `sharepassword`.

### Highlights

- Added configurable application timezone support for displayed/admin-facing times.
- Updated generated access codes to `10` characters.
- Added access-code formatting service.
- Updated create, access, audit, dashboard, and credential views to use configured application time.
- Updated configuration documentation and deployment defaults for timezone settings.

### Notes

- Release date: 2026-04-19
- Tag: `0.3.1`

## v0.3.0

Database storage, path base, and admin password hardening release of `sharepassword`.

### Highlights

- Added configurable application path base support for deployments under a subpath.
- Added configurable authentication session timeout and sliding-expiration settings.
- Added EF Core-backed storage support with SQLite, SQL Server, and PostgreSQL providers.
- Added provider-specific migrations and database-backed share/audit stores.
- Added database storage configuration sections and startup registration.
- Added admin password hash generation script.
- Enforced hashed admin passwords using PBKDF2-SHA256 and removed plaintext admin password support.
- Improved CI release artifact publishing.

### Notes

- Release date: 2026-04-19
- Tag: `0.3.0`

## v0.2.6

Instructions field and input-hardening release of `sharepassword`.

### Highlights

- Added `Instructions` field to password share creation with multiline support and a `1000` character limit.
- Added `Instructions` retrieval/display alongside secret text while preserving formatting.
- Added explicit operator guidance on the "Password Share Created" page:
	- Send recipient, link, and expiration time via email.
	- Send access code via SMS to recipient mobile phone.
- Hardened user-input handling and validation for link token, access code, and key form fields.

### Notes

- Release date: 2026-02-25
- Tag: `0.2.6`

## v0.2.5

Security headers and session-cookie validation release of `sharepassword`.

### Highlights

- Added security headers middleware in `Program.cs`:
	- `Content-Security-Policy` (with `frame-ancestors 'none'`)
	- `X-Frame-Options: DENY`
	- `X-Content-Type-Options: nosniff`
	- `Referrer-Policy: strict-origin-when-cross-origin`
- Added integration test to verify auth cookie is session-based (non-persistent, no `Expires`/`Max-Age`).

### Notes

- Release date: 2026-02-24
- Tag: `0.2.5`

## v0.2.4

Security and deployment hardening release of `sharepassword`.

### Highlights

- Added built-in health endpoint at `/health` and integration test validation.
- Hardened auth cookie/session behavior for security (`Secure` cookies and non-persistent sign-in).
- Switched local development defaults to HTTPS and aligned startup/config documentation.
- Added and improved Azure App Service deployment automation script with appsettings-driven configuration and robust app-settings application.

### Notes

- Release date: 2026-02-24
- Tag: `0.2.4`

## v0.2.3

Secret text handling and usability release of `sharepassword`.

### Highlights

- Added multiline secret text support up to `1000` characters in the share creation flow.
- Preserved exact secret formatting for plain text, special characters, YAML, and JSON across create/access workflows.
- Added live character counter with remaining characters display and over-limit warning in the admin create form.
- Updated shared credential display to readonly multiline rendering for better fidelity of retrieved secret text.
- Added unit and integration tests for multiline/special-content round-trip behavior.

### Notes

- Release date: 2026-02-24
- Tag: `0.2.3`

## v0.2.2

UX and branding refresh release of `sharepassword`.

### Highlights

- Modernized the app interface for a cleaner, more professional and accessible experience.
- Improved Admin and Share screen readability with refined table, form, badge, and pagination styling.
- Moved logo asset to `wwwroot/images/logo.png` and updated navbar branding.
- Updated Access Mode label to `Entra ID Required + Email + Code` for clearer user expectations.
- Synchronized project version metadata to `0.2.2`.

### Notes

- Release date: 2026-02-23
- Tag: `0.2.2`

## v0.2.1

Security maintenance release of `sharepassword`.

### Highlights

- Hardened audit logging against log forging by sanitizing user-derived values before logging.
- Applied centralized newline sanitization for audit actor, target, correlation, and details fields.
- Preserved audit functionality and log levels while improving safety of console output.

### Notes

- Release date: 2026-02-23
- Tag: `0.2.1`

## v0.2.0

Feature and security release of `sharepassword`.

### Highlights

- Added per-share option to require Entra ID (OIDC) login before access.
- Enforced recipient-only access for OIDC-protected share links.
- Added role-aware audit improvements for OIDC login attempt/success/failure flows.
- Added audit logs dashboard improvements with default 100 rows, paging, and search.
- Added config-driven console audit logging with levels `DEBUG`, `INFO`, and `ERROR`.
- Improved user ownership scoping so non-admin users only see/revoke shares they created, while admins see all shares.
- Added robust Azure Key Vault permission error handling for share creation.
- Updated Azure provisioning script to assign `Key Vault Secrets Officer` role to app principal.

### Notes

- Release date: 2026-02-23
- Tag: `0.2.0`

## v0.1.4-alpha.1

Alpha feature release of `sharepassword`.

### Highlights

- Added role-based access controls with separate `Admin` and `User` roles.
- Added configurable OIDC group claim mapping to roles (`AdminGroups`/`UserGroups`).
- Restricted audit log visibility to admin role.
- Updated dashboard behavior so users only see and revoke shares they created.
- Updated configuration files and documentation for new OIDC role/group settings.

### Notes

- Release date: 2026-02-20
- Tag: `v0.1.4-alpha.1`
- Prerelease: `true`

## v0.1.3

Security maintenance release of `sharepassword`.

### Highlights

- Patched vendored jQuery validation plugin to address code scanning alert #5 (`Unsafe jQuery plugin`).
- Updated `sharepassword/wwwroot/lib/jquery-validation/dist/jquery.validate.js` with the remediation change.

### Notes

- Release date: 2026-02-20
- Tag: `v0.1.3`

## v0.1.2

Feature release of `sharepassword`.

### Highlights

- Added end-user "delete after retrieve" flow for shared passwords.
- Added explicit confirmation prompt before password deletion.
- Added deleted-state confirmation page after successful removal.
- Added integration test for end-user deletion flow.
- Consolidated and aligned release documentation and flow diagrams.

### Notes

- Release date: 2026-02-20
- Tag: `v0.1.2`

## v0.1.1

Maintenance release of `sharepassword`.

### Highlights

- Updated jQuery frontend library from `v3.7.1` to `v4.0.0`.
- Refreshed vendored jQuery distribution assets in `wwwroot/lib/jquery/dist`.
- Verified solution build and test suite after dependency update.

### Notes

- Release date: 2026-02-20
- Tag: `v0.1.1`

## v0.1.0

Initial public release of `sharepassword`.

### Highlights

- ASP.NET Core web app for secure password sharing with expiring links and access codes.
- Azure Key Vault integration for encrypted credential storage.
- Azure Table Storage-based audit logging.
- Automated background cleanup for expired shares.
- Unit and integration test coverage for core workflows.

### Notes

- Release date: 2026-02-19
- Tag: `v0.1.0`
