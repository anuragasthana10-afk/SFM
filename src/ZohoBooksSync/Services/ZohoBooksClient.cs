using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ZohoBooksSync.Models;

namespace ZohoBooksSync.Services;

public sealed class ZohoBooksClient
{
    private readonly HttpClient _httpClient;
    private readonly JsonSerializerOptions _serializerOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public ZohoBooksClient(HttpClient httpClient, ZohoSettings settings)
    {
        _httpClient = httpClient;
        Settings = settings;
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Zoho-oauthtoken {settings.AccessToken}");
        _httpClient.DefaultRequestHeaders.Add("X-com-zoho-books-organizationid", settings.OrganizationId);
    }

    public ZohoSettings Settings { get; }

    public async Task<string> CreateItemAsync(ZohoItem item, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("items", item, _serializerOptions, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        return ExtractId(response, body, "item", i => i.GetProperty("item_id").GetString());
    }

    public async Task UpdateItemAsync(string itemId, ZohoItem item, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"items/{itemId}", item, _serializerOptions, cancellationToken);
        await EnsureSuccess(response);
    }

    public async Task<string> CreateInvoiceAsync(ZohoInvoice invoice, CancellationToken cancellationToken = default)
    {
        var payload = CloneInvoice(invoice);
        var response = await _httpClient.PostAsJsonAsync("invoices", payload, _serializerOptions, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        return ExtractId(response, body, "invoice", i => i.GetProperty("invoice_id").GetString());
    }

    public async Task UpdateInvoiceAsync(string invoiceId, ZohoInvoice invoice, CancellationToken cancellationToken = default)
    {
        var payload = CloneInvoice(invoice);
        var response = await _httpClient.PutAsJsonAsync($"invoices/{invoiceId}", payload, _serializerOptions, cancellationToken);
        await EnsureSuccess(response);
    }

    public async Task<IReadOnlyList<ZohoPayment>> GetPaymentsForInvoiceAsync(string invoiceId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync($"customerpayments?invoice_id={invoiceId}", cancellationToken);
        await EnsureSuccess(response);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        var document = JsonDocument.Parse(body);
        var payments = document.RootElement.GetProperty("customerpayments");
        var result = new List<ZohoPayment>();
        foreach (var payment in payments.EnumerateArray())
        {
            result.Add(JsonSerializer.Deserialize<ZohoPayment>(payment, _serializerOptions)!);
        }

        return result;
    }

    private static ZohoInvoice CloneInvoice(ZohoInvoice invoice)
    {
        var clonedLines = invoice.LineItems.Select(line => new ZohoInvoiceLine
        {
            ItemId = line.ItemId,
            ItemLocalId = line.ItemLocalId,
            Rate = line.Rate,
            Quantity = line.Quantity,
            Description = line.Description
        }).ToList();

        return new ZohoInvoice
        {
            InvoiceId = invoice.InvoiceId,
            LocalId = invoice.LocalId,
            CustomerId = invoice.CustomerId,
            InvoiceNumber = invoice.InvoiceNumber,
            DueDate = invoice.DueDate,
            LineItems = clonedLines
        };
    }

    private static string ExtractId(HttpResponseMessage response, string body, string propertyName, Func<JsonElement, string?> selector)
    {
        EnsureSuccess(response).GetAwaiter().GetResult();
        var document = JsonDocument.Parse(body);
        var container = document.RootElement.GetProperty(propertyName);
        var id = selector(container);
        if (id is null)
        {
            throw new ZohoApiException($"Unable to read {propertyName} identifier from Zoho response", body);
        }

        return id;
    }

    private static async Task EnsureSuccess(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var content = await response.Content.ReadAsStringAsync();
        throw new ZohoApiException($"Zoho Books API responded with {(int)response.StatusCode} {response.ReasonPhrase}", content);
    }
}
