# Email architecture notes

## Attachment storage switch (default: off)

To avoid rapid database growth from large attachments, keep attachment persistence disabled by default and gate it behind a configuration switch.

**Recommended configuration keys**

- `Email:StoreAttachments` (boolean, default: `false`)
- `Email:StoreFullMessages` (boolean, default: `true`)
- `Email:MaxAttachmentSizeMb` (integer, optional guardrail)
- `Email:AllowedAttachmentTypes` (string list, optional allowlist)

**Behavior**

- When `Email:StoreAttachments=false`:
  - Message metadata is stored as normal.
  - Full message content is stored if `Email:StoreFullMessages=true`.
  - Attachments are not persisted in the database.
  - UI should show attachments as "available on mailbox" and fetch on-demand if the provider supports it.
- When `Email:StoreAttachments=true`:
  - Attachments are stored in the email content/attachment table alongside message content.
  - Enforce `MaxAttachmentSizeMb` and `AllowedAttachmentTypes` to reduce bloat.

**Suggested schema extension**

Add a separate attachment table to avoid inflating your message content rows:

- `EmailAttachment`
  - `AttachmentId` (PK)
  - `MessageId` (FK -> EmailMessageMetadata)
  - `FileName`
  - `ContentType`
  - `SizeBytes`
  - `Content` (varbinary or blob)

This keeps metadata queries fast while still allowing optional attachment persistence.
