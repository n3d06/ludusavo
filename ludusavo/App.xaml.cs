using System.IO;
using System.Threading;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using ludusavo.Services;
using ludusavo.ViewModels;
using ludusavo.Views.Pages;

namespace ludusavo;

public partial class App : System.Windows.Application
{
    private IServiceProvider? _serviceProvider;
    private System.Windows.Forms.NotifyIcon? _notifyIcon;
    private static Mutex? _mutex;

    protected override void OnStartup(StartupEventArgs e)
    {
        const string appName = "ludusavo-SingleInstanceAppMutex";
        _mutex = new Mutex(true, appName, out bool createdNew);

        if (!createdNew)
        {
            System.Windows.MessageBox.Show("Ứng dụng ludusavo đã đang chạy (Vui lòng kiểm tra khay hệ thống / góc phải màn hình).", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
            System.Windows.Application.Current.Shutdown();
            return;
        }

        base.OnStartup(e);

        // Global exception handlers for crash logging
        DispatcherUnhandledException += (s, ex) =>
        {
            LogCrash(ex.Exception);
            ex.Handled = true; // prevent crash, show message instead
            System.Windows.MessageBox.Show(
                $"Đã xảy ra lỗi:\n{ex.Exception.Message}\n\nChi tiết đã lưu vào crash_log.txt",
                "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
        };
        AppDomain.CurrentDomain.UnhandledException += (s, ex) =>
        {
            if (ex.ExceptionObject is Exception e2) LogCrash(e2);
        };

        var services = new ServiceCollection();
        ConfigureServices(services);
        _serviceProvider = services.BuildServiceProvider();

        var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();

        // Setup System Tray
        SetupTrayIcon(mainWindow);

        var startMinimized = e.Args.Contains("--minimized");
        if (!startMinimized)
        {
            mainWindow.Show();
        }
    }

    private void ConfigureServices(IServiceCollection services)
    {
        // Services
        services.AddSingleton<IConfigService, ConfigService>();
        services.AddSingleton<IManifestService, ManifestService>();
        services.AddSingleton<IScannerService, ScannerService>();
        services.AddSingleton<IBackupService, BackupService>();
        services.AddSingleton<IRestoreService, RestoreService>();
        services.AddSingleton<IGitHubService, GitHubService>();
        services.AddSingleton<ISyncService, SyncService>();
        services.AddSingleton<IGameWatcherService, GameWatcherService>();

        // ViewModels
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<GamesViewModel>();
        services.AddSingleton<CloudViewModel>();
        services.AddSingleton<SettingsViewModel>();

        // Views & Pages
        services.AddSingleton<MainWindow>();
        services.AddSingleton<GamesPage>();
        services.AddSingleton<CloudPage>();
        services.AddSingleton<SettingsPage>();
    }

    private void SetupTrayIcon(MainWindow mainWindow)
    {
        try
        {
            System.Drawing.Icon? trayIcon = null;
            var config = _serviceProvider?.GetService<IConfigService>();
            if (config != null)
            {
                var assetIcon = Path.Combine(config.RootDir, "assets", "icon-512.ico");
                if (File.Exists(assetIcon))
                {
                    try { trayIcon = new System.Drawing.Icon(assetIcon); } catch { }
                }
            }

            if (trayIcon == null)
            {
                var exePath = Environment.ProcessPath;
                if (!string.IsNullOrEmpty(exePath) && File.Exists(exePath))
                {
                    try { trayIcon = System.Drawing.Icon.ExtractAssociatedIcon(exePath); } catch { }
                }
            }

            _notifyIcon = new System.Windows.Forms.NotifyIcon
            {
                Text = "ludusavo - Game Save Manager",
                Icon = trayIcon ?? System.Drawing.SystemIcons.Application,
                Visible = true
            };

            _notifyIcon.DoubleClick += (s, e) =>
            {
                mainWindow.Show();
                mainWindow.WindowState = WindowState.Normal;
                mainWindow.Activate();
            };

            var contextMenu = new System.Windows.Forms.ContextMenuStrip();
            contextMenu.Items.Add("Mở ludusavo", null, (s, e) =>
            {
                mainWindow.Show();
                mainWindow.WindowState = WindowState.Normal;
                mainWindow.Activate();
            });



            contextMenu.Items.Add("Thoát", null, (s, e) =>
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
                mainWindow.ForceClose();
                Shutdown();
            });

            _notifyIcon.ContextMenuStrip = contextMenu;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Tray] Could not initialize tray icon: {ex.Message}");
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_notifyIcon != null)
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
        }

        var watcher = _serviceProvider?.GetService<IGameWatcherService>();
        watcher?.Stop();

        base.OnExit(e);
    }

    private static void LogCrash(Exception ex)
    {
        try
        {
            var logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "crash_log.txt");
            var entry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]\n{ex}\n\n";
            File.AppendAllText(logPath, entry);
        }
        catch { }
    }
}
