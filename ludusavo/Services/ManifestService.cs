using System.IO;
using System.Net.Http;
using System.Text.Json;
using ludusavo.Models;

namespace ludusavo.Services;

public class ManifestService : IManifestService
{
    private static readonly HttpClient ManifestHttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(60),
        DefaultRequestHeaders = { { "User-Agent", "ludusavo-Desktop" } }
    };

    private readonly IConfigService _configService;
    private readonly Dictionary<string, GameEntry> _gamesById = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<int, GameEntry> _gamesBySteamId = new();
    private List<GameEntry> _gamesList = new();

    public bool IsLoaded { get; private set; }
    public int GameCount => _gamesList.Count;

    public ManifestService(IConfigService configService)
    {
        _configService = configService;
    }

    public async Task<bool> LoadManifestAsync()
    {
        var cachePath = _configService.ManifestCachePath;

        // 1. If not yet in Documents, check if present in dev mode local data/cache
        if (!File.Exists(cachePath))
        {
            var fallback = Path.Combine(_configService.RootDir, "data", "cache", "manifest_processed.json");
            if (File.Exists(fallback))
            {
                try
                {
                    var dir = Path.GetDirectoryName(cachePath);
                    if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                    File.Copy(fallback, cachePath, overwrite: true);
                }
                catch { }
            }
        }

        // 2. If still not present, automatically download from GitHub / CDN into Documents
        if (!File.Exists(cachePath))
        {
            var downloaded = await DownloadManifestAsync(cachePath);
            if (!downloaded)
            {
                Console.WriteLine($"[ManifestService] Manifest could not be downloaded to {cachePath}");
                return false;
            }
        }

        if (!File.Exists(cachePath))
        {
            Console.WriteLine($"[ManifestService] Manifest cache not found at {cachePath}");
            return false;
        }

        try
        {
            await using var stream = File.OpenRead(cachePath);
            var loaded = await JsonSerializer.DeserializeAsync<List<GameEntry>>(stream);

            if (loaded != null && loaded.Count > 0)
            {
                _gamesList = loaded;
                _gamesById.Clear();
                _gamesBySteamId.Clear();

                foreach (var g in _gamesList)
                {
                    _gamesById[g.Id] = g;
                    var sid = g.GetSteamAppId();
                    if (sid.HasValue)
                    {
                        _gamesBySteamId[sid.Value] = g;
                    }
                }

                IsLoaded = true;
                
                // Load custom manifest
                await LoadCustomManifestAsync();
                
                return true;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ManifestService] Error loading manifest: {ex.Message}");
        }

        return false;
    }

    public IReadOnlyList<GameEntry> GetAllGames() => _gamesList;

    public GameEntry? GetGameById(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        _gamesById.TryGetValue(id, out var game);
        return game;
    }

    public GameEntry? GetGameBySteamId(int steamId)
    {
        _gamesBySteamId.TryGetValue(steamId, out var game);
        return game;
    }

    public IEnumerable<GameEntry> SearchGames(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return _gamesList;

        var q = query.Trim();
        return _gamesList.Where(g =>
            g.Name.Contains(q, StringComparison.OrdinalIgnoreCase) ||
            g.Id.Contains(q, StringComparison.OrdinalIgnoreCase));
    }

    private async Task LoadCustomManifestAsync()
    {
        var customPath = Path.Combine(_configService.CacheDir, "custom_manifest.json");
        if (!File.Exists(customPath)) return;

        try
        {
            await using var stream = File.OpenRead(customPath);
            var loaded = await JsonSerializer.DeserializeAsync<List<GameEntry>>(stream);
            if (loaded != null)
            {
                foreach (var g in loaded)
                {
                    // Update or Add
                    var existing = _gamesList.FirstOrDefault(x => x.Id == g.Id);
                    if (existing != null)
                    {
                        _gamesList.Remove(existing);
                    }
                    _gamesList.Add(g);
                    
                    _gamesById[g.Id] = g;
                    var sid = g.GetSteamAppId();
                    if (sid.HasValue)
                    {
                        _gamesBySteamId[sid.Value] = g;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ManifestService] Error loading custom manifest: {ex.Message}");
        }
    }

    public async Task AddCustomGameAsync(GameEntry game)
    {
        var customPath = Path.Combine(_configService.CacheDir, "custom_manifest.json");
        List<GameEntry> customGames = new();

        if (File.Exists(customPath))
        {
            try
            {
                await using var stream = File.OpenRead(customPath);
                var loaded = await JsonSerializer.DeserializeAsync<List<GameEntry>>(stream);
                if (loaded != null)
                {
                    customGames = loaded;
                }
            }
            catch { }
        }

        var existingIdx = customGames.FindIndex(g => g.Id == game.Id);
        if (existingIdx >= 0)
        {
            customGames[existingIdx] = game;
        }
        else
        {
            customGames.Add(game);
        }

        // Save back
        var options = new JsonSerializerOptions { WriteIndented = true };
        var json = JsonSerializer.Serialize(customGames, options);
        await File.WriteAllTextAsync(customPath, json);

        // Update in-memory state
        var existingListIdx = _gamesList.FindIndex(g => g.Id == game.Id);
        if (existingListIdx >= 0)
        {
            _gamesList.RemoveAt(existingListIdx);
        }
        _gamesList.Add(game);
        _gamesById[game.Id] = game;
    }

    public async Task RemoveCustomGameAsync(string gameId)
    {
        var customPath = Path.Combine(_configService.CacheDir, "custom_manifest.json");
        List<GameEntry> customGames = new();

        if (File.Exists(customPath))
        {
            try
            {
                await using var stream = File.OpenRead(customPath);
                var loaded = await JsonSerializer.DeserializeAsync<List<GameEntry>>(stream);
                if (loaded != null)
                {
                    customGames = loaded;
                }
            }
            catch { }
        }

        var existingIdx = customGames.FindIndex(g => g.Id == gameId);
        if (existingIdx >= 0)
        {
            customGames.RemoveAt(existingIdx);
            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(customGames, options);
            await File.WriteAllTextAsync(customPath, json);
        }

        // Update in-memory state
        var existingListIdx = _gamesList.FindIndex(g => g.Id == gameId);
        if (existingListIdx >= 0)
        {
            _gamesList.RemoveAt(existingListIdx);
        }
        _gamesById.Remove(gameId);
    }

    private static async Task<bool> DownloadManifestAsync(string targetPath)
    {
        var urls = new[]
        {
            "https://raw.githubusercontent.com/3ky4r0/ludusavo/main/data/cache/manifest_processed.json",
            "https://cdn.jsdelivr.net/gh/3ky4r0/ludusavo@main/data/cache/manifest_processed.json"
        };

        var dir = Path.GetDirectoryName(targetPath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        var tempPath = targetPath + $".{Guid.NewGuid():N}.tmp";

        foreach (var url in urls)
        {
            try
            {
                using var response = await ManifestHttpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
                if (response.IsSuccessStatusCode)
                {
                    await using (var remoteStream = await response.Content.ReadAsStreamAsync())
                    await using (var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        await remoteStream.CopyToAsync(fileStream);
                    }

                    if (File.Exists(tempPath) && new FileInfo(tempPath).Length > 1000)
                    {
                        if (File.Exists(targetPath)) File.Delete(targetPath);
                        File.Move(tempPath, targetPath);
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ManifestService] Failed downloading manifest from {url}: {ex.Message}");
            }
            finally
            {
                if (File.Exists(tempPath))
                {
                    try { File.Delete(tempPath); } catch { }
                }
            }
        }

        return false;
    }
}
