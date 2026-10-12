using ludusavo.Services;

namespace ludusavo.Commands;

public class ConfigHandler
{
    private readonly IConfigService _configService;
    private readonly IGitHubService _gitHubService;

    public ConfigHandler(IConfigService configService, IGitHubService gitHubService)
    {
        _configService = configService;
        _gitHubService = gitHubService;
    }

    public async Task ExecuteAsync(string[]? args = null)
    {
        var settings = _configService.Settings;

        // Xử lý nếu có đối số nhanh: config token <val> hoặc config owner <val> hoặc config repo <val>
        if (args != null && args.Length >= 2)
        {
            var key = args[0].ToLowerInvariant();
            var val = args[1].Trim();

            switch (key)
            {
                case "token":
                    settings.GitHubToken = val;
                    AnsiConsole.MarkupLine("[green]Đã cập nhật GitHub Token.[/]");
                    break;
                case "owner":
                    settings.GitHubOwner = val;
                    AnsiConsole.MarkupLine($"[green]Đã cập nhật GitHub Owner:[/] {val}");
                    break;
                case "repo":
                    settings.GitHubRepo = val;
                    AnsiConsole.MarkupLine($"[green]Đã cập nhật GitHub Repo:[/] {val}");
                    break;
                default:
                    AnsiConsole.MarkupLine($"[red]Khóa cấu hình không hợp lệ:[/] {key}. Hỗ trợ: token, owner, repo.");
                    return;
            }

            _configService.SaveSettings(settings);
            await TestConnectionNoticeAsync();
            return;
        }

        // Chế độ tương tác hỏi đáp (Interactive)
        AnsiConsole.MarkupLine("[cyan]Cấu hình GitHub Synchronization[/]");
        AnsiConsole.MarkupLine("[dim]Nhấn Enter nếu muốn giữ nguyên giá trị hiện tại.[/]\n");

        var currentOwner = settings.GitHubOwner ?? string.Empty;
        var newOwner = AnsiConsole.Prompt("GitHub Owner (Username hoặc Org):", currentOwner);

        var currentRepo = settings.GitHubRepo ?? string.Empty;
        var newRepo = AnsiConsole.Prompt("GitHub Repository (ví dụ: game-saves):", currentRepo);

        var currentTokenMask = string.IsNullOrWhiteSpace(settings.GitHubToken) ? "(chưa có)" : ConsoleHelper.MaskToken(settings.GitHubToken);
        var newTokenInput = AnsiConsole.Prompt($"GitHub Personal Access Token [hiện tại: {currentTokenMask}]:", secret: true);

        if (!string.IsNullOrWhiteSpace(newOwner))
            settings.GitHubOwner = newOwner.Trim();

        if (!string.IsNullOrWhiteSpace(newRepo))
            settings.GitHubRepo = newRepo.Trim();

        if (!string.IsNullOrWhiteSpace(newTokenInput))
            settings.GitHubToken = newTokenInput.Trim();

        _configService.SaveSettings(settings);
        AnsiConsole.MarkupLine("\n[green]Đã lưu cấu hình thành công![/]");

        await TestConnectionNoticeAsync();
    }

    private async Task TestConnectionNoticeAsync()
    {
        if (_gitHubService.IsConfigured)
        {
            await AnsiConsole.Status()
                .StartAsync("Đang kiểm tra kết nối với thông tin mới...", async ctx =>
                {
                    var res = await _gitHubService.TestConnectionAsync();
                    if (res.success)
                    {
                        AnsiConsole.MarkupLine($"\n[green]{res.message}[/]");
                    }
                    else
                    {
                        AnsiConsole.MarkupLine($"\n[red]{res.message}[/]");
                    }
                });
        }
    }
}
