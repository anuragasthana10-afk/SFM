namespace CRM.Classes.ExternalServices.Zoho.Books.Models
{
    public sealed class Invoice
    {
        public int LocalId { get; set; }
        public int ContactLocalId { get; set; }
        public DateTime InvoiceDate { get; set; }
        public IReadOnlyList<InvoiceLineItem> LineItems { get; set; }
        public string CurrencyCode { get; set; }
        public string InvoiceNumber { get; set; }
        public string Jurisdiction { get; set; }
        public string RelationshipManager { get; set; }
        public string Subject { get; set; }
        public string Notes { get; set; }
        public string TaxId { get; set; }
        public string TaxName { get; set; }
        public decimal? TaxPercentage { get; set; }
        public string TaxTreatment { get; set; }
        public string PlaceOfSupply { get; set; }
        public string Reason { get; set; }
        public string InvoiceFileAttachment_FileName { get; set; }
    }

    public sealed class InvoiceLineItem
    {
        public int ItemLocalId { get; set; }
        public string Description { get; set; }
        public string AccountCode { get; set; }
        public decimal Rate { get; set; }
        public int Quantity { get; set; }
        public decimal? Discount { get; set; }
        public string TaxId { get; set; }
        public string TaxName { get; set; }
        public decimal? TaxPercentage { get; set; }
    }
}
