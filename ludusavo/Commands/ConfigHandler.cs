using ludusavo.Services;

namespace ludusavo.Commands;

public class ConfigHandler
{
    private readonly IConfigService _configService;
    private readonly IGoogleDriveService _driveService;

    public ConfigHandler(IConfigService configService, IGoogleDriveService driveService)
    {
        _configService = configService;
        _driveService = driveService;
    }

    public async Task ExecuteAsync(string[]? args = null)
    {
        var settings = _configService.Settings;

        // Xử lý đối số nhanh từ CLI: config id <val> hoặc config secret <val>
        if (args != null && args.Length >= 2)
        {
            var key = args[0].ToLowerInvariant();
            var val = args[1].Trim();

            switch (key)
            {
                case "id":
                case "client_id":
                case "clientid":
                    settings.GoogleClientId = val;
                    AnsiConsole.WriteLine("Đã cập nhật Google Client ID.");
                    break;
                case "secret":
                case "client_secret":
                case "clientsecret":
                    settings.GoogleClientSecret = val;
                    AnsiConsole.WriteLine("Đã cập nhật Google Client Secret.");
                    break;
                default:
                    AnsiConsole.WriteLine($"Khóa cấu hình không hợp lệ: {key}. Hỗ trợ: id, secret.");
                    return;
            }

            _configService.SaveSettings(settings);
            return;
        }

        // Chế độ tương tác hỏi đáp (Interactive)
        AnsiConsole.WriteLine("Cấu hình Google Drive");
        AnsiConsole.WriteLine("Nhấn Enter nếu muốn giữ nguyên giá trị hiện tại.\n");

        if (DefaultConfig.HasEmbeddedCredentials && string.IsNullOrWhiteSpace(settings.GoogleClientId))
        {
            AnsiConsole.WriteLine("(*) Ứng dụng đã có sẵn Google Client ID & Secret từ bản phát hành.");
            AnsiConsole.WriteLine("    Bạn có thể dùng ngay lệnh 'sav login' để đăng nhập, hoặc nhập bên dưới nếu muốn dùng tài khoản nhà phát triển riêng.\n");
        }
        else if (!settings.HasCredentials)
        {
            AnsiConsole.WriteLine("--- Hướng dẫn tạo Google OAuth Client ID & Secret (1-2 phút) ---");
            AnsiConsole.WriteLine("1. Truy cập: https://console.cloud.google.com/");
            AnsiConsole.WriteLine("2. Tạo Project mới -> Vào 'APIs & Services' -> Bật 'Google Drive API'");
            AnsiConsole.WriteLine("3. Vào 'OAuth consent screen' -> Chọn 'External' -> Thêm email của bạn vào 'Test users'");
            AnsiConsole.WriteLine("4. Vào 'Credentials' -> 'Create Credentials' -> 'OAuth client ID' -> Chọn 'Desktop app'");
            AnsiConsole.WriteLine("5. Copy Client ID và Client Secret dán vào bên dưới.\n");
        }

        var curId = !string.IsNullOrWhiteSpace(settings.GoogleClientId) 
            ? settings.GoogleClientId 
            : (DefaultConfig.HasEmbeddedCredentials ? "[Mặc định]" : string.Empty);
        var newId = AnsiConsole.Prompt("Google Client ID:", curId);

        var curSecret = !string.IsNullOrWhiteSpace(settings.GoogleClientSecret)
            ? settings.GoogleClientSecret
            : (DefaultConfig.HasEmbeddedCredentials ? "[Mặc định]" : string.Empty);
        var newSecret = AnsiConsole.Prompt("Google Client Secret:", curSecret, secret: true);

        if (!string.IsNullOrWhiteSpace(newId) && newId != "[Mặc định]")
            settings.GoogleClientId = newId.Trim();

        if (!string.IsNullOrWhiteSpace(newSecret) && newSecret != "[Mặc định]")
            settings.GoogleClientSecret = newSecret.Trim();

        _configService.SaveSettings(settings);
        AnsiConsole.WriteLine("\nĐã lưu cấu hình Google Drive thành công!");

        if (settings.HasCredentials)
        {
            if (AnsiConsole.Confirm("Bạn có muốn đăng nhập Google Drive qua trình duyệt ngay bây giờ không?", defaultVal: true))
            {
                await LoginAsync();
            }
        }
    }

    public async Task LoginAsync()
    {
        var settings = _configService.Settings;

        if (!settings.HasCredentials)
        {
            AnsiConsole.WriteLine("Chưa có Client ID hoặc Client Secret. Vui lòng chạy lệnh 'sav config' để nhập thông tin.");
            return;
        }

        bool success = false;
        string? message = null;

        await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .StartAsync("Đang chuẩn bị xác thực Google OAuth 2.0...", async ctx =>
            {
                var res = await _driveService.LoginAsync(status => ctx.Status(status));
                success = res.success;
                message = res.message;
            });

        if (success)
        {
            AnsiConsole.WriteLine($"\n{message ?? "Đăng nhập Google Drive thành công!"}");
        }
        else
        {
            AnsiConsole.WriteLine($"\nĐăng nhập thất bại: {message ?? "Lỗi không xác định"}");
        }
    }

    public Task LogoutAsync()
    {
        var settings = _configService.Settings;

        if (string.IsNullOrWhiteSpace(settings.GoogleRefreshToken) && string.IsNullOrWhiteSpace(settings.GoogleAccessToken))
        {
            AnsiConsole.WriteLine("Bạn chưa đăng nhập tài khoản Google Drive nào.");
            return Task.CompletedTask;
        }

        var prevEmail = !string.IsNullOrEmpty(settings.UserEmail) ? $" ({settings.UserEmail})" : "";

        settings.GoogleAccessToken = string.Empty;
        settings.GoogleRefreshToken = string.Empty;
        settings.GoogleTokenExpiry = null;
        settings.UserEmail = string.Empty;
        settings.GoogleRootFolderId = string.Empty;

        _configService.SaveSettings(settings);
        _driveService.InvalidateCatalogCache();

        AnsiConsole.WriteLine($"Đã đăng xuất khỏi tài khoản Google Drive{prevEmail}.");
        return Task.CompletedTask;
    }
}
