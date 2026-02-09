using CRM.Models;

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
    Task<int> EnsureUserAsync(string username, string firstName, string lastName, CancellationToken cancellationToken);
    Task<IReadOnlySet<string>> GetReadMessageIdsAsync(int accountId, int userId, CancellationToken cancellationToken);
    Task<IReadOnlySet<string>> GetReadConversationIdsAsync(int accountId, int userId, CancellationToken cancellationToken);
    Task MarkMessageOpenedAsync(string messageId, int userId, CancellationToken cancellationToken);
    Task RecordAuditAsync(int userId, string username, string actionType, string? messageId, string? conversationId, int? oldAccountId, int? newAccountId, string? details, CancellationToken cancellationToken);
}
