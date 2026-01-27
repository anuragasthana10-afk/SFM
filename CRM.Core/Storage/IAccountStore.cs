using CRM.Core.Models;

namespace CRM.Core.Storage;

public interface IAccountStore
{
    Task<IReadOnlyList<Account>> GetAllAsync(CancellationToken cancellationToken);
    Task<Account?> GetByIdAsync(int id, CancellationToken cancellationToken);
    Task AddAsync(Account account, CancellationToken cancellationToken);
}
