namespace DreamLauncher.Models;

public sealed class LaunchContext
{
    public string ExchangeCode { get; init; } = string.Empty;
    public string AccountId { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string DiscordId { get; init; } = string.Empty;
}
