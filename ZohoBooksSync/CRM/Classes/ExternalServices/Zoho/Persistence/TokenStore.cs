using System.Data;
using System.Data.Common;
using System.Data.Entity;

namespace CRM.Classes.ExternalServices.Zoho.Persistence
{
public sealed class TokenStore
{
    private const string TokenTable = "Sec_Crd";
    public const string UaeTokenCode = "ZOHO_UAE";
    public const string SwissTokenCode = "ZOHO_SWISS";
    private readonly Database _database;
    private readonly string _tokenCode;

    public TokenStore(Database database, string tokenCode)
    {
        _database = database;
        _tokenCode = tokenCode;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        var sql = $@"
            IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = '{TokenTable}')
            BEGIN
                CREATE TABLE {TokenTable} (
                    Code NVARCHAR(32) NOT NULL PRIMARY KEY,
                    Client_ID NVARCHAR(256) NULL,
                    Client_Secret NVARCHAR(MAX) NULL,
                    Refresh_Token NVARCHAR(MAX) NULL,
                    Access_Token NVARCHAR(MAX) NULL,
                    TokenValidity DATETIME2 NULL,
                    CreateDate DATETIME2 NULL,
                    CreateUserID INT NULL,
                    ModifyDate DATETIME2 NULL,
                    ModifyUserID INT NULL,
                    RefreshToken_URL NVARCHAR(512) NULL
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
            SELECT Access_Token,
                   TokenValidity,
                   Refresh_Token,
                   Client_ID,
                   Client_Secret,
                   RefreshToken_URL
            FROM {TokenTable}
            WHERE Code = @Code;";

        await EnsureConnectionOpenAsync(cancellationToken);
        using (var command = CreateCommand(sql))
        {
            AddParameter(command, "@Code", _tokenCode);
            using (var reader = await command.ExecuteReaderAsync(cancellationToken))
            {
            if (!await reader.ReadAsync(cancellationToken))
            {
                return null;
            }

            return new TokenData
            {
                AccessToken = reader.IsDBNull(0) ? string.Empty : reader.GetString(0),
                AccessTokenExpiresAtUtc = reader.IsDBNull(1) ? (DateTime?)null : reader.GetDateTime(1),
                RefreshToken = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                ClientId = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                ClientSecret = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
                TokenEndpoint = reader.IsDBNull(5) ? string.Empty : reader.GetString(5)
            };
            }
        }
    }

    public async Task UpsertAsync(TokenData data, CancellationToken cancellationToken = default)
    {
        var sql = $@"
            MERGE {TokenTable} AS target
            USING (SELECT @Code AS Code) AS source
            ON target.Code = source.Code
            WHEN MATCHED THEN
                UPDATE SET
                    Access_Token = @AccessToken,
                    TokenValidity = @AccessTokenExpiresAtUtc,
                    Refresh_Token = @RefreshToken,
                    Client_ID = @ClientId,
                    Client_Secret = @ClientSecret,
                    RefreshToken_URL = @TokenEndpoint,
                    ModifyDate = @ModifyDate,
                    ModifyUserID = @ModifyUserID
            WHEN NOT MATCHED THEN
                INSERT (Code, Access_Token, TokenValidity, Refresh_Token, Client_ID, Client_Secret, RefreshToken_URL, CreateDate, CreateUserID)
                VALUES (@Code, @AccessToken, @AccessTokenExpiresAtUtc, @RefreshToken, @ClientId, @ClientSecret, @TokenEndpoint, @CreateDate, @CreateUserID);";

        await EnsureConnectionOpenAsync(cancellationToken);
        using (var command = CreateCommand(sql))
        {
            AddTokenParameters(command, data);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    public async Task UpdateAccessTokenAsync(string accessToken, DateTime? expiresAtUtc, CancellationToken cancellationToken = default)
    {
        var sql = $@"
            UPDATE {TokenTable}
            SET Access_Token = @AccessToken,
                TokenValidity = @AccessTokenExpiresAtUtc,
                ModifyDate = @ModifyDate,
                ModifyUserID = @ModifyUserID
            WHERE Code = @Code;";

        await EnsureConnectionOpenAsync(cancellationToken);
        using (var command = CreateCommand(sql))
        {
            AddParameter(command, "@AccessToken", accessToken);
            AddParameter(command, "@AccessTokenExpiresAtUtc", expiresAtUtc.HasValue ? (object)expiresAtUtc.Value : DBNull.Value);
            AddParameter(command, "@ModifyDate", DateTime.UtcNow);
            AddParameter(command, "@ModifyUserID", 1);
            AddParameter(command, "@Code", _tokenCode);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private void AddTokenParameters(DbCommand command, TokenData data)
    {
        AddParameter(command, "@AccessToken", string.IsNullOrWhiteSpace(data.AccessToken) ? (object)DBNull.Value : data.AccessToken);
        AddParameter(command, "@AccessTokenExpiresAtUtc", data.AccessTokenExpiresAtUtc.HasValue ? (object)data.AccessTokenExpiresAtUtc.Value : DBNull.Value);
        AddParameter(command, "@RefreshToken", string.IsNullOrWhiteSpace(data.RefreshToken) ? (object)DBNull.Value : data.RefreshToken);
        AddParameter(command, "@ClientId", string.IsNullOrWhiteSpace(data.ClientId) ? (object)DBNull.Value : data.ClientId);
        AddParameter(command, "@ClientSecret", string.IsNullOrWhiteSpace(data.ClientSecret) ? (object)DBNull.Value : data.ClientSecret);
        AddParameter(command, "@TokenEndpoint", string.IsNullOrWhiteSpace(data.TokenEndpoint) ? (object)DBNull.Value : data.TokenEndpoint);
        AddParameter(command, "@CreateDate", DateTime.UtcNow);
        AddParameter(command, "@CreateUserID", 1);
        AddParameter(command, "@ModifyDate", DateTime.UtcNow);
        AddParameter(command, "@ModifyUserID", 1);
        AddParameter(command, "@Code", _tokenCode);
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
        public DateTime? AccessTokenExpiresAtUtc { get; set; }
        public string RefreshToken { get; set; }
        public string ClientId { get; set; }
        public string ClientSecret { get; set; }
        public string TokenEndpoint { get; set; }
    }
}
}
