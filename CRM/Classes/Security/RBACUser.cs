namespace CRM.Classes.Security;

public sealed class RBACUser
{
    public int User_Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
}
