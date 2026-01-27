namespace CRM.Core.Models;

public sealed class Account
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public Guid AccountGuid { get; set; } = Guid.NewGuid();
}
