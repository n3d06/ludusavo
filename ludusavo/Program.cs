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
        services.AddSingleton<IGitHubService, GitHubService>();
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

                case "config":
                    await sp.GetRequiredService<ConfigHandler>().ExecuteAsync(extraArgs);
                    break;

                case "add":
                    await sp.GetRequiredService<AddHandler>().ExecuteAsync();
                    break;

                case "remove":
                    await sp.GetRequiredService<RemoveHandler>().ExecuteAsync(firstArg);
                    break;

                case "help":
                case "--help":
                case "-h":
                case "/?":
                    ShowHelp();
                    break;

                default:
                    AnsiConsole.MarkupLine($"[red]Lệnh không hợp lệ:[/] '{Markup.Escape(cmd)}'.");
                    AnsiConsole.MarkupLine("[grey]Gõ [bold cyan]sav help[/] để xem danh sách các lệnh hỗ trợ.[/]");
                    break;
            }
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[bold red]Lỗi khi thực thi lệnh '{cmd}':[/] {Markup.Escape(ex.Message)}");
        }
    }

    private static async Task RunInteractiveLoopAsync(IServiceProvider sp)
    {
        ConsoleHelper.ShowHeader();

        var menuOptions = new (string cmd, string desc)[]
        {
            ("ls", "Danh sách game có trên máy"),
            ("lsc", "Danh sách game trên GitHub"),
            ("push", "Nén & đẩy bản save lên GitHub"),
            ("pull", "Tải bản save từ GitHub về máy"),
            ("scan", "Quét lại toàn bộ save game"),
            ("status", "Kiểm tra kết nối GitHub"),
            ("config", "Cấu hình GitHub (Token, Owner, Repo)"),
            ("add", "Thêm game tùy chỉnh (Custom)"),
            ("remove", "Xóa game tùy chỉnh"),
            ("help", "Xem trợ giúp lệnh"),
            ("exit", "Thoát chương trình")
        };

        while (true)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[cyan]Chọn chức năng:[/]");
            for (int i = 0; i < menuOptions.Length; i++)
            {
                var num = (i + 1 == menuOptions.Length) ? "0" : (i + 1).ToString();
                AnsiConsole.WriteLine($"[{num}] {menuOptions[i].cmd,-7} - {menuOptions[i].desc}");
            }

            var input = AnsiConsole.Prompt("\nNhập số hoặc lệnh (0-10):");
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

    private static void ShowHelp()
    {
        AnsiConsole.WriteLine("Sử dụng: sav [lệnh] [tham số]");
        AnsiConsole.WriteLine();
        AnsiConsole.WriteLine("Các lệnh hỗ trợ:");
        AnsiConsole.WriteLine("ls              Liệt kê các game có save trên máy");
        AnsiConsole.WriteLine("lsc             Liệt kê các bản save hiện có trên GitHub");
        AnsiConsole.WriteLine("scan            Quét lại (rescan) toàn bộ database và file save");
        AnsiConsole.WriteLine("push [số|all]   Nén backup và đẩy game lên GitHub");
        AnsiConsole.WriteLine("pull [số|all]   Tải bản save từ GitHub và khôi phục vào máy");
        AnsiConsole.WriteLine("status          Kiểm tra trạng thái kết nối tới GitHub");
        AnsiConsole.WriteLine("config          Xem hoặc sửa thông tin cấu hình GitHub");
        AnsiConsole.WriteLine("add             Thêm game tùy chỉnh (custom game)");
        AnsiConsole.WriteLine("remove [số]     Xóa game tùy chỉnh theo số thứ tự");
        AnsiConsole.WriteLine("help            Hiển thị trợ giúp này");
    }
}

