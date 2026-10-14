using System.IO;
using ludusavo.Models;
using ludusavo.Services;

namespace ludusavo.Commands;

public class AddHandler
{
    private readonly IManifestService _manifestService;
    private readonly IScannerService _scannerService;
    private readonly ISyncService _syncService;

    public AddHandler(
        IManifestService manifestService,
        IScannerService scannerService,
        ISyncService syncService)
    {
        _manifestService = manifestService;
        _scannerService = scannerService;
        _syncService = syncService;
    }

    public async Task ExecuteAsync()
    {
        AnsiConsole.WriteLine("Add Custom Game\n");

        string gameName;
        while (true)
        {
            gameName = AnsiConsole.Prompt("Enter game name:");
            if (!string.IsNullOrWhiteSpace(gameName)) break;
            AnsiConsole.WriteLine("Game name cannot be empty!");
        }

        string folderPath = string.Empty;
        while (true)
        {
            folderPath = AnsiConsole.Prompt("Enter save directory path:");
            folderPath = folderPath.Trim('\"', '\'').Trim();

            if (string.IsNullOrWhiteSpace(folderPath))
            {
                AnsiConsole.WriteLine("Path cannot be empty!");
                continue;
            }

            if (!Directory.Exists(folderPath))
            {
                var confirm = AnsiConsole.Confirm($"Directory '{folderPath}' does not exist on this machine. Are you sure you want to use this path?", false);
                if (confirm) break;
            }
            else
            {
                break;
            }
        }

        var steamIdInput = AnsiConsole.Prompt("Enter Steam App ID (optional, press Enter to skip):");

        int? steamId = null;
        if (int.TryParse(steamIdInput, out int sId) && sId > 0)
        {
            steamId = sId;
        }

        var portablePath = _scannerService.ToPlaceholderPath(folderPath);

        var newGame = new GameEntry
        {
            Id = "custom_" + Guid.NewGuid().ToString("N").Substring(0, 8),
            Name = gameName.Trim(),
            SteamId = steamId,
            SavePaths = new List<SavePathEntry>
            {
                new SavePathEntry { Path = portablePath }
            }
        };

        await AnsiConsole.Status()
            .StartAsync("Saving custom game and syncing to Google Drive...", async ctx =>
            {
                await _manifestService.AddCustomGameAsync(newGame);
                try
                {
                    await _syncService.UploadCustomManifestAsync();
                }
                catch { }
            });

        AnsiConsole.WriteLine($"\nSuccessfully added custom game: {newGame.Name}");
        AnsiConsole.WriteLine($"ID: {newGame.Id}");
        AnsiConsole.WriteLine($"Portable path: {portablePath}");
        AnsiConsole.WriteLine("Run 'sav push' to back up this game save to Google Drive.");
    }
}
