using System.IO;
using System.Text.Json;
using ludusavo.Services;

namespace ludusavo.Commands;

public class StatusHandler
{
    private readonly IConfigService _configService;
    private readonly IGoogleDriveService _driveService;

    public StatusHandler(IConfigService configService, IGoogleDriveService driveService)
    {
        _configService = configService;
        _driveService = driveService;
    }

    public async Task ExecuteAsync()
    {
        var settings = _configService.Settings;

        // 1. Client ID info
        string idInfo;
        if (!string.IsNullOrWhiteSpace(settings.GoogleClientId))
        {
            idInfo = settings.GoogleClientId.Length > 12
                ? settings.GoogleClientId.Substring(0, 8) + "..."
                : settings.GoogleClientId;
        }
        else if (DefaultConfig.HasEmbeddedCredentials)
        {
            idInfo = "Default (embedded)";
        }
        else
        {
            idInfo = "[red]Not configured[/]";
        }

        // 2. Account info
        var accountInfo = !string.IsNullOrWhiteSpace(settings.UserEmail)
            ? settings.UserEmail
            : "[dim]Not logged in[/]";

        // 3. Connection state
        string connectionStatus;
        string? hint = null;

        if (!settings.HasCredentials)
        {
            connectionStatus = "[yellow]Action required[/] [dim](OAuth Client ID is missing)[/]";
            hint = "Run 'sav config' to enter Google Client ID & Secret, then 'sav login'.";
        }
        else if (string.IsNullOrWhiteSpace(settings.GoogleRefreshToken))
        {
            connectionStatus = "[red]Not logged in[/]";
            hint = "Run 'sav login' to log in with your Google account via browser.";
        }
        else
        {
            bool connected = false;
            string? message = null;
            long used = 0, limit = 0;

            await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .StartAsync("Testing connection to Google Drive API...", async ctx =>
                {
                    try
                    {
                        var quota = await _driveService.GetStorageQuotaAsync();
                        used = quota.usedBytes;
                        limit = quota.totalBytes;
                        connected = limit > 0 || !string.IsNullOrEmpty(quota.userEmail);
                    }
                    catch (Exception ex)
                    {
                        connected = false;
                        message = ex.Message;
                    }
                });

            if (connected)
            {
                var storageInfo = limit > 0
                    ? $" [dim](Storage: {ConsoleHelper.FormatSize(used)} / {ConsoleHelper.FormatSize(limit)})[/]"
                    : "";
                connectionStatus = $"[green]Connected[/]{storageInfo}";
                hint = "Run 'sav ls' to check save sync status, or 'sav push' to backup.";
            }
            else
            {
                connectionStatus = $"[red]Connection failed[/] [dim]({message ?? "Token expired"})[/]";
                hint = "Run 'sav login' to re-authenticate with Google Drive.";
            }
        }

        // 4. Local machine info
        int detectedCount = 0;
        var cachePath = Path.Combine(_configService.CacheDir, "detected_games.json");
        if (File.Exists(cachePath))
        {
            try
            {
                var json = File.ReadAllText(cachePath);
                var ids = JsonSerializer.Deserialize<List<string>>(json);
                detectedCount = ids?.Count ?? 0;
            }
            catch { }
        }

        var gamesInfo = detectedCount > 0
            ? $"{detectedCount} game{(detectedCount > 1 ? "s" : "")} detected"
            : "[dim]No games scanned yet (run 'sav scan')[/]";

        // 5. Render output
        AnsiConsole.WriteLine("Google Drive");
        AnsiConsole.MarkupLine($"  Account:       {accountInfo}");
        AnsiConsole.MarkupLine($"  Status:        {connectionStatus}");
        AnsiConsole.WriteLine("  Cloud Storage: Google Drive ('sav' folder)");
        AnsiConsole.MarkupLine($"  Client ID:     {idInfo}");
        AnsiConsole.WriteLine();

        AnsiConsole.WriteLine("Local Machine");
        AnsiConsole.MarkupLine($"  Games:         {gamesInfo}");
        AnsiConsole.MarkupLine($"  Config:        [dim]{_configService.ConfigDir}[/]");
        AnsiConsole.MarkupLine($"  Cache:         [dim]{_configService.CacheDir}[/]");

        if (!string.IsNullOrEmpty(hint))
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine($"[dim]hint: {hint}[/]");
        }
    }
}
