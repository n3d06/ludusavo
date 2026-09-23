using System.IO;
using ludusavo.Models;

namespace ludusavo.Services;

public class SyncService : ISyncService
{
    private readonly IGitHubService _gitHubService;
    private readonly IBackupService _backupService;
    private readonly IRestoreService _restoreService;
    private readonly IConfigService _configService;

    public SyncService(
        IGitHubService gitHubService,
        IBackupService backupService,
        IRestoreService restoreService,
        IConfigService configService)
    {
        _gitHubService = gitHubService;
        _backupService = backupService;
        _restoreService = restoreService;
        _configService = configService;
    }

    public async Task CheckSyncStatusAsync(DetectedGame game)
    {
        if (game == null) return;

        if (!_gitHubService.IsConfigured)
        {
            game.Status = game.Files.Count > 0 ? SyncStatus.LocalOnly : SyncStatus.Unknown;
            return;
        }

        try
        {
            var remote = await _gitHubService.GetRemoteMetaAsync(game.Id);
            UpdateGameStatusWithRemote(game, remote);
        }
        catch
        {
            game.Status = game.Files.Count > 0 ? SyncStatus.LocalOnly : SyncStatus.Unknown;
        }
    }

    private void UpdateGameStatusWithRemote(DetectedGame game, RemoteGameMeta? remote)
    {
        if (remote == null)
        {
            game.Status = game.Files.Count > 0 ? SyncStatus.LocalOnly : SyncStatus.Unknown;
            return;
        }

        game.RemoteModified = remote.Timestamp;

        // Check if local cache zip exists and matches remote archiveHash
        var localZip = Path.Combine(_backupService.GetGameCacheDir(game.Id), "latest.zip");
        if (File.Exists(localZip))
        {
            var localHash = BackupService.ComputeSha256(localZip);
            if (string.Equals(localHash, remote.ArchiveHash, StringComparison.OrdinalIgnoreCase))
            {
                game.Status = SyncStatus.Synced;
                return;
            }
        }

        // Compare timestamps
        if (game.LastModified.HasValue)
        {
            var diff = (game.LastModified.Value.ToUniversalTime() - remote.Timestamp.ToUniversalTime()).TotalSeconds;
            if (diff > 5)
            {
                game.Status = SyncStatus.LocalNewer;
            }
            else if (diff < -5)
            {
                game.Status = SyncStatus.RemoteNewer;
            }
            else
            {
                game.Status = SyncStatus.Synced;
            }
        }
        else
        {
            game.Status = SyncStatus.RemoteOnly;
        }
    }

    public async Task CheckAllSyncStatusesAsync(IEnumerable<DetectedGame> games)
    {
        if (!_gitHubService.IsConfigured)
        {
            foreach (var game in games)
            {
                game.Status = game.Files.Count > 0 ? SyncStatus.LocalOnly : SyncStatus.Unknown;
            }
            return;
        }

        try
        {
            var catalog = await _gitHubService.GetAllRemoteMetasAsync();
            foreach (var game in games)
            {
                catalog.TryGetValue(game.Id, out var remote);
                UpdateGameStatusWithRemote(game, remote);
            }
        }
        catch
        {
            foreach (var game in games)
            {
                game.Status = game.Files.Count > 0 ? SyncStatus.LocalOnly : SyncStatus.Unknown;
            }
        }
    }

    public async Task<(bool success, string? message)> SyncGameToCloudAsync(DetectedGame game)
    {
        if (game == null) return (false, "Game null");

        game.IsBusy = true;
        game.StatusMessage = "Đang sao lưu...";

        try
        {
            // 1. Create local backup zip
            var backupResult = await _backupService.CreateBackupAsync(game);
            if (!backupResult.Success)
            {
                game.IsBusy = false;
                game.StatusMessage = backupResult.ErrorMessage ?? "Lỗi sao lưu";
                return (false, backupResult.ErrorMessage);
            }

            // 2. Upload to GitHub
            if (_gitHubService.IsConfigured)
            {
                game.StatusMessage = "Đang tải lên GitHub...";
                var uploadResult = await _gitHubService.UploadGameSaveAsync(
                    game.Id,
                    backupResult.ZipPath,
                    backupResult.MetaPath,
                    backupResult.MappingPath);

                if (!uploadResult.success)
                {
                    game.IsBusy = false;
                    game.StatusMessage = uploadResult.error ?? "Lỗi upload GitHub";
                    return (false, uploadResult.error);
                }

                game.Status = SyncStatus.Synced;
                game.StatusMessage = "Đã đồng bộ lên Cloud!";
            }
            else
            {
                game.Status = SyncStatus.LocalOnly;
                game.StatusMessage = "Đã lưu bản sao trên máy (chưa cấu hình GitHub)";
            }

            game.IsBusy = false;
            return (true, game.StatusMessage);
        }
        catch (Exception ex)
        {
            game.IsBusy = false;
            game.StatusMessage = ex.Message;
            return (false, ex.Message);
        }
    }

    public async Task<(bool success, string? message)> RestoreGameFromCloudAsync(DetectedGame game)
    {
        if (game == null) return (false, "Game null");

        game.IsBusy = true;
        game.StatusMessage = "Đang tải từ GitHub...";

        try
        {
            var cacheDir = _backupService.GetGameCacheDir(game.Id);
            if (_gitHubService.IsConfigured)
            {
                var dl = await _gitHubService.DownloadGameSaveAsync(game.Id, cacheDir);
                if (!dl.success)
                {
                    game.IsBusy = false;
                    game.StatusMessage = dl.error ?? "Lỗi tải từ GitHub";
                    return (false, dl.error);
                }
            }

            game.StatusMessage = "Đang giải nén vào máy...";
            var restoreRes = await _restoreService.RestoreGameAsync(game.Id);
            if (!restoreRes.Success)
            {
                game.IsBusy = false;
                game.StatusMessage = restoreRes.ErrorMessage ?? "Lỗi khôi phục save";
                return (false, restoreRes.ErrorMessage);
            }

            game.Status = SyncStatus.Synced;
            game.StatusMessage = $"Đã khôi phục {restoreRes.RestoredFilesCount} files thành công!";
            game.IsBusy = false;
            return (true, game.StatusMessage);
        }
        catch (Exception ex)
        {
            game.IsBusy = false;
            game.StatusMessage = ex.Message;
            return (false, ex.Message);
        }
    }

    public async Task<(bool success, string? message)> UploadCustomManifestAsync()
    {
        if (!_gitHubService.IsConfigured) return (false, "GitHub chưa được cấu hình");
        var customPath = Path.Combine(_configService.CacheDir, "custom_manifest.json");
        if (!File.Exists(customPath)) return (true, null);

        return await _gitHubService.UploadFileAsync("saves/custom_manifest.json", customPath, "Update custom_manifest.json");
    }

    public async Task<(bool success, string? message)> DownloadCustomManifestAsync()
    {
        if (!_gitHubService.IsConfigured) return (false, "GitHub chưa được cấu hình");
        var customPath = Path.Combine(_configService.CacheDir, "custom_manifest.json");
        return await _gitHubService.DownloadFileAsync("saves/custom_manifest.json", customPath);
    }
}
