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
    private readonly ZohoTokenProvider _tokenProvider;
    private readonly Dictionary<string, ReportingTag> _reportingTags = new(StringComparer.OrdinalIgnoreCase);

    public ZohoBooksClient(HttpClient httpClient, ZohoBooksOptions options, ReferenceStore referenceStore, ZohoTokenProvider tokenProvider)
    {
        _httpClient = httpClient;
        _options = options;
        _referenceStore = referenceStore;
        _tokenProvider = tokenProvider;
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

            var reportingTagDetails = await BuildReportingTagDetailsAsync(invoice, cancellationToken);
            var payload = new
            {
                customer_id = contactId,
                date = invoice.InvoiceDate.ToString("yyyy-MM-dd"),
                currency_code = invoice.CurrencyCode,
                line_items = lineItems,
                reporting_tag_details = reportingTagDetails,
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

    public async Task UpdateContactsAsync(IEnumerable<Contact> contacts, CancellationToken cancellationToken = default)
    {
        foreach (var contact in contacts)
        {
            var remoteId = _referenceStore.GetContactId(contact.LocalId);
            if (string.IsNullOrWhiteSpace(remoteId))
            {
                continue;
            }

            var payload = new
            {
                contact_name = contact.Name,
                email = contact.Email,
                phone = contact.Phone
            };

            await PutAsync($"contacts/{remoteId}", payload, cancellationToken);
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

            var reportingTagDetails = await BuildReportingTagDetailsAsync(invoice, cancellationToken);
            var payload = new
            {
                customer_id = contactId,
                date = invoice.InvoiceDate.ToString("yyyy-MM-dd"),
                currency_code = invoice.CurrencyCode,
                line_items = lineItems,
                reporting_tag_details = reportingTagDetails,
                notes = invoice.Notes
            };

            await PutAsync($"invoices/{remoteId}", payload, cancellationToken);
        }

        await _referenceStore.SaveAsync(cancellationToken);
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
        using var request = await CreateRequestAsync(HttpMethod.Get, path, cancellationToken);
        var response = await _httpClient.SendAsync(request, cancellationToken);
        return await EnsureSuccessAsync(response, cancellationToken);
    }

    private async Task<JsonElement> SendAsync(HttpMethod method, string path, object payload, CancellationToken cancellationToken)
    {
        using var request = await CreateRequestAsync(method, path, cancellationToken);
        request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        var response = await _httpClient.SendAsync(request, cancellationToken);
        return await EnsureSuccessAsync(response, cancellationToken);
    }

    private async Task<HttpRequestMessage> CreateRequestAsync(HttpMethod method, string path, CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(method, $"{_options.BaseUrl}/{path}");
        var accessToken = await _tokenProvider.GetAccessTokenAsync(cancellationToken);
        request.Headers.Authorization = new AuthenticationHeaderValue("Zoho-oauthtoken", accessToken);
        request.Headers.Add("X-com-zoho-books-organizationid", _options.OrganizationId);
        return request;
    }

    private async Task<IReadOnlyList<object>> BuildReportingTagDetailsAsync(Invoice invoice, CancellationToken cancellationToken)
    {
        var details = new List<object>();

        if (!string.IsNullOrWhiteSpace(invoice.Jurisdiction))
        {
            var option = await EnsureReportingTagOptionAsync("Jurisdiction", invoice.Jurisdiction, cancellationToken);
            details.Add(new
            {
                reporting_tag_id = option.TagId,
                reporting_tag_option_id = option.OptionId
            });
        }

        if (!string.IsNullOrWhiteSpace(invoice.RelationshipManager))
        {
            var option = await EnsureReportingTagOptionAsync("RelationshipManager", invoice.RelationshipManager, cancellationToken);
            details.Add(new
            {
                reporting_tag_id = option.TagId,
                reporting_tag_option_id = option.OptionId
            });
        }

        return details;
    }

    private async Task<ReportingTagOption> EnsureReportingTagOptionAsync(string tagName, string optionName, CancellationToken cancellationToken)
    {
        var optionKey = BuildReportingTagOptionKey(tagName, optionName);
        var existingOptionId = _referenceStore.GetReportingTagOptionId(optionKey);
        if (!string.IsNullOrWhiteSpace(existingOptionId))
        {
            var tag = await GetReportingTagAsync(tagName, cancellationToken);
            return new ReportingTagOption(tag.Id, existingOptionId);
        }

        var reportingTag = await GetReportingTagAsync(tagName, cancellationToken);
        if (reportingTag.OptionsByName.TryGetValue(optionName, out var remoteOptionId))
        {
            _referenceStore.SetReportingTagOptionId(optionKey, remoteOptionId);
            return new ReportingTagOption(reportingTag.Id, remoteOptionId);
        }

        var payload = new
        {
            option_name = optionName
        };

        var response = await PostAsync($"settings/reportingtags/{reportingTag.Id}/options", payload, cancellationToken);
        var createdOptionId = ExtractId(response, "reporting_tag_option", "option_id");
        _referenceStore.SetReportingTagOptionId(optionKey, createdOptionId);
        reportingTag.OptionsByName[optionName] = createdOptionId;
        return new ReportingTagOption(reportingTag.Id, createdOptionId);
    }

    private async Task<ReportingTag> GetReportingTagAsync(string tagName, CancellationToken cancellationToken)
    {
        if (_reportingTags.TryGetValue(tagName, out var cachedTag))
        {
            return cachedTag;
        }

        var response = await GetAsync("settings/reportingtags", cancellationToken);
        if (!response.TryGetProperty("reporting_tags", out var tagsElement))
        {
            throw new InvalidOperationException("Unable to load reporting tags from Zoho Books.");
        }

        foreach (var tagElement in tagsElement.EnumerateArray())
        {
            if (!tagElement.TryGetProperty("tag_name", out var tagNameElement))
            {
                continue;
            }

            var name = tagNameElement.GetString();
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            var tagId = tagElement.GetProperty("tag_id").GetString() ?? string.Empty;
            var optionsByName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            if (tagElement.TryGetProperty("tag_options", out var optionsElement))
            {
                foreach (var optionElement in optionsElement.EnumerateArray())
                {
                    var optionName = optionElement.GetProperty("option_name").GetString();
                    var optionId = optionElement.GetProperty("option_id").GetString();
                    if (!string.IsNullOrWhiteSpace(optionName) && !string.IsNullOrWhiteSpace(optionId))
                    {
                        optionsByName[optionName] = optionId;
                    }
                }
            }

            var reportingTag = new ReportingTag(tagId, optionsByName);
            _reportingTags[name] = reportingTag;
        }

        if (_reportingTags.TryGetValue(tagName, out var foundTag))
        {
            return foundTag;
        }

        throw new InvalidOperationException($"Reporting tag '{tagName}' not found in Zoho Books.");
    }

    private static string BuildReportingTagOptionKey(string tagName, string optionName)
        => $"{tagName.Trim()}::{optionName.Trim()}".ToLowerInvariant();

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

    private sealed record ReportingTag(string Id, Dictionary<string, string> OptionsByName);

    private sealed record ReportingTagOption(string TagId, string OptionId);
}
