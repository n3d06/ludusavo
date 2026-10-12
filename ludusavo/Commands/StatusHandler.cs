using Spectre.Console;
using ludusavo.Services;

namespace ludusavo.Commands;

public class StatusHandler
{
    private readonly IConfigService _configService;
    private readonly IGitHubService _gitHubService;

    public StatusHandler(IConfigService configService, IGitHubService gitHubService)
    {
        _configService = configService;
        _gitHubService = gitHubService;
    }

    public async Task ExecuteAsync()
    {
        AnsiConsole.WriteLine();
        var settings = _configService.Settings;

        var repoInfo = (!string.IsNullOrWhiteSpace(settings.GitHubOwner) && !string.IsNullOrWhiteSpace(settings.GitHubRepo))
            ? $"{settings.GitHubOwner}/{settings.GitHubRepo}"
            : "[red]Chưa cấu hình[/]";

        AnsiConsole.MarkupLine($"GitHub Repo:   {repoInfo}");
        AnsiConsole.MarkupLine($"GitHub Token:  {ConsoleHelper.MaskToken(settings.GitHubToken)}");
        AnsiConsole.MarkupLine($"Thư mục Cache: {_configService.CacheDir}");

        if (!_gitHubService.IsConfigured)
        {
            AnsiConsole.MarkupLine("[red]Chưa cấu hình đầy đủ thông tin GitHub (Owner, Repo, Token)![/]");
            AnsiConsole.MarkupLine("Gõ [cyan]sav config[/] để thiết lập thông tin kết nối.");
            return;
        }

        bool connected = false;
        string? message = null;

        await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .StartAsync("Đang kiểm tra kết nối tới GitHub API...", async ctx =>
            {
                var result = await _gitHubService.TestConnectionAsync();
                connected = result.success;
                message = result.message;
            });

        if (connected)
        {
            if (!string.IsNullOrEmpty(message))
            {
                AnsiConsole.MarkupLine(Markup.Escape(message));
            }

            try
            {
                var rate = await _gitHubService.GetRateLimitAsync();
                AnsiConsole.MarkupLine($"Giới hạn GitHub API còn lại: {rate.remaining}/{rate.limit}");
            }
            catch { }
        }
        else
        {
            AnsiConsole.MarkupLine($"[red]Lỗi kết nối GitHub:[/] {Markup.Escape(message ?? "Không xác định")}");
            AnsiConsole.MarkupLine("Vui lòng kiểm tra lại Token hoặc repository bằng lệnh [cyan]sav config[/].");
        }
    }
}

