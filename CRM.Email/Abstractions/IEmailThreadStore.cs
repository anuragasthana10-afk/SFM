using CRM.Core.Models;

namespace CRM.Email.Abstractions;

public interface IEmailThreadStore
{
    Task UpsertThreadAsync(EmailThread thread, CancellationToken cancellationToken);
    Task UpsertMessagesAsync(IEnumerable<EmailMessageMetadata> messages, CancellationToken cancellationToken);
    Task<EmailContent?> GetContentAsync(string messageId, CancellationToken cancellationToken);
    Task SaveContentAsync(EmailContent content, CancellationToken cancellationToken);
}
