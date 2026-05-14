using System.IO;
using DreamLauncher.Models;

namespace DreamLauncher.Services;

public sealed class BuildVerificationService
{
    public BuildVerificationResult Verify(BuildDefinition build)
    {
        var items = new List<BuildVerificationItem>();
        var rootExists = Directory.Exists(build.Path);
        var executablePath = ResolveExecutable(build);
        var executableExists = File.Exists(executablePath);
        var fortniteGamePath = Path.Combine(build.Path, "FortniteGame");
        var binariesPath = Path.Combine(build.Path, "FortniteGame", "Binaries", "Win64");
        var enginePath = Path.Combine(build.Path, "Engine");

        items.Add(new BuildVerificationItem
        {
            Title = "Build root",
            State = rootExists ? "OK" : "Missing",
            Details = rootExists ? build.Path : "The selected build folder does not exist."
        });

        items.Add(new BuildVerificationItem
        {
            Title = "Game executable",
            State = executableExists ? "OK" : "Missing",
            Details = executableExists ? executablePath : $"Expected executable: {executablePath}"
        });

        items.Add(CheckDirectory("FortniteGame folder", fortniteGamePath, required: true));
        items.Add(CheckDirectory("Win64 binaries", binariesPath, required: true));
        items.Add(CheckDirectory("Engine folder", enginePath, required: false));

        items.Add(new BuildVerificationItem
        {
            Title = "Exchange-code arguments",
            State = build.UsesExchangeCode ? "OK" : "Warning",
            Details = build.UsesExchangeCode
                ? "Launch arguments contain {exchangeCode}."
                : "Launch may fail unless the build manifest includes {exchangeCode}."
        });

        var canLaunch = rootExists && executableExists;

        return new BuildVerificationResult
        {
            CanLaunch = canLaunch,
            Summary = canLaunch
                ? $"{build.Name} passed required launch checks."
                : $"{build.Name} is missing required files.",
            Items = items
        };
    }

    private static BuildVerificationItem CheckDirectory(string title, string path, bool required)
    {
        var exists = Directory.Exists(path);

        return new BuildVerificationItem
        {
            Title = title,
            State = exists ? "OK" : required ? "Missing" : "Warning",
            Details = exists
                ? path
                : required
                    ? $"Required path was not found: {path}"
                    : $"Optional path was not found: {path}"
        };
    }

    private static string ResolveExecutable(BuildDefinition build)
    {
        return build.ResolvedExecutable;
    }
}
