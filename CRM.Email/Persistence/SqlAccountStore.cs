using CRM.Core.Models;
using CRM.Core.Storage;
using Microsoft.Data.SqlClient;

namespace CRM.Email.Persistence;

public sealed class SqlAccountStore : IAccountStore
{
    private readonly SqlConnectionFactory _connectionFactory;

    public SqlAccountStore(SqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IReadOnlyList<Account>> GetAllAsync(CancellationToken cancellationToken)
    {
        var results = new List<Account>();

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var command = new SqlCommand("SELECT Id, Name, AccountGuid FROM dbo.Messaging_Accounts ORDER BY Id", connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new Account
            {
                Id = reader.GetInt32(0),
                Name = reader.GetString(1),
                AccountGuid = reader.GetGuid(2)
            });
        }

        return results;
    }

    public async Task<Account?> GetByIdAsync(int id, CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var command = new SqlCommand("SELECT Id, Name, AccountGuid FROM dbo.Messaging_Accounts WHERE Id = @Id", connection);
        command.Parameters.AddWithValue("@Id", id);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
        {
            return new Account
            {
                Id = reader.GetInt32(0),
                Name = reader.GetString(1),
                AccountGuid = reader.GetGuid(2)
            };
        }

        return null;
    }

    public async Task AddAsync(Account account, CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var command = new SqlCommand(@"INSERT INTO dbo.Messaging_Accounts (Name, AccountGuid) VALUES (@Name, @AccountGuid);", connection);
        command.Parameters.AddWithValue("@Name", account.Name);
        command.Parameters.AddWithValue("@AccountGuid", account.AccountGuid == Guid.Empty ? Guid.NewGuid() : account.AccountGuid);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
