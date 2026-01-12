using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using CRM.Classes.ExternalServices.Zoho.Books.Configuration;
using CRM.Classes.ExternalServices.Zoho.Books.Models;
using CRM.Classes.ExternalServices.Zoho.Books.Persistence;
using CRM.Classes.ExternalServices.Zoho.Services;

namespace CRM.Classes.ExternalServices.Zoho.Books.Services
{
public sealed class ZohoBooksClient
{
    private readonly HttpClient _httpClient;
    private readonly IZohoApiConfigurationOptions _options;
    private readonly ZohoBooksConnectionOptions _connectionOptions;
    private readonly ReferenceStore _referenceStore;
    private readonly ZohoTokenProvider _tokenProvider;
    private readonly Dictionary<string, ReportingTag> _reportingTags = new Dictionary<string, ReportingTag>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _reportingTagOptionCache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _currencyCache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _accountCache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    private const string ReportingTagBasePath = "settings/tags";

    public ZohoBooksClient(
        HttpClient httpClient,
        IZohoApiConfigurationOptions options,
        ZohoBooksConnectionOptions connectionOptions,
        ReferenceStore referenceStore,
        ZohoTokenProvider tokenProvider)
    {
        _httpClient = httpClient;
        _options = options;
        _connectionOptions = connectionOptions;
        _referenceStore = referenceStore;
        _tokenProvider = tokenProvider;
    }

    public async Task SyncInventoryItemsAsync(IEnumerable<InventoryItem> items, CancellationToken cancellationToken = default)
    {
        var itemList = items.ToList();
        var existingItems = await _referenceStore.GetItemIdsAsync(itemList.Select(item => item.LocalId), cancellationToken);

        foreach (var item in itemList)
        {
            if (!existingItems.TryGetValue(item.LocalId, out var existingId))
            {
                var payload = new
                {
                    name = item.Name,
                    sku = item.Sku,
                    rate = item.Rate,
                    quantity = item.Quantity
                };

                try
                {
                    var response = await PostAsync("items", payload, cancellationToken);
                    var remoteId = ExtractId(response, "item", "item_id");
                    await _referenceStore.SetItemIdAsync(item.LocalId, remoteId, cancellationToken);
                    await _referenceStore.LogSyncOperationAsync(ReferenceStore.SyncEntityType.InventoryItem.ToString(), item.LocalId, "Create", true, remoteId, null, null, cancellationToken);
                }
                catch (Exception ex)
                {
                    await _referenceStore.LogSyncOperationAsync(ReferenceStore.SyncEntityType.InventoryItem.ToString(), item.LocalId, "Create", false, null, ex.Message, null, cancellationToken);
                    throw;
                }

                continue;
            }

            try
            {
                await UpdateInventoryItemAsync(existingId, item, cancellationToken);
                await _referenceStore.LogSyncOperationAsync(ReferenceStore.SyncEntityType.InventoryItem.ToString(), item.LocalId, "Update", true, existingId, null, null, cancellationToken);
            }
            catch (Exception ex)
            {
                await _referenceStore.LogSyncOperationAsync(ReferenceStore.SyncEntityType.InventoryItem.ToString(), item.LocalId, "Update", false, existingId, ex.Message, null, cancellationToken);
                throw;
            }
        }
    }

    public async Task SyncContactsAsync(IEnumerable<Contact> contacts, CancellationToken cancellationToken = default)
    {
        var contactList = contacts.ToList();
        var existingContacts = await _referenceStore.GetContactIdsAsync(contactList.Select(contact => contact.LocalId), cancellationToken);

        foreach (var contact in contactList)
        {
            if (existingContacts.ContainsKey(contact.LocalId))
            {
                continue;
            }

            var payload = new
            {
                contact_name = contact.Name,
                email = contact.Email,
                phone = contact.Phone
            };

            try
            {
                    var response = await PostAsync("contacts", payload, cancellationToken);
                    var remoteId = ExtractId(response, "contact", "contact_id");
                    await _referenceStore.SetContactIdAsync(contact.LocalId, remoteId, cancellationToken);
                    await _referenceStore.LogSyncOperationAsync(ReferenceStore.SyncEntityType.Contact.ToString(), contact.LocalId, "Create", true, remoteId, null, null, cancellationToken);
                }
                catch (Exception ex)
                {
                    await _referenceStore.LogSyncOperationAsync(ReferenceStore.SyncEntityType.Contact.ToString(), contact.LocalId, "Create", false, null, ex.Message, null, cancellationToken);
                    throw;
                }
        }
    }

