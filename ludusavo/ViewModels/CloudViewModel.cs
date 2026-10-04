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
    private RemoteGameMeta? _selectedSave;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _statusText = "Ready";

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
            StatusText = "GitHub not configured in Settings.";
            return;
        }

        IsLoading = true;
        StatusText = "Fetching cloud saves from GitHub...";

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
            StatusText = $"Error: {ex.Message}";
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
            RateLimitInfo = $"GitHub API: {rate.remaining}/{rate.limit} requests remaining (resets in {rate.resetMinutes}m)";

            StatusText = $"Found {RemoteSaves.Count} cloud backups on GitHub.";
            IsInitialized = true;

            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                ApplySearchFilter(SearchText);
            }
        }
        catch (Exception ex)
        {
            StatusText = $"Error: {ex.Message}";
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
        StatusText = $"Downloading {meta.GameName} from GitHub...";

        try
        {
            var tempDir = Path.Combine(Path.GetTempPath(), $"savesync_dl_{meta.GameId}");
            var dl = await _gitHubService.DownloadGameSaveAsync(meta.GameId, tempDir);
            if (!dl.success)
            {
                StatusText = $"Download failed: {dl.error}";
                return;
            }

            StatusText = "Restoring saves to local PC...";
            var zipPath = Path.Combine(tempDir, "latest.zip");
            var restore = await _restoreService.RestoreGameAsync(meta.GameId, zipPath);

            if (restore.Success)
            {
                StatusText = $"Successfully restored {restore.RestoredFilesCount} files for {meta.GameName}!";
            }
            else
            {
                StatusText = $"Restore failed: {restore.ErrorMessage}";
            }
        }
        catch (Exception ex)
        {
            StatusText = $"Error: {ex.Message}";
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
            StatusText = "GitHub not configured in Settings.";
            return;
        }

        bool confirm = false;
        await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
        {
            var result = System.Windows.MessageBox.Show(
                $"Are you sure you want to delete the cloud backup for \"{meta.GameName}\" on GitHub?\n\n(Note: Your local save files will not be affected, but the cloud files will be permanently deleted)",
                "Confirm Cloud Backup Deletion",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Warning);
            confirm = result == System.Windows.MessageBoxResult.Yes;
        });

        if (!confirm) return;

        IsLoading = true;
        StatusText = $"Deleting backup for {meta.GameName} on GitHub...";

        try
        {
            var del = await _gitHubService.DeleteGameSaveAsync(meta.GameId);
            if (del.success)
            {
                RemoteSaves.Remove(meta);
                StatusText = $"Successfully deleted cloud backup for {meta.GameName}!";

                var rate = await _gitHubService.GetRateLimitAsync();
                RateLimitInfo = $"GitHub API: {rate.remaining}/{rate.limit} requests remaining (resets in {rate.resetMinutes}m)";
            }
            else
            {
                StatusText = $"Deletion failed: {del.error}";
            }
        }
        catch (Exception ex)
        {
            StatusText = $"Error during deletion: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }
}
