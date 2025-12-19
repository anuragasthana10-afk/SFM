namespace ZohoBooksSync.Persistence;

public interface IReferenceStore
{
    Task<string?> GetItemReferenceAsync(string localItemId, CancellationToken cancellationToken = default);
    Task<string?> GetInvoiceReferenceAsync(string localInvoiceId, CancellationToken cancellationToken = default);
    Task SaveItemReferenceAsync(string localItemId, string zohoItemId, CancellationToken cancellationToken = default);
    Task SaveInvoiceReferenceAsync(string localInvoiceId, string zohoInvoiceId, CancellationToken cancellationToken = default);
}
