# CRM email demo setup

This repository provides a three-project demo layout:

- `CRM`: domain models + storage abstractions
- `CRM.Email`: provider-agnostic email services + Microsoft MailKit adapter + SQL persistence
- `CRM.Web`: MVC demo UI

## Key demo capabilities

- Sync mailbox emails into CRM metadata storage.
- Account table with `Id`, `Name`, and `AccountGuid` for thread association.
- MVC list of emails per account.
- UI to move an email thread between accounts.
- UI for mapping orphan emails to an account.
- UI for composing and sending emails from CRM (with account GUID stamp).
- UI for replying and forwarding emails.

## SQL Server persistence

The demo uses SQL Server tables for all account/email data. The MVC app runs a schema initializer on startup, creating tables if they do not exist.

Configure the connection string in `CRM.Web/appsettings.json`:

```
ConnectionStrings:
  CrmDatabase: Server=localhost;Database=CrmEmailDemo;Trusted_Connection=True;TrustServerCertificate=True
```

## Provider switching

Configure the provider in `CRM.Web/appsettings.json`:

- `Email:ProviderType = "Local"` for the local stub provider used by the demo UI.
- `Email:ProviderType = "MicrosoftMailKit"` for Office 365 with MailKit + OAuth2.

## Attachment storage

Attachment persistence is off by default to avoid rapid DB growth.

```
Email:
  Storage:
    StoreFullMessages: true
    StoreAttachments: false
```

When `StoreFullMessages` is enabled, the sync service stores full message content. When `StoreAttachments` is enabled, the sync service persists attachments while honoring optional max size and content-type allowlist settings.


Local and Microsoft providers stamp account GUID in message body to support reliable account association.


## Compose UI options

- `Email:UI:ShowBccField` controls optional Bcc field visibility in compose/reply/forward forms.
- Compose screen uses a built-in rich text editor and posts both plain text and HTML body.
