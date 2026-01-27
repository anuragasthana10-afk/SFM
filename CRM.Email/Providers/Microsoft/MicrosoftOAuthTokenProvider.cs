using System.Net.Http.Json;
using CRM.Email.Abstractions;

namespace CRM.Email.Providers.Microsoft;

public sealed class MicrosoftOAuthTokenProvider : IAccessTokenProvider
{
    private readonly HttpClient _httpClient;
    private readonly EmailProviderOptions _options;

    public MicrosoftOAuthTokenProvider(HttpClient httpClient, EmailProviderOptions options)
    {
        _httpClient = httpClient;
        _options = options;
    }

    public async Task<string> GetAccessTokenAsync(string scope, CancellationToken cancellationToken)
    {
        var tokenEndpoint = $"https://login.microsoftonline.com/{_options.TenantId}/oauth2/v2.0/token";

        using var request = new HttpRequestMessage(HttpMethod.Post, tokenEndpoint)
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = _options.ClientId,
                ["client_secret"] = _options.ClientSecret,
                ["scope"] = scope,
                ["grant_type"] = "client_credentials"
            })
        };

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken: cancellationToken);
        if (payload is null || string.IsNullOrWhiteSpace(payload.AccessToken))
        {
            throw new InvalidOperationException("Microsoft OAuth token response was empty.");
        }

        return payload.AccessToken;
    }

    private sealed class TokenResponse
    {
        public string AccessToken { get; set; } = string.Empty;
        public string TokenType { get; set; } = string.Empty;
        public int ExpiresIn { get; set; }
    }
}
