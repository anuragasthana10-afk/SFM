using CRM.Core.Models;

namespace CRM.Core.Storage;

public interface IContactStore
{
    Task<IReadOnlyList<Contact>> GetAllAsync(CancellationToken cancellationToken);
    Task<Contact?> GetByIdAsync(int id, CancellationToken cancellationToken);
    Task<IReadOnlyList<int>> GetAccountIdsForContactAsync(int contactId, CancellationToken cancellationToken);
    Task AddAsync(Contact contact, IReadOnlyList<int> accountIds, CancellationToken cancellationToken);
    Task UpdateAsync(Contact contact, IReadOnlyList<int> accountIds, CancellationToken cancellationToken);
    Task DeleteAsync(int id, CancellationToken cancellationToken);
}
