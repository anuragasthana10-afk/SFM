namespace CRM.Web.Models;

public sealed class AccountEditViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public Guid AccountGuid { get; set; } = Guid.NewGuid();
}
