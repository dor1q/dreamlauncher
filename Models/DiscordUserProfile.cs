using System.Text.Json.Serialization;

namespace DreamLauncher.Models;

public sealed class DiscordUserProfile
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("username")]
    public string Username { get; init; } = string.Empty;

    [JsonPropertyName("global_name")]
    public string? GlobalName { get; init; }

    [JsonPropertyName("avatar")]
    public string? Avatar { get; init; }

    [JsonPropertyName("discriminator")]
    public string? Discriminator { get; init; }

    [JsonIgnore]
    public string DisplayName => string.IsNullOrWhiteSpace(GlobalName) ? Username : GlobalName;
}
