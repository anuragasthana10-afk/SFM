using CRM.Core.Models;
using CRM.Email.Abstractions;

namespace CRM.Email.Services;

public sealed class LocalEmailProvider : IEmailProvider, IEmailService
{
    public Task<IReadOnlyList<EmailMessageMetadata>> SyncAsync(
        EmailSyncRequest request,
        CancellationToken cancellationToken)
    {
        var demoMessage = new EmailMessageMetadata
        {
            MessageId = "demo-message-1",
            ConversationId = "demo-thread-1",
            Subject = "Welcome to the CRM email demo",
            Snippet = "This is a sample email synced into the CRM.",
            ReceivedAt = DateTimeOffset.UtcNow,
            Participants = new List<EmailParticipant>
            {
                new() { Address = "sales@contoso.com", DisplayName = "Contoso Sales" },
                new() { Address = request.MailboxAddress, DisplayName = "CRM Mailbox" }
            },
            HasAttachments = false
        };

        return Task.FromResult<IReadOnlyList<EmailMessageMetadata>>(new List<EmailMessageMetadata> { demoMessage });
    }

    public Task<EmailContent?> FetchContentAsync(string messageId, CancellationToken cancellationToken)
    {
        var content = new EmailContent
        {
            MessageId = messageId,
            TextBody = "Demo email content stored in CRM.",
            HtmlBody = "<p>Demo email content stored in CRM.</p>"
        };

        return Task.FromResult<EmailContent?>(content);
    }

    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
