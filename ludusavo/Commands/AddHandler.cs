using System.IO;
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
        AnsiConsole.WriteLine("Thêm game tùy chỉnh (Custom game)\n");

        string gameName;
        while (true)
        {
            gameName = AnsiConsole.Prompt("Nhập tên Game:");
            if (!string.IsNullOrWhiteSpace(gameName)) break;
            AnsiConsole.WriteLine("Tên game không được để trống!");
        }

        string folderPath = string.Empty;
        while (true)
        {
            folderPath = AnsiConsole.Prompt("Nhập đường dẫn thư mục Save game:");
            folderPath = folderPath.Trim('\"', '\'').Trim();

            if (string.IsNullOrWhiteSpace(folderPath))
            {
                AnsiConsole.WriteLine("Đường dẫn không được để trống!");
                continue;
            }

            if (!Directory.Exists(folderPath))
            {
                var confirm = AnsiConsole.Confirm($"Thư mục '{folderPath}' hiện chưa tồn tại trên máy. Bạn có chắc chắn muốn dùng đường dẫn này?", false);
                if (confirm) break;
            }
            else
            {
                break;
            }
        }

        var steamIdInput = AnsiConsole.Prompt("Nhập Steam App ID (tùy chọn, nhấn Enter nếu không có):");

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
            .StartAsync("Đang lưu game tùy chỉnh và đồng bộ lên Google Drive...", async ctx =>
            {
                await _manifestService.AddCustomGameAsync(newGame);
                try
                {
                    await _syncService.UploadCustomManifestAsync();
                }
                catch { }
            });

        AnsiConsole.WriteLine($"\nĐã thêm thành công game tùy chỉnh: {newGame.Name}");
        AnsiConsole.WriteLine($"ID: {newGame.Id}");
        AnsiConsole.WriteLine($"Đường dẫn portable: {portablePath}");
        AnsiConsole.WriteLine("Gõ 'sav push' để đẩy file save của game này lên Google Drive.");
    }
}
