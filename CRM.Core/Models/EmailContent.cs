namespace CRM.Core.Models;

public sealed class EmailContent
{
    public string MessageId { get; set; } = string.Empty;
    public string? HtmlBody { get; set; }
    public string? TextBody { get; set; }
    public List<EmailAttachment> Attachments { get; set; } = new();
}
