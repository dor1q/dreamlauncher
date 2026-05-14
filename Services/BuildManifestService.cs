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

    public async Task<BuildDefinition> AddExistingBuildExecutableAsync(string executablePath)
    {
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            throw new InvalidOperationException("Build executable is required.");
        }

        var fullExecutable = Path.GetFullPath(executablePath);

        if (!File.Exists(fullExecutable))
        {
            throw new FileNotFoundException("Build executable was not found.", fullExecutable);
        }

        var fullRoot = ResolveBuildRootFromExecutable(fullExecutable);
        var relativeExecutable = Path.GetRelativePath(fullRoot, fullExecutable);
        var folderName = new DirectoryInfo(fullRoot).Name;
        var build = new BuildDefinition
        {
            Id = CreateBuildId(folderName),
            Name = string.IsNullOrWhiteSpace(folderName) ? Path.GetFileNameWithoutExtension(fullExecutable) : folderName,
            Path = fullRoot,
            Executable = relativeExecutable,
            Arguments = BuildDefinition.DefaultArguments()
        };

        var manifest = await LoadAsync();
        manifest.Builds.RemoveAll(item =>
            string.Equals(item.ResolvedExecutable, fullExecutable, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(Path.GetFullPath(item.Path), fullRoot, StringComparison.OrdinalIgnoreCase));
        manifest.Builds.Add(build);

        await SaveAsync(manifest);
        return build;
    }

    public async Task<bool> RemoveBuildAsync(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return false;
        }

        var manifest = await LoadAsync();
        var removed = manifest.Builds.RemoveAll(build =>
            string.Equals(build.Id, id, StringComparison.OrdinalIgnoreCase)) > 0;

        if (removed)
        {
            await SaveAsync(manifest);
        }

        return removed;
    }

    public async Task<BuildDefinition?> SetBuildDllAsync(string buildId, string dllPath)
    {
        if (string.IsNullOrWhiteSpace(buildId) || string.IsNullOrWhiteSpace(dllPath))
        {
            return null;
        }

        var fullDllPath = Path.GetFullPath(dllPath);

        if (!File.Exists(fullDllPath))
        {
            throw new FileNotFoundException("DLL file was not found.", fullDllPath);
        }

        var manifest = await LoadAsync();
        var index = manifest.Builds.FindIndex(build =>
            string.Equals(build.Id, buildId, StringComparison.OrdinalIgnoreCase));

        if (index < 0)
        {
            return null;
        }

        var build = manifest.Builds[index];
        var updated = CopyBuild(build, dllPath: MakePathRelativeToBuild(build, fullDllPath), injectDllOnLaunch: true);
        manifest.Builds[index] = updated;

        await SaveAsync(manifest);
        return updated;
    }

    public async Task<BuildDefinition?> ClearBuildDllAsync(string buildId)
    {
        if (string.IsNullOrWhiteSpace(buildId))
        {
            return null;
        }

        var manifest = await LoadAsync();
        var index = manifest.Builds.FindIndex(build =>
            string.Equals(build.Id, buildId, StringComparison.OrdinalIgnoreCase));

        if (index < 0)
        {
            return null;
        }

        var updated = CopyBuild(manifest.Builds[index], dllPath: null, injectDllOnLaunch: false);
        manifest.Builds[index] = updated;

        await SaveAsync(manifest);
        return updated;
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

    private static BuildDefinition CopyBuild(BuildDefinition build, string? dllPath, bool injectDllOnLaunch)
    {
        return new BuildDefinition
        {
            Id = build.Id,
            Name = build.Name,
            Path = build.Path,
            Executable = build.Executable,
            DllPath = dllPath,
            InjectDllOnLaunch = injectDllOnLaunch,
            Arguments = [.. build.Arguments],
            Env = new Dictionary<string, string>(build.Env, StringComparer.OrdinalIgnoreCase)
        };
    }

    private static string MakePathRelativeToBuild(BuildDefinition build, string fullPath)
    {
        var root = Path.GetFullPath(build.Path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var normalized = Path.GetFullPath(fullPath);
        var rootWithSeparator = root + Path.DirectorySeparatorChar;

        return normalized.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase)
            ? Path.GetRelativePath(root, normalized)
            : normalized;
    }

    private static string ResolveBuildRootFromExecutable(string executablePath)
    {
        var defaultExecutable = BuildDefinition.DefaultExecutable
            .Replace('\\', Path.DirectorySeparatorChar)
            .Replace('/', Path.DirectorySeparatorChar);
        var normalizedExecutable = executablePath
            .Replace('\\', Path.DirectorySeparatorChar)
            .Replace('/', Path.DirectorySeparatorChar);

        if (normalizedExecutable.EndsWith(defaultExecutable, StringComparison.OrdinalIgnoreCase))
        {
            var root = normalizedExecutable[..^defaultExecutable.Length]
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            if (!string.IsNullOrWhiteSpace(root))
            {
                return root;
            }
        }

        return Path.GetDirectoryName(executablePath)
            ?? throw new InvalidOperationException("Executable directory could not be resolved.");
    }
}
