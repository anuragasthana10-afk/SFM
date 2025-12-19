using System.Text.Json;
using System.Text.Json.Serialization;

namespace ZohoBooksSync.Models;

public sealed class ZohoSettings
{
    [JsonPropertyName("organizationId")]
    public required string OrganizationId { get; init; }

    [JsonPropertyName("accessToken")]
    public required string AccessToken { get; init; }

    [JsonPropertyName("refreshToken")]
    public string? RefreshToken { get; init; }

    [JsonPropertyName("clientId")]
    public string? ClientId { get; init; }

    [JsonPropertyName("clientSecret")]
    public string? ClientSecret { get; init; }

    [JsonPropertyName("apiBaseUrl")]
    public string? ApiBaseUrl { get; init; }

    [JsonPropertyName("demoCustomerId")]
    public string? DemoCustomerId { get; init; }
}

public static class ZohoSettingsLoader
{
    public static ZohoSettings Load(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Missing configuration file at {path}");
        }

        var json = File.ReadAllText(path);
        var settings = JsonSerializer.Deserialize<ZohoSettings>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        return settings ?? throw new InvalidOperationException("Unable to load Zoho settings");
    }
}
