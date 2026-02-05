using CRM.Core.Models;

namespace CRM.Web.Models;

public sealed class OrphanEmailsViewModel
{
    public IReadOnlyList<OrphanMessageCandidateViewModel> OrphanMessages { get; init; } = Array.Empty<OrphanMessageCandidateViewModel>();
    public IReadOnlyList<Account> AllAccounts { get; init; } = Array.Empty<Account>();
    public IReadOnlyList<OrphanMessageCandidateViewModel> DiscardedMessages { get; init; } = Array.Empty<OrphanMessageCandidateViewModel>();
}

public sealed class OrphanMessageCandidateViewModel
{
    public required EmailMessageMetadata Message { get; init; }
    public string SenderAddress { get; init; } = string.Empty;
    public IReadOnlyList<int> SuggestedAccountIds { get; init; } = Array.Empty<int>();
}
