namespace CRM.Classes.ExternalServices.Zoho.Services
{
    public interface IZohoApiConfigurationOptions
    {
        string AccessToken { get; set; }
        string RefreshToken { get; set; }
        string ClientId { get; set; }
        string ClientSecret { get; set; }
        string TokenEndpoint { get; set; }
    }
}
