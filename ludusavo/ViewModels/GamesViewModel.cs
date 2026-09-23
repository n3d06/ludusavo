using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ludusavo.Models;
using ludusavo.Services;

namespace ludusavo.ViewModels;

public partial class GamesViewModel : ObservableObject
{
    private readonly IManifestService _manifestService;
    private readonly IScannerService _scannerService;
    private readonly ISyncService _syncService;
    private readonly IGameWatcherService _gameWatcher;

    [ObservableProperty]
    private ObservableCollection<DetectedGame> _games = new();

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private bool _isScanning;

    [ObservableProperty]
    private string _scanStatusText = "Sẵn sàng";

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
        IGameWatcherService gameWatcher)
    {
        _manifestService = manifestService;
        _scannerService = scannerService;
        _syncService = syncService;
        _gameWatcher = gameWatcher;

        _gameWatcher.OnGameExitedAndSynced += game =>
        {
            System.Windows.Application.Current?.Dispatcher?.InvokeAsync(() =>
            {
                ScanStatusText = $"Tự động đồng bộ {game.Name} thành công!";
            });
        };
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
        ScanStatusText = "Đang tải cơ sở dữ liệu game...";

        try
        {
            if (!_manifestService.IsLoaded)
            {
                // Download custom manifest from cloud first if configured
                ScanStatusText = "Đang tải custom_manifest.json từ Cloud (nếu có)...";
                await _syncService.DownloadCustomManifestAsync();

                var loaded = await _manifestService.LoadManifestAsync();
                if (!loaded)
                {
                    ScanStatusText = "Không tìm thấy dữ liệu manifest_processed.json!";
                    IsScanning = false;
                    return;
                }
            }

            ScanStatusText = $"Đang quét hệ thống ({_manifestService.GameCount:N0} games)...";

            var progress = new Progress<(int current, int total, string currentName)>(p =>
            {
                if (p.total > 0)
                {
                    ScanProgress = (double)p.current / p.total * 100;
                    ScanStatusText = $"Đang quét: {p.currentName} ({p.current}/{p.total})";
                }
            });

            var detected = await _scannerService.GetDetectedGamesAsync(
                _manifestService.GetAllGames(),
                forceRescan: forceRescan,
                progress: progress);

            Games.Clear();
            foreach (var g in detected.OrderBy(d => d.Name))
            {
                Games.Add(g);
            }

            ScanStatusText = $"Tìm thấy {Games.Count} game có file save trên máy.";

            // Start watcher for detected games
            _gameWatcher.Start(Games);

            // Check cloud sync statuses in background
            _ = Task.Run(async () =>
            {
                await _syncService.CheckAllSyncStatusesAsync(Games);
            });
        }
        catch (Exception ex)
        {
            ScanStatusText = $"Lỗi quét: {ex.Message}";
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
    public async Task AddCustomGameAsync()
    {
        // 1. Pick Folder
        using var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "Chọn thư mục lưu save game",
            UseDescriptionForTitle = true
        };

        if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
            return;

        var folderPath = dialog.SelectedPath;

        // 2. Input Name & Banner
        string gameName = "";
        string bannerUrl = "";
        await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
        {
            var window = new System.Windows.Window
            {
                Title = "Thêm Game Custom",
                Width = 350,
                Height = 220,
                WindowStartupLocation = System.Windows.WindowStartupLocation.CenterScreen,
                ResizeMode = System.Windows.ResizeMode.NoResize
            };
            var tbName = new System.Windows.Controls.TextBox { Margin = new System.Windows.Thickness(10) };
            var tbBanner = new System.Windows.Controls.TextBox { Margin = new System.Windows.Thickness(10) };
            var btn = new System.Windows.Controls.Button { Content = "OK", Margin = new System.Windows.Thickness(10), IsDefault = true };
            btn.Click += (s, e) => window.DialogResult = true;
            
            var stack = new System.Windows.Controls.StackPanel();
            stack.Children.Add(new System.Windows.Controls.TextBlock { Text = "Nhập tên game:", Margin = new System.Windows.Thickness(10,10,10,0) });
            stack.Children.Add(tbName);
            stack.Children.Add(new System.Windows.Controls.TextBlock { Text = "Link ảnh Banner (Tùy chọn):", Margin = new System.Windows.Thickness(10,10,10,0) });
            stack.Children.Add(tbBanner);
            stack.Children.Add(btn);
            window.Content = stack;

            if (window.ShowDialog() == true)
            {
                gameName = tbName.Text.Trim();
                bannerUrl = tbBanner.Text.Trim();
            }
        });

        if (string.IsNullOrEmpty(gameName)) return;

        // Convert absolute path to a placeholder path (e.g. <appdata>/...) for cross-PC compatibility
        var portablePath = _scannerService.ToPlaceholderPath(folderPath);

        var newGame = new GameEntry
        {
            Id = "custom_" + Guid.NewGuid().ToString("N").Substring(0, 8),
            Name = gameName,
            CustomBannerUrl = string.IsNullOrEmpty(bannerUrl) ? null : bannerUrl,
            SavePaths = new List<SavePathEntry>
            {
                new SavePathEntry { Path = portablePath }
            }
        };

        ScanStatusText = $"Đang thêm game {gameName}...";
        await _manifestService.AddCustomGameAsync(newGame);
        
        ScanStatusText = "Đang đồng bộ custom_manifest.json lên Cloud...";
        await _syncService.UploadCustomManifestAsync();

        ScanStatusText = "Thêm thành công! Đang tải lại danh sách...";
        // Reload games list
        await LoadAndScanGamesAsync(forceRescan: true);
    }

    [RelayCommand]
    public async Task EditCustomGameAsync(DetectedGame? game)
    {
        if (game == null || !game.IsCustomGame) return;

        var existingMeta = _manifestService.GetGameById(game.Id);
        if (existingMeta == null) return;

        string currentPath = existingMeta.SavePaths.FirstOrDefault()?.Path ?? "";

        // 1. Pick Folder
        using var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "Chọn thư mục lưu save game",
            UseDescriptionForTitle = true
        };

        if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
            return;

        var folderPath = dialog.SelectedPath;

        // 2. Input Name & Banner
        string gameName = "";
        string bannerUrl = "";
        await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
        {
            var window = new System.Windows.Window
            {
                Title = "Sửa Game Custom",
                Width = 350,
                Height = 220,
                WindowStartupLocation = System.Windows.WindowStartupLocation.CenterScreen,
                ResizeMode = System.Windows.ResizeMode.NoResize
            };
            var tbName = new System.Windows.Controls.TextBox { Margin = new System.Windows.Thickness(10), Text = game.Name };
            var tbBanner = new System.Windows.Controls.TextBox { Margin = new System.Windows.Thickness(10), Text = game.CustomBannerUrl };
            var btn = new System.Windows.Controls.Button { Content = "Lưu", Margin = new System.Windows.Thickness(10), IsDefault = true };
            btn.Click += (s, e) => window.DialogResult = true;
            
            var stack = new System.Windows.Controls.StackPanel();
            stack.Children.Add(new System.Windows.Controls.TextBlock { Text = "Sửa tên game:", Margin = new System.Windows.Thickness(10,10,10,0) });
            stack.Children.Add(tbName);
            stack.Children.Add(new System.Windows.Controls.TextBlock { Text = "Link ảnh Banner (Tùy chọn):", Margin = new System.Windows.Thickness(10,10,10,0) });
            stack.Children.Add(tbBanner);
            stack.Children.Add(btn);
            window.Content = stack;

            if (window.ShowDialog() == true)
            {
                gameName = tbName.Text?.Trim() ?? string.Empty;
                bannerUrl = tbBanner.Text?.Trim() ?? string.Empty;
            }
        });

        if (string.IsNullOrEmpty(gameName)) return;

        var portablePath = _scannerService.ToPlaceholderPath(folderPath);

        var updatedGame = new GameEntry
        {
            Id = game.Id,
            Name = gameName,
            CustomBannerUrl = string.IsNullOrEmpty(bannerUrl) ? null : bannerUrl,
            SavePaths = new List<SavePathEntry>
            {
                new SavePathEntry { Path = portablePath }
            }
        };

        ScanStatusText = $"Đang cập nhật game {gameName}...";
        await _manifestService.AddCustomGameAsync(updatedGame);
        
        ScanStatusText = "Đang đồng bộ custom_manifest.json lên Cloud...";
        await _syncService.UploadCustomManifestAsync();

        ScanStatusText = "Cập nhật thành công! Đang tải lại danh sách...";
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
                $"Bạn có chắc muốn xóa game {game.Name} khỏi danh sách Custom không?\n(Lưu ý: Không xóa file save của bạn)",
                "Xác nhận xóa",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Question);
            confirm = result == System.Windows.MessageBoxResult.Yes;
        });

        if (!confirm) return;

        ScanStatusText = $"Đang xóa game {game.Name}...";
        await _manifestService.RemoveCustomGameAsync(game.Id);
        
        ScanStatusText = "Đang đồng bộ custom_manifest.json lên Cloud...";
        await _syncService.UploadCustomManifestAsync();

        ScanStatusText = "Xóa thành công! Đang tải lại danh sách...";
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
