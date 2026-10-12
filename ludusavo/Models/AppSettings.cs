using System.Text.Json.Serialization;

namespace ludusavo.Models;

public class AppSettings
{
    [JsonPropertyName("googleClientId")]
    public string GoogleClientId { get; set; } = string.Empty;

    [JsonPropertyName("googleClientSecret")]
    public string GoogleClientSecret { get; set; } = string.Empty;

    [JsonPropertyName("googleRefreshToken")]
    public string GoogleRefreshToken { get; set; } = string.Empty;

    [JsonPropertyName("googleAccessToken")]
    public string GoogleAccessToken { get; set; } = string.Empty;

    [JsonPropertyName("googleTokenExpiry")]
    public DateTime? GoogleTokenExpiry { get; set; }

    [JsonPropertyName("googleRootFolderId")]
    public string GoogleRootFolderId { get; set; } = string.Empty;

    [JsonPropertyName("userEmail")]
    public string UserEmail { get; set; } = string.Empty;

    [JsonIgnore]
    public string EffectiveClientId =>
        !string.IsNullOrWhiteSpace(GoogleClientId)
            ? GoogleClientId
            : DefaultConfig.GoogleClientId;

    [JsonIgnore]
    public string EffectiveClientSecret =>
        !string.IsNullOrWhiteSpace(GoogleClientSecret)
            ? GoogleClientSecret
            : DefaultConfig.GoogleClientSecret;

    [JsonIgnore]
    public bool HasCredentials =>
        !string.IsNullOrWhiteSpace(EffectiveClientId) &&
        !string.IsNullOrWhiteSpace(EffectiveClientSecret);

    [JsonIgnore]
    public bool IsGoogleDriveConfigured =>
        HasCredentials && !string.IsNullOrWhiteSpace(GoogleRefreshToken);
}
