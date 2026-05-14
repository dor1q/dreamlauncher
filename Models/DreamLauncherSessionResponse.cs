namespace DreamLauncher.Models;

public sealed class DreamLauncherSessionResponse
{
    public string AccessToken { get; init; } = string.Empty;
    public string TokenType { get; init; } = "DreamLauncher";
    public int ExpiresInSeconds { get; init; }
    public string AccountId { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public DiscordUserProfile Discord { get; init; } = new();
}
