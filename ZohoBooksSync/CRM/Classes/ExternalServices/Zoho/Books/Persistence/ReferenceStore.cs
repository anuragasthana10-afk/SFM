using System;
using System.Data;
using System.Data.Common;
using System.Data.Entity;
using System.Linq;

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
    private const string ReportingTagTable = "ZohoBooks_ReportingTags";
    private const string CurrencyReferenceTable = "ZohoBooks_CurrencyReferences";

    private readonly Database _database;
    private readonly string _location;

    public ReferenceStore(Database database, string location)
    {
        _database = database;
        _location = string.IsNullOrWhiteSpace(location)
            ? throw new ArgumentException("Location is required for reference storage.", nameof(location))
            : location.Trim().ToLowerInvariant();
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        var sql = $@"
            IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = '{ReferenceTable}')
            BEGIN
                CREATE TABLE {ReferenceTable} (
                    EntityType NVARCHAR(32) NOT NULL,
                    LocalKey INT NOT NULL,
                    Location NVARCHAR(16) NOT NULL,
                    RemoteId NVARCHAR(100) NOT NULL,
                    CONSTRAINT PK_ZohoBooks_ReferenceStore PRIMARY KEY (EntityType, LocalKey, Location)
                );
            END
            IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = '{SyncLogTable}')
            BEGIN
                CREATE TABLE {SyncLogTable} (
                    Id INT IDENTITY(1,1) PRIMARY KEY,
                    EntityType NVARCHAR(32) NOT NULL,
                    LocalKey INT NOT NULL,
                    LocalKeyText NVARCHAR(64) NULL,
                    Location NVARCHAR(16) NOT NULL,
                    Operation NVARCHAR(16) NOT NULL,
                    Success BIT NOT NULL,
                    RemoteId NVARCHAR(100) NULL,
                    ErrorMessage NVARCHAR(MAX) NULL,
                    OccurredAtUtc DATETIME2 NOT NULL
                );
            END
            IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = '{ReportingTagOptionTable}')
            BEGIN
                CREATE TABLE {ReportingTagOptionTable} (
                    OptionKey NVARCHAR(256) NOT NULL,
                    Location NVARCHAR(16) NOT NULL,
                    RemoteId NVARCHAR(256) NOT NULL,
                    CONSTRAINT PK_ZohoBooks_ReportingTagOptions PRIMARY KEY (OptionKey, Location)
                );
            END
            IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = '{ReportingTagTable}')
            BEGIN
                CREATE TABLE {ReportingTagTable} (
                    TagName NVARCHAR(128) NOT NULL,
                    Location NVARCHAR(16) NOT NULL,
                    TagId NVARCHAR(100) NOT NULL,
                    CONSTRAINT PK_ZohoBooks_ReportingTags PRIMARY KEY (TagName, Location)
                );
            END
            IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = '{CurrencyReferenceTable}')
            BEGIN
                CREATE TABLE {CurrencyReferenceTable} (
                    CurrencyCode NVARCHAR(16) NOT NULL,
                    Location NVARCHAR(16) NOT NULL,
                    RemoteId NVARCHAR(100) NOT NULL,
                    CONSTRAINT PK_ZohoBooks_CurrencyReferences PRIMARY KEY (CurrencyCode, Location)
                );
            END";

        await EnsureConnectionOpenAsync(cancellationToken);
        using (var command = CreateCommand(sql))
        {
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await EnsureLocationColumnAsync(ReferenceTable, true, cancellationToken);
        await EnsureLocationColumnAsync(SyncLogTable, false, cancellationToken);
        await EnsureLocationColumnAsync(ReportingTagOptionTable, true, cancellationToken);
        await EnsureLocationColumnAsync(ReportingTagTable, true, cancellationToken);
        await EnsureLocationColumnAsync(CurrencyReferenceTable, true, cancellationToken);

        await EnsurePrimaryKeyIncludesLocationAsync(ReferenceTable, "PK_ZohoBooks_ReferenceStore", "EntityType, LocalKey", cancellationToken);
        await EnsurePrimaryKeyIncludesLocationAsync(ReportingTagOptionTable, "PK_ZohoBooks_ReportingTagOptions", "OptionKey", cancellationToken);
        await EnsurePrimaryKeyIncludesLocationAsync(ReportingTagTable, "PK_ZohoBooks_ReportingTags", "TagName", cancellationToken);
        await EnsurePrimaryKeyIncludesLocationAsync(CurrencyReferenceTable, "PK_ZohoBooks_CurrencyReferences", "CurrencyCode", cancellationToken);

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

    public Task<string> GetReportingTagIdAsync(string tagName, CancellationToken cancellationToken = default)
        => GetReportingTagIdInternalAsync(tagName, cancellationToken);

    public Task SetReportingTagIdAsync(string tagName, string tagId, CancellationToken cancellationToken = default)
        => SetReportingTagIdInternalAsync(tagName, tagId, cancellationToken);

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
                Location,
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
                @Location,
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
            AddParameter(command, "@Location", _location);
            AddParameter(command, "@Operation", operation);
            AddParameter(command, "@Success", success);
            AddParameter(command, "@RemoteId", string.IsNullOrWhiteSpace(remoteId) ? (object)DBNull.Value : remoteId);
            AddParameter(command, "@ErrorMessage", string.IsNullOrWhiteSpace(errorMessage) ? (object)DBNull.Value : errorMessage);
            AddParameter(command, "@OccurredAtUtc", DateTime.UtcNow);
            await command.ExecuteNonQueryAsync(cancellationToken);
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
            WHERE EntityType = @EntityType AND Location = @Location AND LocalKey IN ({string.Join(", ", parameters)});";

        await EnsureConnectionOpenAsync(cancellationToken);
        using (var command = CreateCommand(sql))
        {
            AddParameter(command, "@EntityType", category);
            AddParameter(command, "@Location", _location);
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
            USING (SELECT @EntityType AS EntityType, @LocalKey AS LocalKey, @Location AS Location, @RemoteId AS RemoteId) AS source
            ON target.EntityType = source.EntityType AND target.LocalKey = source.LocalKey AND target.Location = source.Location
            WHEN MATCHED THEN
                UPDATE SET RemoteId = source.RemoteId
            WHEN NOT MATCHED THEN
                INSERT (EntityType, LocalKey, Location, RemoteId)
                VALUES (source.EntityType, source.LocalKey, source.Location, source.RemoteId);";

        await EnsureConnectionOpenAsync(cancellationToken);
        using (var command = CreateCommand(sql))
        {
            AddParameter(command, "@EntityType", category);
            AddParameter(command, "@LocalKey", localKey);
            AddParameter(command, "@Location", _location);
            AddParameter(command, "@RemoteId", remoteId);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private async Task<string> GetReportingTagOptionIdInternalAsync(string optionKey, CancellationToken cancellationToken)
    {
        var sql = $@"
            SELECT RemoteId
            FROM {ReportingTagOptionTable}
            WHERE OptionKey = @OptionKey AND Location = @Location;";

        await EnsureConnectionOpenAsync(cancellationToken);
        using (var command = CreateCommand(sql))
        {
            AddParameter(command, "@OptionKey", optionKey);
            AddParameter(command, "@Location", _location);
            var result = await command.ExecuteScalarAsync(cancellationToken);
            return result == null || result == DBNull.Value ? null : result.ToString();
        }
    }

    private async Task SetReportingTagOptionIdInternalAsync(string optionKey, string remoteId, CancellationToken cancellationToken)
    {
        var sql = $@"
            MERGE {ReportingTagOptionTable} AS target
            USING (SELECT @OptionKey AS OptionKey, @Location AS Location, @RemoteId AS RemoteId) AS source
            ON target.OptionKey = source.OptionKey AND target.Location = source.Location
            WHEN MATCHED THEN
                UPDATE SET RemoteId = source.RemoteId
            WHEN NOT MATCHED THEN
                INSERT (OptionKey, Location, RemoteId)
                VALUES (source.OptionKey, source.Location, source.RemoteId);";

        await EnsureConnectionOpenAsync(cancellationToken);
        using (var command = CreateCommand(sql))
        {
            AddParameter(command, "@OptionKey", optionKey);
            AddParameter(command, "@Location", _location);
            AddParameter(command, "@RemoteId", remoteId);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private async Task<string> GetReportingTagIdInternalAsync(string tagName, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(tagName))
        {
            return null;
        }

        var sql = $@"
            SELECT TagId
            FROM {ReportingTagTable}
            WHERE TagName = @TagName AND Location = @Location;";

        await EnsureConnectionOpenAsync(cancellationToken);
        using (var command = CreateCommand(sql))
        {
            AddParameter(command, "@TagName", tagName);
            AddParameter(command, "@Location", _location);
            var result = await command.ExecuteScalarAsync(cancellationToken);
            return result == null || result == DBNull.Value ? null : result.ToString();
        }
    }

    private async Task SetReportingTagIdInternalAsync(string tagName, string tagId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(tagName) || string.IsNullOrWhiteSpace(tagId))
        {
            return;
        }

        var sql = $@"
            MERGE {ReportingTagTable} AS target
            USING (SELECT @TagName AS TagName, @Location AS Location, @TagId AS TagId) AS source
            ON target.TagName = source.TagName AND target.Location = source.Location
            WHEN MATCHED THEN
                UPDATE SET TagId = source.TagId
            WHEN NOT MATCHED THEN
                INSERT (TagName, Location, TagId)
                VALUES (source.TagName, source.Location, source.TagId);";

        await EnsureConnectionOpenAsync(cancellationToken);
        using (var command = CreateCommand(sql))
        {
            AddParameter(command, "@TagName", tagName);
            AddParameter(command, "@Location", _location);
            AddParameter(command, "@TagId", tagId);
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
            WHERE CurrencyCode = @CurrencyCode AND Location = @Location;";

        await EnsureConnectionOpenAsync(cancellationToken);
        using (var command = CreateCommand(sql))
        {
            AddParameter(command, "@CurrencyCode", currencyCode);
            AddParameter(command, "@Location", _location);
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
            USING (SELECT @CurrencyCode AS CurrencyCode, @Location AS Location, @RemoteId AS RemoteId) AS source
            ON target.CurrencyCode = source.CurrencyCode AND target.Location = source.Location
            WHEN MATCHED THEN
                UPDATE SET RemoteId = source.RemoteId
            WHEN NOT MATCHED THEN
                INSERT (CurrencyCode, Location, RemoteId)
                VALUES (source.CurrencyCode, source.Location, source.RemoteId);";

        await EnsureConnectionOpenAsync(cancellationToken);
        using (var command = CreateCommand(sql))
        {
            AddParameter(command, "@CurrencyCode", currencyCode);
            AddParameter(command, "@Location", _location);
            AddParameter(command, "@RemoteId", remoteId);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private async Task EnsureLocationColumnAsync(string tableName, bool updateExisting, CancellationToken cancellationToken)
    {
        var existsSql = @"
            SELECT COUNT(*)
            FROM sys.columns
            WHERE name = 'Location' AND object_id = OBJECT_ID(@TableName);";

        using (var command = CreateCommand(existsSql))
        {
            AddParameter(command, "@TableName", tableName);
            var result = await command.ExecuteScalarAsync(cancellationToken);
            var hasLocation = result != null && result != DBNull.Value && Convert.ToInt32(result) > 0;
            if (!hasLocation)
            {
                var addColumnSql = $"ALTER TABLE {tableName} ADD Location NVARCHAR(16) NOT NULL CONSTRAINT DF_{tableName}_Location DEFAULT ('');";
                using (var addCommand = CreateCommand(addColumnSql))
                {
                    await addCommand.ExecuteNonQueryAsync(cancellationToken);
                }
            }
        }

        if (!updateExisting)
        {
            return;
        }

        var updateSql = $@"
            UPDATE {tableName}
            SET Location = @Location
            WHERE Location IS NULL OR Location = '';";

        using (var command = CreateCommand(updateSql))
        {
            AddParameter(command, "@Location", _location);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private async Task EnsurePrimaryKeyIncludesLocationAsync(
        string tableName,
        string constraintName,
        string keyColumns,
        CancellationToken cancellationToken)
    {
        var hasLocationSql = @"
            SELECT COUNT(*)
            FROM sys.index_columns ic
            INNER JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
            INNER JOIN sys.key_constraints kc ON kc.parent_object_id = ic.object_id AND kc.unique_index_id = ic.index_id
            INNER JOIN sys.tables t ON t.object_id = ic.object_id
            WHERE t.name = @TableName AND kc.type = 'PK' AND c.name = 'Location';";

        using (var command = CreateCommand(hasLocationSql))
        {
            AddParameter(command, "@TableName", tableName);
            var result = await command.ExecuteScalarAsync(cancellationToken);
            var hasLocation = result != null && result != DBNull.Value && Convert.ToInt32(result) > 0;
            if (hasLocation)
            {
                return;
            }
        }

        var pkNameSql = @"
            SELECT kc.name
            FROM sys.key_constraints kc
            INNER JOIN sys.tables t ON kc.parent_object_id = t.object_id
            WHERE t.name = @TableName AND kc.type = 'PK';";

        string pkName = null;
        using (var command = CreateCommand(pkNameSql))
        {
            AddParameter(command, "@TableName", tableName);
            var result = await command.ExecuteScalarAsync(cancellationToken);
            if (result != null && result != DBNull.Value)
            {
                pkName = result.ToString();
            }
        }

        if (!string.IsNullOrWhiteSpace(pkName))
        {
            var dropSql = $"ALTER TABLE {tableName} DROP CONSTRAINT [{pkName}];";
            using (var command = CreateCommand(dropSql))
            {
                await command.ExecuteNonQueryAsync(cancellationToken);
            }
        }

        var addSql = $"ALTER TABLE {tableName} ADD CONSTRAINT {constraintName} PRIMARY KEY ({keyColumns}, Location);";
        using (var command = CreateCommand(addSql))
        {
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
