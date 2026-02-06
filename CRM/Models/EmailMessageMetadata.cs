namespace CRM.Models;

public sealed class EmailMessageMetadata
{
    public string MessageId { get; set; } = string.Empty;
    public string ConversationId { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string? Snippet { get; set; }
    public DateTimeOffset ReceivedAt { get; set; }
    public List<EmailParticipant> Participants { get; set; } = new();
    public int? AccountId { get; set; }
    public bool HasAttachments { get; set; }
}
