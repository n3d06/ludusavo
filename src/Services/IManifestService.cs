using ludusavo.Models;

namespace ludusavo.Services;

public interface IManifestService
{
    bool IsLoaded { get; }
    int GameCount { get; }
    Task<bool> LoadManifestAsync(bool forceDownload = false);
    IReadOnlyList<GameEntry> GetAllGames();
    GameEntry? GetGameById(string id);
    GameEntry? GetGameBySteamId(int steamId);
    IEnumerable<GameEntry> SearchGames(string query);
    Task AddCustomGameAsync(GameEntry game);
    Task RemoveCustomGameAsync(string gameId);
}
