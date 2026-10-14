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
        // Store all user data and settings in Documents/ludusavo
        var docsDir = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        RootDir = Path.Combine(docsDir, "ludusavo");

        DataDir = Path.Combine(RootDir, "data");
        CacheDir = Path.Combine(DataDir, "cache");
        ManifestCachePath = Path.Combine(CacheDir, "manifest_processed.json");

        // Ensure directories exist in Documents
        Directory.CreateDirectory(RootDir);
        Directory.CreateDirectory(DataDir);
        Directory.CreateDirectory(CacheDir);

        _settingsFilePath = Path.Combine(RootDir, "appsettings.json");

        // Migrate any existing configurations or cache from source/exe folder
        MigrateExistingData();

        LoadSettings();
    }

    private void MigrateExistingData()
    {
        try
        {
            var exeDir = AppDomain.CurrentDomain.BaseDirectory;
            string? oldRoot = null;
            var dir = new DirectoryInfo(exeDir);
            while (dir != null)
            {
                if (File.Exists(Path.Combine(dir.FullName, "ludusavo.sln")) ||
                    File.Exists(Path.Combine(dir.FullName, "ludusavo.slnx")))
                {
                    oldRoot = dir.FullName;
                    break;
                }
                dir = dir.Parent;
            }

            if (oldRoot == null || string.Equals(oldRoot, RootDir, StringComparison.OrdinalIgnoreCase))
                return;

            // Migrate appsettings.json if not present in Documents
            var oldSettings = Path.Combine(oldRoot, "appsettings.json");
            if (File.Exists(oldSettings) && !File.Exists(_settingsFilePath))
            {
                File.Copy(oldSettings, _settingsFilePath, true);
            }

            // Migrate .env if not present in Documents
            var oldEnv = Path.Combine(oldRoot, ".env");
            var newEnv = Path.Combine(RootDir, ".env");
            if (File.Exists(oldEnv) && !File.Exists(newEnv))
            {
                File.Copy(oldEnv, newEnv, true);
            }

            // Migrate cache files from old root
            var oldCache = Path.Combine(oldRoot, "data", "cache");
            if (Directory.Exists(oldCache))
            {
                // Copy manifest_processed.json if not yet in Documents
                var oldManifest = Path.Combine(oldCache, "manifest_processed.json");
                if (File.Exists(oldManifest) && !File.Exists(ManifestCachePath))
                {
                    File.Copy(oldManifest, ManifestCachePath, true);
                }

                // Copy custom_manifest.json
                var oldCustom = Path.Combine(oldCache, "custom_manifest.json");
                var newCustom = Path.Combine(CacheDir, "custom_manifest.json");
                if (File.Exists(oldCustom) && !File.Exists(newCustom))
                {
                    File.Copy(oldCustom, newCustom, true);
                }

                // Copy detected_games.json
                var oldDetected = Path.Combine(oldCache, "detected_games.json");
                var newDetected = Path.Combine(CacheDir, "detected_games.json");
                if (File.Exists(oldDetected) && !File.Exists(newDetected))
                {
                    File.Copy(oldDetected, newDetected, true);
                }

                // Copy game save directories (e.g., counter-strike-2, etc.)
                foreach (var subDir in Directory.GetDirectories(oldCache))
                {
                    var name = Path.GetFileName(subDir);
                    var targetSubDir = Path.Combine(CacheDir, name);
                    if (!Directory.Exists(targetSubDir))
                    {
                        CopyDirectory(subDir, targetSubDir);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ConfigService] Migration note: {ex.Message}");
        }
    }

    private static void CopyDirectory(string sourceDir, string targetDir)
    {
        Directory.CreateDirectory(targetDir);
        foreach (var file in Directory.GetFiles(sourceDir))
        {
            var dest = Path.Combine(targetDir, Path.GetFileName(file));
            File.Copy(file, dest, true);
        }
        foreach (var sub in Directory.GetDirectories(sourceDir))
        {
            var dest = Path.Combine(targetDir, Path.GetFileName(sub));
            CopyDirectory(sub, dest);
        }
    }

    private void LoadSettings()
    {
        _settings = new AppSettings();

        // 1. Try to load from appsettings.json in Documents
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

        // 2. Read fallback values from .env in Documents
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

            // Also keep .env in sync inside Documents/ludusavo
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
