# SharePassword App Overview

SharePassword is a secure exchange app. It helps internal users move sensitive information across an organizational boundary in two directions, without placing that information directly in email, chat, or a ticket:

- **Password shares** — send a temporary password, API key, or other secret *to* another person.
- **Information requests** — collect information *back from* an external partner.

Both workflows use expiring links, a separate access code, auditing, and optional browser-side encryption. After sign-in, a two-card dashboard lets a user pick which console to work in.

## Who Uses It

- Sender / requester: an admin or authorized internal user who creates a secure share or an information request.
- Recipient: the person who opens a share and reads the secret.
- Partner: the external person who submits or updates information for a request.
- Administrator: the person who configures users, authentication, storage, auditing, and operational settings.

## Password Shares

### What A Sender Does

1. Sign in and open **Admin console - Password shares**.
2. Create a secure share.
3. Enter the recipient email, username or account name, secret, optional instructions, and expiration time.
4. Choose access controls, such as Microsoft Entra ID sign-in or extra-password protection.
5. Send the generated share link and the separate access code through different channels.

The dashboard lets senders monitor active shares, see whether a share has been opened, check expiration, and revoke shares that should no longer be usable. Standard users see only shares they created; admins see all.

### What A Recipient Does

1. Open the share link (`/s/{token}`).
2. Enter their email address and the access code.
3. Sign in with Microsoft Entra ID if the share requires it.
4. Enter the extra password if the sender protected the share with browser-side encryption.
5. View and copy the username, secret, and instructions.
6. Delete the share after saving the credential.

If the recipient does not delete the share, the app removes it automatically when it expires.

## Information Requests

### What A Requester Does

1. Sign in and open **Admin console - Information requests**.
2. Create a request with the partner email, instructions describing what is needed, and an expiration time.
3. Optionally require Microsoft Entra ID sign-in for the partner.
4. Send the generated request link and the separate 15-character access code through different channels.

The dashboard shows active, expiring-soon, submitted, and revoked counts. The requester can open a request's details to read the submitted response, extend its expiration, or revoke it.

### What A Partner Does

1. Open the request link (`/r/{token}`).
2. Enter their email address and the access code (or sign in with Microsoft Entra ID when required).
3. Enter their information and submit it.
4. Reopen the link to update the submitted information until the request expires.

If the response is protected with browser-side encryption, the partner enters the extra password to decrypt the existing response in the browser; the same password stays in page memory so the update can be re-saved without typing it again. Expired requests are removed automatically.

## Signing In

- **Local admin** — the configured administrator account, usable as break-glass access.
- **Local users** — accounts an administrator creates and manages.
- **Microsoft Entra ID (OIDC)** — optional single sign-on, mapped to `Admin`, `User`, and `Auditor` roles by group membership.

Local accounts can require a **second factor** after the password step:

- **Authenticator app (TOTP)** — set up with a QR code, manual key, and provisioning URI that can be printed for safekeeping.
- **Passkeys (WebAuthn/FIDO2)** — registered from the user's profile and usable in place of an authenticator code.

A user with both methods chooses one at sign-in. Administrators can reset a user's authenticator or remove their passkeys if a device is lost. Repeated failed password attempts pause sign-in for that account.

## Main Security Controls

- Temporary share and request links.
- One-time access codes sent separately from the link.
- Expiration and automatic cleanup for both shares and requests.
- Manual revocation from each console.
- Failed-attempt pause controls for share and request access.
- Second-factor sign-in (authenticator app or passkey) for local accounts.
- Per-account sign-in throttling.
- Audit logging for creation, access, revocation, expiration extension, sign-in, and system events.
- Optional Microsoft Entra ID recipient/partner verification.
- Server-side encryption for stored secrets and responses.
- Optional browser-side encryption with an extra password.

## Extra-Password Protection

When the sender (share) or partner (request response) enables extra-password protection, the secret or response is encrypted in the browser before it is submitted to the server. The server stores only the encrypted payload. The extra password is not stored or sent to the server.

This mode is useful for higher-sensitivity items where database readers, backups, or server-side decrypt access should not reveal the stored value.

Important limitation: browser-side encryption does not protect against an administrator who can change the JavaScript served to users. For that stronger threat model, the decrypting client must be delivered from a separately trusted and integrity-controlled place.

## In Short

SharePassword is for safely handing a secret to someone, or safely collecting information back from someone, for a limited time. It gives internal users controlled delivery, tracking, revocation, expiration, and optional browser-side encryption when the stored value should not be readable by the server. See [FEATURES.md](FEATURES.md) for the complete feature inventory and [../DESIGN.md](../DESIGN.md) for the architecture.
</content>
