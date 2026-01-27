namespace CRM.Web.Models;

public sealed class ComposeEmailViewModel
{
    public string From { get; set; } = string.Empty;
    public string To { get; set; } = string.Empty;
    public string Cc { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public Guid? AccountGuid { get; set; }
}
