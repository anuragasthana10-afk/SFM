namespace CRM.Classes.ExternalServices.Zoho.Books.Persistence
{
    public sealed class SyncOperationRecord
    {
        public string EntityType { get; set; }
        public int LocalKey { get; set; }
        public string LocalKeyText { get; set; }
        public byte Location { get; set; }
        public string Operation { get; set; }
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
    }
}
