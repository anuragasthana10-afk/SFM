using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using ZohoBooksSync.Configuration;
using ZohoBooksSync.Models;
using ZohoBooksSync.Persistence;

namespace ZohoBooksSync.Services;

public sealed class ZohoBooksClient
{
    private readonly HttpClient _httpClient;
    private readonly ZohoBooksOptions _options;
    private readonly ReferenceStore _referenceStore;

    public ZohoBooksClient(HttpClient httpClient, ZohoBooksOptions options, ReferenceStore referenceStore)
    {
        _httpClient = httpClient;
        _options = options;
        _referenceStore = referenceStore;
    }

    public async Task SyncInventoryItemsAsync(IEnumerable<InventoryItem> items, CancellationToken cancellationToken = default)
    {
        foreach (var item in items)
        {
            var existingId = _referenceStore.GetItemId(item.LocalId);
            if (string.IsNullOrWhiteSpace(existingId))
            {
                var payload = new
                {
                    name = item.Name,
                    sku = item.Sku,
                    rate = item.Rate,
                    quantity = item.Quantity
                };

                var response = await PostAsync("items", payload, cancellationToken);
                var remoteId = ExtractId(response, "item", "item_id");
                _referenceStore.SetItemId(item.LocalId, remoteId);
                continue;
            }

            await UpdateInventoryItemAsync(existingId, item, cancellationToken);
        }

        await _referenceStore.SaveAsync(cancellationToken);
    }

    public async Task SyncContactsAsync(IEnumerable<Contact> contacts, CancellationToken cancellationToken = default)
    {
        foreach (var contact in contacts)
        {
            var existingId = _referenceStore.GetContactId(contact.LocalId);
            if (!string.IsNullOrWhiteSpace(existingId))
            {
                continue;
            }

            var payload = new
            {
                contact_name = contact.Name,
                email = contact.Email,
                phone = contact.Phone
            };

            var response = await PostAsync("contacts", payload, cancellationToken);
            var remoteId = ExtractId(response, "contact", "contact_id");
            _referenceStore.SetContactId(contact.LocalId, remoteId);
        }

        await _referenceStore.SaveAsync(cancellationToken);
    }

    public async Task SyncInvoicesAsync(IEnumerable<Invoice> invoices, CancellationToken cancellationToken = default)
    {
        foreach (var invoice in invoices)
        {
            var existingId = _referenceStore.GetInvoiceId(invoice.LocalId);
            if (!string.IsNullOrWhiteSpace(existingId))
            {
                continue;
            }

            var contactId = _referenceStore.GetContactId(invoice.ContactLocalId)
                ?? throw new InvalidOperationException($"Missing contact reference for {invoice.ContactLocalId}.");

            var lineItems = invoice.LineItems.Select(item => new
            {
                item_id = _referenceStore.GetItemId(item.ItemLocalId)
                    ?? throw new InvalidOperationException($"Missing item reference for {item.ItemLocalId}.")
                ,
                name = item.Description,
                rate = item.Rate,
                quantity = item.Quantity
            });

            var payload = new
            {
                customer_id = contactId,
                date = invoice.InvoiceDate.ToString("yyyy-MM-dd"),
                line_items = lineItems,
                notes = invoice.Notes
            };

            var response = await PostAsync("invoices", payload, cancellationToken);
            var remoteId = ExtractId(response, "invoice", "invoice_id");
            _referenceStore.SetInvoiceId(invoice.LocalId, remoteId);
        }

        await _referenceStore.SaveAsync(cancellationToken);
    }

    public async Task UpdateInventoryItemsAsync(IEnumerable<InventoryItem> items, CancellationToken cancellationToken = default)
    {
        foreach (var item in items)
        {
            var remoteId = _referenceStore.GetItemId(item.LocalId);
            if (string.IsNullOrWhiteSpace(remoteId))
            {
                continue;
            }

            await UpdateInventoryItemAsync(remoteId, item, cancellationToken);
        }
    }

