using ludusavo.Models;
using ludusavo.Services;

namespace ludusavo.Commands;

public class PushHandler
{
    private readonly IManifestService _manifestService;
    private readonly IScannerService _scannerService;
    private readonly IBackupService _backupService;
    private readonly ISyncService _syncService;
    private readonly IGoogleDriveService _driveService;

    public PushHandler(
        IManifestService manifestService,
        IScannerService scannerService,
        IBackupService backupService,
        ISyncService syncService,
        IGoogleDriveService driveService)
    {
        _manifestService = manifestService;
        _scannerService = scannerService;
        _backupService = backupService;
        _syncService = syncService;
        _driveService = driveService;
    }

    public async Task ExecuteAsync(params string[]? targets)
    {
        if (!_driveService.IsConfigured)
        {
            AnsiConsole.WriteLine("Google Drive chưa được cấu hình! Vui lòng dùng lệnh 'sav config' hoặc 'sav login' trước.");
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
            AnsiConsole.WriteLine("Không tìm thấy game nào có save trên máy để đẩy lên Google Drive.");
            return;
        }

        var targetInput = targets != null && targets.Length > 0 ? string.Join(" ", targets).Trim() : null;

        // Nếu có tham số target truyền từ CLI: ví dụ push 1 3 5 hoặc push all
        if (!string.IsNullOrWhiteSpace(targetInput))
        {
            if (targetInput.Equals("all", StringComparison.OrdinalIgnoreCase) || targetInput.Equals("a", StringComparison.OrdinalIgnoreCase))
            {
                await PushBatchAsync(games);
                return;
            }

            if (ConsoleHelper.TryParseSelectionIndices(targetInput, games.Count, out var indices))
            {
                var selected = indices.Select(i => games[i - 1]).ToList();
                if (selected.Count == 1)
                {
                    await PushSingleAsync(selected[0]);
                }
                else
                {
                    await PushBatchAsync(selected);
                }
                return;
            }

            var matched = games.FirstOrDefault(g => g.Name.Equals(targetInput, StringComparison.OrdinalIgnoreCase) || g.Id.Equals(targetInput, StringComparison.OrdinalIgnoreCase));
            if (matched != null)
            {
                await PushSingleAsync(matched);
                return;
            }

            AnsiConsole.WriteLine($"Không tìm thấy game khớp với '{targetInput}'.");
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
        AnsiConsole.WriteLine($"{"[A]".PadRight(maxIndexWidth)}  Đẩy tất cả game lên Google Drive");
        AnsiConsole.WriteLine($"{"[0]".PadRight(maxIndexWidth)}  Hủy bỏ\n");

        var choice = AnsiConsole.Prompt("Nhập số thứ tự game muốn push (ví dụ: 1 3 5 hoặc 'A' / '0'):");

        choice = choice.Trim();

        if (choice == "0")
        {
            AnsiConsole.WriteLine("Đã hủy thao tác.");
            return;
        }

        if (choice.Equals("A", StringComparison.OrdinalIgnoreCase) || choice.Equals("ALL", StringComparison.OrdinalIgnoreCase))
        {
            await PushBatchAsync(games);
            return;
        }

        if (ConsoleHelper.TryParseSelectionIndices(choice, games.Count, out var selectedIndices))
        {
            var selected = selectedIndices.Select(i => games[i - 1]).ToList();
            if (selected.Count == 1)
            {
                await PushSingleAsync(selected[0]);
            }
            else
            {
                await PushBatchAsync(selected);
            }
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
            });

        if (success)
        {
            AnsiConsole.WriteLine($"Đã đẩy game '{game.Name}' lên Google Drive thành công.");
        }
        else
        {
            AnsiConsole.WriteLine($"Đẩy game thất bại: {msg ?? "Lỗi không xác định"}");
        }
    }

    private async Task PushBatchAsync(List<DetectedGame> games)
    {
        AnsiConsole.WriteLine($"Bắt đầu sao lưu {games.Count} game lên Google Drive...");

        bool success = false;
        string? message = null;
        int successCount = 0;
        int failCount = 0;

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
            });

        if (success)
        {
            AnsiConsole.WriteLine($"Hoàn tất! Đã đẩy {successCount} game lên Google Drive thành công.");
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
