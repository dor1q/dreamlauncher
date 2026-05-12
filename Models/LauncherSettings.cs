namespace DreamLauncher.Models;

public sealed class LauncherSettings
{
    private const string DefaultBackendUrl = "http://127.0.0.1:8080";
    private const string DefaultGameServerHost = "127.0.0.1";
    private const int DefaultGameServerPort = 7777;

    public static LauncherSettings Default { get; } = new()
    {
        BackendUrl = DefaultBackendUrl,
        GameServerHost = DefaultGameServerHost,
        GameServerPort = DefaultGameServerPort
    };

    public string BackendUrl { get; init; } = DefaultBackendUrl;
    public string GameServerHost { get; init; } = DefaultGameServerHost;
    public int GameServerPort { get; init; } = DefaultGameServerPort;

    public static LauncherSettings FromInput(string? backendUrl, string? gameServerHost, string? gameServerPort)
    {
        return new LauncherSettings
        {
            BackendUrl = NormalizeBackendUrl(backendUrl),
            GameServerHost = string.IsNullOrWhiteSpace(gameServerHost) ? Default.GameServerHost : gameServerHost.Trim(),
            GameServerPort = NormalizePort(gameServerPort)
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
            GameServerPort = settings.GameServerPort is > 0 and <= 65535 ? settings.GameServerPort : Default.GameServerPort
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

    private static int NormalizePort(string? value)
    {
        return int.TryParse(value, out var port) && port is > 0 and <= 65535
            ? port
            : Default.GameServerPort;
    }
}
