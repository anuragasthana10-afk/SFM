namespace ZohoBooksSync.Configuration;

public sealed class ZohoBooksOptions
{
    public string AccessToken { get; init; } = string.Empty;
    public string RefreshToken { get; init; } = string.Empty;
    public string ClientId { get; init; } = string.Empty;
    public string ClientSecret { get; init; } = string.Empty;
    public string TokenEndpoint { get; init; } = "https://accounts.zoho.com/oauth/v2/token";

    public static ZohoBooksOptions FromEnvironment()
    {
        return new ZohoBooksOptions
        {
            AccessToken = Environment.GetEnvironmentVariable("ZOHO_BOOKS_ACCESS_TOKEN")
                ?? string.Empty,
            RefreshToken = Environment.GetEnvironmentVariable("ZOHO_BOOKS_REFRESH_TOKEN")
                ?? string.Empty,
            ClientId = Environment.GetEnvironmentVariable("ZOHO_BOOKS_CLIENT_ID")
                ?? string.Empty,
            ClientSecret = Environment.GetEnvironmentVariable("ZOHO_BOOKS_CLIENT_SECRET")
                ?? string.Empty,
            TokenEndpoint = Environment.GetEnvironmentVariable("ZOHO_BOOKS_TOKEN_ENDPOINT")
                ?? "https://accounts.zoho.com/oauth/v2/token"
        };
    }
}
