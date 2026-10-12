using ludusavo.Models;

namespace ludusavo.Services;

public interface ISyncService
{
    Task CheckSyncStatusAsync(DetectedGame game);
    Task CheckAllSyncStatusesAsync(IEnumerable<DetectedGame> games);
    Task<(bool success, string? message)> SyncGameToCloudAsync(DetectedGame game);
    Task<(bool success, string? message, int successCount, int failCount)> SyncGamesBatchToCloudAsync(List<DetectedGame> games, Action<string>? progressCallback = null);
    Task<(bool success, string? message)> RestoreGameFromCloudAsync(DetectedGame game);
    Task<(bool success, string? message)> UploadCustomManifestAsync();
    Task<(bool success, string? message)> DownloadCustomManifestAsync();
}
