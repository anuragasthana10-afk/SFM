using System.Text.Json;
using ZohoBooksSync.Models;
using ZohoBooksSync.Persistence;
using ZohoBooksSync.Services;

var configurationPath = args.Length > 0 ? args[0] : "appsettings.json";
var settings = ZohoSettingsLoader.Load(configurationPath);

var httpClient = new HttpClient
{
    BaseAddress = new Uri(settings.ApiBaseUrl ?? "https://books.zoho.com/api/v3/")
};

var referenceStore = new FileReferenceStore("data/reference-store.json");
var client = new ZohoBooksClient(httpClient, settings);
var syncService = new ZohoBooksSyncService(client, referenceStore);

Console.WriteLine("Zoho Books Sync starting...");
Console.WriteLine("This sample will sync a single item and invoice, then pull payments.");

var item = new ZohoItem
{
    LocalId = "SKU-1000",
    Name = "Sample Item",
    Rate = 29.99m,
    Description = "Demo item created by the sync utility",
    Unit = "pcs"
};

await syncService.SyncItemsAsync(new[] { item });
Console.WriteLine("Item synced");

var invoice = new ZohoInvoice
{
    LocalId = "INV-1000",
    CustomerId = settings.DemoCustomerId,
    LineItems =
    [
        new ZohoInvoiceLine
        {
            ItemLocalId = item.LocalId,
            Description = "Sample item line",
            Quantity = 2,
            Rate = 29.99m
        }
    ],
    DueDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)),
    InvoiceNumber = "INV-1000",
};

await syncService.SyncInvoicesAsync(new[] { invoice });
Console.WriteLine("Invoice synced");

var payments = await syncService.PullPaymentsAsync(invoice.LocalId);
Console.WriteLine(JsonSerializer.Serialize(payments, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine("Sync operations complete");
