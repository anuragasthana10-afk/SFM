namespace ZohoBooksSync.Models
{
    public sealed class Invoice
    {
        public int LocalId { get; set; }
        public int ContactLocalId { get; set; }
        public DateTime InvoiceDate { get; set; }
        public IReadOnlyList<InvoiceLineItem> LineItems { get; set; }
        public string CurrencyCode { get; set; }
        public string Jurisdiction { get; set; }
        public string RelationshipManager { get; set; }
        public string Notes { get; set; }
    }

    public sealed class InvoiceLineItem
    {
        public int ItemLocalId { get; set; }
        public string Description { get; set; }
        public decimal Rate { get; set; }
        public int Quantity { get; set; }
    }
}
