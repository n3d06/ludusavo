using CommunityToolkit.Mvvm.ComponentModel;

namespace ludusavo.Services;

public class RemoteGameMeta : ObservableObject
{
    public string GameId { get; set; } = string.Empty;
    public string GameName { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public int FileCount { get; set; }
    public long TotalSize { get; set; }
    public string ArchiveHash { get; set; } = string.Empty;
    public int? SteamId { get; set; }
    public string? CustomBannerUrl { get; set; }

    public string? BannerUrl => BannerCache.GetBannerUrl(SteamId, CustomBannerUrl);

    public RemoteGameMeta()
    {
        BannerCache.BannerUpdated += OnBannerUpdated;
    }

    private void OnBannerUpdated(int steamId)
    {
        if (SteamId == steamId)
        {
            System.Windows.Application.Current?.Dispatcher?.InvokeAsync(() =>
            {
                OnPropertyChanged(nameof(BannerUrl));
            });
        }
    }

    public string FormattedSize
    {
        get
        {
            if (TotalSize <= 0) return string.Empty;
            if (TotalSize < 1024) return $"{TotalSize} B";
            if (TotalSize < 1024 * 1024) return $"{TotalSize / 1024.0:F1} KB";
            return $"{TotalSize / (1024.0 * 1024.0):F1} MB";
        }
    }

    public string FormattedTimestamp => Timestamp == DateTime.MinValue || Timestamp.Year <= 1
        ? "Unknown"
        : Timestamp.ToString("yyyy-MM-dd HH:mm");
}

public interface IGitHubService
{
    bool IsConfigured { get; }
    Task<(bool success, string? message)> TestConnectionAsync();
    Task<List<string>> ListRemoteGameIdsAsync();
    Task<RemoteGameMeta?> GetRemoteMetaAsync(string gameId);
    Task<Dictionary<string, RemoteGameMeta>> GetAllRemoteMetasAsync(bool forceRefresh = false);
    void InvalidateCatalogCache();
    Task<(bool success, string? error)> UploadGameSaveAsync(string gameId, string zipPath, string metaPath, string mappingPath);
    Task<(bool success, string? error)> DownloadGameSaveAsync(string gameId, string targetDir);
    Task<(bool success, string? error)> UploadFileAsync(string repoPath, string localPath, string commitMessage);
    Task<(bool success, string? error)> DownloadFileAsync(string repoPath, string localPath);
    Task<(bool success, string? error)> DeleteGameSaveAsync(string gameId);
    Task<(int remaining, int limit, int resetMinutes)> GetRateLimitAsync();
}
