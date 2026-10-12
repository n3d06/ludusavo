using ludusavo.Models;
using ludusavo.Services;

namespace ludusavo.Commands;

public class PushHandler
{
    private readonly IManifestService _manifestService;
    private readonly IScannerService _scannerService;
    private readonly IBackupService _backupService;
    private readonly ISyncService _syncService;
    private readonly IGitHubService _gitHubService;

    public PushHandler(
        IManifestService manifestService,
        IScannerService scannerService,
        IBackupService backupService,
        ISyncService syncService,
        IGitHubService gitHubService)
    {
        _manifestService = manifestService;
        _scannerService = scannerService;
        _backupService = backupService;
        _syncService = syncService;
        _gitHubService = gitHubService;
    }

    public async Task ExecuteAsync(string? target = null)
    {
        if (!_gitHubService.IsConfigured)
        {
            AnsiConsole.WriteLine("GitHub chưa được cấu hình! Vui lòng dùng lệnh 'sav config' trước.");
            return;
        }

        List<DetectedGame> games = new();

        await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .StartAsync("Đang chuẩn bị danh sách game trên máy...", async ctx =>
            {
                if (!_manifestService.IsLoaded)
                {
                    await _manifestService.LoadManifestAsync();
                }
                var all = _manifestService.GetAllGames();
                games = (await _scannerService.GetDetectedGamesAsync(all, forceRescan: false))
                    .OrderBy(g => g.Name, StringComparer.OrdinalIgnoreCase)
                    .ToList();
            });

        if (games.Count == 0)
        {
            AnsiConsole.WriteLine("Không tìm thấy game nào có save trên máy để đẩy lên GitHub.");
            return;
        }

        // Nếu có tham số target truyền từ CLI: ví dụ push 1 hoặc push all
        if (!string.IsNullOrWhiteSpace(target))
        {
            if (target.Equals("all", StringComparison.OrdinalIgnoreCase) || target.Equals("a", StringComparison.OrdinalIgnoreCase))
            {
                await PushAllAsync(games);
                return;
            }

            if (int.TryParse(target, out int num) && num >= 1 && num <= games.Count)
            {
                await PushSingleAsync(games[num - 1]);
                return;
            }

            var matched = games.FirstOrDefault(g => g.Name.Equals(target, StringComparison.OrdinalIgnoreCase) || g.Id.Equals(target, StringComparison.OrdinalIgnoreCase));
            if (matched != null)
            {
                await PushSingleAsync(matched);
                return;
            }

            AnsiConsole.WriteLine($"Không tìm thấy game khớp với '{target}'.");
            return;
        }

        int maxIndexWidth = $"[{games.Count}]".Length;
        int maxNameWidth = games.Max(g => g.Name.Length);
        int maxSizeWidth = games.Max(g => ConsoleHelper.FormatSize(g.TotalSizeBytes).Length);
        int maxFileWidth = games.Max(g => $"{g.FileCount} files".Length);

        for (int i = 0; i < games.Count; i++)
        {
            var g = games[i];
            var idxStr = $"[{i + 1}]".PadRight(maxIndexWidth);
            var nameStr = g.Name.PadRight(maxNameWidth);
            var sizeStr = ConsoleHelper.FormatSize(g.TotalSizeBytes).PadLeft(maxSizeWidth);
            var fileStr = (g.FileCount == 1 ? "1 file" : $"{g.FileCount} files").PadRight(maxFileWidth);

            AnsiConsole.WriteLine($"{idxStr}  {nameStr}  {sizeStr}  {fileStr}");
        }
        AnsiConsole.WriteLine($"{"[A]".PadRight(maxIndexWidth)}  Đẩy tất cả game lên GitHub");
        AnsiConsole.WriteLine($"{"[0]".PadRight(maxIndexWidth)}  Hủy bỏ\n");

        var choice = AnsiConsole.Prompt("Nhập số thứ tự game muốn push (hoặc 'A' / '0'):");

        choice = choice.Trim();

        if (choice == "0")
        {
            AnsiConsole.WriteLine("Đã hủy thao tác.");
            return;
        }

        if (choice.Equals("A", StringComparison.OrdinalIgnoreCase) || choice.Equals("ALL", StringComparison.OrdinalIgnoreCase))
        {
            await PushAllAsync(games);
            return;
        }

        if (int.TryParse(choice, out int selectedIdx) && selectedIdx >= 1 && selectedIdx <= games.Count)
        {
            await PushSingleAsync(games[selectedIdx - 1]);
        }
        else
        {
            AnsiConsole.WriteLine("Lựa chọn không hợp lệ.");
        }
    }

    private async Task PushSingleAsync(DetectedGame game)
    {
        AnsiConsole.WriteLine($"Bắt đầu sao lưu game: {game.Name}");

        bool success = false;
        string? msg = null;
        string? commitSha = null;

        await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .StartAsync($"Đang nén bản save: {game.Name}...", async ctx =>
            {
                var syncRes = await _syncService.SyncGamesBatchToCloudAsync(new List<DetectedGame> { game }, statusText =>
                {
                    ctx.Status(statusText);
                });
                success = syncRes.success;
                msg = syncRes.message;
                commitSha = syncRes.commitSha;
            });

        if (success)
        {
            var shortSha = !string.IsNullOrEmpty(commitSha) && commitSha.Length >= 7 
                ? commitSha.Substring(0, 7) 
                : commitSha;
            var shaText = !string.IsNullOrEmpty(shortSha) ? $" (Commit: {shortSha})" : "";
            AnsiConsole.WriteLine($"Đã đẩy game '{game.Name}' lên GitHub thành công.{shaText}");
        }
        else
        {
            AnsiConsole.WriteLine($"Đẩy game thất bại: {msg ?? "Lỗi không xác định"}");
        }
    }

    private async Task PushAllAsync(List<DetectedGame> games)
    {
        AnsiConsole.WriteLine($"Bắt đầu sao lưu tất cả {games.Count} game lên GitHub...");

        bool success = false;
        string? message = null;
        int successCount = 0;
        int failCount = 0;
        string? commitSha = null;

        await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .StartAsync("Đang chuẩn bị sao lưu các game...", async ctx =>
            {
                var res = await _syncService.SyncGamesBatchToCloudAsync(games, statusText =>
                {
                    ctx.Status(statusText);
                });

                success = res.success;
                message = res.message;
                successCount = res.successCount;
                failCount = res.failCount;
                commitSha = res.commitSha;
            });

        if (success)
        {
            var shortSha = !string.IsNullOrEmpty(commitSha) && commitSha.Length >= 7 
                ? commitSha.Substring(0, 7) 
                : commitSha;
            var shaText = !string.IsNullOrEmpty(shortSha) ? $" (Commit: {shortSha})" : "";
            AnsiConsole.WriteLine($"Hoàn tất! Đã đẩy {successCount} game lên GitHub thành công.{shaText}");
            if (failCount > 0)
            {
                AnsiConsole.WriteLine($"Có {failCount} game không thể nén do lỗi file.");
            }
        }
        else
        {
            AnsiConsole.WriteLine($"Đẩy game thất bại: {message ?? "Lỗi không xác định"}");
        }
    }
}

