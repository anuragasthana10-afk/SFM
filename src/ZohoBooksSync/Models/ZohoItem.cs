using System.Text.Json.Serialization;

namespace ZohoBooksSync.Models;

public sealed class ZohoItem
{
    public required string LocalId { get; init; }

    [JsonPropertyName("item_id")]
    public string? ItemId { get; init; }

    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("rate")]
    public required decimal Rate { get; init; }

    [JsonPropertyName("description")]
    public string? Description { get; init; }

    [JsonPropertyName("unit")]
    public string? Unit { get; init; }

    [JsonPropertyName("sku")]
    public string? Sku => LocalId;
}
