using Microsoft.Data.SqlClient;

namespace ZohoBooksSync.Persistence;

public sealed class TokenStore
{
    private const string TokenTable = "ZohoBooks_TokenStore";
    private readonly string _connectionString;

    public TokenStore(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        var sql = $"""
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
            END
            """;

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<TokenData?> GetAsync(CancellationToken cancellationToken = default)
    {
        var sql = $"""
            SELECT AccessToken, RefreshToken, ClientId, ClientSecret, TokenEndpoint
            FROM {TokenTable}
            WHERE Id = 1;
            """;

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new TokenData(
            reader.IsDBNull(0) ? string.Empty : reader.GetString(0),
            reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
            reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
            reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
            reader.IsDBNull(4) ? string.Empty : reader.GetString(4));
    }

    public async Task UpsertAsync(TokenData data, CancellationToken cancellationToken = default)
    {
        var sql = $"""
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
                VALUES (1, @AccessToken, @RefreshToken, @ClientId, @ClientSecret, @TokenEndpoint);
            """;

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        AddTokenParameters(command, data);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task UpdateAccessTokenAsync(string accessToken, CancellationToken cancellationToken = default)
    {
        var sql = $"""
            UPDATE {TokenTable}
            SET AccessToken = @AccessToken
            WHERE Id = 1;
            """;

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@AccessToken", accessToken);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static void AddTokenParameters(SqlCommand command, TokenData data)
    {
        command.Parameters.AddWithValue("@AccessToken", string.IsNullOrWhiteSpace(data.AccessToken) ? DBNull.Value : data.AccessToken);
        command.Parameters.AddWithValue("@RefreshToken", string.IsNullOrWhiteSpace(data.RefreshToken) ? DBNull.Value : data.RefreshToken);
        command.Parameters.AddWithValue("@ClientId", string.IsNullOrWhiteSpace(data.ClientId) ? DBNull.Value : data.ClientId);
        command.Parameters.AddWithValue("@ClientSecret", string.IsNullOrWhiteSpace(data.ClientSecret) ? DBNull.Value : data.ClientSecret);
        command.Parameters.AddWithValue("@TokenEndpoint", string.IsNullOrWhiteSpace(data.TokenEndpoint) ? DBNull.Value : data.TokenEndpoint);
    }

    public sealed record TokenData(
        string AccessToken,
        string RefreshToken,
        string ClientId,
        string ClientSecret,
        string TokenEndpoint);
}
