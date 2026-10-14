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

        ShowHelp();
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<ICredentialManager, WindowsCredentialManager>();
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
                    await sp.GetRequiredService<PushHandler>().ExecuteAsync(extraArgs);
                    break;

                case "pull":
                    await sp.GetRequiredService<PullHandler>().ExecuteAsync(extraArgs);
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
                    AnsiConsole.WriteLine($"Unknown command: '{cmd}'.");
                    AnsiConsole.WriteLine("Type 'sav help' to see the list of supported commands.");
                    break;
            }
        }
        catch (Exception ex)
        {
            AnsiConsole.WriteLine($"Error executing command '{cmd}': {ex.Message}");
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
        AnsiConsole.WriteLine("usage: sav [-v | --version] [-h | --help] <command> [<args>]");
        AnsiConsole.WriteLine();
        AnsiConsole.WriteLine("These are common sav commands used in various situations:");
        AnsiConsole.WriteLine();
        AnsiConsole.WriteLine("examine games and save files");
        AnsiConsole.WriteLine("   ls         List games with detected saves on this PC");
        AnsiConsole.WriteLine("   scan       Rescan all save games and manifest database");
        AnsiConsole.WriteLine();
        AnsiConsole.WriteLine("cloud synchronization (Google Drive)");
        AnsiConsole.WriteLine("   push       Compress and upload save games to Google Drive");
        AnsiConsole.WriteLine("   pull       Download and restore save games from Google Drive");
        AnsiConsole.WriteLine("   lsc        List save backups available on Google Drive");
        AnsiConsole.WriteLine("   status     Check connection and cloud storage status");
        AnsiConsole.WriteLine();
        AnsiConsole.WriteLine("manage custom games");
        AnsiConsole.WriteLine("   add        Add a custom game to tracking list");
        AnsiConsole.WriteLine("   remove     Remove a custom game by index");
        AnsiConsole.WriteLine();
        AnsiConsole.WriteLine("account and configuration");
        AnsiConsole.WriteLine("   login      Log in to Google Drive via browser");
        AnsiConsole.WriteLine("   logout     Log out of current Google Drive account");
        AnsiConsole.WriteLine("   config     View or configure Google Client ID & Secret");
        AnsiConsole.WriteLine();
        AnsiConsole.WriteLine("See 'sav <command> --help' or 'sav help' to read about a specific subcommand.");
    }
}
