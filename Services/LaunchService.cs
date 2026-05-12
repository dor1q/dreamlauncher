using System.Diagnostics;
using System.IO;
using DreamLauncher.Models;

namespace DreamLauncher.Services;

public sealed class LaunchService
{
    public string Launch(BuildDefinition build, LaunchContext context)
    {
        var executable = ResolveExecutable(build);

        if (!File.Exists(executable))
        {
            throw new FileNotFoundException("Executable not found", executable);
        }

        var info = new ProcessStartInfo
        {
            FileName = executable,
            WorkingDirectory = Path.GetDirectoryName(executable)!,
            UseShellExecute = false
        };

        foreach (var argument in ResolveArguments(build, context))
        {
            info.ArgumentList.Add(argument);
        }

        foreach (var item in build.Env)
        {
            info.Environment[item.Key] = item.Value;
        }

        Process.Start(info);
        return executable;
    }

    public void OpenInExplorer(string path)
    {
        var info = new ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = $"/select,\"{path}\"",
            UseShellExecute = true
        };

        Process.Start(info);
    }

    private static string ResolveExecutable(BuildDefinition build)
    {
        return Path.IsPathRooted(build.Executable)
            ? build.Executable
            : Path.GetFullPath(Path.Combine(build.Path, build.Executable));
    }

    private static IEnumerable<string> ResolveArguments(BuildDefinition build, LaunchContext context)
    {
        return build.Arguments.Select(argument => argument
            .Replace("{exchangeCode}", context.ExchangeCode, StringComparison.OrdinalIgnoreCase)
            .Replace("{accountId}", context.AccountId, StringComparison.OrdinalIgnoreCase)
            .Replace("{displayName}", context.DisplayName, StringComparison.OrdinalIgnoreCase)
            .Replace("{discordId}", context.DiscordId, StringComparison.OrdinalIgnoreCase));
    }
}
