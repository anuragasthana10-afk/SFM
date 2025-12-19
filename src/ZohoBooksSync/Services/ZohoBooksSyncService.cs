using ZohoBooksSync.Models;
using ZohoBooksSync.Persistence;

namespace ZohoBooksSync.Services;

public sealed class ZohoBooksSyncService
{
    private readonly ZohoBooksClient _client;
    private readonly IReferenceStore _referenceStore;

    public ZohoBooksSyncService(ZohoBooksClient client, IReferenceStore referenceStore)
    {
        _client = client;
        _referenceStore = referenceStore;
    }

    public async Task SyncItemsAsync(IEnumerable<ZohoItem> items, CancellationToken cancellationToken = default)
    {
        foreach (var item in items)
        {
            var existingId = await _referenceStore.GetItemReferenceAsync(item.LocalId, cancellationToken);
            if (existingId is null)
            {
                var zohoItemId = await _client.CreateItemAsync(item, cancellationToken);
                await _referenceStore.SaveItemReferenceAsync(item.LocalId, zohoItemId, cancellationToken);
            }
            else
            {
                await _client.UpdateItemAsync(existingId, item, cancellationToken);
            }
        }
    }

    public async Task SyncInvoicesAsync(IEnumerable<ZohoInvoice> invoices, CancellationToken cancellationToken = default)
    {
        foreach (var invoice in invoices)
        {
            await HydrateInvoiceLineItemIds(invoice, cancellationToken);
            var existingId = await _referenceStore.GetInvoiceReferenceAsync(invoice.LocalId, cancellationToken);
            if (existingId is null)
            {
                var zohoInvoiceId = await _client.CreateInvoiceAsync(invoice, cancellationToken);
                await _referenceStore.SaveInvoiceReferenceAsync(invoice.LocalId, zohoInvoiceId, cancellationToken);
            }
            else
            {
                await _client.UpdateInvoiceAsync(existingId, invoice, cancellationToken);
            }
        }
    }

    public async Task UpdateItemAsync(string localId, ZohoItem item, CancellationToken cancellationToken = default)
    {
        var existingId = await _referenceStore.GetItemReferenceAsync(localId, cancellationToken)
            ?? throw new InvalidOperationException($"No Zoho reference stored for item {localId}");

        await _client.UpdateItemAsync(existingId, item, cancellationToken);
    }

    public async Task UpdateInvoiceAsync(string localId, ZohoInvoice invoice, CancellationToken cancellationToken = default)
    {
        await HydrateInvoiceLineItemIds(invoice, cancellationToken);
        var existingId = await _referenceStore.GetInvoiceReferenceAsync(localId, cancellationToken)
            ?? throw new InvalidOperationException($"No Zoho reference stored for invoice {localId}");

        await _client.UpdateInvoiceAsync(existingId, invoice, cancellationToken);
    }

    public async Task<IReadOnlyList<ZohoPayment>> PullPaymentsAsync(string localInvoiceId, CancellationToken cancellationToken = default)
    {
        var existingId = await _referenceStore.GetInvoiceReferenceAsync(localInvoiceId, cancellationToken)
            ?? throw new InvalidOperationException($"No Zoho reference stored for invoice {localInvoiceId}");

        return await _client.GetPaymentsForInvoiceAsync(existingId, cancellationToken);
    }

    private async Task HydrateInvoiceLineItemIds(ZohoInvoice invoice, CancellationToken cancellationToken)
    {
        foreach (var line in invoice.LineItems)
        {
            if (!string.IsNullOrEmpty(line.ItemId) || string.IsNullOrEmpty(line.ItemLocalId))
            {
                continue;
            }

            var itemReference = await _referenceStore.GetItemReferenceAsync(line.ItemLocalId, cancellationToken);
            if (itemReference is null)
            {
                throw new InvalidOperationException($"Missing Zoho item reference for local id {line.ItemLocalId}");
            }

            line.ItemId = itemReference;
        }
    }
}
