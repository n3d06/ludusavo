using Spectre.Console;
using ludusavo.Models;
using ludusavo.Services;

namespace ludusavo.Commands;

public class LsHandler
{
    private readonly IManifestService _manifestService;
    private readonly IScannerService _scannerService;

    public LsHandler(IManifestService manifestService, IScannerService scannerService)
    {
        _manifestService = manifestService;
        _scannerService = scannerService;
    }

    public async Task<List<DetectedGame>> ExecuteAsync(bool forceRescan = false)
    {
        List<DetectedGame> games = new();

        await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .StartAsync(forceRescan ? "Đang quét lại toàn bộ game trên máy..." : "Đang tìm kiếm save game trên máy...", async ctx =>
            {
                if (!_manifestService.IsLoaded)
                {
                    ctx.Status("Đang tải dữ liệu database game...");
                    await _manifestService.LoadManifestAsync();
                }

                ctx.Status("Đang quét các đường dẫn save game...");
                var allGames = _manifestService.GetAllGames();
                games = (await _scannerService.GetDetectedGamesAsync(allGames, forceRescan: forceRescan))
                    .OrderBy(g => g.Name, StringComparer.OrdinalIgnoreCase)
                    .ToList();
            });

        if (games.Count == 0)
        {
            AnsiConsole.MarkupLine("Không tìm thấy game nào có file save trên máy.");
            AnsiConsole.MarkupLine("Gõ [cyan]sav add[/] để thêm thủ công hoặc [cyan]sav scan[/] để quét lại.");
            return games;
        }

        AnsiConsole.WriteLine();
        int index = 1;
        foreach (var game in games)
        {
            var modified = ConsoleHelper.FormatDateTime(game.LastModified);
            var type = game.IsCustomGame
                ? "Custom"
                : (game.SteamId.HasValue && game.SteamId.Value > 0)
                    ? "Steam"
                    : "Official";

            var size = ConsoleHelper.FormatSize(game.TotalSizeBytes);
            var fileStr = game.FileCount == 1 ? "1 file" : $"{game.FileCount} files";

            AnsiConsole.WriteLine($"  [{index}] {game.Name} ({type}, {fileStr}, {size}, {modified})");
            index++;
        }
        return games;
    }
}

