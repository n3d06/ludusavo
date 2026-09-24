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

    [JsonPropertyName("autoSyncOnExit")]
    public bool AutoSyncOnExit { get; set; } = false;

    [JsonPropertyName("minimizeToTray")]
    public bool MinimizeToTray { get; set; } = false;

    [JsonPropertyName("startWithWindows")]
    public bool StartWithWindows { get; set; } = false;

    [JsonPropertyName("syncIntervalMinutes")]
    public int SyncIntervalMinutes { get; set; } = 15;

    [JsonPropertyName("customScanPaths")]
    public List<string> CustomScanPaths { get; set; } = new();

    [JsonPropertyName("pinnedGameIds")]
    public List<string> PinnedGameIds { get; set; } = new();

    [JsonIgnore]
    public bool IsGitHubConfigured =>
        !string.IsNullOrWhiteSpace(GitHubToken) &&
        !string.IsNullOrWhiteSpace(GitHubOwner) &&
        !string.IsNullOrWhiteSpace(GitHubRepo);
}
