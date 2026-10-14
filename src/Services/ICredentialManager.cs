namespace ludusavo.Services;

public interface ICredentialManager
{
    bool SaveGoogleCredentials(string? refreshToken, string? accessToken, DateTime? expiry, string? clientSecret, string? userEmail);
    (string? refreshToken, string? accessToken, DateTime? expiry, string? clientSecret, string? userEmail) LoadGoogleCredentials();
    bool DeleteGoogleCredentials();
}
