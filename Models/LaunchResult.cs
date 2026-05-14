namespace DreamLauncher.Models;

public sealed class LaunchResult
{
    public string Executable { get; init; } = string.Empty;
    public int ProcessId { get; init; }
    public string? InjectedDll { get; init; }
}
