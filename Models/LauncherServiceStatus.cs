namespace DreamLauncher.Models;

public sealed class LauncherServiceStatus
{
    public string Id { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
    public string State { get; init; } = string.Empty;
    public string? Details { get; init; }
    public int? Port { get; init; }
    public int? ConnectedClients { get; init; }
}
