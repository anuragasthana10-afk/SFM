namespace CRM.Core.Models;

public sealed class EmailThread
{
    public string ConversationId { get; set; } = string.Empty;
    public int AccountId { get; set; }
    public DateTimeOffset LastUpdatedAt { get; set; }
    public List<string> MessageIds { get; set; } = new();
}
