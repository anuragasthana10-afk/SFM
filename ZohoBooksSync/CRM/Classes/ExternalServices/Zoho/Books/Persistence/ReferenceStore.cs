using System.Data;
using System.Data.Common;
using System.Data.Entity;

namespace CRM.Classes.ExternalServices.Zoho.Books.Persistence
{
public sealed class ReferenceStore
{
    private enum ReferenceCategory
    {
        Items,
        Contacts,
        Invoices
    }

    public enum SyncEntityType
    {
        InventoryItem,
        Contact,
        Invoice,
        ReportingTagOption,
        ReportingTag
    }

    private const string SyncLogTable = "ZohoBooks_SyncOperationLog";
    private const string ReferenceTable = "ZohoBooks_ReferenceStore";
    private const string ReportingTagOptionTable = "ZohoBooks_ReportingTagOptions";
    private const string CurrencyReferenceTable = "ZohoBooks_CurrencyReferences";

    private readonly Database _database;

    public ReferenceStore(Database database)
    {
        _database = database;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        var sql = $@"
            IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = '{ReferenceTable}')
            BEGIN
                CREATE TABLE {ReferenceTable} (
                    EntityType NVARCHAR(32) NOT NULL,
                    LocalKey INT NOT NULL,
                    RemoteId NVARCHAR(100) NOT NULL,
                    CONSTRAINT PK_ZohoBooks_ReferenceStore PRIMARY KEY (EntityType, LocalKey)
                );
            END
            IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = '{SyncLogTable}')
            BEGIN
                CREATE TABLE {SyncLogTable} (
                    Id INT IDENTITY(1,1) PRIMARY KEY,
                    EntityType NVARCHAR(32) NOT NULL,
                    LocalKey INT NOT NULL,
                    LocalKeyText NVARCHAR(128) NULL,
                    Operation NVARCHAR(128) NOT NULL,
                    Success BIT NOT NULL,
                    RemoteId NVARCHAR(100) NULL,
                    ErrorMessage NVARCHAR(MAX) NULL,
                    OccurredAtUtc DATETIME2 NOT NULL
                );
            END
            IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = '{ReportingTagOptionTable}')
            BEGIN
                CREATE TABLE {ReportingTagOptionTable} (
                    OptionKey NVARCHAR(256) NOT NULL PRIMARY KEY,
                    RemoteId NVARCHAR(256) NOT NULL
                );
            END
            IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = '{CurrencyReferenceTable}')
            BEGIN
                CREATE TABLE {CurrencyReferenceTable} (
                    CurrencyCode NVARCHAR(16) NOT NULL PRIMARY KEY,
                    RemoteId NVARCHAR(100) NOT NULL
                );
            END";

        await EnsureConnectionOpenAsync(cancellationToken);
        using (var command = CreateCommand(sql))
        {
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    public Task<Dictionary<int, string>> GetItemIdsAsync(IEnumerable<int> localIds, CancellationToken cancellationToken = default)
        => GetReferenceIdsAsync(ReferenceCategory.Items.ToString(), localIds, cancellationToken);

    public Task<Dictionary<int, string>> GetContactIdsAsync(IEnumerable<int> localIds, CancellationToken cancellationToken = default)
        => GetReferenceIdsAsync(ReferenceCategory.Contacts.ToString(), localIds, cancellationToken);

    public Task<Dictionary<int, string>> GetInvoiceIdsAsync(IEnumerable<int> localIds, CancellationToken cancellationToken = default)
        => GetReferenceIdsAsync(ReferenceCategory.Invoices.ToString(), localIds, cancellationToken);

    public Task<string> GetReportingTagOptionIdAsync(string key, CancellationToken cancellationToken = default)
        => GetReportingTagOptionIdInternalAsync(key, cancellationToken);

    public Task SetItemIdAsync(int localId, string remoteId, CancellationToken cancellationToken = default)
        => SetReferenceIdAsync(ReferenceCategory.Items.ToString(), localId, remoteId, cancellationToken);

    public Task SetContactIdAsync(int localId, string remoteId, CancellationToken cancellationToken = default)
        => SetReferenceIdAsync(ReferenceCategory.Contacts.ToString(), localId, remoteId, cancellationToken);

    public Task SetInvoiceIdAsync(int localId, string remoteId, CancellationToken cancellationToken = default)
        => SetReferenceIdAsync(ReferenceCategory.Invoices.ToString(), localId, remoteId, cancellationToken);

    public Task SetReportingTagOptionIdAsync(string key, string remoteId, CancellationToken cancellationToken = default)
        => SetReportingTagOptionIdInternalAsync(key, remoteId, cancellationToken);

    public Task<string> GetCurrencyIdAsync(string currencyCode, CancellationToken cancellationToken = default)
        => GetCurrencyIdInternalAsync(currencyCode, cancellationToken);

    public Task SetCurrencyIdAsync(string currencyCode, string remoteId, CancellationToken cancellationToken = default)
        => SetCurrencyIdInternalAsync(currencyCode, remoteId, cancellationToken);

    public async Task LogSyncOperationAsync(
        string entityType,
        int localKey,
        string operation,
        bool success,
        string remoteId,
        string errorMessage,
        string localKeyText,
        CancellationToken cancellationToken = default)
    {
        var sql = $@"
            INSERT INTO {SyncLogTable} (
                EntityType,
                LocalKey,
                LocalKeyText,
                Operation,
                Success,
                RemoteId,
                ErrorMessage,
                OccurredAtUtc
            )
            VALUES (
                @EntityType,
                @LocalKey,
                @LocalKeyText,
                @Operation,
                @Success,
                @RemoteId,
                @ErrorMessage,
                @OccurredAtUtc
            );";

        await EnsureConnectionOpenAsync(cancellationToken);
        using (var command = CreateCommand(sql))
        {
            AddParameter(command, "@EntityType", entityType);
            AddParameter(command, "@LocalKey", localKey);
            AddParameter(command, "@LocalKeyText", string.IsNullOrWhiteSpace(localKeyText) ? (object)DBNull.Value : localKeyText);
            AddParameter(command, "@Operation", operation);
            AddParameter(command, "@Success", success);
            AddParameter(command, "@RemoteId", string.IsNullOrWhiteSpace(remoteId) ? (object)DBNull.Value : remoteId);
            AddParameter(command, "@ErrorMessage", string.IsNullOrWhiteSpace(errorMessage) ? (object)DBNull.Value : errorMessage);
            AddParameter(command, "@OccurredAtUtc", DateTime.UtcNow);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private async Task<string> GetReferenceIdAsync(string category, int localKey, CancellationToken cancellationToken)
    {
        var sql = $@"
            SELECT RemoteId
            FROM {ReferenceTable}
            WHERE EntityType = @EntityType AND LocalKey = @LocalKey;";

        await EnsureConnectionOpenAsync(cancellationToken);
        using (var command = CreateCommand(sql))
        {
            AddParameter(command, "@EntityType", category);
            AddParameter(command, "@LocalKey", localKey);
            var result = await command.ExecuteScalarAsync(cancellationToken);
            return result == null || result == DBNull.Value ? null : result.ToString();
        }
    }

    private async Task<Dictionary<int, string>> GetReferenceIdsAsync(
        string category,
        IEnumerable<int> localKeys,
        CancellationToken cancellationToken)
    {
        var keys = localKeys.Distinct().ToArray();
        if (keys.Length == 0)
        {
            return new Dictionary<int, string>();
        }

        var parameters = keys
            .Select((_, index) => $"@Key{index}")
            .ToArray();

        var sql = $@"
            SELECT LocalKey, RemoteId
            FROM {ReferenceTable}
            WHERE EntityType = @EntityType AND LocalKey IN ({string.Join(", ", parameters)});";

        await EnsureConnectionOpenAsync(cancellationToken);
        using (var command = CreateCommand(sql))
        {
            AddParameter(command, "@EntityType", category);
            for (var i = 0; i < keys.Length; i++)
            {
                AddParameter(command, parameters[i], keys[i]);
            }

            var results = new Dictionary<int, string>();
            using (var reader = await command.ExecuteReaderAsync(cancellationToken))
            {
                while (await reader.ReadAsync(cancellationToken))
                {
                    results[reader.GetInt32(0)] = reader.GetString(1);
                }
            }

            return results;
        }
    }

    private async Task SetReferenceIdAsync(string category, int localKey, string remoteId, CancellationToken cancellationToken)
    {
        var sql = $@"
            MERGE {ReferenceTable} AS target
            USING (SELECT @EntityType AS EntityType, @LocalKey AS LocalKey, @RemoteId AS RemoteId) AS source
            ON target.EntityType = source.EntityType AND target.LocalKey = source.LocalKey
            WHEN MATCHED THEN
                UPDATE SET RemoteId = source.RemoteId
            WHEN NOT MATCHED THEN
                INSERT (EntityType, LocalKey, RemoteId)
                VALUES (source.EntityType, source.LocalKey, source.RemoteId);";

        await EnsureConnectionOpenAsync(cancellationToken);
        using (var command = CreateCommand(sql))
        {
            AddParameter(command, "@EntityType", category);
            AddParameter(command, "@LocalKey", localKey);
            AddParameter(command, "@RemoteId", remoteId);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private async Task<string> GetReportingTagOptionIdInternalAsync(string optionKey, CancellationToken cancellationToken)
    {
        var sql = $@"
            SELECT RemoteId
            FROM {ReportingTagOptionTable}
            WHERE OptionKey = @OptionKey;";

        await EnsureConnectionOpenAsync(cancellationToken);
        using (var command = CreateCommand(sql))
        {
            AddParameter(command, "@OptionKey", optionKey);
            var result = await command.ExecuteScalarAsync(cancellationToken);
            return result == null || result == DBNull.Value ? null : result.ToString();
        }
    }

    private async Task SetReportingTagOptionIdInternalAsync(string optionKey, string remoteId, CancellationToken cancellationToken)
    {
        var sql = $@"
            MERGE {ReportingTagOptionTable} AS target
            USING (SELECT @OptionKey AS OptionKey, @RemoteId AS RemoteId) AS source
            ON target.OptionKey = source.OptionKey
            WHEN MATCHED THEN
                UPDATE SET RemoteId = source.RemoteId
            WHEN NOT MATCHED THEN
                INSERT (OptionKey, RemoteId)
                VALUES (source.OptionKey, source.RemoteId);";

        await EnsureConnectionOpenAsync(cancellationToken);
        using (var command = CreateCommand(sql))
        {
            AddParameter(command, "@OptionKey", optionKey);
            AddParameter(command, "@RemoteId", remoteId);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private async Task<string> GetCurrencyIdInternalAsync(string currencyCode, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(currencyCode))
        {
            return null;
        }

        var sql = $@"
            SELECT RemoteId
            FROM {CurrencyReferenceTable}
            WHERE CurrencyCode = @CurrencyCode;";

        await EnsureConnectionOpenAsync(cancellationToken);
        using (var command = CreateCommand(sql))
        {
            AddParameter(command, "@CurrencyCode", currencyCode);
            var result = await command.ExecuteScalarAsync(cancellationToken);
            return result == null || result == DBNull.Value ? null : result.ToString();
        }
    }

    private async Task SetCurrencyIdInternalAsync(string currencyCode, string remoteId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(currencyCode) || string.IsNullOrWhiteSpace(remoteId))
        {
            return;
        }

        var sql = $@"
            MERGE {CurrencyReferenceTable} AS target
            USING (SELECT @CurrencyCode AS CurrencyCode, @RemoteId AS RemoteId) AS source
            ON target.CurrencyCode = source.CurrencyCode
            WHEN MATCHED THEN
                UPDATE SET RemoteId = source.RemoteId
            WHEN NOT MATCHED THEN
                INSERT (CurrencyCode, RemoteId)
                VALUES (source.CurrencyCode, source.RemoteId);";

        await EnsureConnectionOpenAsync(cancellationToken);
        using (var command = CreateCommand(sql))
        {
            AddParameter(command, "@CurrencyCode", currencyCode);
            AddParameter(command, "@RemoteId", remoteId);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private async Task EnsureConnectionOpenAsync(CancellationToken cancellationToken)
    {
        if (_database.Connection.State != ConnectionState.Open)
        {
            await _database.Connection.OpenAsync(cancellationToken);
        }
    }

    private DbCommand CreateCommand(string sql)
    {
        var command = _database.Connection.CreateCommand();
        command.CommandText = sql;
        return command;
    }

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}
}
