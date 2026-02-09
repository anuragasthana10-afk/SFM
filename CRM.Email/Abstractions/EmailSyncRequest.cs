namespace CRM.Email.Abstractions;

public sealed class EmailSyncRequest
{
    public string MailboxAddress { get; set; } = string.Empty;
    public DateTimeOffset? Since { get; set; }
}
