using System.IO;
using System.Net.Http;

namespace ludusavo.Services;

public static class BannerCache
{
    private static readonly string CacheDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ludusavo", "banners");

    private static readonly HttpClient HttpClient = new() { Timeout = TimeSpan.FromSeconds(15) };
    private static readonly HashSet<int> DownloadingSteamIds = new();
    private static readonly object LockObj = new();

    static BannerCache()
    {
        try
        {
            Directory.CreateDirectory(CacheDir);
        }
        catch { }
    }

    public static string? GetBannerUrl(int? steamId, string? customBannerUrl)
    {
        if (!string.IsNullOrEmpty(customBannerUrl))
        {
            return customBannerUrl;
        }

        if (!steamId.HasValue)
        {
            return null;
        }

        var localFile = Path.Combine(CacheDir, $"{steamId.Value}.jpg");
        if (File.Exists(localFile))
        {
            return localFile;
        }

        var remoteUrl = $"https://cdn.cloudflare.steamstatic.com/steam/apps/{steamId.Value}/header.jpg";

        lock (LockObj)
        {
            if (DownloadingSteamIds.Add(steamId.Value))
            {
                _ = DownloadBannerAsync(steamId.Value, remoteUrl, localFile);
            }
        }

        return remoteUrl;
    }

    private static async Task DownloadBannerAsync(int steamId, string remoteUrl, string localPath)
    {
        try
        {
            var bytes = await HttpClient.GetByteArrayAsync(remoteUrl);
            if (bytes != null && bytes.Length > 1000)
            {
                await File.WriteAllBytesAsync(localPath, bytes);
            }
        }
        catch { }
        finally
        {
            lock (LockObj)
            {
                DownloadingSteamIds.Remove(steamId);
            }
        }
    }
}
