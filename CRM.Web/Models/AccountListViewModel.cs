using CRM.Models;

namespace CRM.Web.Models;

public sealed class AccountListViewModel
{
    public IReadOnlyList<Account> Accounts { get; init; } = Array.Empty<Account>();
}
