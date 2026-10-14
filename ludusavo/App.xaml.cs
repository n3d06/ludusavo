using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Media.Animation;
using Microsoft.Extensions.DependencyInjection;
using ludusavo.Services;
using ludusavo.ViewModels;
using ludusavo.Views.Pages;

namespace ludusavo;

public partial class App : System.Windows.Application
{
    private IServiceProvider? _serviceProvider;
    private static Mutex? _mutex;

    static App()
    {
        try
        {
            // Dynamically detect monitor's native refresh rate (e.g. 120Hz, 144Hz, 165Hz, 240Hz)
            var refreshRate = DetectScreenRefreshRate();
            if (refreshRate > 60)
            {
                // Unlock WPF animation and composition clock from default 60 FPS to monitor refresh rate
                Timeline.DesiredFrameRateProperty.OverrideMetadata(
                    typeof(Timeline),
                    new FrameworkPropertyMetadata((int?)refreshRate));
            }
        }
        catch { }
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        const string appName = "ludusavo-SingleInstanceAppMutex";
        _mutex = new Mutex(true, appName, out bool createdNew);

        if (!createdNew)
        {
            System.Windows.MessageBox.Show("Ứng dụng ludusavo đã đang chạy.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
            System.Windows.Application.Current.Shutdown();
            return;
        }

        base.OnStartup(e);

        // Remove legacy startup registry key if previously set
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);
            key?.DeleteValue("ludusavo", false);
        }
        catch { }

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
        mainWindow.Show();
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

    private static void LogCrash(Exception ex)
    {
        try
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "ludusavo");
            Directory.CreateDirectory(dir);
            var logPath = Path.Combine(dir, "crash_log.txt");
            var entry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]\n{ex}\n\n";
            File.AppendAllText(logPath, entry);
        }
        catch { }
    }

    [DllImport("user32.dll")]
    private static extern bool EnumDisplaySettings(string? lpszDeviceName, int iModeNum, ref DEVMODE lpDevMode);

    [DllImport("user32.dll")]
    private static extern IntPtr GetDC(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

    [DllImport("gdi32.dll")]
    private static extern int GetDeviceCaps(IntPtr hdc, int nIndex);

    [StructLayout(LayoutKind.Sequential)]
    private struct DEVMODE
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string dmDeviceName;
        public short dmSpecVersion;
        public short dmDriverVersion;
        public short dmSize;
        public short dmDriverExtra;
        public int dmFields;
        public int dmPositionX;
        public int dmPositionY;
        public int dmDisplayOrientation;
        public int dmDisplayFixedOutput;
        public short dmColor;
        public short dmDuplex;
        public short dmYResolution;
        public short dmTTOption;
        public short dmCollate;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string dmFormName;
        public short dmLogPixels;
        public short dmBitsPerPel;
        public int dmPelsWidth;
        public int dmPelsHeight;
        public int dmDisplayFlags;
        public int dmDisplayFrequency;
        public int dmICMMethod;
        public int dmICMIntent;
        public int dmMediaType;
        public int dmDitherType;
        public int dmReserved1;
        public int dmReserved2;
        public int dmPanningWidth;
        public int dmPanningHeight;
    }

    private static int DetectScreenRefreshRate()
    {
        try
        {
            var devMode = new DEVMODE { dmSize = (short)Marshal.SizeOf<DEVMODE>() };
            if (EnumDisplaySettings(null, -1, ref devMode) && devMode.dmDisplayFrequency > 0)
            {
                return devMode.dmDisplayFrequency;
            }
        }
        catch { }

        try
        {
            var hdc = GetDC(IntPtr.Zero);
            if (hdc != IntPtr.Zero)
            {
                var rate = GetDeviceCaps(hdc, 116); // 116 = VREFRESH
                ReleaseDC(IntPtr.Zero, hdc);
                if (rate > 0) return rate;
            }
        }
        catch { }

        return 60;
    }
}
