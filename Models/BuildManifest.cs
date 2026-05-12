namespace DreamLauncher.Models;

public sealed class BuildManifest
{
    public List<BuildDefinition> Builds { get; init; } = [];
}
