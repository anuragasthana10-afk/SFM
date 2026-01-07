namespace ZohoBooksSync.Models;

public sealed class Invoice
{
    public required int LocalId { get; init; }
    public required int ContactLocalId { get; init; }
    public required DateTime InvoiceDate { get; init; }
    public required IReadOnlyList<InvoiceLineItem> LineItems { get; init; }
    public string? CurrencyCode { get; init; }
    public string? Jurisdiction { get; init; }
    public string? RelationshipManager { get; init; }
    public string? Notes { get; init; }
}

public sealed class InvoiceLineItem
{
    public required int ItemLocalId { get; init; }
    public required string Description { get; init; }
    public decimal Rate { get; init; }
    public int Quantity { get; init; }
}
