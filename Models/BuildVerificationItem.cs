namespace DreamLauncher.Models;

public sealed class BuildVerificationResult
{
    public string Summary { get; init; } = string.Empty;
    public bool CanLaunch { get; init; }
    public List<BuildVerificationItem> Items { get; init; } = [];
}

public sealed class BuildVerificationItem
{
    public string Title { get; init; } = string.Empty;
    public string State { get; init; } = string.Empty;
    public string Details { get; init; } = string.Empty;
}
