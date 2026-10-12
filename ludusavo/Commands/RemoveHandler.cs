using Spectre.Console;
using ludusavo.Models;
using ludusavo.Services;

namespace ludusavo.Commands;

public class RemoveHandler
{
    private readonly IManifestService _manifestService;
    private readonly ISyncService _syncService;

    public RemoveHandler(IManifestService manifestService, ISyncService syncService)
    {
        _manifestService = manifestService;
        _syncService = syncService;
    }

    public async Task ExecuteAsync(string? target = null)
    {
        if (!_manifestService.IsLoaded)
        {
            await _manifestService.LoadManifestAsync();
        }

        var customGames = _manifestService.GetAllGames()
            .Where(g => g.Id.StartsWith("custom_", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (customGames.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]Hiện tại chưa có game tùy chỉnh (custom game) nào để xóa.[/]");
            return;
        }

        // Nếu có tham số dòng lệnh
        if (!string.IsNullOrWhiteSpace(target))
        {
            if (int.TryParse(target, out int num) && num >= 1 && num <= customGames.Count)
            {
                await RemoveGameAsync(customGames[num - 1]);
                return;
            }

            var matched = customGames.FirstOrDefault(g => g.Name.Equals(target, StringComparison.OrdinalIgnoreCase) || g.Id.Equals(target, StringComparison.OrdinalIgnoreCase));
            if (matched != null)
            {
                await RemoveGameAsync(matched);
                return;
            }

            AnsiConsole.MarkupLine($"[red]Không tìm thấy custom game nào khớp với '{target}'.[/]");
            return;
        }

        AnsiConsole.WriteLine();
        for (int i = 0; i < customGames.Count; i++)
        {
            var g = customGames[i];
            AnsiConsole.WriteLine($"  [{i + 1}] {g.Name} ({g.Id})");
        }
        AnsiConsole.WriteLine("  [0] Hủy bỏ\n");

        var choice = AnsiConsole.Prompt(
            new TextPrompt<string>("Nhập số thứ tự game muốn xóa (hoặc '0'):"));

        choice = choice.Trim();

        if (choice == "0")
        {
            AnsiConsole.MarkupLine("Đã hủy thao tác.");
            return;
        }

        if (int.TryParse(choice, out int idx) && idx >= 1 && idx <= customGames.Count)
        {
            await RemoveGameAsync(customGames[idx - 1]);
        }
        else
        {
            AnsiConsole.MarkupLine("[red]Lựa chọn không hợp lệ.[/]");
        }
    }

    private async Task RemoveGameAsync(GameEntry game)
    {
        var confirm = AnsiConsole.Confirm($"Bạn có chắc chắn muốn xóa game tùy chỉnh '[bold]{Markup.Escape(game.Name)}[/]' không? [dim](Lưu ý: File save trên ổ đĩa sẽ KHÔNG bị xóa)[/]", false);
        if (!confirm)
        {
            AnsiConsole.MarkupLine("[yellow]Đã hủy bỏ xóa.[/]");
            return;
        }

        await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .StartAsync($"Đang xóa {game.Name} và đồng bộ lên GitHub...", async ctx =>
            {
                await _manifestService.RemoveCustomGameAsync(game.Id);
                try
                {
                    await _syncService.UploadCustomManifestAsync();
                }
                catch { }
            });

        AnsiConsole.MarkupLine($"[green]Đã xóa thành công game tùy chỉnh '{Markup.Escape(game.Name)}'.[/]");
    }
}

