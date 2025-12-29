using System.Net.Http.Headers;
using System.Text.Json;
using ZohoBooksSync.Configuration;

namespace ZohoBooksSync.Services;

public sealed class ZohoTokenProvider
{
    private readonly HttpClient _httpClient;
    private readonly ZohoBooksOptions _options;
    private string? _cachedAccessToken;
    private DateTimeOffset _expiresAt;

    public ZohoTokenProvider(HttpClient httpClient, ZohoBooksOptions options)
    {
        _httpClient = httpClient;
        _options = options;
    }

    public async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(_cachedAccessToken) && DateTimeOffset.UtcNow < _expiresAt)
        {
            return _cachedAccessToken;
        }

        if (!string.IsNullOrWhiteSpace(_options.AccessToken))
        {
            _cachedAccessToken = _options.AccessToken;
            _expiresAt = DateTimeOffset.UtcNow.AddMinutes(50);
            return _cachedAccessToken;
        }

        if (string.IsNullOrWhiteSpace(_options.RefreshToken)
            || string.IsNullOrWhiteSpace(_options.ClientId)
            || string.IsNullOrWhiteSpace(_options.ClientSecret))
        {
            throw new InvalidOperationException("Missing Zoho OAuth credentials. Provide access token or refresh token flow values.");
        }

        var form = new Dictionary<string, string>
        {
            ["refresh_token"] = _options.RefreshToken,
            ["client_id"] = _options.ClientId,
            ["client_secret"] = _options.ClientSecret,
            ["grant_type"] = "refresh_token"
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, _options.TokenEndpoint)
        {
            Content = new FormUrlEncodedContent(form)
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var response = await _httpClient.SendAsync(request, cancellationToken);
        var payload = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"Zoho OAuth token refresh failed ({(int)response.StatusCode}): {payload}");
        }

        using var document = JsonDocument.Parse(payload);
        var root = document.RootElement;
        var token = root.GetProperty("access_token").GetString();
        var expiresIn = root.TryGetProperty("expires_in", out var expiresElement)
            ? expiresElement.GetInt32()
            : 3600;

        if (string.IsNullOrWhiteSpace(token))
        {
            throw new InvalidOperationException("Zoho OAuth response missing access_token.");
        }

        _cachedAccessToken = token;
        _expiresAt = DateTimeOffset.UtcNow.AddSeconds(expiresIn - 60);
        return _cachedAccessToken;
    }
}
