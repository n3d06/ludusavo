using System.IO;
using System.Text.Json;
using ludusavo.Models;

namespace ludusavo.Services;

public class ConfigService : IConfigService
{
    private readonly string _settingsFilePath;
    private AppSettings _settings = new();

    public AppSettings Settings => _settings;
    public string RootDir { get; private set; }
    public string DataDir { get; private set; }
    public string CacheDir { get; private set; }
    public string ManifestCachePath { get; private set; }

    public ConfigService()
    {
        // Determine root directory (either repo/solution root or executable dir)
        var exeDir = AppDomain.CurrentDomain.BaseDirectory;
        string? candidate = exeDir;
        string? resolvedRoot = null;

        while (!string.IsNullOrEmpty(candidate))
        {
            if (File.Exists(Path.Combine(candidate, "ludusavo.sln")) ||
                File.Exists(Path.Combine(candidate, "ludusavo.slnx")) ||
                Directory.Exists(Path.Combine(candidate, "data", "cache")))
            {
                resolvedRoot = candidate;
                break;
            }
            var p = Directory.GetParent(candidate);
            candidate = p?.FullName;
        }

        if (resolvedRoot == null)
        {
            candidate = Directory.GetCurrentDirectory();
            while (!string.IsNullOrEmpty(candidate))
            {
                if (File.Exists(Path.Combine(candidate, "ludusavo.sln")) ||
                    File.Exists(Path.Combine(candidate, "ludusavo.slnx")) ||
                    Directory.Exists(Path.Combine(candidate, "data", "cache")))
                {
                    resolvedRoot = candidate;
                    break;
                }
                var p = Directory.GetParent(candidate);
                candidate = p?.FullName;
            }
        }

        RootDir = resolvedRoot ?? exeDir;

        // Store all generated user data in Documents/ludusavo for easy user access and management
        var docsDir = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        var userDocsDir = Path.Combine(docsDir, "ludusavo");
        Directory.CreateDirectory(userDocsDir);

        DataDir = Path.Combine(userDocsDir, "data");
        CacheDir = Path.Combine(DataDir, "cache");
        Directory.CreateDirectory(DataDir);
        Directory.CreateDirectory(CacheDir);

        _settingsFilePath = Path.Combine(userDocsDir, "appsettings.json");

        // Automatically migrate any existing files from app root to Documents/ludusavo
        MigrateExistingData();

        // Manifest cache path: prefer Documents, fallback to bundled app root
        var docManifest = Path.Combine(CacheDir, "manifest_processed.json");
        var bundledManifest = Path.Combine(RootDir, "data", "cache", "manifest_processed.json");
        ManifestCachePath = File.Exists(docManifest) ? docManifest : bundledManifest;

        LoadSettings();
    }

    private void MigrateExistingData()
    {
        try
        {
            // 1. Migrate appsettings.json
            var oldSettings = Path.Combine(RootDir, "appsettings.json");
            if (File.Exists(oldSettings) && !File.Exists(_settingsFilePath))
            {
                File.Copy(oldSettings, _settingsFilePath, overwrite: false);
            }

            // 2. Migrate existing data/cache (custom_manifest, detected_games, local save zip backups)
            var oldCacheDir = Path.Combine(RootDir, "data", "cache");
            if (Directory.Exists(oldCacheDir) && !string.Equals(oldCacheDir, CacheDir, StringComparison.OrdinalIgnoreCase))
            {
                foreach (var file in Directory.GetFiles(oldCacheDir))
                {
                    var dest = Path.Combine(CacheDir, Path.GetFileName(file));
                    if (!File.Exists(dest))
                    {
                        File.Copy(file, dest, overwrite: true);
                    }
                }

                foreach (var dir in Directory.GetDirectories(oldCacheDir))
                {
                    var dirName = Path.GetFileName(dir);
                    var destDir = Path.Combine(CacheDir, dirName);
                    if (!Directory.Exists(destDir))
                    {
                        Directory.CreateDirectory(destDir);
                        foreach (var f in Directory.GetFiles(dir))
                        {
                            File.Copy(f, Path.Combine(destDir, Path.GetFileName(f)), overwrite: true);
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ConfigService] Migration notice: {ex.Message}");
        }
    }

    private void LoadSettings()
    {
        _settings = new AppSettings();

        // 1. Try to load from appsettings.json if it exists
        if (File.Exists(_settingsFilePath))
        {
            try
            {
                var json = File.ReadAllText(_settingsFilePath);
                var loaded = JsonSerializer.Deserialize<AppSettings>(json);
                if (loaded != null)
                {
                    _settings = loaded;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ConfigService] Error reading settings: {ex.Message}");
            }
        }

        // 2. Read fallback values from .env if properties are empty
        var envPath = Path.Combine(RootDir, ".env");
        if (File.Exists(envPath))
        {
            try
            {
                var lines = File.ReadAllLines(envPath);
                foreach (var line in lines)
                {
                    var trimmed = line.Trim();
                    if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith('#')) continue;

                    var parts = trimmed.Split('=', 2);
                    if (parts.Length != 2) continue;

                    var key = parts[0].Trim();
                    var val = parts[1].Trim().Trim('"', '\'');

                    if (key == "GITHUB_TOKEN" && string.IsNullOrEmpty(_settings.GitHubToken))
                        _settings.GitHubToken = val;
                    else if (key == "GITHUB_OWNER" && string.IsNullOrEmpty(_settings.GitHubOwner))
                        _settings.GitHubOwner = val;
                    else if (key == "GITHUB_REPO" && string.IsNullOrEmpty(_settings.GitHubRepo))
                        _settings.GitHubRepo = val;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ConfigService] Error reading .env: {ex.Message}");
            }
        }
    }

    public void SaveSettings(AppSettings newSettings)
    {
        _settings = newSettings;
        try
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(_settings, options);
            File.WriteAllText(_settingsFilePath, json);

            // Also keep .env in sync for interoperability
            var envPath = Path.Combine(RootDir, ".env");
            var envContent = $"GITHUB_TOKEN={_settings.GitHubToken}\nGITHUB_OWNER={_settings.GitHubOwner}\nGITHUB_REPO={_settings.GitHubRepo}\n";
            File.WriteAllText(envPath, envContent);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ConfigService] Error saving settings: {ex.Message}");
        }
    }

    public void ReloadSettings()
    {
        LoadSettings();
    }
}
