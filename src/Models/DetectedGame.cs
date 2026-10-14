using ludusavo.Services;

namespace ludusavo.Models;

public class DetectedGame
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int? SteamId { get; set; }

    public bool IsCustomGame => Id.StartsWith("custom_", StringComparison.OrdinalIgnoreCase);

    public string? CustomBannerUrl { get; set; }
    public string? BannerUrl => BannerCache.GetBannerUrl(SteamId, CustomBannerUrl);

    public int FileCount { get; set; }
    public long TotalSizeBytes { get; set; }
    public DateTime? LastModified { get; set; }
    public DateTime? RemoteModified { get; set; }
    public SyncStatus Status { get; set; } = SyncStatus.Unknown;
    public bool IsBusy { get; set; }
    public string StatusMessage { get; set; } = string.Empty;
    public bool IsPinned { get; set; }
    public List<SaveFileDetail> Files { get; set; } = new();

    public string TotalSizeFormatted
    {
        get
        {
            if (TotalSizeBytes <= 0) return "0 B";
            string[] suffixes = { "B", "KB", "MB", "GB" };
            int order = 0;
            double len = TotalSizeBytes;
            while (len >= 1024 && order < suffixes.Length - 1)
            {
                order++;
                len /= 1024;
            }
            return $"{len:0.##} {suffixes[order]}";
        }
    }

    public string LastModifiedFormatted => LastModified.HasValue
        ? LastModified.Value.ToString("yyyy-MM-dd HH:mm:ss")
        : "Unknown";

    public string DisplayStatus
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(StatusMessage))
                return StatusMessage;

            return Status switch
            {
                SyncStatus.Synced => "In sync",
                SyncStatus.LocalNewer => "Local is newer",
                SyncStatus.RemoteNewer => "Cloud is newer",
                SyncStatus.LocalOnly => "Local only",
                SyncStatus.RemoteOnly => "Cloud only",
                SyncStatus.Conflict => "Conflict",
                SyncStatus.Syncing => "Syncing...",
                _ => "Not checked"
            };
        }
    }
}

public class SaveFileDetail
{
    public string AbsolutePath { get; set; } = string.Empty;
    public string PlaceholderPath { get; set; } = string.Empty;
    public long Size { get; set; }
    public DateTime LastWriteTime { get; set; }
    public string? Sha256 { get; set; }
}
