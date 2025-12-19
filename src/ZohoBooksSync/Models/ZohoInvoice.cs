using System.Text.Json.Serialization;

namespace ZohoBooksSync.Models;

public sealed class ZohoInvoice
{
    public required string LocalId { get; init; }

    [JsonPropertyName("invoice_id")]
    public string? InvoiceId { get; init; }

    [JsonPropertyName("customer_id")]
    public required string? CustomerId { get; init; }

    [JsonPropertyName("invoice_number")]
    public required string InvoiceNumber { get; init; }

    [JsonPropertyName("due_date")]
    public required DateOnly DueDate { get; init; }

    [JsonPropertyName("line_items")]
    public required List<ZohoInvoiceLine> LineItems { get; init; }
}

public sealed class ZohoInvoiceLine
{
    [JsonPropertyName("item_id")]
    public string? ItemId { get; init; }

    [JsonIgnore]
    public string? ItemLocalId { get; init; }

    [JsonPropertyName("rate")]
    public decimal Rate { get; init; }

    [JsonPropertyName("quantity")]
    public decimal Quantity { get; init; }

    [JsonPropertyName("description")]
    public string? Description { get; init; }
}

public sealed class ZohoPayment
{
    [JsonPropertyName("payment_id")]
    public string? PaymentId { get; init; }

    [JsonPropertyName("payment_mode")]
    public string? PaymentMode { get; init; }

    [JsonPropertyName("amount")]
    public decimal Amount { get; init; }

    [JsonPropertyName("payment_number")]
    public string? PaymentNumber { get; init; }

    [JsonPropertyName("date")]
    public DateOnly Date { get; init; }
}
