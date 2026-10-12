using System.Reflection;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using ludusavo.Commands;
using ludusavo.Services;

namespace ludusavo;

public static class Program
{
    public static async Task Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        Console.InputEncoding = Encoding.UTF8;

        var services = new ServiceCollection();
        ConfigureServices(services);
        var serviceProvider = services.BuildServiceProvider();

        if (args.Length > 0)
        {
            await RunSingleCommandAsync(serviceProvider, args);
            return;
        }

        // Chế độ Interactive khi không truyền tham số
        await RunInteractiveLoopAsync(serviceProvider);
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<IConfigService, ConfigService>();
        services.AddSingleton<IManifestService, ManifestService>();
        services.AddSingleton<IScannerService, ScannerService>();
        services.AddSingleton<IBackupService, BackupService>();
        services.AddSingleton<IRestoreService, RestoreService>();
        services.AddSingleton<IGoogleDriveService, GoogleDriveService>();
        services.AddSingleton<ISyncService, SyncService>();

        // Handlers
        services.AddTransient<StatusHandler>();
        services.AddTransient<ConfigHandler>();
        services.AddTransient<LsHandler>();
        services.AddTransient<LscHandler>();
        services.AddTransient<SamHandler>();
        services.AddTransient<PushHandler>();
        services.AddTransient<PullHandler>();
        services.AddTransient<AddHandler>();
        services.AddTransient<RemoveHandler>();
    }

    private static async Task RunSingleCommandAsync(IServiceProvider sp, string[] args)
    {
        var cmd = args[0].ToLowerInvariant();
        var extraArgs = args.Length > 1 ? args.Skip(1).ToArray() : Array.Empty<string>();
        var firstArg = extraArgs.Length > 0 ? extraArgs[0] : null;

        try
        {
            switch (cmd)
            {
                case "ls":
                    await sp.GetRequiredService<LsHandler>().ExecuteAsync();
                    break;

                case "lsc":
                    await sp.GetRequiredService<LscHandler>().ExecuteAsync();
                    break;

                case "scan":
                case "sam":
                    await sp.GetRequiredService<SamHandler>().ExecuteAsync();
                    break;

                case "push":
                    await sp.GetRequiredService<PushHandler>().ExecuteAsync(firstArg);
                    break;

                case "pull":
                    await sp.GetRequiredService<PullHandler>().ExecuteAsync(firstArg);
                    break;

                case "status":
                    await sp.GetRequiredService<StatusHandler>().ExecuteAsync();
                    break;

                case "login":
                    await sp.GetRequiredService<ConfigHandler>().LoginAsync();
                    break;

                case "logout":
                    await sp.GetRequiredService<ConfigHandler>().LogoutAsync();
                    break;

                case "config":
                    await sp.GetRequiredService<ConfigHandler>().ExecuteAsync(extraArgs);
                    break;

                case "add":
                    await sp.GetRequiredService<AddHandler>().ExecuteAsync();
                    break;

                case "remove":
                    await sp.GetRequiredService<RemoveHandler>().ExecuteAsync(firstArg);
                    break;

                case "version":
                case "--version":
                case "-v":
                    ShowVersion();
                    break;

                case "help":
                case "--help":
                case "-h":
                case "/?":
                    ShowHelp();
                    break;

                default:
                    AnsiConsole.WriteLine($"Lệnh không hợp lệ: '{cmd}'.");
                    AnsiConsole.WriteLine("Gõ 'sav help' để xem danh sách các lệnh hỗ trợ.");
                    break;
            }
        }
        catch (Exception ex)
        {
            AnsiConsole.WriteLine($"Lỗi khi thực thi lệnh '{cmd}': {ex.Message}");
        }
    }

    private static async Task RunInteractiveLoopAsync(IServiceProvider sp)
    {
        ConsoleHelper.ShowHeader();

        var menuOptions = new (string cmd, string desc)[]
        {
            ("ls", "Danh sách game có trên máy"),
            ("lsc", "Danh sách game trên Google Drive"),
            ("push", "Nén & đẩy bản save lên Google Drive"),
            ("pull", "Tải bản save từ Google Drive về máy"),
            ("scan", "Quét lại toàn bộ save game"),
            ("status", "Kiểm tra kết nối Google Drive"),
            ("login", "Đăng nhập Google Drive qua trình duyệt"),
            ("logout", "Đăng xuất tài khoản Google Drive"),
            ("config", "Cấu hình Google Client ID & Secret"),
            ("add", "Thêm game tùy chỉnh (Custom)"),
            ("remove", "Xóa game tùy chỉnh"),
            ("help", "Xem trợ giúp lệnh"),
            ("exit", "Thoát chương trình")
        };

        while (true)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.WriteLine("Chọn chức năng:");
            for (int i = 0; i < menuOptions.Length; i++)
            {
                var num = (i + 1 == menuOptions.Length) ? "0" : (i + 1).ToString();
                AnsiConsole.WriteLine($"[{num}] {menuOptions[i].cmd,-7} - {menuOptions[i].desc}");
            }

            var input = AnsiConsole.Prompt("\nNhập số hoặc lệnh (0-12):");
            if (string.IsNullOrWhiteSpace(input)) continue;

            string cmd = input.ToLowerInvariant();
            if (int.TryParse(input, out int choiceNum))
            {
                if (choiceNum == 0 || choiceNum == menuOptions.Length)
                {
                    break;
                }
                if (choiceNum >= 1 && choiceNum < menuOptions.Length)
                {
                    cmd = menuOptions[choiceNum - 1].cmd;
                }
            }

            if (cmd == "exit" || cmd == "0")
            {
                break;
            }

            AnsiConsole.WriteLine();
            await RunSingleCommandAsync(sp, new[] { cmd });
        }
    }

    private static void ShowVersion()
    {
        var asm = typeof(Program).Assembly;
        var infoVer = asm.GetCustomAttribute<System.Reflection.AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (!string.IsNullOrWhiteSpace(infoVer))
        {
            var clean = infoVer.Split('+')[0].TrimStart('v');
            AnsiConsole.WriteLine($"sav version {clean}");
            return;
        }

        var ver = asm.GetName().Version?.ToString(3) ?? "1.0.0";
        AnsiConsole.WriteLine($"sav version {ver}");
    }

    private static void ShowHelp()
    {
        AnsiConsole.WriteLine("Sử dụng: sav [lệnh] [tham số]");
        AnsiConsole.WriteLine();
        AnsiConsole.WriteLine("Các lệnh hỗ trợ:");
        AnsiConsole.WriteLine("ls              Liệt kê các game có save trên máy");
        AnsiConsole.WriteLine("lsc             Liệt kê các bản save hiện có trên Google Drive");
        AnsiConsole.WriteLine("scan            Quét lại (rescan) toàn bộ database và file save");
        AnsiConsole.WriteLine("push [số|all]   Nén backup và đẩy game lên Google Drive");
        AnsiConsole.WriteLine("pull [số|all]   Tải bản save từ Google Drive và khôi phục vào máy");
        AnsiConsole.WriteLine("status          Kiểm tra trạng thái kết nối tới Google Drive");
        AnsiConsole.WriteLine("login           Đăng nhập tài khoản Google Drive qua trình duyệt");
        AnsiConsole.WriteLine("logout          Đăng xuất tài khoản Google Drive hiện tại");
        AnsiConsole.WriteLine("config          Xem hoặc sửa cấu hình Google Client ID & Secret");
        AnsiConsole.WriteLine("version         Hiển thị phiên bản ứng dụng");
        AnsiConsole.WriteLine("add             Thêm game tùy chỉnh (custom game)");
        AnsiConsole.WriteLine("remove [số]     Xóa game tùy chỉnh theo số thứ tự");
        AnsiConsole.WriteLine("help            Hiển thị trợ giúp này");
    }
}
