namespace DreamLauncher.Models;

public sealed class DiscordLoginStartResponse
{
    public string AuthorizationUrl { get; init; } = string.Empty;
    public string State { get; init; } = string.Empty;
    public string RedirectUri { get; init; } = string.Empty;
    public int ExpiresInSeconds { get; init; }
}
