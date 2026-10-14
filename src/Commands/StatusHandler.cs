using ludusavo.Services;

namespace ludusavo.Commands;

public class StatusHandler
{
    private readonly IConfigService _configService;
    private readonly IGoogleDriveService _driveService;

    public StatusHandler(IConfigService configService, IGoogleDriveService driveService)
    {
        _configService = configService;
        _driveService = driveService;
    }

    public async Task ExecuteAsync()
    {
        var settings = _configService.Settings;

        var accountInfo = !string.IsNullOrWhiteSpace(settings.UserEmail)
            ? settings.UserEmail
            : "Chưa đăng nhập";

        string idInfo;
        if (!string.IsNullOrWhiteSpace(settings.GoogleClientId))
        {
            idInfo = settings.GoogleClientId.Length > 12 ? settings.GoogleClientId.Substring(0, 8) + "..." : settings.GoogleClientId;
        }
        else if (DefaultConfig.HasEmbeddedCredentials)
        {
            idInfo = "Mặc định (từ bản build)";
        }
        else
        {
            idInfo = "Chưa cấu hình";
        }

        AnsiConsole.WriteLine($"Tài khoản Google: {accountInfo}");
        AnsiConsole.WriteLine($"Google Client ID: {idInfo}");
        AnsiConsole.WriteLine($"Thư mục Cloud:    Google Drive (thư mục 'sav')");
        AnsiConsole.WriteLine($"Thư mục Cache:    {_configService.CacheDir}");

        if (!_driveService.IsConfigured)
        {
            AnsiConsole.WriteLine("Chưa cấu hình hoặc chưa đăng nhập Google Drive!");
            AnsiConsole.WriteLine("Gõ 'sav config' hoặc 'sav login' để thiết lập kết nối.");
            return;
        }

        bool connected = false;
        string? message = null;

        await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .StartAsync("Đang kiểm tra kết nối tới Google Drive API...", async ctx =>
            {
                var result = await _driveService.TestConnectionAsync();
                connected = result.success;
                message = result.message;
            });

        if (connected)
        {
            if (!string.IsNullOrEmpty(message))
            {
                AnsiConsole.WriteLine(message);
            }
        }
        else
        {
            AnsiConsole.WriteLine($"Lỗi kết nối Google Drive: {message ?? "Không xác định"}");
            AnsiConsole.WriteLine("Vui lòng thử đăng nhập lại bằng lệnh 'sav login'.");
        }
    }
}
