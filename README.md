# Zoho Books Sync

This repository contains a lightweight .NET 8 console application that demonstrates how to integrate with the Zoho Books API to:

- Sync inventory items from a local payload to Zoho Books.
- Sync invoices, referencing stored item identifiers when available.
- Persist Zoho reference identifiers so subsequent updates use the existing records.
- Update items and invoices using stored references instead of duplicating records.
- Pull customer payments for a synced invoice.

## Project structure

```
src/ZohoBooksSync
├── ZohoBooksSync.csproj
├── Program.cs
├── appsettings.json          # sample configuration
├── Models/                   # request/response DTOs
├── Persistence/              # reference id storage
└── Services/                 # Zoho Books API client + sync orchestrator
```

## Configuration

Populate `src/ZohoBooksSync/appsettings.json` with your Zoho Books OAuth credentials and organization details. The sample `Program.cs` loads this file by default, or you can pass an alternate path as the first command-line argument.

Key settings:

- `organizationId`: Your Zoho Books organization ID.
- `accessToken`: OAuth access token with permissions for items, invoices, and payments.
- `refreshToken`, `clientId`, `clientSecret`: Optional fields you can use to refresh tokens externally.
- `apiBaseUrl`: Base API URL. Defaults to `https://books.zoho.com/api/v3/`.
- `demoCustomerId`: Customer ID used for the demo invoice in `Program.cs`.

## Usage

1. Restore and build the project with the .NET 8 SDK.
2. Run the app from `src/ZohoBooksSync`:
   ```bash
   dotnet run --project src/ZohoBooksSync/ZohoBooksSync.csproj
   ```
3. The sample workflow will:
   - Sync a demo item (`SKU-1000`).
   - Sync a demo invoice that references the item.
   - Pull and print payment details for the invoice.

Reference IDs are stored locally at `data/reference-store.json`. You can reuse the `ZohoBooksSyncService` class in other applications to sync your own items and invoices.
