using CRM.Core.Models;
using CRM.Core.Storage;
using Microsoft.Data.SqlClient;

namespace CRM.Email.Persistence;

public sealed class SqlContactStore : IContactStore
{
    private readonly SqlConnectionFactory _connectionFactory;

    public SqlContactStore(SqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IReadOnlyList<Contact>> GetAllAsync(CancellationToken cancellationToken)
    {
        var results = new List<Contact>();
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var command = new SqlCommand("SELECT Id, Name, EmailAddress FROM dbo.Messaging_Contacts ORDER BY Id", connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new Contact
            {
                Id = reader.GetInt32(0),
                Name = reader.GetString(1),
                EmailAddress = reader.GetString(2)
            });
        }

        return results;
    }

    public async Task<Contact?> GetByIdAsync(int id, CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var command = new SqlCommand("SELECT Id, Name, EmailAddress FROM dbo.Messaging_Contacts WHERE Id = @Id", connection);
        command.Parameters.AddWithValue("@Id", id);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new Contact
        {
            Id = reader.GetInt32(0),
            Name = reader.GetString(1),
            EmailAddress = reader.GetString(2)
        };
    }

    public async Task<IReadOnlyList<int>> GetAccountIdsForContactAsync(int contactId, CancellationToken cancellationToken)
    {
        var results = new List<int>();
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var command = new SqlCommand("SELECT AccountId FROM dbo.Messaging_ContactAccounts WHERE ContactId = @ContactId ORDER BY AccountId", connection);
        command.Parameters.AddWithValue("@ContactId", contactId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(reader.GetInt32(0));
        }

        return results;
    }

    public async Task AddAsync(Contact contact, IReadOnlyList<int> accountIds, CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var insert = new SqlCommand(@"INSERT INTO dbo.Messaging_Contacts (Name, EmailAddress) OUTPUT INSERTED.Id VALUES (@Name, @EmailAddress)", connection);
        insert.Parameters.AddWithValue("@Name", contact.Name);
        insert.Parameters.AddWithValue("@EmailAddress", contact.EmailAddress.Trim().ToLowerInvariant());
        var contactId = (int)(await insert.ExecuteScalarAsync(cancellationToken) ?? 0);

        foreach (var accountId in accountIds.Distinct())
        {
            var map = new SqlCommand("INSERT INTO dbo.Messaging_ContactAccounts (ContactId, AccountId) VALUES (@ContactId, @AccountId)", connection);
            map.Parameters.AddWithValue("@ContactId", contactId);
            map.Parameters.AddWithValue("@AccountId", accountId);
            await map.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    public async Task UpdateAsync(Contact contact, IReadOnlyList<int> accountIds, CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var update = new SqlCommand("UPDATE dbo.Messaging_Contacts SET Name=@Name, EmailAddress=@EmailAddress WHERE Id=@Id", connection);
        update.Parameters.AddWithValue("@Id", contact.Id);
        update.Parameters.AddWithValue("@Name", contact.Name);
        update.Parameters.AddWithValue("@EmailAddress", contact.EmailAddress.Trim().ToLowerInvariant());
        await update.ExecuteNonQueryAsync(cancellationToken);

        var deleteMap = new SqlCommand("DELETE FROM dbo.Messaging_ContactAccounts WHERE ContactId = @ContactId", connection);
        deleteMap.Parameters.AddWithValue("@ContactId", contact.Id);
        await deleteMap.ExecuteNonQueryAsync(cancellationToken);

        foreach (var accountId in accountIds.Distinct())
        {
            var map = new SqlCommand("INSERT INTO dbo.Messaging_ContactAccounts (ContactId, AccountId) VALUES (@ContactId, @AccountId)", connection);
            map.Parameters.AddWithValue("@ContactId", contact.Id);
            map.Parameters.AddWithValue("@AccountId", accountId);
            await map.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var command = new SqlCommand("DELETE FROM dbo.Messaging_ContactAccounts WHERE ContactId=@Id; DELETE FROM dbo.Messaging_Contacts WHERE Id=@Id;", connection);
        command.Parameters.AddWithValue("@Id", id);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
