using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading;

namespace ludusavo.Services;

public static class BannerCache
{
    private static readonly string CacheDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "ludusavo", "banners");

    private static readonly HttpClient HttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(15),
        DefaultRequestHeaders = { { "User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36" } }
    };

    private static readonly SemaphoreSlim ApiThrottler = new(3, 3);
    private static readonly HashSet<int> DownloadingSteamIds = new();
    private static readonly object LockObj = new();

    public static event Action<int>? BannerUpdated;

    static BannerCache()
    {
        try
        {
            Directory.CreateDirectory(CacheDir);

            // Migrate any old banners from %LOCALAPPDATA%\ludusavo\banners
            var oldCacheDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ludusavo", "banners");
            if (Directory.Exists(oldCacheDir))
            {
                foreach (var file in Directory.GetFiles(oldCacheDir))
                {
                    var dest = Path.Combine(CacheDir, Path.GetFileName(file));
                    if (!File.Exists(dest))
                    {
                        File.Copy(file, dest, overwrite: true);
                    }
                }
            }
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

        var notFoundMarker = Path.Combine(CacheDir, $"{steamId.Value}.nomedia");
        if (File.Exists(notFoundMarker))
        {
            return null;
        }

        lock (LockObj)
        {
            if (DownloadingSteamIds.Add(steamId.Value))
            {
                _ = DownloadBannerViaStoreApiAsync(steamId.Value, localFile, notFoundMarker);
            }
        }

        return null;
    }

    private static async Task DownloadBannerViaStoreApiAsync(int steamId, string localPath, string notFoundMarker)
    {
        await ApiThrottler.WaitAsync();
        try
        {
            // Call Steam Store API directly to fetch the authentic header image URL
            var apiUrl = $"https://store.steampowered.com/api/appdetails?appids={steamId}&cc=us&l=en";
            using var apiResp = await HttpClient.GetAsync(apiUrl);
            if (apiResp.IsSuccessStatusCode)
            {
                var jsonStr = await apiResp.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(jsonStr);
                if (doc.RootElement.TryGetProperty(steamId.ToString(), out var appObj) &&
                    appObj.TryGetProperty("success", out var successElem) &&
                    successElem.GetBoolean() &&
                    appObj.TryGetProperty("data", out var dataObj) &&
                    dataObj.TryGetProperty("header_image", out var headerElem))
                {
                    var headerUrl = headerElem.GetString();
                    if (!string.IsNullOrEmpty(headerUrl))
                    {
                        var bytes = await HttpClient.GetByteArrayAsync(headerUrl);
                        if (bytes != null && bytes.Length > 1000)
                        {
                            await File.WriteAllBytesAsync(localPath, bytes);
                            BannerUpdated?.Invoke(steamId);
                            return;
                        }
                    }
                }
            }

            // If the game doesn't have a store page or header (e.g. Spacewar), mark .nomedia
            try
            {
                await File.WriteAllTextAsync(notFoundMarker, "no_banner");
            }
            catch { }
        }
        catch
        {
            // Transient network failure - will retry on next app launch
        }
        finally
        {
            lock (LockObj)
            {
                DownloadingSteamIds.Remove(steamId);
            }
            ApiThrottler.Release();
        }
    }
}
