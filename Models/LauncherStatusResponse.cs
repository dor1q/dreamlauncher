namespace DreamLauncher.Models;

public sealed class LauncherStatusResponse
{
    public string Status { get; init; } = string.Empty;
    public DateTimeOffset? CheckedAtUtc { get; init; }
    public DateTimeOffset? StartedAtUtc { get; init; }
    public int UptimeSeconds { get; init; }
    public List<LauncherServiceStatus> Services { get; init; } = [];
}
