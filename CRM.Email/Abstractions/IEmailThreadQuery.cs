using CRM.Core.Models;

namespace CRM.Email.Abstractions;

public interface IEmailThreadQuery
{
    Task<IReadOnlyList<EmailMessageMetadata>> GetMessagesByAccountAsync(int accountId, CancellationToken cancellationToken);
    Task<IReadOnlyList<EmailThreadSummary>> GetThreadsByAccountAsync(int accountId, CancellationToken cancellationToken);
    Task<IReadOnlyList<EmailMessageMetadata>> GetOrphanMessagesAsync(CancellationToken cancellationToken);
    Task<EmailMessageMetadata?> GetMessageByIdAsync(string messageId, CancellationToken cancellationToken);
    Task<IReadOnlyList<EmailMessageMetadata>> GetMessagesByConversationAsync(string conversationId, CancellationToken cancellationToken);
    Task ReassignThreadAsync(string conversationId, int newAccountId, CancellationToken cancellationToken);
    Task SetAccountForMessageAsync(string messageId, int accountId, CancellationToken cancellationToken);
}
