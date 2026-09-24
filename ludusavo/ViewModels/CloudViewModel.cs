using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ludusavo.Models;
using ludusavo.Services;

namespace ludusavo.ViewModels;

public partial class CloudViewModel : ObservableObject
{
    private readonly IGitHubService _gitHubService;
    private readonly IRestoreService _restoreService;
    private readonly IManifestService _manifestService;

    [ObservableProperty]
    private ObservableCollection<RemoteGameMeta> _remoteSaves = new();

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _statusText = "Sẵn sàng";

    [ObservableProperty]
    private string _rateLimitInfo = string.Empty;

    [ObservableProperty]
    private bool _isCompactMode;

    [ObservableProperty]
    private bool _isInitialized;

    private System.Threading.CancellationTokenSource? _searchCts;

    partial void OnSearchTextChanged(string value)
    {
        _searchCts?.Cancel();
        _searchCts = new System.Threading.CancellationTokenSource();
        var token = _searchCts.Token;

        _ = System.Threading.Tasks.Task.Delay(250, token).ContinueWith(t =>
        {
            if (t.IsCanceled) return;

            System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
            {
                ApplySearchFilter(value);
            });
        }, System.Threading.Tasks.TaskScheduler.Default);
    }

    public void ApplySearchFilter(string? query)
    {
        var view = CollectionViewSource.GetDefaultView(RemoteSaves);
        if (view == null) return;

        if (string.IsNullOrWhiteSpace(query))
        {
            view.Filter = null;
        }
        else
        {
            var q = query.Trim();
            view.Filter = obj =>
            {
                if (obj is RemoteGameMeta m)
                {
                    return m.GameName.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                           m.GameId.Contains(q, StringComparison.OrdinalIgnoreCase);
                }
                return false;
            };
        }
    }

    public CloudViewModel(
        IGitHubService gitHubService,
        IRestoreService restoreService,
        IManifestService manifestService)
    {
        _gitHubService = gitHubService;
        _restoreService = restoreService;
        _manifestService = manifestService;
    }

    public async Task InitializeAsync(bool forceRefresh = false)
    {
        if (IsInitialized && !forceRefresh) return;
        await RefreshCloudSavesInternalAsync(forceRefresh);
    }

    [RelayCommand]
    public async Task RefreshCloudSavesAsync()
    {
        await RefreshCloudSavesInternalAsync(forceRefresh: true);
    }

    public async Task RefreshCloudSavesInternalAsync(bool forceRefresh = false)
    {
        if (!_gitHubService.IsConfigured)
        {
            StatusText = "Chưa cấu hình GitHub trong Cài đặt.";
            return;
        }

        IsLoading = true;
        StatusText = "Đang tải danh sách save từ GitHub...";

        try
        {
            if (!_manifestService.IsLoaded)
            {
                await _manifestService.LoadManifestAsync();
            }

            var allMetas = await _gitHubService.GetAllRemoteMetasAsync(forceRefresh);
            await PopulateFromMetasAsync(allMetas);
        }
        catch (Exception ex)
        {
            StatusText = $"Lỗi: {ex.Message}";
            IsLoading = false;
        }
    }

    public async Task PopulateFromMetasAsync(Dictionary<string, RemoteGameMeta> allMetas)
    {
        try
        {
            if (!_manifestService.IsLoaded)
            {
                await _manifestService.LoadManifestAsync();
            }

            RemoteSaves.Clear();

            foreach (var kvp in allMetas)
            {
                var meta = kvp.Value;
                var gameEntry = _manifestService.GetGameById(meta.GameId);
                if (gameEntry != null)
                {
                    meta.SteamId = gameEntry.GetSteamAppId();
                    meta.CustomBannerUrl = gameEntry.CustomBannerUrl;
                }
                RemoteSaves.Add(meta);
            }

            var rate = await _gitHubService.GetRateLimitAsync();
            RateLimitInfo = $"GitHub API: {rate.remaining}/{rate.limit} requests còn lại (reset sau {rate.resetMinutes}p)";

            StatusText = $"Tìm thấy {RemoteSaves.Count} bản sao lưu trên GitHub.";
            IsInitialized = true;

            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                ApplySearchFilter(SearchText);
            }
        }
        catch (Exception ex)
        {
            StatusText = $"Lỗi: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public async Task DownloadAndRestoreAsync(RemoteGameMeta? meta)
    {
        if (meta == null) return;

        IsLoading = true;
        StatusText = $"Đang tải {meta.GameName} từ GitHub...";

        try
        {
            var tempDir = Path.Combine(Path.GetTempPath(), $"savesync_dl_{meta.GameId}");
            var dl = await _gitHubService.DownloadGameSaveAsync(meta.GameId, tempDir);
            if (!dl.success)
            {
                StatusText = $"Tải thất bại: {dl.error}";
                return;
            }

            StatusText = $"Đang khôi phục save vào máy...";
            var zipPath = Path.Combine(tempDir, "latest.zip");
            var restore = await _restoreService.RestoreGameAsync(meta.GameId, zipPath);

            if (restore.Success)
            {
                StatusText = $"Đã khôi phục thành công {restore.RestoredFilesCount} files của {meta.GameName}!";
            }
            else
            {
                StatusText = $"Khôi phục thất bại: {restore.ErrorMessage}";
            }
        }
        catch (Exception ex)
        {
            StatusText = $"Lỗi: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public async Task DeleteCloudSaveAsync(RemoteGameMeta? meta)
    {
        if (meta == null) return;

        if (!_gitHubService.IsConfigured)
        {
            StatusText = "Chưa cấu hình GitHub trong Cài đặt.";
            return;
        }

        bool confirm = false;
        await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
        {
            var result = System.Windows.MessageBox.Show(
                $"Bạn có chắc chắn muốn xóa bản sao lưu của \"{meta.GameName}\" trên Cloud GitHub không?\n\n(Lưu ý: Không ảnh hưởng đến file save trên máy tính của bạn, nhưng file trên GitHub sẽ bị xóa vĩnh viễn)",
                "Xác nhận xóa sao lưu Cloud",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Warning);
            confirm = result == System.Windows.MessageBoxResult.Yes;
        });

        if (!confirm) return;

        IsLoading = true;
        StatusText = $"Đang xóa bản sao lưu của {meta.GameName} trên GitHub...";

        try
        {
            var del = await _gitHubService.DeleteGameSaveAsync(meta.GameId);
            if (del.success)
            {
                RemoteSaves.Remove(meta);
                StatusText = $"Đã xóa thành công bản sao lưu của {meta.GameName} trên Cloud!";

                var rate = await _gitHubService.GetRateLimitAsync();
                RateLimitInfo = $"GitHub API: {rate.remaining}/{rate.limit} requests còn lại (reset sau {rate.resetMinutes}p)";
            }
            else
            {
                StatusText = $"Xóa thất bại: {del.error}";
            }
        }
        catch (Exception ex)
        {
            StatusText = $"Lỗi khi xóa: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }
}
