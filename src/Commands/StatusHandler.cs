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

        var accountInfo = !string.IsNullOrWhiteSpace(settings.UserEmail)
            ? settings.UserEmail
            : "Not logged in";

        string idInfo;
        if (!string.IsNullOrWhiteSpace(settings.GoogleClientId))
        {
            idInfo = settings.GoogleClientId.Length > 12 ? settings.GoogleClientId.Substring(0, 8) + "..." : settings.GoogleClientId;
        }
        else if (DefaultConfig.HasEmbeddedCredentials)
        {
            idInfo = "Default (embedded)";
        }
        else
        {
            idInfo = "Not configured";
        }

        AnsiConsole.WriteLine($"Google Account:      {accountInfo}");
        AnsiConsole.WriteLine($"Google Client ID:    {idInfo}");
        AnsiConsole.WriteLine($"Authentication:      OAuth 2.0 (PKCE)");
        AnsiConsole.WriteLine($"Credential Storage:  Windows Credential Manager");
        AnsiConsole.WriteLine($"Cloud Storage:       Google Drive ('sav' folder)");
        AnsiConsole.WriteLine($"Config Directory:    {_configService.ConfigDir}");
        AnsiConsole.WriteLine($"Local Cache:         {_configService.CacheDir}");

        if (!_driveService.IsConfigured)
        {
            AnsiConsole.WriteLine("Google Drive is not configured or not logged in!");
            AnsiConsole.WriteLine("Run 'sav config' or 'sav login' to set up connection.");
            return;
        }

        bool connected = false;
        string? message = null;

        await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .StartAsync("Testing connection to Google Drive API...", async ctx =>
            {
                var result = await _driveService.TestConnectionAsync();
                connected = result.success;
                message = result.message;
            });

        if (connected)
        {
            if (!string.IsNullOrEmpty(message))
            {
                AnsiConsole.WriteLine(message);
            }
        }
        else
        {
            AnsiConsole.WriteLine($"Google Drive connection error: {message ?? "Unknown"}");
            AnsiConsole.WriteLine("Please try logging in again with 'sav login'.");
        }
    }
}
