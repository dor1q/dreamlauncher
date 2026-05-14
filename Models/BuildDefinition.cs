using System.Text.Json.Serialization;
using System.IO;

namespace DreamLauncher.Models;

public sealed class BuildDefinition
{
    public const string DefaultExecutable = "FortniteGame\\Binaries\\Win64\\FortniteClient-Win64-Shipping.exe";

    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Path { get; init; } = string.Empty;
    public string Executable { get; init; } = DefaultExecutable;
    public string? DllPath { get; init; }
    public bool InjectDllOnLaunch { get; init; }
    public List<string> Arguments { get; init; } = [];
    public Dictionary<string, string> Env { get; init; } = [];

    [JsonIgnore]
    public bool UsesExchangeCode =>
        Arguments.Any(argument => argument.Contains("{exchangeCode}", StringComparison.OrdinalIgnoreCase));

    [JsonIgnore]
    public string ResolvedExecutable =>
        System.IO.Path.IsPathRooted(Executable)
            ? Executable
            : System.IO.Path.GetFullPath(System.IO.Path.Combine(Path, Executable));

    [JsonIgnore]
    public string ExecutableFileName => System.IO.Path.GetFileName(ResolvedExecutable);

    [JsonIgnore]
    public bool ShouldInjectDll => InjectDllOnLaunch && !string.IsNullOrWhiteSpace(DllPath);

    [JsonIgnore]
    public string? ResolvedDllPath =>
        string.IsNullOrWhiteSpace(DllPath)
            ? null
            : System.IO.Path.IsPathRooted(DllPath)
                ? DllPath
                : System.IO.Path.GetFullPath(System.IO.Path.Combine(Path, DllPath));

    [JsonIgnore]
    public string DllFileName => ResolvedDllPath is null
        ? "DLL not configured"
        : System.IO.Path.GetFileName(ResolvedDllPath);

    public bool IsValid =>
        !string.IsNullOrWhiteSpace(Id) &&
        !string.IsNullOrWhiteSpace(Name) &&
        !string.IsNullOrWhiteSpace(Path) &&
        !string.IsNullOrWhiteSpace(Executable);

    public static List<string> DefaultArguments() =>
        [
            "-AUTH_LOGIN=unused",
            "-AUTH_PASSWORD={exchangeCode}",
            "-AUTH_TYPE=exchangecode"
        ];
}
