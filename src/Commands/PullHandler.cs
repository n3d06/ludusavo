using ludusavo.Services;

namespace ludusavo.Commands;

public class PullHandler
{
    private readonly IGoogleDriveService _driveService;
    private readonly IRestoreService _restoreService;
    private readonly IBackupService _backupService;

    public PullHandler(
        IGoogleDriveService driveService,
        IRestoreService restoreService,
        IBackupService backupService)
    {
        _driveService = driveService;
        _restoreService = restoreService;
        _backupService = backupService;
    }

    public async Task ExecuteAsync(params string[]? targets)
    {
        if (!_driveService.IsConfigured)
        {
            AnsiConsole.WriteLine("Google Drive chưa được cấu hình! Vui lòng dùng lệnh 'sav config' hoặc 'sav login' trước.");
            return;
        }

        List<RemoteGameMeta> remoteList = new();

        await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .StartAsync("Đang lấy danh sách các bản save từ Google Drive...", async ctx =>
            {
                var dict = await _driveService.GetAllRemoteMetasAsync(forceRefresh: true);
                remoteList = dict.Values.OrderBy(m => m.GameName, StringComparer.OrdinalIgnoreCase).ToList();
            });

        if (remoteList.Count == 0)
        {
            AnsiConsole.WriteLine("Không có bản save nào trên Google Drive để tải về.");
            return;
        }

        var targetInput = targets != null && targets.Length > 0 ? string.Join(" ", targets).Trim() : null;

        // Nếu có tham số target truyền từ CLI: ví dụ pull 1 3 5 hoặc pull all
        if (!string.IsNullOrWhiteSpace(targetInput))
        {
            if (targetInput.Equals("all", StringComparison.OrdinalIgnoreCase) || targetInput.Equals("a", StringComparison.OrdinalIgnoreCase))
            {
                await PullBatchAsync(remoteList);
                return;
            }

            if (ConsoleHelper.TryParseSelectionIndices(targetInput, remoteList.Count, out var indices))
            {
                var selected = indices.Select(i => remoteList[i - 1]).ToList();
                if (selected.Count == 1)
                {
                    await PullSingleAsync(selected[0]);
                }
                else
                {
                    await PullBatchAsync(selected);
                }
                return;
            }

            var matched = remoteList.FirstOrDefault(m => m.GameName.Equals(targetInput, StringComparison.OrdinalIgnoreCase) || m.GameId.Equals(targetInput, StringComparison.OrdinalIgnoreCase));
            if (matched != null)
            {
                await PullSingleAsync(matched);
                return;
            }

            AnsiConsole.WriteLine($"Không tìm thấy bản save khớp với '{targetInput}'.");
            return;
        }

        int maxIndexWidth = $"[{remoteList.Count}]".Length;
        int maxNameWidth = remoteList.Max(m => m.GameName.Length);
        int maxSizeWidth = remoteList.Max(m => (string.IsNullOrEmpty(m.FormattedSize) ? "-" : m.FormattedSize).Length);

        for (int i = 0; i < remoteList.Count; i++)
        {
            var m = remoteList[i];
            var idxStr = $"[{i + 1}]".PadRight(maxIndexWidth);
            var nameStr = m.GameName.PadRight(maxNameWidth);
            var sizeStr = (string.IsNullOrEmpty(m.FormattedSize) ? "-" : m.FormattedSize).PadLeft(maxSizeWidth);
            var dateStr = ConsoleHelper.FormatDateTime(m.Timestamp);

            AnsiConsole.WriteLine($"{idxStr}  {nameStr}  {sizeStr}  {dateStr}");
        }
        AnsiConsole.WriteLine($"{"[A]".PadRight(maxIndexWidth)}  Kéo và khôi phục tất cả game về máy");
        AnsiConsole.WriteLine($"{"[0]".PadRight(maxIndexWidth)}  Hủy bỏ\n");

        var choice = AnsiConsole.Prompt("Nhập số thứ tự game muốn pull (ví dụ: 1 3 5 hoặc 'A' / '0'):");

        choice = choice.Trim();

        if (choice == "0")
        {
            AnsiConsole.WriteLine("Đã hủy thao tác.");
            return;
        }

        if (choice.Equals("A", StringComparison.OrdinalIgnoreCase) || choice.Equals("ALL", StringComparison.OrdinalIgnoreCase))
        {
            await PullBatchAsync(remoteList);
            return;
        }

        if (ConsoleHelper.TryParseSelectionIndices(choice, remoteList.Count, out var selectedIndices))
        {
            var selected = selectedIndices.Select(i => remoteList[i - 1]).ToList();
            if (selected.Count == 1)
            {
                await PullSingleAsync(selected[0]);
            }
            else
            {
                await PullBatchAsync(selected);
            }
        }
        else
        {
            AnsiConsole.WriteLine("Lựa chọn không hợp lệ.");
        }
    }

    private async Task PullSingleAsync(RemoteGameMeta meta)
    {
        AnsiConsole.WriteLine($"Bắt đầu tải và khôi phục game: {meta.GameName}");

        bool success = false;
        string? error = null;
        int restoredCount = 0;

        await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .StartAsync("Đang tải bản save từ Google Drive...", async ctx =>
            {
                var cacheDir = _backupService.GetGameCacheDir(meta.GameId);
                var dl = await _driveService.DownloadGameSaveAsync(meta.GameId, cacheDir);
                if (!dl.success)
                {
                    error = dl.error ?? "Lỗi tải file từ Google Drive";
                    return;
                }

                ctx.Status("Đang giải nén và khôi phục vào thư mục save của máy...");
                var zipPath = Path.Combine(cacheDir, "latest.zip");
                var restoreRes = await _restoreService.RestoreGameAsync(meta.GameId, zipPath);
                success = restoreRes.Success;
                error = restoreRes.ErrorMessage;
                restoredCount = restoreRes.RestoredFilesCount;
            });

        if (success)
        {
            AnsiConsole.WriteLine($"Đã khôi phục thành công {restoredCount} file save cho '{meta.GameName}'.");
        }
        else
        {
            AnsiConsole.WriteLine($"Khôi phục thất bại: {error ?? "Lỗi không xác định"}");
        }
    }

    private async Task PullBatchAsync(List<RemoteGameMeta> list)
    {
        AnsiConsole.WriteLine($"Bắt đầu kéo {list.Count} game từ Google Drive về máy...");

        int successCount = 0;
        int failCount = 0;

        for (int i = 0; i < list.Count; i++)
        {
            var meta = list[i];
            AnsiConsole.Write($"[{i + 1}/{list.Count}] Đang xử lý {meta.GameName}... ");

            var cacheDir = _backupService.GetGameCacheDir(meta.GameId);
            var dl = await _driveService.DownloadGameSaveAsync(meta.GameId, cacheDir);
            if (!dl.success)
            {
                AnsiConsole.WriteLine($"Lỗi tải: {dl.error ?? ""}");
                failCount++;
                continue;
            }

            var zipPath = Path.Combine(cacheDir, "latest.zip");
            var restoreRes = await _restoreService.RestoreGameAsync(meta.GameId, zipPath);
            if (restoreRes.Success)
            {
                AnsiConsole.WriteLine($"Khôi phục {restoreRes.RestoredFilesCount} files");
                successCount++;
            }
            else
            {
                AnsiConsole.WriteLine($"Lỗi giải nén: {restoreRes.ErrorMessage ?? ""}");
                failCount++;
            }
        }

        AnsiConsole.WriteLine($"\nHoàn tất! Thành công: {successCount}, Thất bại: {failCount}.");
    }
}
