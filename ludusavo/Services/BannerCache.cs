using System.Collections.Concurrent;
using System.Net.Http;
using System.Text.Json;

namespace ludusavo.Services;

public static class BannerCache
{
    private static readonly HttpClient HttpClient = new() { Timeout = TimeSpan.FromSeconds(10) };
    private static readonly ConcurrentDictionary<int, string?> UrlCache = new();
    private static readonly HashSet<int> PendingSteamIds = new();
    private static readonly object LockObj = new();

    public static event Action<int, string?>? BannerResolved;

    public static string? GetBannerUrl(int? steamId, string? customBannerUrl)
    {
        if (!string.IsNullOrEmpty(customBannerUrl))
        {
            return customBannerUrl;
        }

        if (!steamId.HasValue || steamId.Value <= 0)
        {
            return null;
        }

        if (UrlCache.TryGetValue(steamId.Value, out var cachedUrl))
        {
            return cachedUrl;
        }

        lock (LockObj)
        {
            if (PendingSteamIds.Add(steamId.Value))
            {
                _ = FetchBannerFromStoreApiAsync(steamId.Value);
            }
        }

        return null;
    }

    public static async Task<string?> FetchBannerFromStoreApiAsync(int steamId)
    {
        try
        {
            var apiUrl = $"https://store.steampowered.com/api/appdetails?appids={steamId}";
            var jsonString = await HttpClient.GetStringAsync(apiUrl);
            using var doc = JsonDocument.Parse(jsonString);

            string? headerImage = null;
            if (doc.RootElement.TryGetProperty(steamId.ToString(), out var appObj) &&
                appObj.TryGetProperty("success", out var successProp) &&
                successProp.GetBoolean() &&
                appObj.TryGetProperty("data", out var dataObj) &&
                dataObj.TryGetProperty("header_image", out var headerImgProp))
            {
                headerImage = headerImgProp.GetString();
            }

            UrlCache[steamId] = headerImage;

            if (!string.IsNullOrEmpty(headerImage))
            {
                if (System.Windows.Application.Current?.Dispatcher is { } dispatcher)
                {
                    _ = dispatcher.InvokeAsync(() => BannerResolved?.Invoke(steamId, headerImage));
                }
                else
                {
                    BannerResolved?.Invoke(steamId, headerImage);
                }
            }

            return headerImage;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[BannerCache] Store API error for app {steamId}: {ex.Message}");
            UrlCache[steamId] = null;
            return null;
        }
        finally
        {
            lock (LockObj)
            {
                PendingSteamIds.Remove(steamId);
            }
        }
    }
}
