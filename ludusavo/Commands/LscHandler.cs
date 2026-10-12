using Spectre.Console;
using ludusavo.Services;

namespace ludusavo.Commands;

public class LscHandler
{
    private readonly IGitHubService _gitHubService;

    public LscHandler(IGitHubService gitHubService)
    {
        _gitHubService = gitHubService;
    }

    public async Task<List<RemoteGameMeta>> ExecuteAsync()
    {
        if (!_gitHubService.IsConfigured)
        {
            AnsiConsole.MarkupLine("[red]GitHub chưa được cấu hình! Vui lòng dùng lệnh [bold cyan]sav config[/] trước.[/]");
            return new List<RemoteGameMeta>();
        }

        List<RemoteGameMeta> list = new();

        await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .StartAsync("Đang lấy danh sách bản save từ GitHub...", async ctx =>
            {
                var metasDict = await _gitHubService.GetAllRemoteMetasAsync(forceRefresh: true);
                list = metasDict.Values.OrderBy(m => m.GameName).ToList();
            });

        if (list.Count == 0)
        {
            AnsiConsole.MarkupLine("Chưa có bản save nào trên GitHub repository.");
            AnsiConsole.MarkupLine("Gõ [cyan]sav push[/] để đẩy bản save đầu tiên lên cloud.");
            return list;
        }

        AnsiConsole.WriteLine();
        int index = 1;
        foreach (var item in list)
        {
            var dateStr = ConsoleHelper.FormatDateTime(item.Timestamp);
            var hashShort = !string.IsNullOrEmpty(item.ArchiveHash) && item.ArchiveHash.Length >= 8
                ? item.ArchiveHash.Substring(0, 8)
                : "-";
            var size = string.IsNullOrEmpty(item.FormattedSize) ? "-" : item.FormattedSize;

            AnsiConsole.WriteLine($"  [{index}] {item.GameName} ({size}, {dateStr}, {hashShort})");
            index++;
        }
        return list;
    }
}

