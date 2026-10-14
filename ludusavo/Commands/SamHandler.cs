using ludusavo.Services;

namespace ludusavo.Commands;

public class SamHandler
{
    private readonly IManifestService _manifestService;
    private readonly IScannerService _scannerService;
    private readonly ISyncService _syncService;
    private readonly LsHandler _lsHandler;

    public SamHandler(
        IManifestService manifestService,
        IScannerService scannerService,
        ISyncService syncService,
        LsHandler lsHandler)
    {
        _manifestService = manifestService;
        _scannerService = scannerService;
        _syncService = syncService;
        _lsHandler = lsHandler;
    }

    public async Task ExecuteAsync()
    {
        AnsiConsole.MarkupLine("Đang quét lại toàn bộ save game trên hệ thống...");

        await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .StartAsync("Đang đồng bộ custom manifest từ Google Drive (nếu có)...", async ctx =>
            {
                try
                {
                    await _syncService.DownloadCustomManifestAsync();
                }
                catch { }

                ctx.Status("Đang tải lại database manifest từ Ludusavi...");
                await _manifestService.LoadManifestAsync(forceDownload: true);
            });

        // Gọi LsHandler với forceRescan: true
        await _lsHandler.ExecuteAsync(forceRescan: true);
    }
}

