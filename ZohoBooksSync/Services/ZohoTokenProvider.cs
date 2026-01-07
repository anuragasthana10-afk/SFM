using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using ZohoBooksSync.Configuration;
using ZohoBooksSync.Persistence;

namespace ZohoBooksSync.Services
{
public sealed class ZohoTokenProvider
{
    private readonly HttpClient _httpClient;
    private readonly ZohoBooksOptions _options;
    private readonly TokenStore _tokenStore;
    private string _cachedAccessToken;
    private DateTime _expiresAtUtc;
    private TokenStore.TokenData _tokenData;

    public ZohoTokenProvider(HttpClient httpClient, ZohoBooksOptions options, TokenStore tokenStore)
    {
        _httpClient = httpClient;
        _options = options;
        _tokenStore = tokenStore;
        _cachedAccessToken = string.Empty;
    }

    public async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(_cachedAccessToken) && DateTime.UtcNow < _expiresAtUtc)
        {
            return _cachedAccessToken;
        }

        var tokenData = await GetTokenDataAsync(cancellationToken);
        var expiresAtUtc = tokenData.AccessTokenExpiresAtUtc;

        if (!string.IsNullOrWhiteSpace(tokenData.AccessToken)
            && expiresAtUtc.HasValue
            && DateTime.UtcNow < expiresAtUtc.Value)
        {
            _cachedAccessToken = tokenData.AccessToken;
            _expiresAtUtc = expiresAtUtc.Value;
            return _cachedAccessToken;
        }

        if (string.IsNullOrWhiteSpace(tokenData.RefreshToken)
            || string.IsNullOrWhiteSpace(tokenData.ClientId)
            || string.IsNullOrWhiteSpace(tokenData.ClientSecret))
        {
            throw new InvalidOperationException("Missing Zoho OAuth credentials. Provide access token or refresh token flow values.");
        }

        var form = new Dictionary<string, string>
        {
            ["refresh_token"] = tokenData.RefreshToken,
            ["client_id"] = tokenData.ClientId,
            ["client_secret"] = tokenData.ClientSecret,
            ["grant_type"] = "refresh_token"
        };

        string payload;
        using (var request = new HttpRequestMessage(HttpMethod.Post, tokenData.TokenEndpoint))
        {
            request.Content = new FormUrlEncodedContent(form);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var response = await _httpClient.SendAsync(request, cancellationToken);
            payload = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"Zoho OAuth token refresh failed ({(int)response.StatusCode}): {payload}");
            }
        }

        string token;
        int expiresIn;
        using (var document = JsonDocument.Parse(payload))
        {
            var root = document.RootElement;
            token = root.GetProperty("access_token").GetString();
            expiresIn = root.TryGetProperty("expires_in", out var expiresElement)
                ? expiresElement.GetInt32()
                : 3600;
        }

        if (string.IsNullOrWhiteSpace(token))
        {
            throw new InvalidOperationException("Zoho OAuth response missing access_token.");
        }

        _cachedAccessToken = token;
        _expiresAtUtc = DateTime.UtcNow.AddSeconds(expiresIn - 60);
        tokenData.AccessToken = token;
        tokenData.AccessTokenExpiresAtUtc = _expiresAtUtc;
        _tokenData = tokenData;
        await _tokenStore.UpdateAccessTokenAsync(token, _expiresAtUtc, cancellationToken);
        return _cachedAccessToken;
    }

    private async Task<TokenStore.TokenData> GetTokenDataAsync(CancellationToken cancellationToken)
    {
        if (_tokenData != null)
        {
            return _tokenData;
        }

        var existing = await _tokenStore.GetAsync(cancellationToken);
        if (existing != null)
        {
            _tokenData = existing;
            return existing;
        }

        var seeded = new TokenStore.TokenData
        {
            AccessToken = _options.AccessToken,
            RefreshToken = _options.RefreshToken,
            ClientId = _options.ClientId,
            ClientSecret = _options.ClientSecret,
            TokenEndpoint = _options.TokenEndpoint
        };

        if (!string.IsNullOrWhiteSpace(seeded.AccessToken)
            || !string.IsNullOrWhiteSpace(seeded.RefreshToken)
            || !string.IsNullOrWhiteSpace(seeded.ClientId)
            || !string.IsNullOrWhiteSpace(seeded.ClientSecret)
            || !string.IsNullOrWhiteSpace(seeded.TokenEndpoint))
        {
            await _tokenStore.UpsertAsync(seeded, cancellationToken);
        }

        _tokenData = seeded;
        return seeded;
    }
}
}
