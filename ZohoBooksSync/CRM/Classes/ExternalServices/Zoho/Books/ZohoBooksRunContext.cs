using System;
using CRM.Classes.ExternalServices.Zoho.Books.Configuration;
using CRM.Classes.ExternalServices.Zoho.Persistence;
using CRM.Classes.ExternalServices.Zoho.Services;

namespace CRM.Classes.ExternalServices.Zoho.Books
{
    public sealed class ZohoBooksRunContext
    {
        public ZohoBooksLocation Location { get; }
        public string LocationName => Location.ToString().ToLowerInvariant();
        public IZohoApiConfigurationOptions Options { get; }
        public ZohoBooksConnectionOptions ConnectionOptions { get; }
        public string TokenCode { get; }
        public string OrganizationIdEnvironmentVariable { get; }

        private ZohoBooksRunContext(
            ZohoBooksLocation location,
            IZohoApiConfigurationOptions options,
            ZohoBooksConnectionOptions connectionOptions,
            string tokenCode,
            string organizationIdEnvironmentVariable)
        {
            Location = location;
            Options = options;
            ConnectionOptions = connectionOptions;
            TokenCode = tokenCode;
            OrganizationIdEnvironmentVariable = organizationIdEnvironmentVariable;
        }

        public static bool TryCreate(string locationInput, out ZohoBooksRunContext context, out string error)
        {
            context = null;
            error = null;

            var resolvedInput = string.IsNullOrWhiteSpace(locationInput)
                ? Environment.GetEnvironmentVariable("ZOHO_BOOKS_LOCATION") ?? "uae"
                : locationInput;
            var locationValue = resolvedInput.Trim();
            ZohoBooksLocation location;

            if (locationValue.Equals("uae", StringComparison.OrdinalIgnoreCase))
            {
                location = ZohoBooksLocation.Uae;
            }
            else if (locationValue.Equals("swiss", StringComparison.OrdinalIgnoreCase))
            {
                location = ZohoBooksLocation.Swiss;
            }
            else
            {
                error = $"Unsupported ZOHO_BOOKS_LOCATION '{resolvedInput}'. Use 'uae' or 'swiss'.";
                return false;
            }

            var isUae = location == ZohoBooksLocation.Uae;
            var options = isUae
                ? (IZohoApiConfigurationOptions)UaeZohoAPIConfigurationOptions.FromEnvironment()
                : SwissZohoAPIConfigurationOptions.FromEnvironment();
            var connectionOptions = ZohoBooksConnectionOptions.FromEnvironment();
            connectionOptions.ActiveOrganizationId = isUae
                ? connectionOptions.UaeOrganizationId
                : connectionOptions.SwissOrganizationId;
            connectionOptions.Location = location;

            var tokenCode = isUae ? TokenStore.UaeTokenCode : TokenStore.SwissTokenCode;
            var organizationEnvVar = isUae
                ? "ZOHO_BOOKS_UAE_ORGANIZATION_ID"
                : "ZOHO_BOOKS_SWISS_ORGANIZATION_ID";

            context = new ZohoBooksRunContext(location, options, connectionOptions, tokenCode, organizationEnvVar);
            return true;
        }
    }
}
