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
        AnsiConsole.MarkupLine("Rescanning all game saves on system...");

        await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .StartAsync("Syncing custom manifest from Google Drive (if present)...", async ctx =>
            {
                try
                {
                    await _syncService.DownloadCustomManifestAsync();
                }
                catch { }

                ctx.Status("Reloading manifest database from Ludusavi...");
                await _manifestService.LoadManifestAsync(forceDownload: true);
            });

        // Call LsHandler with forceRescan: true
        await _lsHandler.ExecuteAsync(forceRescan: true);
    }
}

