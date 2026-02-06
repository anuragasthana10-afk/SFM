namespace CRM.Models;

public sealed class EmailThreadSummary
{
    public string ConversationId { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public DateTimeOffset LastReceivedAt { get; set; }
    public int MessageCount { get; set; }
    public int? AccountId { get; set; }
}
