namespace DreamLauncher.Models;

public enum ServiceState
{
    NotChecked,
    Online,
    Offline
}

public sealed class ServiceCheckResult
{
    public static ServiceCheckResult NotChecked { get; } = new() { State = ServiceState.NotChecked };

    public ServiceState State { get; init; }
    public long LatencyMs { get; init; }
    public int? StatusCode { get; init; }
    public string? Summary { get; init; }
    public string? Error { get; init; }
}
