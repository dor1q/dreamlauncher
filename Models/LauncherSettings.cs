using System.Text.Json.Serialization;

namespace DreamLauncher.Models;

public sealed class LauncherSettings
{
    private const string DefaultBackendUrl = "http://127.0.0.1:8080";
    private const string DefaultGameServerHost = "127.0.0.1";
    private const int DefaultGameServerPort = 7777;
    private const int DefaultDiscordRedirectPort = 53121;

    public static LauncherSettings Default { get; } = new()
    {
        BackendUrl = DefaultBackendUrl,
        GameServerHost = DefaultGameServerHost,
        GameServerPort = DefaultGameServerPort,
        DiscordRedirectPort = DefaultDiscordRedirectPort
    };

    public string BackendUrl { get; init; } = DefaultBackendUrl;
    public string GameServerHost { get; init; } = DefaultGameServerHost;
    public int GameServerPort { get; init; } = DefaultGameServerPort;
    public string DiscordClientId { get; init; } = string.Empty;
    public string DiscordClientSecret { get; init; } = string.Empty;
    public int DiscordRedirectPort { get; init; } = DefaultDiscordRedirectPort;
    public string DiscordScopes { get; init; } = "identify";

    [JsonIgnore]
    public string DiscordRedirectUri => $"http://127.0.0.1:{DiscordRedirectPort}/callback/";

    public static LauncherSettings FromInput(
        string? backendUrl,
        string? gameServerHost,
        string? gameServerPort,
        string? discordClientId,
        string? discordClientSecret,
        string? discordRedirectPort)
    {
        return new LauncherSettings
        {
            BackendUrl = NormalizeBackendUrl(backendUrl),
            GameServerHost = string.IsNullOrWhiteSpace(gameServerHost) ? Default.GameServerHost : gameServerHost.Trim(),
            GameServerPort = NormalizePort(gameServerPort, Default.GameServerPort),
            DiscordClientId = string.IsNullOrWhiteSpace(discordClientId) ? string.Empty : discordClientId.Trim(),
            DiscordClientSecret = string.IsNullOrWhiteSpace(discordClientSecret) ? string.Empty : discordClientSecret.Trim(),
            DiscordRedirectPort = NormalizePort(discordRedirectPort, Default.DiscordRedirectPort),
            DiscordScopes = "identify"
        };
    }

    public static LauncherSettings Merge(LauncherSettings? settings)
    {
        if (settings is null)
        {
            return Default;
        }

        return new LauncherSettings
        {
            BackendUrl = NormalizeBackendUrl(settings.BackendUrl),
            GameServerHost = string.IsNullOrWhiteSpace(settings.GameServerHost) ? Default.GameServerHost : settings.GameServerHost.Trim(),
            GameServerPort = settings.GameServerPort is > 0 and <= 65535 ? settings.GameServerPort : Default.GameServerPort,
            DiscordClientId = string.IsNullOrWhiteSpace(settings.DiscordClientId) ? string.Empty : settings.DiscordClientId.Trim(),
            DiscordClientSecret = string.IsNullOrWhiteSpace(settings.DiscordClientSecret) ? string.Empty : settings.DiscordClientSecret.Trim(),
            DiscordRedirectPort = settings.DiscordRedirectPort is > 0 and <= 65535 ? settings.DiscordRedirectPort : Default.DiscordRedirectPort,
            DiscordScopes = string.IsNullOrWhiteSpace(settings.DiscordScopes) ? "identify" : settings.DiscordScopes.Trim()
        };
    }

    private static string NormalizeBackendUrl(string? value)
    {
        var raw = string.IsNullOrWhiteSpace(value) ? Default.BackendUrl : value.Trim();

        if (!raw.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !raw.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            raw = $"http://{raw}";
        }

        if (!Uri.TryCreate(raw, UriKind.Absolute, out var uri))
        {
            return Default.BackendUrl;
        }

        return uri.ToString().TrimEnd('/');
    }

    private static int NormalizePort(string? value, int fallback)
    {
        return int.TryParse(value, out var port) && port is > 0 and <= 65535
            ? port
            : fallback;
    }
}