    public async Task SyncInvoicesAsync(IEnumerable<Invoice> invoices, CancellationToken cancellationToken = default)
    {
        var invoiceList = invoices.ToList();
        var existingInvoices = await _referenceStore.GetInvoiceIdsAsync(invoiceList.Select(invoice => invoice.LocalId), cancellationToken);
        var contactIds = await _referenceStore.GetContactIdsAsync(invoiceList.Select(invoice => invoice.ContactLocalId), cancellationToken);
        var itemLocalIds = invoiceList.SelectMany(invoice => invoice.LineItems.Select(item => item.ItemLocalId)).Distinct();
        var itemIds = await _referenceStore.GetItemIdsAsync(itemLocalIds, cancellationToken);

        foreach (var invoice in invoiceList)
        {
            if (existingInvoices.ContainsKey(invoice.LocalId))
            {
                continue;
            }

            if (!contactIds.TryGetValue(invoice.ContactLocalId, out var contactId))
            {
                var message = $"Missing contact reference for {invoice.ContactLocalId}.";
                await _referenceStore.LogSyncOperationAsync(ReferenceStore.SyncEntityType.Invoice.ToString(), invoice.LocalId, "Create", false, null, message, null, cancellationToken);
                throw new InvalidOperationException(message);
            }

            var lineItems = await BuildLineItemsAsync(invoice, itemIds, cancellationToken);

            try
            {
                await EnsureCurrencyAsync(invoice.CurrencyCode, cancellationToken);
                var reportingTagDetails = await BuildReportingTagDetailsAsync(invoice, cancellationToken);
                var payload = new Dictionary<string, object>
                {
                    ["customer_id"] = contactId,
                    ["date"] = invoice.InvoiceDate.ToString("yyyy-MM-dd"),
                    ["currency_code"] = invoice.CurrencyCode,
                    ["line_items"] = lineItems,
                    ["tags"] = reportingTagDetails,
                    ["notes"] = invoice.Notes,
                    ["tax_name"] = invoice.TaxName,
                    ["tax_percentage"] = invoice.TaxPercentage,
                    ["tax_treatment"] = invoice.TaxTreatment,
                    ["place_of_supply"] = invoice.PlaceOfSupply
                };

                if (!string.IsNullOrWhiteSpace(invoice.TaxId))
                {
                    payload["tax_id"] = invoice.TaxId;
                }

                var response = await PostAsync("invoices", payload, cancellationToken);
                var remoteId = ExtractId(response, "invoice", "invoice_id");
                await _referenceStore.SetInvoiceIdAsync(invoice.LocalId, remoteId, cancellationToken);
                await _referenceStore.LogSyncOperationAsync(ReferenceStore.SyncEntityType.Invoice.ToString(), invoice.LocalId, "Create", true, remoteId, null, null, cancellationToken);
            }
            catch (Exception ex)
            {
                await _referenceStore.LogSyncOperationAsync(ReferenceStore.SyncEntityType.Invoice.ToString(), invoice.LocalId, "Create", false, null, ex.Message, null, cancellationToken);
                throw;
            }
        }
    }

    public async Task UpdateInventoryItemsAsync(IEnumerable<InventoryItem> items, CancellationToken cancellationToken = default)
    {
        var itemList = items.ToList();
        var existingItems = await _referenceStore.GetItemIdsAsync(itemList.Select(item => item.LocalId), cancellationToken);

        foreach (var item in itemList)
        {
            if (!existingItems.TryGetValue(item.LocalId, out var remoteId))
            {
                continue;
            }

            try
            {
                await UpdateInventoryItemAsync(remoteId, item, cancellationToken);
                await _referenceStore.LogSyncOperationAsync(ReferenceStore.SyncEntityType.InventoryItem.ToString(), item.LocalId, "Update", true, remoteId, null, null, cancellationToken);
            }
            catch (Exception ex)
            {
                await _referenceStore.LogSyncOperationAsync(ReferenceStore.SyncEntityType.InventoryItem.ToString(), item.LocalId, "Update", false, remoteId, ex.Message, null, cancellationToken);
                throw;
            }
        }
    }

