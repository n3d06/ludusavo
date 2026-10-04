using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ludusavo.Models;
using ludusavo.Services;
using ludusavo.Views.Dialogs;

namespace ludusavo.ViewModels;

public partial class GamesViewModel : ObservableObject
{
    private readonly IManifestService _manifestService;
    private readonly IScannerService _scannerService;
    private readonly ISyncService _syncService;
    private readonly IConfigService _configService;
    private readonly CloudViewModel _cloudViewModel;
    private readonly IGitHubService _gitHubService;

    [ObservableProperty]
    private ObservableCollection<DetectedGame> _games = new();

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private bool _isScanning;

    [ObservableProperty]
    private string _scanStatusText = "Ready";

    [ObservableProperty]
    private double _scanProgress;

    [ObservableProperty]
    private DetectedGame? _selectedGame;

    [ObservableProperty]
    private bool _isCompactMode;

    public GamesViewModel(
        IManifestService manifestService,
        IScannerService scannerService,
        ISyncService syncService,
        IConfigService configService,
        CloudViewModel cloudViewModel,
        IGitHubService gitHubService)
    {
        _manifestService = manifestService;
        _scannerService = scannerService;
        _syncService = syncService;
        _configService = configService;
        _cloudViewModel = cloudViewModel;
        _gitHubService = gitHubService;
    }

    public async Task InitializeAsync()
    {
        if (Games.Count > 0) return;
        await LoadAndScanGamesAsync(forceRescan: false);
    }

    [RelayCommand]
    public async Task RefreshScanAsync()
    {
        await LoadAndScanGamesAsync(forceRescan: true);
    }

    private async Task LoadAndScanGamesAsync(bool forceRescan)
    {
        if (IsScanning) return;

        IsScanning = true;
        ScanProgress = 0;
        ScanStatusText = "Loading game database...";

        try
        {
            if (!_manifestService.IsLoaded || forceRescan)
            {
                ScanStatusText = "Syncing manifest and cloud saves...";

                // Fetch manifest and cloud save metadata once on startup/refresh
                var downloadManifestTask = _syncService.DownloadCustomManifestAsync();
                var getRemoteMetasTask = _gitHubService.GetAllRemoteMetasAsync(forceRefresh: forceRescan);

                await Task.WhenAll(downloadManifestTask, getRemoteMetasTask);

                if (!_manifestService.IsLoaded)
                {
                    if (!File.Exists(_configService.ManifestCachePath))
                    {
                        ScanStatusText = "Downloading game database (first-time setup)...";
                    }

                    var loaded = await _manifestService.LoadManifestAsync();
                    if (!loaded)
                    {
                        ScanStatusText = "Failed to load or download game database!";
                        IsScanning = false;
                        return;
                    }
                }

                // Update cloud saves into CloudViewModel immediately
                var remoteMetas = await getRemoteMetasTask;
                _ = _cloudViewModel.PopulateFromMetasAsync(remoteMetas);
            }
            else if (!_cloudViewModel.IsInitialized)
            {
                _ = _cloudViewModel.InitializeAsync();
            }

            ScanStatusText = $"Scanning system ({_manifestService.GameCount:N0} games)...";

            var progress = new Progress<(int current, int total, string currentName)>(p =>
            {
                if (p.total > 0)
                {
                    ScanProgress = (double)p.current / p.total * 100;
                    ScanStatusText = $"Scanning: {p.currentName} ({p.current}/{p.total})";
                }
            });

            var detected = await _scannerService.GetDetectedGamesAsync(
                _manifestService.GetAllGames(),
                forceRescan: forceRescan,
                progress: progress);

            // Apply pinned state from local settings
            var pinnedIds = _configService.Settings.PinnedGameIds ?? new List<string>();
            foreach (var g in detected)
            {
                g.IsPinned = pinnedIds.Contains(g.Id);
            }

            Games.Clear();
            // Pinned games first, then alphabetical
            foreach (var g in detected.OrderByDescending(d => d.IsPinned).ThenBy(d => d.Name))
            {
                Games.Add(g);
            }

            ScanStatusText = $"Found {Games.Count} games with saves on this PC.";

            // Check cloud sync statuses in background
            _ = Task.Run(async () =>
            {
                await _syncService.CheckAllSyncStatusesAsync(Games);
            });
        }
        catch (Exception ex)
        {
            ScanStatusText = $"Scan error: {ex.Message}";
        }
        finally
        {
            IsScanning = false;
        }
    }

    [RelayCommand]
    public async Task SyncGameAsync(DetectedGame? game)
    {
        if (game == null) return;
        await _syncService.SyncGameToCloudAsync(game);
    }

    [RelayCommand]
    public async Task RestoreGameAsync(DetectedGame? game)
    {
        if (game == null) return;
        await _syncService.RestoreGameFromCloudAsync(game);
    }


