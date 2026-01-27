using CRM.Core.Models;

namespace CRM.Email.Abstractions;

public interface IEmailProvider
{
    Task<IReadOnlyList<EmailMessageMetadata>> SyncAsync(EmailSyncRequest request, CancellationToken cancellationToken);
    Task<EmailContent?> FetchContentAsync(string messageId, CancellationToken cancellationToken);
}
