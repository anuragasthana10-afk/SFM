namespace ZohoBooksSync.Models;

public sealed class InvoicePayment
{
    public required string PaymentId { get; init; }
    public decimal Amount { get; init; }
    public DateTime Date { get; init; }
    public string? PaymentMode { get; init; }
    public string? Description { get; init; }
}
