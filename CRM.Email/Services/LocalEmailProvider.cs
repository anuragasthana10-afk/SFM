using CRM.Core.Models;
using CRM.Core.Storage;
using CRM.Email.Abstractions;

namespace CRM.Email.Services;

public sealed class LocalEmailProvider : IEmailProvider, IEmailService
{
    private readonly IAccountStore _accountStore;
    private readonly IEmailThreadStore _threadStore;

    public LocalEmailProvider(IAccountStore accountStore, IEmailThreadStore threadStore)
    {
        _accountStore = accountStore;
        _threadStore = threadStore;
    }

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

    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        var accountId = await ResolveAccountIdAsync(message.AccountGuidStamp, cancellationToken);
        var messageId = Guid.NewGuid().ToString("N");
        var conversationId = string.IsNullOrWhiteSpace(message.ConversationId) ? Guid.NewGuid().ToString("N") : message.ConversationId;

        var metadata = new EmailMessageMetadata
        {
            MessageId = messageId,
            ConversationId = conversationId,
            Subject = message.Subject,
            Snippet = message.Subject,
            ReceivedAt = DateTimeOffset.UtcNow,
            Participants = BuildParticipants(message),
            AccountId = accountId,
            HasAttachments = message.Attachments.Count > 0
        };

        await _threadStore.UpsertMessagesAsync(new[] { metadata }, cancellationToken);

        var content = new EmailContent
        {
            MessageId = messageId,
            HtmlBody = StampAccountGuid(message.HtmlBody, message.AccountGuidStamp, isHtml: true),
            TextBody = StampAccountGuid(message.TextBody, message.AccountGuidStamp, isHtml: false),
            Attachments = message.Attachments
        };

        await _threadStore.SaveContentAsync(content, cancellationToken);
    }

    private async Task<int?> ResolveAccountIdAsync(Guid? accountGuid, CancellationToken cancellationToken)
    {
        if (accountGuid is null)
        {
            return null;
        }

        var accounts = await _accountStore.GetAllAsync(cancellationToken);
        return accounts.FirstOrDefault(a => a.AccountGuid == accountGuid.Value)?.Id;
    }


    private static string? StampAccountGuid(string? body, Guid? accountGuid, bool isHtml)
    {
        if (accountGuid is null)
        {
            return body;
        }

        var stamp = $"[[CRM-ACCOUNT:{accountGuid}]]";
        if (string.IsNullOrWhiteSpace(body))
        {
            return stamp;
        }

        return isHtml
            ? $"{body}\n<!-- {stamp} -->"
            : $"{body}\n{stamp}";
    }

    private static List<EmailParticipant> BuildParticipants(EmailMessage message)
    {
        var participants = new List<EmailParticipant>
        {
            new() { Address = message.From, DisplayName = message.From }
        };

        participants.AddRange(message.To.Select(to => new EmailParticipant { Address = to }));
        participants.AddRange(message.Cc.Select(cc => new EmailParticipant { Address = cc }));
        participants.AddRange(message.Bcc.Select(bcc => new EmailParticipant { Address = bcc }));

        return participants;
    }
}
