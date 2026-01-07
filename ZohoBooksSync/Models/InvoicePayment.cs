namespace ZohoBooksSync.Models
{
    public sealed class InvoicePayment
    {
        public string PaymentId { get; set; }
        public decimal Amount { get; set; }
        public DateTime Date { get; set; }
        public string PaymentMode { get; set; }
        public string Description { get; set; }
    }
}
