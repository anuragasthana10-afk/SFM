namespace ZohoBooksSync.Configuration;

public sealed class ZohoBooksConnectionOptions
{
    public string BaseUrl { get; init; } = "https://www.zohoapis.com/books/v3";
    public string OrganizationId { get; init; } = string.Empty;
    public string SqlConnectionString { get; init; } = string.Empty;

    public static ZohoBooksConnectionOptions FromEnvironment()
    {
        return new ZohoBooksConnectionOptions
        {
            BaseUrl = Environment.GetEnvironmentVariable("ZOHO_BOOKS_BASE_URL")
                ?? "https://www.zohoapis.com/books/v3",
            OrganizationId = Environment.GetEnvironmentVariable("ZOHO_BOOKS_ORGANIZATION_ID")
                ?? string.Empty,
            SqlConnectionString = Environment.GetEnvironmentVariable("ZOHO_BOOKS_SQL_CONNECTION_STRING")
                ?? string.Empty
        };
    }
}
