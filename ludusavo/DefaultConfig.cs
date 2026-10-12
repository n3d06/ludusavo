using System.Reflection;

namespace ludusavo;

public static class DefaultConfig
{
    public static string GoogleClientId { get; } =
        typeof(DefaultConfig).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(a => a.Key == "DefaultGoogleClientId")?.Value ?? string.Empty;

    public static string GoogleClientSecret { get; } =
        typeof(DefaultConfig).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(a => a.Key == "DefaultGoogleClientSecret")?.Value ?? string.Empty;

    public static bool HasEmbeddedCredentials =>
        !string.IsNullOrWhiteSpace(GoogleClientId) &&
        !string.IsNullOrWhiteSpace(GoogleClientSecret);
}