    public async Task UpdateInvoicesAsync(IEnumerable<Invoice> invoices, CancellationToken cancellationToken = default)
    {
        foreach (var invoice in invoices)
        {
            var remoteId = _referenceStore.GetInvoiceId(invoice.LocalId);
            if (string.IsNullOrWhiteSpace(remoteId))
            {
                continue;
            }

            var contactId = _referenceStore.GetContactId(invoice.ContactLocalId)
                ?? throw new InvalidOperationException($"Missing contact reference for {invoice.ContactLocalId}.");

            var lineItems = invoice.LineItems.Select(item => new
            {
                item_id = _referenceStore.GetItemId(item.ItemLocalId)
                    ?? throw new InvalidOperationException($"Missing item reference for {item.ItemLocalId}.")
                ,
                name = item.Description,
                rate = item.Rate,
                quantity = item.Quantity
            });

            var payload = new
            {
                customer_id = contactId,
                date = invoice.InvoiceDate.ToString("yyyy-MM-dd"),
                line_items = lineItems,
                notes = invoice.Notes
            };

            await PutAsync($"invoices/{remoteId}", payload, cancellationToken);
        }
    }

    public async Task<IReadOnlyList<InvoicePayment>> PullPaymentsAsync(IEnumerable<string> invoiceLocalIds, CancellationToken cancellationToken = default)
    {
        var results = new List<InvoicePayment>();

        foreach (var localId in invoiceLocalIds)
        {
            var invoiceId = _referenceStore.GetInvoiceId(localId);
            if (string.IsNullOrWhiteSpace(invoiceId))
            {
                continue;
            }

            var response = await GetAsync($"invoices/{invoiceId}/payments", cancellationToken);
            var paymentsElement = response.GetProperty("payments");
            foreach (var payment in paymentsElement.EnumerateArray())
            {
                results.Add(new InvoicePayment
                {
                    PaymentId = payment.GetProperty("payment_id").GetString() ?? string.Empty,
                    Amount = payment.GetProperty("amount").GetDecimal(),
                    Date = DateTime.Parse(payment.GetProperty("date").GetString() ?? string.Empty),
                    PaymentMode = payment.TryGetProperty("payment_mode", out var mode) ? mode.GetString() : null,
                    Description = payment.TryGetProperty("description", out var description) ? description.GetString() : null
                });
            }
        }

        return results;
    }

    private async Task UpdateInventoryItemAsync(string remoteId, InventoryItem item, CancellationToken cancellationToken)
    {
        var payload = new
        {
            name = item.Name,
            sku = item.Sku,
            rate = item.Rate,
            quantity = item.Quantity
        };

        await PutAsync($"items/{remoteId}", payload, cancellationToken);
    }

    private async Task<JsonElement> PostAsync(string path, object payload, CancellationToken cancellationToken)
    {
        var response = await SendAsync(HttpMethod.Post, path, payload, cancellationToken);
        return response;
    }

    private async Task<JsonElement> PutAsync(string path, object payload, CancellationToken cancellationToken)
    {
        var response = await SendAsync(HttpMethod.Put, path, payload, cancellationToken);
        return response;
    }

    private async Task<JsonElement> GetAsync(string path, CancellationToken cancellationToken)
    {
        using var request = CreateRequest(HttpMethod.Get, path);
        var response = await _httpClient.SendAsync(request, cancellationToken);
        return await EnsureSuccessAsync(response, cancellationToken);
    }

    private async Task<JsonElement> SendAsync(HttpMethod method, string path, object payload, CancellationToken cancellationToken)
    {
        using var request = CreateRequest(method, path);
        request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        var response = await _httpClient.SendAsync(request, cancellationToken);
        return await EnsureSuccessAsync(response, cancellationToken);
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string path)
    {
        var request = new HttpRequestMessage(method, $"{_options.BaseUrl}/{path}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Zoho-oauthtoken", _options.AccessToken);
        request.Headers.Add("X-com-zoho-books-organizationid", _options.OrganizationId);
        return request;
    }

    private static async Task<JsonElement> EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"Zoho Books API error ({(int)response.StatusCode}): {content}");
        }

        using var document = JsonDocument.Parse(content);
        return document.RootElement.Clone();
    }

    private static string ExtractId(JsonElement response, string containerProperty, string idProperty)
    {
        if (response.TryGetProperty(containerProperty, out var container)
            && container.TryGetProperty(idProperty, out var id))
        {
            return id.GetString() ?? string.Empty;
        }

        throw new InvalidOperationException($"Unable to locate {idProperty} in Zoho response.");
    }
}
