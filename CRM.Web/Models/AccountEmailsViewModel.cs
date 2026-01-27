using CRM.Core.Models;

namespace CRM.Web.Models;

public sealed class AccountEmailsViewModel
{
    public Account Account { get; init; } = new();
    public IReadOnlyList<EmailMessageMetadata> Messages { get; init; } = Array.Empty<EmailMessageMetadata>();
    public IReadOnlyList<EmailThreadSummary> Threads { get; init; } = Array.Empty<EmailThreadSummary>();
}
