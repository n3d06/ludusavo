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
            AnsiConsole.MarkupLine("[red]GitHub chưa được cấu hình! Vui lòng dùng lệnh [bold cyan]sav config[/] trước.[/]");
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
            AnsiConsole.MarkupLine("[yellow]Không tìm thấy game nào có save trên máy để đẩy lên GitHub.[/]");
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

            AnsiConsole.MarkupLine($"[red]Không tìm thấy game khớp với '{target}'.[/]");
            return;
        }

        // Hiển thị danh sách đánh số
        AnsiConsole.WriteLine();
        for (int i = 0; i < games.Count; i++)
        {
            var g = games[i];
            AnsiConsole.WriteLine($"[{i + 1}] {g.Name} ({ConsoleHelper.FormatSize(g.TotalSizeBytes)}, {g.FileCount} files)");
        }
        AnsiConsole.WriteLine("[A] Đẩy tất cả game lên GitHub");
        AnsiConsole.WriteLine("[0] Hủy bỏ\n");

        var choice = AnsiConsole.Prompt("Nhập số thứ tự game muốn push (hoặc 'A' / '0'):");

        choice = choice.Trim();

        if (choice == "0")
        {
            AnsiConsole.MarkupLine("Đã hủy thao tác.");
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
            AnsiConsole.MarkupLine("[red]Lựa chọn không hợp lệ.[/]");
        }
    }

    private async Task PushSingleAsync(DetectedGame game)
    {
        AnsiConsole.MarkupLine($"\nĐang sao lưu và đẩy game: [bold]{Markup.Escape(game.Name)}[/]");

        bool success = false;
        string? msg = null;

        await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .StartAsync("Đang nén file save và tải lên GitHub...", async ctx =>
            {
                var syncRes = await _syncService.SyncGamesBatchToCloudAsync(new List<DetectedGame> { game }, statusText =>
                {
                    ctx.Status(Markup.Escape(statusText));
                });
                success = syncRes.success;
                msg = syncRes.message;
            });

        if (success)
        {
            AnsiConsole.MarkupLine($"[green]Đã đẩy game '{Markup.Escape(game.Name)}' lên GitHub thành công (1 commit).[/]");
        }
        else
        {
            AnsiConsole.MarkupLine($"[red]Đẩy game thất bại:[/] {Markup.Escape(msg ?? "Lỗi không xác định")}");
        }
    }

    private async Task PushAllAsync(List<DetectedGame> games)
    {
        AnsiConsole.MarkupLine($"\nĐang sao lưu và đẩy tất cả [bold]{games.Count}[/] game lên GitHub...");

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
                    ctx.Status(Markup.Escape(statusText));
                });

                success = res.success;
                message = res.message;
                successCount = res.successCount;
                failCount = res.failCount;
            });

        if (success)
        {
            AnsiConsole.MarkupLine($"[green]Hoàn tất![/] Đã đẩy [green]{successCount}[/] game lên GitHub trong [bold]1 commit duy nhất[/].");
            if (failCount > 0)
            {
                AnsiConsole.MarkupLine($"[yellow]Có {failCount} game không thể nén do lỗi file.[/]");
            }
        }
        else
        {
            AnsiConsole.MarkupLine($"[red]Đẩy game thất bại:[/] {Markup.Escape(message ?? "Lỗi không xác định")}");
        }
    }
}

