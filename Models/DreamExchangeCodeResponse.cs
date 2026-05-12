using System.Text.Json.Serialization;

namespace DreamLauncher.Models;

public sealed class DreamExchangeCodeResponse
{
    [JsonPropertyName("code")]
    public string Code { get; init; } = string.Empty;

    [JsonPropertyName("expiresInSeconds")]
    public int ExpiresInSeconds { get; init; }

    [JsonPropertyName("accountId")]
    public string AccountId { get; init; } = string.Empty;

    [JsonPropertyName("displayName")]
    public string DisplayName { get; init; } = string.Empty;
}
