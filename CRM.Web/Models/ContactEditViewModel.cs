using CRM.Core.Models;

namespace CRM.Web.Models;

public sealed class ContactEditViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string EmailAddress { get; set; } = string.Empty;
    public List<int> SelectedAccountIds { get; set; } = new();
    public IReadOnlyList<Account> Accounts { get; set; } = Array.Empty<Account>();
}

public sealed class ContactListViewModel
{
    public IReadOnlyList<Contact> Contacts { get; init; } = Array.Empty<Contact>();
    public Dictionary<int, IReadOnlyList<int>> ContactAccountIds { get; init; } = new();
    public IReadOnlyList<Account> Accounts { get; set; } = Array.Empty<Account>();
}
