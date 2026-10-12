using System.IO;
using Spectre.Console;
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
        AnsiConsole.MarkupLine("[bold]Thêm game tùy chỉnh (Custom game)[/]\n");

        var gameName = AnsiConsole.Prompt(
            new TextPrompt<string>("Nhập tên Game:")
                .Validate(name => !string.IsNullOrWhiteSpace(name)
                    ? ValidationResult.Success()
                    : ValidationResult.Error("[red]Tên game không được để trống![/]")));

        string folderPath = string.Empty;
        while (true)
        {
            folderPath = AnsiConsole.Prompt(
                new TextPrompt<string>("Nhập đường dẫn thư mục Save game:")
                    .Validate(path => !string.IsNullOrWhiteSpace(path)
                        ? ValidationResult.Success()
                        : ValidationResult.Error("[red]Đường dẫn không được để trống![/]")));

            folderPath = folderPath.Trim('\"', '\'').Trim();

            if (!Directory.Exists(folderPath))
            {
                var confirm = AnsiConsole.Confirm($"[yellow]Thư mục '{folderPath}' hiện chưa tồn tại trên máy. Bạn có chắc chắn muốn dùng đường dẫn này?[/]", false);
                if (confirm) break;
            }
            else
            {
                break;
            }
        }

        var steamIdInput = AnsiConsole.Prompt(
            new TextPrompt<string>("Nhập Steam App ID [dim](tùy chọn, nhấn Enter nếu không có)[/]:")
                .AllowEmpty());

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
            .Spinner(Spinner.Known.Dots)
            .StartAsync("Đang lưu game tùy chỉnh và đồng bộ lên GitHub...", async ctx =>
            {
                await _manifestService.AddCustomGameAsync(newGame);
                try
                {
                    await _syncService.UploadCustomManifestAsync();
                }
                catch { }
            });

        AnsiConsole.MarkupLine($"\n[green]Đã thêm thành công game tùy chỉnh:[/] {Markup.Escape(newGame.Name)}");
        AnsiConsole.MarkupLine($"ID: {newGame.Id}");
        AnsiConsole.MarkupLine($"Đường dẫn portable: {portablePath}");
        AnsiConsole.MarkupLine("Gõ [cyan]sav push[/] để đẩy file save của game này lên GitHub.");
    }
}

