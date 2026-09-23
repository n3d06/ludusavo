using ludusavo.Models;

namespace ludusavo.Services;

public interface IConfigService
{
    AppSettings Settings { get; }
    string RootDir { get; }
    string DataDir { get; }
    string CacheDir { get; }
    string ManifestCachePath { get; }
    void SaveSettings(AppSettings newSettings);
    void ReloadSettings();
}