    public async Task UpdateContactsAsync(IEnumerable<Contact> contacts, CancellationToken cancellationToken = default)
    {
        var contactList = contacts.ToList();
        var existingContacts = await _referenceStore.GetContactIdsAsync(contactList.Select(contact => contact.LocalId), cancellationToken);

        foreach (var contact in contactList)
        {
            if (!existingContacts.TryGetValue(contact.LocalId, out var remoteId))
            {
                continue;
            }

            var payload = new
            {
                contact_name = contact.Name,
                email = contact.Email,
                phone = contact.Phone
            };

            try
            {
                await PutAsync($"contacts/{remoteId}", payload, cancellationToken);
                await _referenceStore.LogSyncOperationAsync(ReferenceStore.SyncEntityType.Contact.ToString(), contact.LocalId, "Update", true, remoteId, null, null, cancellationToken);
            }
            catch (Exception ex)
            {
                await _referenceStore.LogSyncOperationAsync(ReferenceStore.SyncEntityType.Contact.ToString(), contact.LocalId, "Update", false, remoteId, ex.Message, null, cancellationToken);
                throw;
            }
        }
    }

    public async Task UpdateInvoicesAsync(IEnumerable<Invoice> invoices, CancellationToken cancellationToken = default)
    {
        var invoiceList = invoices.ToList();
        var existingInvoices = await _referenceStore.GetInvoiceIdsAsync(invoiceList.Select(invoice => invoice.LocalId), cancellationToken);
        var contactIds = await _referenceStore.GetContactIdsAsync(invoiceList.Select(invoice => invoice.ContactLocalId), cancellationToken);
        var itemLocalIds = invoiceList.SelectMany(invoice => invoice.LineItems.Select(item => item.ItemLocalId)).Distinct();
        var itemIds = await _referenceStore.GetItemIdsAsync(itemLocalIds, cancellationToken);

        foreach (var invoice in invoiceList)
        {
            if (!existingInvoices.TryGetValue(invoice.LocalId, out var remoteId))
            {
                continue;
            }

            if (!contactIds.TryGetValue(invoice.ContactLocalId, out var contactId))
            {
                var message = $"Missing contact reference for {invoice.ContactLocalId}.";
                await _referenceStore.LogSyncOperationAsync(ReferenceStore.SyncEntityType.Invoice.ToString(), invoice.LocalId, "Update", false, remoteId, message, null, cancellationToken);
                throw new InvalidOperationException(message);
            }

            var lineItems = await BuildLineItemsAsync(invoice, itemIds, cancellationToken);

            try
            {
                await EnsureCurrencyAsync(invoice.CurrencyCode, cancellationToken);
                var reportingTagDetails = await BuildReportingTagDetailsAsync(invoice, cancellationToken);
                var payload = new Dictionary<string, object>
                {
                    ["customer_id"] = contactId,
                    ["date"] = invoice.InvoiceDate.ToString("yyyy-MM-dd"),
                    ["currency_code"] = invoice.CurrencyCode,
                    ["line_items"] = lineItems,
                    ["tags"] = reportingTagDetails,
                    ["notes"] = invoice.Notes,
                    ["tax_name"] = invoice.TaxName,
                    ["tax_percentage"] = invoice.TaxPercentage,
                    ["tax_treatment"] = invoice.TaxTreatment,
                    ["place_of_supply"] = invoice.PlaceOfSupply
                };

                if (!string.IsNullOrWhiteSpace(invoice.TaxId))
                {
                    payload["tax_id"] = invoice.TaxId;
                }

                await PutAsync($"invoices/{remoteId}", payload, cancellationToken);
                await _referenceStore.LogSyncOperationAsync(ReferenceStore.SyncEntityType.Invoice.ToString(), invoice.LocalId, "Update", true, remoteId, null, null, cancellationToken);
            }
            catch (Exception ex)
            {
                await _referenceStore.LogSyncOperationAsync(ReferenceStore.SyncEntityType.Invoice.ToString(), invoice.LocalId, "Update", false, remoteId, ex.Message, null, cancellationToken);
                throw;
            }
        }
    }

    public async Task AttachInvoicePdfAsync(int invoiceLocalId, string filePath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("PDF attachment path is required.", nameof(filePath));
        }

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("Invoice PDF attachment was not found.", filePath);
        }

