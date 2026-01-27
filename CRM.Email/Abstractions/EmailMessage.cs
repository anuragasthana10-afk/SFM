using CRM.Core.Models;

namespace CRM.Email.Abstractions;

public sealed class EmailMessage
{
    public string From { get; set; } = string.Empty;
    public List<string> To { get; set; } = new();
    public List<string> Cc { get; set; } = new();
    public string Subject { get; set; } = string.Empty;
    public string? HtmlBody { get; set; }
    public string? TextBody { get; set; }
    public Guid? AccountGuidStamp { get; set; }
    public List<EmailAttachment> Attachments { get; set; } = new();
}
