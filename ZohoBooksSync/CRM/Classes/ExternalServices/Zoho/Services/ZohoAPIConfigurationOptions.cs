using System;

namespace CRM.Classes.ExternalServices.Zoho.Services
{
    public class ZohoAPIConfigurationOptions
    {
        public string AccessToken { get; set; }
        public string RefreshToken { get; set; }
        public string ClientId { get; set; }
        public string ClientSecret { get; set; }
        public string TokenEndpoint { get; set; }

        public ZohoAPIConfigurationOptions()
        {
            AccessToken = string.Empty;
            RefreshToken = string.Empty;
            ClientId = string.Empty;
            ClientSecret = string.Empty;
            TokenEndpoint = "https://accounts.zoho.com/oauth/v2/token";
        }

        public static ZohoAPIConfigurationOptions FromEnvironment()
        {
            return new ZohoAPIConfigurationOptions
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

    public sealed class UaeZohoAPIConfigurationOptions : ZohoAPIConfigurationOptions
    {
        public static UaeZohoAPIConfigurationOptions FromEnvironment()
        {
            return new UaeZohoAPIConfigurationOptions
            {
                AccessToken = Environment.GetEnvironmentVariable("ZOHO_BOOKS_UAE_ACCESS_TOKEN")
                    ?? string.Empty,
                RefreshToken = Environment.GetEnvironmentVariable("ZOHO_BOOKS_UAE_REFRESH_TOKEN")
                    ?? string.Empty,
                ClientId = Environment.GetEnvironmentVariable("ZOHO_BOOKS_UAE_CLIENT_ID")
                    ?? string.Empty,
                ClientSecret = Environment.GetEnvironmentVariable("ZOHO_BOOKS_UAE_CLIENT_SECRET")
                    ?? string.Empty,
                TokenEndpoint = Environment.GetEnvironmentVariable("ZOHO_BOOKS_UAE_TOKEN_ENDPOINT")
                    ?? "https://accounts.zoho.com/oauth/v2/token"
            };
        }
    }

    public sealed class SwissZohoAPIConfigurationOptions : ZohoAPIConfigurationOptions
    {
        public static SwissZohoAPIConfigurationOptions FromEnvironment()
        {
            return new SwissZohoAPIConfigurationOptions
            {
                AccessToken = Environment.GetEnvironmentVariable("ZOHO_BOOKS_SWISS_ACCESS_TOKEN")
                    ?? string.Empty,
                RefreshToken = Environment.GetEnvironmentVariable("ZOHO_BOOKS_SWISS_REFRESH_TOKEN")
                    ?? string.Empty,
                ClientId = Environment.GetEnvironmentVariable("ZOHO_BOOKS_SWISS_CLIENT_ID")
                    ?? string.Empty,
                ClientSecret = Environment.GetEnvironmentVariable("ZOHO_BOOKS_SWISS_CLIENT_SECRET")
                    ?? string.Empty,
                TokenEndpoint = Environment.GetEnvironmentVariable("ZOHO_BOOKS_SWISS_TOKEN_ENDPOINT")
                    ?? "https://accounts.zoho.com/oauth/v2/token"
            };
        }
    }
}
