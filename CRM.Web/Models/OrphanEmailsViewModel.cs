using CRM.Core.Models;

namespace CRM.Web.Models;

public sealed class OrphanEmailsViewModel
{
    public IReadOnlyList<EmailMessageMetadata> OrphanMessages { get; init; } = Array.Empty<EmailMessageMetadata>();
}
