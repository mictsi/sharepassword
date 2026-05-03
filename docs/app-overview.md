# SharePassword App Overview

SharePassword is a secure credential-sharing app. It helps an internal user send a temporary password, API key, or other secret to another person without placing the secret directly in email, chat, or a ticket.

## Who Uses It

- Sender: an admin or authorized internal user who creates a secure share.
- Recipient: the person who receives and opens the share.
- Administrator: the person who configures users, authentication, storage, auditing, and operational settings.

## What A Sender Does

1. Sign in to the app.
2. Create a secure share.
3. Enter the recipient email, username or account name, secret, optional instructions, and expiration time.
4. Choose access controls, such as Microsoft Entra ID sign-in or extra-password protection.
5. Send the generated share link and access code through separate channels.

The dashboard lets senders monitor active shares, see whether a share has been opened, check expiration, and revoke shares that should no longer be usable.

## What A Recipient Does

1. Open the share link.
2. Enter their email address and the access code.
3. Sign in with Microsoft Entra ID if the share requires it.
4. Enter the extra password if the sender protected the share with browser-side encryption.
5. View and copy the username, secret, and instructions.
6. Delete the share after saving the credential.

If the recipient does not delete the share, the app removes it automatically when it expires.

## Main Security Controls

- Temporary share links.
- One-time access codes sent separately from the link.
- Expiration and automatic cleanup.
- Manual revocation from the dashboard.
- Failed-attempt pause controls for share access.
- Audit logging for share creation, access, revocation, sign-in, and system events.
- Optional Microsoft Entra ID recipient verification.
- Server-side encryption for stored secrets.
- Optional browser-side encryption with an extra password.

## Extra-Password Protection

When the sender enables extra-password protection, the secret is encrypted in the browser before it is submitted to the server. The server stores only the encrypted payload. The extra password is not stored or sent to the server.

This mode is useful for higher-sensitivity shares where database readers, backups, or server-side decrypt access should not reveal the stored secret.

Important limitation: browser-side encryption does not protect against an administrator who can change the JavaScript served to users. For that stronger threat model, the decrypting client must be delivered from a separately trusted and integrity-controlled place.

## Typical End-To-End Flow

1. Sender signs in.
2. Sender creates a share.
3. App generates a secure link and access code.
4. Sender sends the link and access code through separate channels.
5. Recipient opens the link and proves access with email, access code, and any required sign-in.
6. Recipient views the credential.
7. Recipient deletes the share, or the app removes it when it expires.

## In Short

SharePassword is for safely handing a secret to someone else for a limited time. It gives the sender controlled delivery, tracking, revocation, expiration, and optional browser-side encryption when the stored secret should not be readable by the server.
