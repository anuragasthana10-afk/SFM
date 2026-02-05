using CRM.Core.Models;

namespace CRM.Email.Abstractions;

public interface IEmailThreadQuery
{
    Task<IReadOnlyList<EmailMessageMetadata>> GetMessagesByAccountAsync(int accountId, CancellationToken cancellationToken);
    Task<IReadOnlyList<EmailThreadSummary>> GetThreadsByAccountAsync(int accountId, CancellationToken cancellationToken);
    Task<IReadOnlyList<EmailMessageMetadata>> GetOrphanMessagesAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<EmailMessageMetadata>> GetDiscardedMessagesAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<int>> GetAccountSuggestionsBySenderAsync(string senderAddress, CancellationToken cancellationToken);
    Task<EmailMessageMetadata?> GetMessageByIdAsync(string messageId, CancellationToken cancellationToken);
    Task<IReadOnlyList<EmailMessageMetadata>> GetMessagesByConversationAsync(string conversationId, CancellationToken cancellationToken);
    Task ReassignThreadAsync(string conversationId, int newAccountId, CancellationToken cancellationToken);
    Task SetAccountForMessageAsync(string messageId, int accountId, CancellationToken cancellationToken);
    Task DiscardMessageAsync(string messageId, CancellationToken cancellationToken);
    Task RestoreDiscardedMessageAsync(string messageId, CancellationToken cancellationToken);
    Task DisassociateMessageAsync(string messageId, CancellationToken cancellationToken);
}