    [RelayCommand]
    public void OpenSaveFolder(DetectedGame? game)
    {
        if (game == null || game.Files.Count == 0) return;
        var firstFile = game.Files.FirstOrDefault()?.AbsolutePath;
        if (!string.IsNullOrEmpty(firstFile))
        {
            var dir = Path.GetDirectoryName(firstFile);
            if (Directory.Exists(dir))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = dir,
                    UseShellExecute = true
                });
            }
        }
    }

    [RelayCommand]
    public void TogglePin(DetectedGame? game)
    {
        if (game == null) return;

        game.IsPinned = !game.IsPinned;

        // Update local settings
        var pinnedIds = _configService.Settings.PinnedGameIds ?? new List<string>();
        if (game.IsPinned)
        {
            if (!pinnedIds.Contains(game.Id))
                pinnedIds.Add(game.Id);
        }
        else
        {
            pinnedIds.Remove(game.Id);
        }
        _configService.Settings.PinnedGameIds = pinnedIds;
        _configService.SaveSettings(_configService.Settings);

        // Re-sort the list: pinned first, then alphabetical
        var sorted = Games.OrderByDescending(g => g.IsPinned).ThenBy(g => g.Name).ToList();
        Games.Clear();
        foreach (var g in sorted)
        {
            Games.Add(g);
        }
    }

    [RelayCommand]
    public async Task AddCustomGameAsync()
    {
        var dialog = new CustomGameDialog("Add Custom Game");
        if (dialog.ShowDialog() != true)
            return;

        var gameName = dialog.GameTitle;
        var folderPath = dialog.FolderPath;
        var steamId = dialog.SteamId;
        var bannerUrl = dialog.BannerUrl;

        // Convert absolute path to a placeholder path (e.g. <appdata>/...) for cross-PC compatibility
        var portablePath = _scannerService.ToPlaceholderPath(folderPath);

        var newGame = new GameEntry
        {
            Id = "custom_" + Guid.NewGuid().ToString("N").Substring(0, 8),
            Name = gameName,
            SteamId = steamId,
            CustomBannerUrl = string.IsNullOrEmpty(bannerUrl) ? null : bannerUrl,
            SavePaths = new List<SavePathEntry>
            {
                new SavePathEntry { Path = portablePath }
            }
        };

        ScanStatusText = $"Adding game {gameName}...";
        await _manifestService.AddCustomGameAsync(newGame);
        
        ScanStatusText = "Syncing custom_manifest.json to Cloud...";
        await _syncService.UploadCustomManifestAsync();

        ScanStatusText = "Game added! Reloading list...";
        // Reload games list
        await LoadAndScanGamesAsync(forceRescan: true);
    }

    [RelayCommand]
    public async Task EditCustomGameAsync(DetectedGame? game)
    {
        if (game == null || !game.IsCustomGame) return;

        var existingMeta = _manifestService.GetGameById(game.Id);
        if (existingMeta == null) return;

        string currentFolder = string.Empty;
        if (game.Files.Count > 0 && !string.IsNullOrEmpty(game.Files[0].AbsolutePath))
        {
            currentFolder = Path.GetDirectoryName(game.Files[0].AbsolutePath) ?? string.Empty;
        }
        else if (existingMeta.SavePaths.Count > 0)
        {
            currentFolder = existingMeta.SavePaths[0].Path;
        }

        var dialog = new CustomGameDialog("Edit Custom Game", game.Name, currentFolder, game.SteamId, game.CustomBannerUrl);
        if (dialog.ShowDialog() != true)
            return;

        var gameName = dialog.GameTitle;
        var folderPath = dialog.FolderPath;
        var steamId = dialog.SteamId;
        var bannerUrl = dialog.BannerUrl;

        var portablePath = _scannerService.ToPlaceholderPath(folderPath);

        var updatedGame = new GameEntry
        {
            Id = game.Id,
            Name = gameName,
            SteamId = steamId,
            CustomBannerUrl = string.IsNullOrEmpty(bannerUrl) ? null : bannerUrl,
            SavePaths = new List<SavePathEntry>
            {
                new SavePathEntry { Path = portablePath }
            }
        };

        ScanStatusText = $"Updating game {gameName}...";
        await _manifestService.AddCustomGameAsync(updatedGame);
        
        ScanStatusText = "Syncing custom_manifest.json to Cloud...";
        await _syncService.UploadCustomManifestAsync();

        ScanStatusText = "Game updated! Reloading list...";
        await LoadAndScanGamesAsync(forceRescan: true);
    }

    [RelayCommand]
    public async Task DeleteCustomGameAsync(DetectedGame? game)
    {
        if (game == null || !game.IsCustomGame) return;

        bool confirm = false;
        await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
        {
            var result = System.Windows.MessageBox.Show(
                $"Are you sure you want to remove \"{game.Name}\" from Custom games?\n(Note: Your local save files will not be deleted)",
                "Confirm Removal",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Question);
            confirm = result == System.Windows.MessageBoxResult.Yes;
        });

        if (!confirm) return;

        ScanStatusText = $"Removing game {game.Name}...";
        await _manifestService.RemoveCustomGameAsync(game.Id);
        
        ScanStatusText = "Syncing custom_manifest.json to Cloud...";
        await _syncService.UploadCustomManifestAsync();

        ScanStatusText = "Game removed! Reloading list...";
        await LoadAndScanGamesAsync(forceRescan: true);
    }

    private System.Threading.CancellationTokenSource? _searchCts;

    partial void OnSearchTextChanged(string value)
    {
        _searchCts?.Cancel();
        _searchCts = new System.Threading.CancellationTokenSource();
        var token = _searchCts.Token;

        _ = System.Threading.Tasks.Task.Delay(300, token).ContinueWith(t =>
        {
            if (t.IsCanceled) return;

            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                var view = CollectionViewSource.GetDefaultView(Games);
                if (view == null) return;

                if (string.IsNullOrWhiteSpace(value))
                {
                    view.Filter = null;
                }
                else
                {
                    var q = value.Trim();
                    view.Filter = obj =>
                    {
                        if (obj is DetectedGame g)
                        {
                            return g.Name.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                                   g.Id.Contains(q, StringComparison.OrdinalIgnoreCase);
                        }
                        return false;
                    };
                }
            });
        }, System.Threading.Tasks.TaskScheduler.Default);
    }
}
