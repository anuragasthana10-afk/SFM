namespace ZohoBooksSync.Models;

public sealed class Contact
{
    public required int LocalId { get; init; }
    public required string Name { get; init; }
    public string? Email { get; init; }
    public string? Phone { get; init; }
}
