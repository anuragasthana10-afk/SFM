namespace CRM.Core.Models;

public sealed class EmailParticipant
{
    public string Address { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public string ParticipantType { get; set; } = "Unknown";
}
