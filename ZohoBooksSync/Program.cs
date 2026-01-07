using ZohoBooksSync.Configuration;
using ZohoBooksSync.Models;
using ZohoBooksSync.Persistence;
using ZohoBooksSync.Services;

var options = ZohoBooksOptions.FromEnvironment();
var connectionOptions = ZohoBooksConnectionOptions.FromEnvironment();

if (string.IsNullOrWhiteSpace(connectionOptions.OrganizationId))
{
    Console.WriteLine("Configure ZOHO_BOOKS_ORGANIZATION_ID to run the sync.");
    return;
}

if (string.IsNullOrWhiteSpace(connectionOptions.SqlConnectionString))
{
    Console.WriteLine("Configure ZOHO_BOOKS_SQL_CONNECTION_STRING to run the sync.");
    return;
}

var referenceStore = new ReferenceStore(connectionOptions.SqlConnectionString);
await referenceStore.InitializeAsync();

var tokenStore = new TokenStore(connectionOptions.SqlConnectionString);
await tokenStore.InitializeAsync();

using var httpClient = new HttpClient();
var tokenProvider = new ZohoTokenProvider(httpClient, options, tokenStore);
var zohoClient = new ZohoBooksClient(httpClient, options, connectionOptions, referenceStore, tokenProvider);

var inventoryItems = new List<InventoryItem>
{
    new()
    {
        LocalId = 100,
        Name = "Widget",
        Sku = "WIDGET-001",
        Rate = 49.99m,
        Quantity = 25
    }
};

var contacts = new List<Contact>
{
    new()
    {
        LocalId = 100,
        Name = "Acme Corp",
        Email = "billing@acme.test",
        Phone = "+1-555-0100"
    }
};

var invoices = new List<Invoice>
{
    new()
    {
        LocalId = 100,
        ContactLocalId = 100,
        InvoiceDate = DateTime.UtcNow.Date,
        CurrencyCode = "USD",
        Jurisdiction = "US",
        RelationshipManager = "Taylor Reed",
        Notes = "Thanks for your business.",
        LineItems = new List<InvoiceLineItem>
        {
            new()
            {
                ItemLocalId = 100,
                Description = "Widget",
                Rate = 49.99m,
                Quantity = 2
            }
        }
    }
};

await zohoClient.SyncInventoryItemsAsync(inventoryItems);
await zohoClient.SyncContactsAsync(contacts);
await zohoClient.SyncInvoicesAsync(invoices);

await zohoClient.UpdateInventoryItemsAsync(inventoryItems);
await zohoClient.UpdateContactsAsync(contacts);
await zohoClient.UpdateInvoicesAsync(invoices);

var payments = await zohoClient.PullPaymentsAsync(new[] { 100 });
foreach (var payment in payments)
{
    Console.WriteLine($"Payment {payment.PaymentId} for {payment.Amount} on {payment.Date:d}");
}
