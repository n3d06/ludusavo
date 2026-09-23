using ludusavo.Models;

namespace ludusavo.Services;

public interface IGameWatcherService
{
    bool IsRunning { get; }
    void Start(IEnumerable<DetectedGame> gamesToWatch);
    void Stop();
    event Action<DetectedGame>? OnGameExitedAndSynced;
}
