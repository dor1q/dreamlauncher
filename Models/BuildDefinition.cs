namespace DreamLauncher.Models;

public sealed class BuildDefinition
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Path { get; init; } = string.Empty;
    public string Executable { get; init; } = string.Empty;
    public List<string> Arguments { get; init; } = [];
    public Dictionary<string, string> Env { get; init; } = [];

    public bool UsesExchangeCode =>
        Arguments.Any(argument => argument.Contains("{exchangeCode}", StringComparison.OrdinalIgnoreCase));

    public bool IsValid =>
        !string.IsNullOrWhiteSpace(Id) &&
        !string.IsNullOrWhiteSpace(Name) &&
        !string.IsNullOrWhiteSpace(Path) &&
        !string.IsNullOrWhiteSpace(Executable);
}
