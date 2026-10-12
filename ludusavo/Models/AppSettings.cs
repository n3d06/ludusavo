using System.Text.Json.Serialization;

namespace ludusavo.Models;

public class AppSettings
{
    [JsonPropertyName("gitHubToken")]
    public string GitHubToken { get; set; } = string.Empty;

    [JsonPropertyName("gitHubOwner")]
    public string GitHubOwner { get; set; } = string.Empty;

    [JsonPropertyName("gitHubRepo")]
    public string GitHubRepo { get; set; } = string.Empty;

    [JsonIgnore]
    public bool IsGitHubConfigured =>
        !string.IsNullOrWhiteSpace(GitHubToken) &&
        !string.IsNullOrWhiteSpace(GitHubOwner) &&
        !string.IsNullOrWhiteSpace(GitHubRepo);
}
