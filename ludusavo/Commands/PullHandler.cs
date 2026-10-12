using ludusavo.Services;

namespace ludusavo.Commands;

public class PullHandler
{
    private readonly IGitHubService _gitHubService;
    private readonly IRestoreService _restoreService;
    private readonly IBackupService _backupService;

    public PullHandler(
        IGitHubService gitHubService,
        IRestoreService restoreService,
        IBackupService backupService)
    {
        _gitHubService = gitHubService;
        _restoreService = restoreService;
        _backupService = backupService;
    }

    public async Task ExecuteAsync(string? target = null)
    {
        if (!_gitHubService.IsConfigured)
        {
            AnsiConsole.MarkupLine("[red]GitHub chưa được cấu hình! Vui lòng dùng lệnh [bold cyan]sav config[/] trước.[/]");
            return;
        }

        List<RemoteGameMeta> remoteList = new();

        await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .StartAsync("Đang lấy danh sách các bản save từ GitHub...", async ctx =>
            {
                var dict = await _gitHubService.GetAllRemoteMetasAsync(forceRefresh: true);
                remoteList = dict.Values.OrderBy(m => m.GameName).ToList();
            });

        if (remoteList.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]Không có bản save nào trên GitHub để tải về.[/]");
            return;
        }

        // Nếu có tham số target truyền từ CLI: ví dụ pull 1 hoặc pull all
        if (!string.IsNullOrWhiteSpace(target))
        {
            if (target.Equals("all", StringComparison.OrdinalIgnoreCase) || target.Equals("a", StringComparison.OrdinalIgnoreCase))
            {
                await PullAllAsync(remoteList);
                return;
            }

            if (int.TryParse(target, out int num) && num >= 1 && num <= remoteList.Count)
            {
                await PullSingleAsync(remoteList[num - 1]);
                return;
            }

            var matched = remoteList.FirstOrDefault(m => m.GameName.Equals(target, StringComparison.OrdinalIgnoreCase) || m.GameId.Equals(target, StringComparison.OrdinalIgnoreCase));
            if (matched != null)
            {
                await PullSingleAsync(matched);
                return;
            }

            AnsiConsole.MarkupLine($"[red]Không tìm thấy bản save trên cloud khớp với '{target}'.[/]");
            return;
        }

        AnsiConsole.WriteLine();
        for (int i = 0; i < remoteList.Count; i++)
        {
            var m = remoteList[i];
            var dateStr = ConsoleHelper.FormatDateTime(m.Timestamp);
            AnsiConsole.WriteLine($"  [{i + 1}] {m.GameName} ({m.FormattedSize}, {dateStr})");
        }
        AnsiConsole.WriteLine("  [A] Kéo và khôi phục tất cả game về máy");
        AnsiConsole.WriteLine("  [0] Hủy bỏ\n");

        var choice = AnsiConsole.Prompt("Nhập số thứ tự game muốn pull (hoặc 'A' / '0'):");

        choice = choice.Trim();

        if (choice == "0")
        {
            AnsiConsole.MarkupLine("Đã hủy thao tác.");
            return;
        }

        if (choice.Equals("A", StringComparison.OrdinalIgnoreCase) || choice.Equals("ALL", StringComparison.OrdinalIgnoreCase))
        {
            await PullAllAsync(remoteList);
            return;
        }

        if (int.TryParse(choice, out int selectedIdx) && selectedIdx >= 1 && selectedIdx <= remoteList.Count)
        {
            await PullSingleAsync(remoteList[selectedIdx - 1]);
        }
        else
        {
            AnsiConsole.MarkupLine("[red]Lựa chọn không hợp lệ.[/]");
        }
    }

    private async Task PullSingleAsync(RemoteGameMeta meta)
    {
        AnsiConsole.MarkupLine($"\nĐang tải và khôi phục game: [bold]{Markup.Escape(meta.GameName)}[/]");

        bool success = false;
        string? error = null;
        int restoredCount = 0;

        await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .StartAsync($"Đang tải bản save từ GitHub...", async ctx =>
            {
                var cacheDir = _backupService.GetGameCacheDir(meta.GameId);
                var dl = await _gitHubService.DownloadGameSaveAsync(meta.GameId, cacheDir);
                if (!dl.success)
                {
                    error = dl.error ?? "Lỗi tải file từ GitHub";
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
            AnsiConsole.MarkupLine($"[bold green]Đã khôi phục thành công {restoredCount} file save cho '{Markup.Escape(meta.GameName)}'![/]");
        }
        else
        {
            AnsiConsole.MarkupLine($"[bold red]Khôi phục thất bại:[/] {Markup.Escape(error ?? "Lỗi không xác định")}");
        }
    }

    private async Task PullAllAsync(List<RemoteGameMeta> list)
    {
        AnsiConsole.MarkupLine($"\n[bold yellow]Bắt đầu kéo tất cả {list.Count} game từ GitHub về máy...[/]");

        int successCount = 0;
        int failCount = 0;

        for (int i = 0; i < list.Count; i++)
        {
            var meta = list[i];
            AnsiConsole.Markup($"[[{i + 1}/{list.Count}]] Đang xử lý [bold]{Markup.Escape(meta.GameName)}[/]... ");

            var cacheDir = _backupService.GetGameCacheDir(meta.GameId);
            var dl = await _gitHubService.DownloadGameSaveAsync(meta.GameId, cacheDir);
            if (!dl.success)
            {
                AnsiConsole.MarkupLine($"[red]Lỗi tải: {Markup.Escape(dl.error ?? "")}[/]");
                failCount++;
                continue;
            }

            var zipPath = Path.Combine(cacheDir, "latest.zip");
            var restoreRes = await _restoreService.RestoreGameAsync(meta.GameId, zipPath);
            if (restoreRes.Success)
            {
                AnsiConsole.MarkupLine($"[green]Khôi phục {restoreRes.RestoredFilesCount} files[/]");
                successCount++;
            }
            else
            {
                AnsiConsole.MarkupLine($"[red]Lỗi giải nén: {Markup.Escape(restoreRes.ErrorMessage ?? "")}[/]");
                failCount++;
            }
        }

        AnsiConsole.MarkupLine($"\n[bold green]Hoàn tất![/] Thành công: [green]{successCount}[/], Thất bại: [red]{failCount}[/].");
    }
}

