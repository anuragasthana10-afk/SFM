namespace ZohoBooksSync.Configuration
{
    public sealed class ZohoBooksConnectionOptions
    {
        public string BaseUrl { get; set; }
        public string OrganizationId { get; set; }
        public string SqlConnectionString { get; set; }

        public ZohoBooksConnectionOptions()
        {
            BaseUrl = "https://www.zohoapis.com/books/v3";
            OrganizationId = string.Empty;
            SqlConnectionString = string.Empty;
        }

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
}
