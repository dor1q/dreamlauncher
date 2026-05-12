using System.IO;
using DreamLauncher.Models;

namespace DreamLauncher.Services;

public sealed class BuildManifestService
{
    private readonly string _appRoot = ResolveAppRoot();

    public string ManifestPath => Path.Combine(_appRoot, "config", "builds.json");
    public string ExampleManifestPath => Path.Combine(_appRoot, "config", "builds.example.json");

    public async Task EnsureManifestAsync()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(ManifestPath)!);

        if (!File.Exists(ManifestPath) && File.Exists(ExampleManifestPath))
        {
            File.Copy(ExampleManifestPath, ManifestPath);
        }

        if (!File.Exists(ManifestPath))
        {
            await JsonFile.WriteAsync(ManifestPath, new BuildManifest());
        }
    }

    public async Task<BuildManifest> LoadAsync()
    {
        await EnsureManifestAsync();

        var manifest = await JsonFile.ReadAsync<BuildManifest>(ManifestPath) ?? new BuildManifest();
        var validBuilds = manifest.Builds
            .Where(build => build.IsValid)
            .ToList();

        return new BuildManifest { Builds = validBuilds };
    }

    public async Task SaveAsync(BuildManifest manifest)
    {
        await EnsureManifestAsync();
        await JsonFile.WriteAsync(ManifestPath, manifest);
    }

    public async Task<BuildDefinition> AddExistingBuildAsync(string rootPath)
    {
        if (string.IsNullOrWhiteSpace(rootPath))
        {
            throw new InvalidOperationException("Build folder is required.");
        }

        var fullRoot = Path.GetFullPath(rootPath);
        var executable = Path.Combine(fullRoot, BuildDefinition.DefaultExecutable);

        if (!File.Exists(executable))
        {
            throw new FileNotFoundException("Fortnite executable was not found in this folder.", executable);
        }

        var manifest = await LoadAsync();
        var folderName = new DirectoryInfo(fullRoot).Name;
        var build = new BuildDefinition
        {
            Id = CreateBuildId(folderName),
            Name = string.IsNullOrWhiteSpace(folderName) ? "Imported Build" : folderName,
            Path = fullRoot,
            Executable = BuildDefinition.DefaultExecutable,
            Arguments = BuildDefinition.DefaultArguments()
        };

        manifest.Builds.RemoveAll(item =>
            string.Equals(Path.GetFullPath(item.Path), fullRoot, StringComparison.OrdinalIgnoreCase));
        manifest.Builds.Add(build);

        await SaveAsync(manifest);
        return build;
    }

    private static string ResolveAppRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);

        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "DreamLauncher.csproj")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        return AppContext.BaseDirectory;
    }

    private static string CreateBuildId(string value)
    {
        var cleaned = new string(value
            .ToLowerInvariant()
            .Select(ch => char.IsLetterOrDigit(ch) ? ch : '-')
            .ToArray())
            .Trim('-');

        if (string.IsNullOrWhiteSpace(cleaned))
        {
            cleaned = "imported-build";
        }

        return $"{cleaned}-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}";
    }
}
