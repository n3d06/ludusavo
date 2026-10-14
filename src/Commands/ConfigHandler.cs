using ludusavo.Services;

namespace ludusavo.Commands;

public class ConfigHandler
{
    private readonly IConfigService _configService;
    private readonly IGoogleDriveService _driveService;

    public ConfigHandler(IConfigService configService, IGoogleDriveService driveService)
    {
        _configService = configService;
        _driveService = driveService;
    }

    public async Task ExecuteAsync(string[]? args = null)
    {
        var settings = _configService.Settings;

        // Handle quick CLI arguments: config id <val> or config secret <val>
        if (args != null && args.Length >= 2)
        {
            var key = args[0].ToLowerInvariant();
            var val = args[1].Trim();

            switch (key)
            {
                case "id":
                case "client_id":
                case "clientid":
                    settings.GoogleClientId = val;
                    AnsiConsole.WriteLine("Updated Google Client ID.");
                    break;
                case "secret":
                case "client_secret":
                case "clientsecret":
                    settings.GoogleClientSecret = val;
                    AnsiConsole.WriteLine("Updated Google Client Secret.");
                    break;
                default:
                    AnsiConsole.WriteLine($"Invalid config key: {key}. Supported keys: id, secret.");
                    return;
            }

            _configService.SaveSettings(settings);
            return;
        }

        // Interactive configuration mode
        AnsiConsole.WriteLine("Google Drive Configuration");
        AnsiConsole.WriteLine("Press Enter to keep current value.\n");

        if (DefaultConfig.HasEmbeddedCredentials && string.IsNullOrWhiteSpace(settings.GoogleClientId))
        {
            AnsiConsole.WriteLine("(*) Application includes default Google Client ID & Secret.");
            AnsiConsole.WriteLine("    You can run 'sav login' directly, or enter your custom developer credentials below.\n");
        }
        else if (!settings.HasCredentials)
        {
            AnsiConsole.WriteLine("--- Guide: Create Google OAuth Client ID & Secret (1-2 mins) ---");
            AnsiConsole.WriteLine("1. Visit: https://console.cloud.google.com/");
            AnsiConsole.WriteLine("2. Create a new Project -> Go to 'APIs & Services' -> Enable 'Google Drive API'");
            AnsiConsole.WriteLine("3. Go to 'OAuth consent screen' -> Choose 'External' -> Add your email to 'Test users'");
            AnsiConsole.WriteLine("4. Go to 'Credentials' -> 'Create Credentials' -> 'OAuth client ID' -> Choose 'Desktop app'");
            AnsiConsole.WriteLine("5. Copy Client ID and Client Secret and paste below.\n");
        }

        var curId = !string.IsNullOrWhiteSpace(settings.GoogleClientId) 
            ? settings.GoogleClientId 
            : (DefaultConfig.HasEmbeddedCredentials ? "[Default]" : string.Empty);
        var newId = AnsiConsole.Prompt("Google Client ID:", curId);

        var curSecret = !string.IsNullOrWhiteSpace(settings.GoogleClientSecret)
            ? settings.GoogleClientSecret
            : (DefaultConfig.HasEmbeddedCredentials ? "[Default]" : string.Empty);
        var newSecret = AnsiConsole.Prompt("Google Client Secret:", curSecret, secret: true);

        if (!string.IsNullOrWhiteSpace(newId) && newId != "[Default]")
            settings.GoogleClientId = newId.Trim();

        if (!string.IsNullOrWhiteSpace(newSecret) && newSecret != "[Default]")
            settings.GoogleClientSecret = newSecret.Trim();

        _configService.SaveSettings(settings);
        AnsiConsole.WriteLine("\nGoogle Drive configuration saved successfully!");

        if (settings.HasCredentials)
        {
            if (AnsiConsole.Confirm("Do you want to log in to Google Drive via browser now?", defaultVal: true))
            {
                await LoginAsync();
            }
        }
    }

    public async Task LoginAsync()
    {
        var settings = _configService.Settings;

        if (!settings.HasCredentials)
        {
            AnsiConsole.WriteLine("Missing Client ID or Client Secret. Please run 'sav config' first.");
            return;
        }

        bool success = false;
        string? message = null;

        await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .StartAsync("Preparing Google OAuth 2.0 authentication...", async ctx =>
            {
                var res = await _driveService.LoginAsync(status => ctx.Status(status));
                success = res.success;
                message = res.message;
            });

        if (success)
        {
            AnsiConsole.WriteLine($"\n{message ?? "Google Drive login successful!"}");
        }
        else
        {
            AnsiConsole.WriteLine($"\nLogin failed: {message ?? "Unknown error"}");
        }
    }

    public Task LogoutAsync()
    {
        var settings = _configService.Settings;

        if (string.IsNullOrWhiteSpace(settings.GoogleRefreshToken) && string.IsNullOrWhiteSpace(settings.GoogleAccessToken))
        {
            AnsiConsole.WriteLine("You are not logged in to any Google Drive account.");
            return Task.CompletedTask;
        }

        var prevEmail = !string.IsNullOrEmpty(settings.UserEmail) ? $" ({settings.UserEmail})" : "";

        settings.GoogleAccessToken = string.Empty;
        settings.GoogleRefreshToken = string.Empty;
        settings.GoogleTokenExpiry = null;
        settings.UserEmail = string.Empty;
        settings.GoogleRootFolderId = string.Empty;

        _configService.SaveSettings(settings);
        _driveService.InvalidateCatalogCache();

        AnsiConsole.WriteLine($"Logged out of Google Drive account{prevEmail}.");
        return Task.CompletedTask;
    }
}
