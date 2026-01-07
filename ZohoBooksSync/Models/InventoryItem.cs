namespace ZohoBooksSync.Models;

public sealed class InventoryItem
{
    public required int LocalId { get; init; }
    public required string Name { get; init; }
    public string? Sku { get; init; }
    public decimal Rate { get; init; }
    public int Quantity { get; init; }
}
