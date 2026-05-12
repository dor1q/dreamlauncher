using System.Text.Json.Serialization;

namespace DreamLauncher.Models;

public sealed class DiscordSession
{
    public string AccessToken { get; init; } = string.Empty;
    public string RefreshToken { get; init; } = string.Empty;
    public string TokenType { get; init; } = "Bearer";
    public string Scope { get; init; } = string.Empty;
    public DateTimeOffset ExpiresAtUtc { get; init; }
    public DiscordUserProfile User { get; init; } = new();

    [JsonIgnore]
    public bool IsExpired => ExpiresAtUtc <= DateTimeOffset.UtcNow.AddMinutes(1);
}
