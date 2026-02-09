using CRM.Models;

namespace CRM.Web.Models;

public sealed class AccountEmailsViewModel
{
    public Account Account { get; init; } = new();
    public IReadOnlyList<EmailMessageMetadata> Messages { get; init; } = Array.Empty<EmailMessageMetadata>();
    public IReadOnlyList<EmailThreadSummary> Threads { get; init; } = Array.Empty<EmailThreadSummary>();
    public string ViewMode { get; init; } = "thread";
    public string? SelectedConversationId { get; init; }
    public IReadOnlySet<string> ReadMessageIds { get; init; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    public IReadOnlySet<string> ReadConversationIds { get; init; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
}