        var invoiceIdLookup = await _referenceStore.GetInvoiceIdsAsync(new[] { invoiceLocalId }, cancellationToken);
        if (!invoiceIdLookup.TryGetValue(invoiceLocalId, out var invoiceId))
        {
            throw new InvalidOperationException($"Missing invoice reference for {invoiceLocalId}.");
        }

        using (var request = await CreateRequestAsync(HttpMethod.Post, $"invoices/{invoiceId}/attachment", cancellationToken))
        using (var content = new MultipartFormDataContent())
        using (var stream = File.OpenRead(filePath))
        using (var fileContent = new StreamContent(stream))
        {
            fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
            content.Add(fileContent, "attachment", Path.GetFileName(filePath));
            request.Content = content;

            var response = await _httpClient.SendAsync(request, cancellationToken);
            await EnsureSuccessAsync(response, cancellationToken);
        }
    }

    public async Task AttachInvoicePdfAsync(int invoiceLocalId, MemoryStream fileContent, CancellationToken cancellationToken = default)
    {
        if (fileContent == null)
        {
            throw new ArgumentNullException(nameof(fileContent));
        }

        var invoiceIdLookup = await _referenceStore.GetInvoiceIdsAsync(new[] { invoiceLocalId }, cancellationToken);
        if (!invoiceIdLookup.TryGetValue(invoiceLocalId, out var invoiceId))
        {
            throw new InvalidOperationException($"Missing invoice reference for {invoiceLocalId}.");
        }

        fileContent.Position = 0;
        using (var request = await CreateRequestAsync(HttpMethod.Post, $"invoices/{invoiceId}/attachment", cancellationToken))
        using (var content = new MultipartFormDataContent())
        using (var streamContent = new StreamContent(fileContent))
        {
            streamContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
            content.Add(streamContent, "attachment", $"invoice-{invoiceLocalId}.pdf");
            request.Content = content;

            var response = await _httpClient.SendAsync(request, cancellationToken);
            await EnsureSuccessAsync(response, cancellationToken);
        }
    }

    public async Task<IReadOnlyList<InvoicePayment>> PullPaymentsAsync(IEnumerable<int> invoiceLocalIds, CancellationToken cancellationToken = default)
    {
        var results = new List<InvoicePayment>();
        var invoiceIdLookup = await _referenceStore.GetInvoiceIdsAsync(invoiceLocalIds, cancellationToken);

        foreach (var localId in invoiceLocalIds)
        {
            if (!invoiceIdLookup.TryGetValue(localId, out var invoiceId))
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

    private async Task<IReadOnlyList<Dictionary<string, object>>> BuildLineItemsAsync(
        Invoice invoice,
        IReadOnlyDictionary<int, string> itemIds,
        CancellationToken cancellationToken)
    {
        var lineItems = new List<Dictionary<string, object>>();

        foreach (var item in invoice.LineItems)
        {
            var lineItem = new Dictionary<string, object>
            {
                ["item_id"] = itemIds.TryGetValue(item.ItemLocalId, out var itemId)
                    ? itemId
                    : throw new InvalidOperationException($"Missing item reference for {item.ItemLocalId}."),
                ["name"] = item.Description,
                ["rate"] = item.Rate,
                ["quantity"] = item.Quantity,
                ["tax_name"] = item.TaxName,
                ["tax_percentage"] = item.TaxPercentage
            };

            if (!string.IsNullOrWhiteSpace(item.TaxId))
            {
                lineItem["tax_id"] = item.TaxId;
            }

            if (!string.IsNullOrWhiteSpace(item.AccountCode))
            {
                var accountId = await EnsureAccountIdAsync(item.AccountCode, cancellationToken);
                lineItem["account_id"] = accountId;
            }

            lineItems.Add(lineItem);
        }

        return lineItems;
    }

    private async Task<string> EnsureAccountIdAsync(string accountCode, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(accountCode))
        {
            return null;
        }

        if (_accountCache.TryGetValue(accountCode, out var cachedId))
        {
            return cachedId;
        }

        var existingId = await _referenceStore.GetAccountIdAsync(accountCode, cancellationToken);
        if (!string.IsNullOrWhiteSpace(existingId))
        {
            _accountCache[accountCode] = existingId;
            return existingId;
        }

        var response = await GetAsync("chartofaccounts", cancellationToken);
        if (response.TryGetProperty("chartofaccounts", out var accountsElement))
        {
            foreach (var account in accountsElement.EnumerateArray())
            {
                var code = account.TryGetProperty("account_code", out var codeElement)
                    ? codeElement.GetString()
                    : null;
                if (!string.Equals(code, accountCode, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var accountId = account.GetProperty("account_id").GetString() ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(accountId))
                {
                    await _referenceStore.SetAccountIdAsync(accountCode, accountId, cancellationToken);
                    _accountCache[accountCode] = accountId;
                    return accountId;
                }
            }
        }

        var message = $"Account code '{accountCode}' was not found in Zoho Books. Create the account in Zoho Books and try again.";
        await _referenceStore.LogSyncOperationAsync(
            ReferenceStore.SyncEntityType.Account.ToString(),
            0,
            "Fetch",
            false,
            null,
            message,
            accountCode,
            cancellationToken);
        throw new InvalidOperationException(message);
    }

    private async Task EnsureCurrencyAsync(string currencyCode, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(currencyCode))
        {
            return;
        }

        if (_currencyCache.ContainsKey(currencyCode))
        {
            return;
        }

        var existingCurrencyId = await _referenceStore.GetCurrencyIdAsync(currencyCode, cancellationToken);
        if (!string.IsNullOrWhiteSpace(existingCurrencyId))
        {
            _currencyCache[currencyCode] = existingCurrencyId;
            return;
        }

        var response = await GetAsync("settings/currencies", cancellationToken);
        if (response.TryGetProperty("currencies", out var currenciesElement))
        {
            foreach (var currency in currenciesElement.EnumerateArray())
            {
                var code = currency.GetProperty("currency_code").GetString();
                if (string.Equals(code, currencyCode, StringComparison.OrdinalIgnoreCase))
                {
                    var currencyId = currency.GetProperty("currency_id").GetString() ?? string.Empty;
                    if (!string.IsNullOrWhiteSpace(currencyId))
                    {
                        await _referenceStore.SetCurrencyIdAsync(currencyCode, currencyId, cancellationToken);
                        _currencyCache[currencyCode] = currencyId;
                        return;
                    }
                }
            }
        }

        var payload = new
        {
            currency_code = currencyCode,
            currency_name = currencyCode
        };

        try
        {
            var createResponse = await PostAsync("settings/currencies", payload, cancellationToken);
            var createdCurrencyId = ExtractId(createResponse, "currency", "currency_id");
            if (!string.IsNullOrWhiteSpace(createdCurrencyId))
            {
                await _referenceStore.SetCurrencyIdAsync(currencyCode, createdCurrencyId, cancellationToken);
                _currencyCache[currencyCode] = createdCurrencyId;
                return;
            }
        }
        catch (Exception)
        {
            var retryResponse = await GetAsync("settings/currencies", cancellationToken);
            if (retryResponse.TryGetProperty("currencies", out var retryCurrencies))
            {
                foreach (var currency in retryCurrencies.EnumerateArray())
                {
                    var code = currency.GetProperty("currency_code").GetString();
                    if (string.Equals(code, currencyCode, StringComparison.OrdinalIgnoreCase))
                    {
                        var currencyId = currency.GetProperty("currency_id").GetString() ?? string.Empty;
                        if (!string.IsNullOrWhiteSpace(currencyId))
                        {
                            await _referenceStore.SetCurrencyIdAsync(currencyCode, currencyId, cancellationToken);
                            _currencyCache[currencyCode] = currencyId;
                            return;
                        }
                    }
                }
            }

            throw;
        }
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
        using (var request = await CreateRequestAsync(HttpMethod.Get, path, cancellationToken))
        {
            var response = await _httpClient.SendAsync(request, cancellationToken);
            return await EnsureSuccessAsync(response, cancellationToken);
        }
    }

    private async Task<JsonElement> SendAsync(HttpMethod method, string path, object payload, CancellationToken cancellationToken)
    {
        using (var request = await CreateRequestAsync(method, path, cancellationToken))
        {
            request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            var response = await _httpClient.SendAsync(request, cancellationToken);
            return await EnsureSuccessAsync(response, cancellationToken);
        }
    }

    private async Task<HttpRequestMessage> CreateRequestAsync(HttpMethod method, string path, CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(method, $"{_connectionOptions.APIBaseUrl}/{path}");
        var accessToken = await _tokenProvider.GetAccessTokenAsync(cancellationToken);
        request.Headers.Authorization = new AuthenticationHeaderValue("Zoho-oauthtoken", accessToken);
        request.Headers.Add("X-com-zoho-books-organizationid", _connectionOptions.ActiveOrganizationId);
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
                tag_id = option.TagId,
                tag_option_id = option.OptionId
            });
        }

        if (!string.IsNullOrWhiteSpace(invoice.RelationshipManager))
        {
            var option = await EnsureReportingTagOptionAsync("RelationshipManager", invoice.RelationshipManager, cancellationToken);
            details.Add(new
            {
                tag_id = option.TagId,
                tag_option_id = option.OptionId
            });
        }

        return details;
    }

    private async Task<ReportingTagOption> EnsureReportingTagOptionAsync(string tagName, string optionName, CancellationToken cancellationToken)
    {
        var optionKey = BuildReportingTagOptionKey(tagName, optionName);
        if (_reportingTagOptionCache.TryGetValue(optionKey, out var cachedOptionId))
        {
            var tag = await GetReportingTagAsync(tagName, cancellationToken);
            return new ReportingTagOption(tag.Id, cachedOptionId);
        }

        var existingOptionId = await _referenceStore.GetReportingTagOptionIdAsync(optionKey, cancellationToken);
        if (!string.IsNullOrWhiteSpace(existingOptionId))
        {
            _reportingTagOptionCache[optionKey] = existingOptionId;
            var tag = await GetReportingTagAsync(tagName, cancellationToken);
            return new ReportingTagOption(tag.Id, existingOptionId);
        }

        var reportingTag = await GetReportingTagAsync(tagName, cancellationToken);
        if (reportingTag.OptionsByName.TryGetValue(optionName, out var remoteOptionId))
        {
            _reportingTagOptionCache[optionKey] = remoteOptionId;
            await _referenceStore.SetReportingTagOptionIdAsync(optionKey, remoteOptionId, cancellationToken);
            return new ReportingTagOption(reportingTag.Id, remoteOptionId);
        }

        var refreshedOptions = await GetReportingTagOptionsAsync(reportingTag.Id, cancellationToken);
        foreach (var option in refreshedOptions)
        {
            reportingTag.OptionsByName[option.Key] = option.Value;
        }

        if (reportingTag.OptionsByName.TryGetValue(optionName, out var refreshedOptionId))
        {
            _reportingTagOptionCache[optionKey] = refreshedOptionId;
            await _referenceStore.SetReportingTagOptionIdAsync(optionKey, refreshedOptionId, cancellationToken);
            return new ReportingTagOption(reportingTag.Id, refreshedOptionId);
        }

        if (!_connectionOptions.AllowReportingTagOptionCreate)
        {
            var message = $"Reporting tag option '{optionName}' is missing in Zoho Books. Create it manually and retry.";
            await _referenceStore.LogSyncOperationAsync(ReferenceStore.SyncEntityType.ReportingTagOption.ToString(), 0, "Create", false, null, message, optionKey, cancellationToken);
            throw new InvalidOperationException(message);
        }

        var payload = new
        {
            option_name = optionName
        };

        try
        {
            var response = await PostAsync($"{ReportingTagBasePath}/{reportingTag.Id}/options", payload, cancellationToken);
            var createdOptionId = ExtractId(response, "reporting_tag_option", "option_id");
            _reportingTagOptionCache[optionKey] = createdOptionId;
            await _referenceStore.SetReportingTagOptionIdAsync(optionKey, createdOptionId, cancellationToken);
            await _referenceStore.LogSyncOperationAsync(ReferenceStore.SyncEntityType.ReportingTagOption.ToString(), 0, "Create", true, createdOptionId, null, optionKey, cancellationToken);
            reportingTag.OptionsByName[optionName] = createdOptionId;
            return new ReportingTagOption(reportingTag.Id, createdOptionId);
        }
        catch (Exception ex)
        {
            await _referenceStore.LogSyncOperationAsync(ReferenceStore.SyncEntityType.ReportingTagOption.ToString(), 0, "Create", false, null, ex.Message, optionKey, cancellationToken);
            throw;
        }
    }

    private async Task<ReportingTag> GetReportingTagAsync(string tagName, CancellationToken cancellationToken)
    {
        if (_reportingTags.TryGetValue(tagName, out var cachedTag))
        {
            return cachedTag;
        }

        var cachedTagId = await _referenceStore.GetReportingTagIdAsync(tagName, cancellationToken);
        if (!string.IsNullOrWhiteSpace(cachedTagId))
        {
            var cachedReportingTag = new ReportingTag(cachedTagId, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
            _reportingTags[tagName] = cachedReportingTag;
            return cachedReportingTag;
        }

        JsonElement response;
        try
        {
            response = await GetAsync(ReportingTagBasePath, cancellationToken);
            await _referenceStore.LogSyncOperationAsync(ReferenceStore.SyncEntityType.ReportingTag.ToString(), 0, "Fetch", true, null, null, tagName, cancellationToken);
        }
        catch (Exception ex)
        {
            await _referenceStore.LogSyncOperationAsync(ReferenceStore.SyncEntityType.ReportingTag.ToString(), 0, "Fetch", false, null, ex.Message, tagName, cancellationToken);
            throw;
        }

        if (!response.TryGetProperty("reporting_tags", out var tagsElement))
        {
            var message = "Unable to load reporting tags from Zoho Books.";
            await _referenceStore.LogSyncOperationAsync(ReferenceStore.SyncEntityType.ReportingTag.ToString(), 0, "Fetch", false, null, message, tagName, cancellationToken);
            throw new InvalidOperationException(message);
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

            var reportingTag = new ReportingTag(tagId, optionsByName);
            _reportingTags[name] = reportingTag;
            await _referenceStore.SetReportingTagIdAsync(name, tagId, cancellationToken);
        }

        if (_reportingTags.TryGetValue(tagName, out var foundTag))
        {
            return foundTag;
        }

        throw new InvalidOperationException($"Reporting tag '{tagName}' not found in Zoho Books.");
    }

    private static string BuildReportingTagOptionKey(string tagName, string optionName)
        => $"{tagName.Trim()}::{optionName.Trim()}".ToLowerInvariant();

    private async Task<Dictionary<string, string>> GetReportingTagOptionsAsync(string tagId, CancellationToken cancellationToken)
    {
        var response = await GetAsync($"{ReportingTagBasePath}/{tagId}", cancellationToken);
        var options = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (response.TryGetProperty("reporting_tag", out var reportingTagElement)
            && reportingTagElement.TryGetProperty("tag_options", out var optionsElement)
            && optionsElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var optionElement in optionsElement.EnumerateArray())
            {
                if (!optionElement.TryGetProperty("tag_option_name", out var nameElement))
                {
                    continue;
                }

                var optionName = nameElement.GetString();
                if (string.IsNullOrWhiteSpace(optionName))
                {
                    continue;
                }

                if (!optionElement.TryGetProperty("tag_option_id", out var idElement))
                {
                    continue;
                }

                string optionId;
                if (idElement.ValueKind == JsonValueKind.Number)
                {
                    optionId = idElement.TryGetInt64(out var idValue) ? idValue.ToString() : idElement.GetRawText();
                }
                else
                {
                    optionId = idElement.GetString();
                }

                if (!string.IsNullOrWhiteSpace(optionId))
                {
                    options[optionName] = optionId;
                }
            }
        }

        return options;
    }

    private static void AddReportingTagOptions(Dictionary<string, string> optionsByName, JsonElement optionsElement)
    {
        if (optionsElement.ValueKind != JsonValueKind.String)
        {
            return;
        }

        var optionsValue = optionsElement.GetString();
        if (string.IsNullOrWhiteSpace(optionsValue))
        {
            return;
        }

        var entries = optionsValue.Split(',');
        foreach (var entry in entries)
        {
            var optionNameStr = entry.Trim();
            if (!string.IsNullOrWhiteSpace(optionNameStr))
            {
                optionsByName[optionNameStr] = optionNameStr;
            }
        }
    }

    private static async Task<JsonElement> EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var content = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"Zoho Books API error ({(int)response.StatusCode}): {content}");
        }

        using (var document = JsonDocument.Parse(content))
        {
            return document.RootElement.Clone();
        }
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

    private sealed class ReportingTag
    {
        public ReportingTag(string id, Dictionary<string, string> optionsByName)
        {
            Id = id;
            OptionsByName = optionsByName;
        }

        public string Id { get; private set; }
        public Dictionary<string, string> OptionsByName { get; private set; }
    }

    private sealed class ReportingTagOption
    {
        public ReportingTagOption(string tagId, string optionId)
        {
            TagId = tagId;
            OptionId = optionId;
        }

        public string TagId { get; private set; }
        public string OptionId { get; private set; }
    }
}
}
