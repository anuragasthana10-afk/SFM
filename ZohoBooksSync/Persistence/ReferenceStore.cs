using Microsoft.Data.SqlClient;

namespace ZohoBooksSync.Persistence;

public sealed class ReferenceStore
{
    private const string ItemsCategory = "Items";
    private const string ContactsCategory = "Contacts";
    private const string InvoicesCategory = "Invoices";
    private const string ReportingTagOptionsCategory = "ReportingTagOptions";
    private const string SyncLogTable = "ZohoBooks_SyncOperationLog";
    private const string ReferenceTable = "ZohoBooks_ReferenceStore";

    private readonly string _connectionString;

    public ReferenceStore(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        var sql = $"""
            IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = '{ReferenceTable}')
            BEGIN
                CREATE TABLE {ReferenceTable} (
                    Category NVARCHAR(64) NOT NULL,
                    LocalKey NVARCHAR(256) NOT NULL,
                    RemoteId NVARCHAR(256) NOT NULL,
                    CONSTRAINT PK_ZohoBooks_ReferenceStore PRIMARY KEY (Category, LocalKey)
                );
            END
            IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = '{SyncLogTable}')
            BEGIN
                CREATE TABLE {SyncLogTable} (
                    Id INT IDENTITY(1,1) PRIMARY KEY,
                    EntityType NVARCHAR(64) NOT NULL,
                    LocalKey NVARCHAR(256) NOT NULL,
                    Operation NVARCHAR(128) NOT NULL,
                    Success BIT NOT NULL,
                    RemoteId NVARCHAR(256) NULL,
                    ErrorMessage NVARCHAR(MAX) NULL,
                    OccurredAtUtc DATETIME2 NOT NULL
                );
            END
            """;

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public Task<Dictionary<string, string>> GetItemIdsAsync(IEnumerable<string> localIds, CancellationToken cancellationToken = default)
        => GetReferenceIdsAsync(ItemsCategory, localIds, cancellationToken);

    public Task<Dictionary<string, string>> GetContactIdsAsync(IEnumerable<string> localIds, CancellationToken cancellationToken = default)
        => GetReferenceIdsAsync(ContactsCategory, localIds, cancellationToken);

    public Task<Dictionary<string, string>> GetInvoiceIdsAsync(IEnumerable<string> localIds, CancellationToken cancellationToken = default)
        => GetReferenceIdsAsync(InvoicesCategory, localIds, cancellationToken);

    public Task<string?> GetReportingTagOptionIdAsync(string key, CancellationToken cancellationToken = default)
        => GetReferenceIdAsync(ReportingTagOptionsCategory, key, cancellationToken);

    public Task SetItemIdAsync(string localId, string remoteId, CancellationToken cancellationToken = default)
        => SetReferenceIdAsync(ItemsCategory, localId, remoteId, cancellationToken);

    public Task SetContactIdAsync(string localId, string remoteId, CancellationToken cancellationToken = default)
        => SetReferenceIdAsync(ContactsCategory, localId, remoteId, cancellationToken);

    public Task SetInvoiceIdAsync(string localId, string remoteId, CancellationToken cancellationToken = default)
        => SetReferenceIdAsync(InvoicesCategory, localId, remoteId, cancellationToken);

    public Task SetReportingTagOptionIdAsync(string key, string remoteId, CancellationToken cancellationToken = default)
        => SetReferenceIdAsync(ReportingTagOptionsCategory, key, remoteId, cancellationToken);

    public async Task LogSyncOperationAsync(
        string entityType,
        string localKey,
        string operation,
        bool success,
        string? remoteId,
        string? errorMessage,
        CancellationToken cancellationToken = default)
    {
        var sql = $"""
            INSERT INTO {SyncLogTable} (
                EntityType,
                LocalKey,
                Operation,
                Success,
                RemoteId,
                ErrorMessage,
                OccurredAtUtc
            )
            VALUES (
                @EntityType,
                @LocalKey,
                @Operation,
                @Success,
                @RemoteId,
                @ErrorMessage,
                @OccurredAtUtc
            );
            """;

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@EntityType", entityType);
        command.Parameters.AddWithValue("@LocalKey", localKey);
        command.Parameters.AddWithValue("@Operation", operation);
        command.Parameters.AddWithValue("@Success", success);
        command.Parameters.AddWithValue("@RemoteId", string.IsNullOrWhiteSpace(remoteId) ? DBNull.Value : remoteId);
        command.Parameters.AddWithValue("@ErrorMessage", string.IsNullOrWhiteSpace(errorMessage) ? DBNull.Value : errorMessage);
        command.Parameters.AddWithValue("@OccurredAtUtc", DateTime.UtcNow);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task<string?> GetReferenceIdAsync(string category, string localKey, CancellationToken cancellationToken)
    {
        var sql = $"""
            SELECT RemoteId
            FROM {ReferenceTable}
            WHERE Category = @Category AND LocalKey = @LocalKey;
            """;

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@Category", category);
        command.Parameters.AddWithValue("@LocalKey", localKey);
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result as string;
    }

    private async Task<Dictionary<string, string>> GetReferenceIdsAsync(
        string category,
        IEnumerable<string> localKeys,
        CancellationToken cancellationToken)
    {
        var keys = localKeys.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (keys.Length == 0)
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        var parameters = keys
            .Select((_, index) => $"@Key{index}")
            .ToArray();

        var sql = $"""
            SELECT LocalKey, RemoteId
            FROM {ReferenceTable}
            WHERE Category = @Category AND LocalKey IN ({string.Join(", ", parameters)});
            """;

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@Category", category);
        for (var i = 0; i < keys.Length; i++)
        {
            command.Parameters.AddWithValue(parameters[i], keys[i]);
        }

        var results = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results[reader.GetString(0)] = reader.GetString(1);
        }

        return results;
    }

    private async Task SetReferenceIdAsync(string category, string localKey, string remoteId, CancellationToken cancellationToken)
    {
        var sql = $"""
            MERGE {ReferenceTable} AS target
            USING (SELECT @Category AS Category, @LocalKey AS LocalKey, @RemoteId AS RemoteId) AS source
            ON target.Category = source.Category AND target.LocalKey = source.LocalKey
            WHEN MATCHED THEN
                UPDATE SET RemoteId = source.RemoteId
            WHEN NOT MATCHED THEN
                INSERT (Category, LocalKey, RemoteId)
                VALUES (source.Category, source.LocalKey, source.RemoteId);
            """;

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@Category", category);
        command.Parameters.AddWithValue("@LocalKey", localKey);
        command.Parameters.AddWithValue("@RemoteId", remoteId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
