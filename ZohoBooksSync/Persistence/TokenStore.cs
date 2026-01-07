using System.Data;
using System.Data.Common;
using System.Data.Entity;

namespace ZohoBooksSync.Persistence
{
public sealed class TokenStore
{
    private const string TokenTable = "ZohoBooks_TokenStore";
    private readonly Database _database;

    public TokenStore(Database database)
    {
        _database = database;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        var sql = $@"
            IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = '{TokenTable}')
            BEGIN
                CREATE TABLE {TokenTable} (
                    Id INT NOT NULL PRIMARY KEY,
                    AccessToken NVARCHAR(MAX) NULL,
                    RefreshToken NVARCHAR(MAX) NULL,
                    ClientId NVARCHAR(256) NULL,
                    ClientSecret NVARCHAR(MAX) NULL,
                    TokenEndpoint NVARCHAR(512) NULL
                );
            END";

        await EnsureConnectionOpenAsync(cancellationToken);
        using (var command = CreateCommand(sql))
        {
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    public async Task<TokenData> GetAsync(CancellationToken cancellationToken = default)
    {
        var sql = $@"
            SELECT AccessToken, RefreshToken, ClientId, ClientSecret, TokenEndpoint
            FROM {TokenTable}
            WHERE Id = 1;";

        await EnsureConnectionOpenAsync(cancellationToken);
        using (var command = CreateCommand(sql))
        using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            if (!await reader.ReadAsync(cancellationToken))
            {
                return null;
            }

            return new TokenData
            {
                AccessToken = reader.IsDBNull(0) ? string.Empty : reader.GetString(0),
                RefreshToken = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                ClientId = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                ClientSecret = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                TokenEndpoint = reader.IsDBNull(4) ? string.Empty : reader.GetString(4)
            };
        }
    }

    public async Task UpsertAsync(TokenData data, CancellationToken cancellationToken = default)
    {
        var sql = $@"
            MERGE {TokenTable} AS target
            USING (SELECT 1 AS Id) AS source
            ON target.Id = source.Id
            WHEN MATCHED THEN
                UPDATE SET
                    AccessToken = @AccessToken,
                    RefreshToken = @RefreshToken,
                    ClientId = @ClientId,
                    ClientSecret = @ClientSecret,
                    TokenEndpoint = @TokenEndpoint
            WHEN NOT MATCHED THEN
                INSERT (Id, AccessToken, RefreshToken, ClientId, ClientSecret, TokenEndpoint)
                VALUES (1, @AccessToken, @RefreshToken, @ClientId, @ClientSecret, @TokenEndpoint);";

        await EnsureConnectionOpenAsync(cancellationToken);
        using (var command = CreateCommand(sql))
        {
            AddTokenParameters(command, data);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    public async Task UpdateAccessTokenAsync(string accessToken, CancellationToken cancellationToken = default)
    {
        var sql = $@"
            UPDATE {TokenTable}
            SET AccessToken = @AccessToken
            WHERE Id = 1;";

        await EnsureConnectionOpenAsync(cancellationToken);
        using (var command = CreateCommand(sql))
        {
            AddParameter(command, "@AccessToken", accessToken);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static void AddTokenParameters(DbCommand command, TokenData data)
    {
        AddParameter(command, "@AccessToken", string.IsNullOrWhiteSpace(data.AccessToken) ? (object)DBNull.Value : data.AccessToken);
        AddParameter(command, "@RefreshToken", string.IsNullOrWhiteSpace(data.RefreshToken) ? (object)DBNull.Value : data.RefreshToken);
        AddParameter(command, "@ClientId", string.IsNullOrWhiteSpace(data.ClientId) ? (object)DBNull.Value : data.ClientId);
        AddParameter(command, "@ClientSecret", string.IsNullOrWhiteSpace(data.ClientSecret) ? (object)DBNull.Value : data.ClientSecret);
        AddParameter(command, "@TokenEndpoint", string.IsNullOrWhiteSpace(data.TokenEndpoint) ? (object)DBNull.Value : data.TokenEndpoint);
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

    public sealed class TokenData
    {
        public string AccessToken { get; set; }
        public string RefreshToken { get; set; }
        public string ClientId { get; set; }
        public string ClientSecret { get; set; }
        public string TokenEndpoint { get; set; }
    }
}
}
