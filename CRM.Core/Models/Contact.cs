namespace CRM.Core.Models;

public sealed class Contact
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string EmailAddress { get; set; } = string.Empty;
}
