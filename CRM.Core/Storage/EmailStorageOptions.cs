namespace CRM.Core.Storage;

public sealed class EmailStorageOptions
{
    public bool StoreAttachments { get; set; }
    public int? MaxAttachmentSizeMb { get; set; }
    public List<string> AllowedAttachmentTypes { get; set; } = new();
}
