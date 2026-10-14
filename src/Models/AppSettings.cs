using System.Text.Json.Serialization;

namespace ludusavo.Models;

public class AppSettings
{
    [JsonPropertyName("googleClientId")]
    public string GoogleClientId { get; set; } = string.Empty;

    [JsonPropertyName("googleRootFolderId")]
    public string GoogleRootFolderId { get; set; } = string.Empty;

    [JsonPropertyName("userEmail")]
    public string UserEmail { get; set; } = string.Empty;

    // Secure credentials - stored in Windows Credential Manager, NEVER written to appsettings.json
    [JsonIgnore]
    public string GoogleClientSecret { get; set; } = string.Empty;

    [JsonIgnore]
    public string GoogleRefreshToken { get; set; } = string.Empty;

    [JsonIgnore]
    public string GoogleAccessToken { get; set; } = string.Empty;

    [JsonIgnore]
    public DateTime? GoogleTokenExpiry { get; set; }

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
        !string.IsNullOrWhiteSpace(EffectiveClientId);

    [JsonIgnore]
    public bool IsGoogleDriveConfigured =>
        HasCredentials && !string.IsNullOrWhiteSpace(GoogleRefreshToken);
}
