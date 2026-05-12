using System.IO;
using DreamLauncher.Models;

namespace DreamLauncher.Services;

public sealed class DiscordSessionService
{
    public string SessionPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Dream Launcher",
        "discord-session.json");

    public async Task<DiscordSession?> LoadAsync()
    {
        var session = await JsonFile.ReadAsync<DiscordSession>(SessionPath);
        return session is null || session.IsExpired ? null : session;
    }

    public async Task SaveAsync(DiscordSession session)
    {
        await JsonFile.WriteAsync(SessionPath, session);
    }

    public Task ClearAsync()
    {
        if (File.Exists(SessionPath))
        {
            File.Delete(SessionPath);
        }

        return Task.CompletedTask;
    }
}
