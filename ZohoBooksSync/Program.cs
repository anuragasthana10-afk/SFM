using ZohoBooksSync.Configuration;
using ZohoBooksSync.Models;
using ZohoBooksSync.Persistence;
using ZohoBooksSync.Services;

var options = ZohoBooksOptions.FromEnvironment();

if (string.IsNullOrWhiteSpace(options.OrganizationId) || string.IsNullOrWhiteSpace(options.AccessToken))
{
    Console.WriteLine("Configure ZOHO_BOOKS_ORGANIZATION_ID and ZOHO_BOOKS_ACCESS_TOKEN to run the sync.");
    return;
}

var referenceStore = new ReferenceStore(options.DataStorePath);
await referenceStore.LoadAsync();

using var httpClient = new HttpClient();
var zohoClient = new ZohoBooksClient(httpClient, options, referenceStore);

var inventoryItems = new List<InventoryItem>
{
    new()
    {
        LocalId = "item-100",
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
        LocalId = "contact-100",
        Name = "Acme Corp",
        Email = "billing@acme.test",
        Phone = "+1-555-0100"
    }
};

var invoices = new List<Invoice>
{
    new()
    {
        LocalId = "invoice-100",
        ContactLocalId = "contact-100",
        InvoiceDate = DateTime.UtcNow.Date,
        Notes = "Thanks for your business.",
        LineItems = new List<InvoiceLineItem>
        {
            new()
            {
                ItemLocalId = "item-100",
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

var payments = await zohoClient.PullPaymentsAsync(new[] { "invoice-100" });
foreach (var payment in payments)
{
    Console.WriteLine($"Payment {payment.PaymentId} for {payment.Amount} on {payment.Date:d}");
}
