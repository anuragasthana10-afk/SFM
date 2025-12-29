namespace ZohoBooksSync.Models;

public sealed class Invoice
{
    public required string LocalId { get; init; }
    public required string ContactLocalId { get; init; }
    public required DateTime InvoiceDate { get; init; }
    public required IReadOnlyList<InvoiceLineItem> LineItems { get; init; }
    public string? Notes { get; init; }
}

public sealed class InvoiceLineItem
{
    public required string ItemLocalId { get; init; }
    public required string Description { get; init; }
    public decimal Rate { get; init; }
    public int Quantity { get; init; }
}
