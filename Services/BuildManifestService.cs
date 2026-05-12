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
}
