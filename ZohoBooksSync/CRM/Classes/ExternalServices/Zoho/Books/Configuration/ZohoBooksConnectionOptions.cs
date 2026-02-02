using System;
using CRM.Classes.ExternalServices.Zoho.Books;

namespace CRM.Classes.ExternalServices.Zoho.Books.Configuration
{
    public sealed class ZohoBooksConnectionOptions
    {
        public string APIBaseUrl { get; set; }
        public string ActiveOrganizationId { get; set; }
        public string UaeOrganizationId { get; set; }
        public string SwissOrganizationId { get; set; }
        public string SqlConnectionString { get; set; }
        public bool AllowReportingTagOptionCreate { get; set; }
        public bool ApplyReportingTagsToLineItems { get; set; }
        public ZohoBooksLocation Location { get; set; }

        public ZohoBooksConnectionOptions()
        {
            APIBaseUrl = "https://www.zohoapis.com/books/v3";
            ActiveOrganizationId = string.Empty;
            UaeOrganizationId = string.Empty;
            SwissOrganizationId = string.Empty;
            SqlConnectionString = string.Empty;
            AllowReportingTagOptionCreate = false;
            ApplyReportingTagsToLineItems = false;
            Location = ZohoBooksLocation.Uae;
        }

        public static ZohoBooksConnectionOptions FromEnvironment()
        {
            return new ZohoBooksConnectionOptions
            {
                APIBaseUrl = Environment.GetEnvironmentVariable("ZOHO_BOOKS_BASE_URL")
                    ?? "https://www.zohoapis.com/books/v3",
                ActiveOrganizationId = string.Empty,
                UaeOrganizationId = Environment.GetEnvironmentVariable("ZOHO_BOOKS_UAE_ORGANIZATION_ID")
                    ?? string.Empty,
                SwissOrganizationId = Environment.GetEnvironmentVariable("ZOHO_BOOKS_SWISS_ORGANIZATION_ID")
                    ?? string.Empty,
                SqlConnectionString = Environment.GetEnvironmentVariable("ZOHO_BOOKS_SQL_CONNECTION_STRING")
                    ?? string.Empty,
                AllowReportingTagOptionCreate = string.Equals(
                    Environment.GetEnvironmentVariable("ZOHO_BOOKS_ALLOW_REPORTING_TAG_OPTION_CREATE"),
                    "true",
                    StringComparison.OrdinalIgnoreCase),
                ApplyReportingTagsToLineItems = string.Equals(
                    Environment.GetEnvironmentVariable("ZOHO_BOOKS_APPLY_REPORTING_TAGS_TO_LINE_ITEMS"),
                    "true",
                    StringComparison.OrdinalIgnoreCase),
                Location = ZohoBooksLocation.Uae
            };
        }
    }
}
