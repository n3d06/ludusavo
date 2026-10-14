using System.IO;
using System.Text.Json;
using ludusavo.Models;

namespace ludusavo.Services;

public class ConfigService : IConfigService
{
    private readonly ICredentialManager _credentialManager;
    private readonly string _settingsFilePath;
    private AppSettings _settings = new();

    public AppSettings Settings => _settings;
    public string RootDir { get; private set; }
    public string ConfigDir { get; private set; }
    public string DataDir { get; private set; }
    public string CacheDir { get; private set; }
    public string ManifestCachePath { get; private set; }
    public string SettingsFilePath => _settingsFilePath;

    public ConfigService(ICredentialManager credentialManager)
    {
        _credentialManager = credentialManager;

        // Determine root directory (either repo/solution root or executable dir)
        var exeDir = AppDomain.CurrentDomain.BaseDirectory;
        string? candidate = exeDir;
        string? resolvedRoot = null;

        while (!string.IsNullOrEmpty(candidate))
        {
            if (File.Exists(Path.Combine(candidate, "sav.sln")) ||
                File.Exists(Path.Combine(candidate, "ludusavo.sln")) ||
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
                if (File.Exists(Path.Combine(candidate, "sav.sln")) ||
                    File.Exists(Path.Combine(candidate, "ludusavo.sln")) ||
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

        // Store configuration in %APPDATA%\sav (standard secure user app data location)
        var appDataDir = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        ConfigDir = Path.Combine(appDataDir, "sav");
        Directory.CreateDirectory(ConfigDir);

        DataDir = Path.Combine(ConfigDir, "data");
        CacheDir = Path.Combine(DataDir, "cache");
        Directory.CreateDirectory(DataDir);
        Directory.CreateDirectory(CacheDir);

        _settingsFilePath = Path.Combine(ConfigDir, "appsettings.json");

        // Automatically migrate any existing files from Documents/sav or RootDir to %APPDATA%\sav
        MigrateExistingData();

        ManifestCachePath = Path.Combine(CacheDir, "manifest_processed.json");

        LoadSettings();
    }

    private void MigrateExistingData()
    {
        try
        {
            // 1. Migrate from old Documents/sav folder if present
            var docsDir = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            var oldDocsDir = Path.Combine(docsDir, "sav");
            if (Directory.Exists(oldDocsDir))
            {
                var oldDocSettings = Path.Combine(oldDocsDir, "appsettings.json");
                if (File.Exists(oldDocSettings) && !File.Exists(_settingsFilePath))
                {
                    File.Copy(oldDocSettings, _settingsFilePath, overwrite: false);
                }

                var oldDocData = Path.Combine(oldDocsDir, "data");
                if (Directory.Exists(oldDocData))
                {
                    CopyDirectoryRecursively(oldDocData, DataDir);
                }
            }

            // 2. Migrate appsettings.json from RootDir if still needed
            var oldRootSettings = Path.Combine(RootDir, "appsettings.json");
            if (File.Exists(oldRootSettings) && !File.Exists(_settingsFilePath))
            {
                File.Copy(oldRootSettings, _settingsFilePath, overwrite: false);
            }

            // 3. Migrate existing data/cache from RootDir
            var oldCacheDir = Path.Combine(RootDir, "data", "cache");
            if (Directory.Exists(oldCacheDir) && !string.Equals(oldCacheDir, CacheDir, StringComparison.OrdinalIgnoreCase))
            {
                CopyDirectoryRecursively(oldCacheDir, CacheDir);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ConfigService] Migration notice: {ex.Message}");
        }
    }

    private static void CopyDirectoryRecursively(string sourceDir, string targetDir)
    {
        Directory.CreateDirectory(targetDir);
        foreach (var file in Directory.GetFiles(sourceDir))
        {
            var dest = Path.Combine(targetDir, Path.GetFileName(file));
            if (!File.Exists(dest))
            {
                File.Copy(file, dest, overwrite: true);
            }
        }

        foreach (var dir in Directory.GetDirectories(sourceDir))
        {
            var destSub = Path.Combine(targetDir, Path.GetFileName(dir));
            CopyDirectoryRecursively(dir, destSub);
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

                // Check for legacy plaintext tokens in appsettings.json to auto-migrate them
                using var doc = JsonDocument.Parse(json);
                string? legacyRefresh = null, legacyAccess = null, legacySecret = null;
                DateTime? legacyExpiry = null;

                if (doc.RootElement.TryGetProperty("googleRefreshToken", out var r) && r.ValueKind == JsonValueKind.String)
                    legacyRefresh = r.GetString();
                if (doc.RootElement.TryGetProperty("googleAccessToken", out var a) && a.ValueKind == JsonValueKind.String)
                    legacyAccess = a.GetString();
                if (doc.RootElement.TryGetProperty("googleClientSecret", out var s) && s.ValueKind == JsonValueKind.String)
                    legacySecret = s.GetString();
                if (doc.RootElement.TryGetProperty("googleTokenExpiry", out var e) && e.TryGetDateTime(out var exp))
                    legacyExpiry = exp;

                if (!string.IsNullOrEmpty(legacyRefresh) || !string.IsNullOrEmpty(legacyAccess) || !string.IsNullOrEmpty(legacySecret))
                {
                    _credentialManager.SaveGoogleCredentials(legacyRefresh, legacyAccess, legacyExpiry, legacySecret, _settings.UserEmail);
                    // Resave to clean up plaintext credentials from the file on disk
                    SaveSettings(_settings);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ConfigService] Error reading settings: {ex.Message}");
            }
        }

        // Also clean up any legacy plaintext tokens in Documents/sav/appsettings.json
        try
        {
            var docsDir = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            var oldDocSettings = Path.Combine(docsDir, "sav", "appsettings.json");
            if (File.Exists(oldDocSettings))
            {
                var oldJson = File.ReadAllText(oldDocSettings);
                using var oldDoc = JsonDocument.Parse(oldJson);
                if (oldDoc.RootElement.TryGetProperty("googleRefreshToken", out _) ||
                    oldDoc.RootElement.TryGetProperty("googleClientSecret", out _))
                {
                    var sanitized = JsonSerializer.Deserialize<AppSettings>(oldJson);
                    if (sanitized != null)
                    {
                        var cleanJson = JsonSerializer.Serialize(sanitized, new JsonSerializerOptions { WriteIndented = true });
                        File.WriteAllText(oldDocSettings, cleanJson);
                    }
                }
            }
        }
        catch { }

        // 2. Load secure credentials from Windows Credential Manager
        var (refreshToken, accessToken, expiry, clientSecret, email) = _credentialManager.LoadGoogleCredentials();
        if (!string.IsNullOrEmpty(refreshToken)) _settings.GoogleRefreshToken = refreshToken;
        if (!string.IsNullOrEmpty(accessToken)) _settings.GoogleAccessToken = accessToken;
        if (expiry.HasValue) _settings.GoogleTokenExpiry = expiry;
        if (!string.IsNullOrEmpty(clientSecret)) _settings.GoogleClientSecret = clientSecret;
        if (!string.IsNullOrEmpty(email) && string.IsNullOrEmpty(_settings.UserEmail)) _settings.UserEmail = email;

        // 3. Read fallback values from .env if properties are empty
        var envCandidates = new[]
        {
            Path.Combine(RootDir, ".env"),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ".env"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "sav", ".env"),
            Path.Combine(ConfigDir, ".env")
        };

        foreach (var envPath in envCandidates)
        {
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

                        if (key == "GOOGLE_CLIENT_ID" && string.IsNullOrEmpty(_settings.GoogleClientId))
                            _settings.GoogleClientId = val;
                        else if (key == "GOOGLE_CLIENT_SECRET" && string.IsNullOrEmpty(_settings.GoogleClientSecret))
                        {
                            _settings.GoogleClientSecret = val;
                            _credentialManager.SaveGoogleCredentials(
                                _settings.GoogleRefreshToken,
                                _settings.GoogleAccessToken,
                                _settings.GoogleTokenExpiry,
                                val,
                                _settings.UserEmail);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ConfigService] Error reading .env: {ex.Message}");
                }
                break;
            }
        }
    }

    public void SaveSettings(AppSettings newSettings)
    {
        _settings = newSettings;
        try
        {
            // 1. Save sensitive credentials to Windows Credential Manager
            _credentialManager.SaveGoogleCredentials(
                _settings.GoogleRefreshToken,
                _settings.GoogleAccessToken,
                _settings.GoogleTokenExpiry,
                _settings.GoogleClientSecret,
                _settings.UserEmail);

            // 2. Save non-sensitive settings to appsettings.json
            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(_settings, options);
            File.WriteAllText(_settingsFilePath, json);
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
