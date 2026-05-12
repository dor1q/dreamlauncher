using System.IO;
using DreamLauncher.Models;

namespace DreamLauncher.Services;

public sealed class SettingsService
{
    public string SettingsPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Dream Launcher",
        "settings.json");

    public async Task<LauncherSettings> LoadAsync()
    {
        var settings = await JsonFile.ReadAsync<LauncherSettings>(SettingsPath);
        return LauncherSettings.Merge(settings);
    }

    public async Task SaveAsync(LauncherSettings settings)
    {
        await JsonFile.WriteAsync(SettingsPath, LauncherSettings.Merge(settings));
    }
}
