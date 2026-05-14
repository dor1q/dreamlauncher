using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using DreamLauncher.Models;

namespace DreamLauncher.Services;

public sealed class DiscordAuthService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromSeconds(15)
    };

    public async Task<DiscordSession> SignInAsync(LauncherSettings settings, CancellationToken cancellationToken = default)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromMinutes(5));

        var start = await StartLoginAsync(settings, timeout.Token);
        var callbackTask = WaitForCallbackAsync(settings.DiscordRedirectPort, timeout.Token);

        OpenBrowser(start.AuthorizationUrl);

        Uri callbackUri;
        try
        {
            callbackUri = await callbackTask;
        }
        catch (OperationCanceledException ex)
        {
            throw new TimeoutException("Discord login timed out.", ex);
        }

        var query = ParseQuery(callbackUri.Query);

        if (query.TryGetValue("error", out var error))
        {
            throw new InvalidOperationException($"Discord rejected authorization: {error}");
        }

        if (!query.TryGetValue("state", out var returnedState) || returnedState != start.State)
        {
            throw new InvalidOperationException("Discord authorization state mismatch.");
        }

        if (!query.TryGetValue("code", out var code) || string.IsNullOrWhiteSpace(code))
        {
            throw new InvalidOperationException("Discord authorization code was not returned.");
        }

        var session = await CompleteLoginAsync(settings, start.State, code, timeout.Token);

        return new DiscordSession
        {
            AccessToken = session.AccessToken,
            TokenType = session.TokenType,
            Scope = "dream.launcher",
            ExpiresAtUtc = DateTimeOffset.UtcNow.AddSeconds(session.ExpiresInSeconds),
            User = session.Discord
        };
    }

    private static async Task<DiscordLoginStartResponse> StartLoginAsync(
        LauncherSettings settings,
        CancellationToken cancellationToken)
    {
        var endpoint = new Uri(
            $"{settings.BackendUrl.TrimEnd('/')}/launcher/api/auth/discord/start?redirect_uri={Uri.EscapeDataString(settings.DiscordRedirectUri)}");

        using var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var response = await Http.SendAsync(request, cancellationToken);
        var raw = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Backend Discord login start failed ({(int)response.StatusCode}): {raw}");
        }

        return JsonSerializer.Deserialize<DiscordLoginStartResponse>(raw, JsonOptions)
            ?? throw new InvalidOperationException("Backend Discord login start response was empty.");
    }

    private static async Task<DreamLauncherSessionResponse> CompleteLoginAsync(
        LauncherSettings settings,
        string state,
        string code,
        CancellationToken cancellationToken)
    {
        var endpoint = new Uri($"{settings.BackendUrl.TrimEnd('/')}/launcher/api/auth/discord/callback");
        var body = JsonSerializer.Serialize(new
        {
            code,
            state,
            redirect_uri = settings.DiscordRedirectUri
        });

        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
        request.Content = new StringContent(body, Encoding.UTF8, "application/json");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var response = await Http.SendAsync(request, cancellationToken);
        var raw = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Backend Discord login callback failed ({(int)response.StatusCode}): {raw}");
        }

        return JsonSerializer.Deserialize<DreamLauncherSessionResponse>(raw, JsonOptions)
            ?? throw new InvalidOperationException("Backend Discord login callback response was empty.");
    }

    private static async Task<Uri> WaitForCallbackAsync(int port, CancellationToken cancellationToken)
    {
        var listener = new TcpListener(IPAddress.Loopback, port);
        listener.Start();

        try
        {
            using var client = await listener.AcceptTcpClientAsync(cancellationToken);
            await using var stream = client.GetStream();
            using var reader = new StreamReader(stream, Encoding.ASCII, leaveOpen: true);

            var requestLine = await reader.ReadLineAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(requestLine))
            {
                throw new InvalidOperationException("OAuth callback was empty.");
            }

            while (!string.IsNullOrEmpty(await reader.ReadLineAsync(cancellationToken)))
            {
            }

            var parts = requestLine.Split([' '], StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2)
            {
                throw new InvalidOperationException("OAuth callback request was invalid.");
            }

            var body = "<!doctype html><html><head><meta charset=\"utf-8\"><title>Dream Launcher</title></head><body style=\"font-family:Segoe UI,Arial,sans-serif;background:#101311;color:#f0f3f1\">Discord login finished. You can return to Dream Launcher.</body></html>";
            var bodyBytes = Encoding.UTF8.GetBytes(body);
            var headerBytes = Encoding.UTF8.GetBytes(
                $"HTTP/1.1 200 OK\r\nContent-Type: text/html; charset=utf-8\r\nContent-Length: {bodyBytes.Length}\r\nConnection: close\r\n\r\n");

            await stream.WriteAsync(headerBytes, cancellationToken);
            await stream.WriteAsync(bodyBytes, cancellationToken);

            return new Uri($"http://127.0.0.1:{port}{parts[1]}");
        }
        finally
        {
            listener.Stop();
        }
    }

    private static void OpenBrowser(string url)
    {
        var info = new ProcessStartInfo
        {
            FileName = url,
            UseShellExecute = true
        };

        Process.Start(info);
    }

    private static Dictionary<string, string> ParseQuery(string query)
    {
        return query.TrimStart('?')
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.Split('=', 2))
            .Where(parts => parts.Length == 2)
            .ToDictionary(
                parts => Uri.UnescapeDataString(parts[0]),
                parts => Uri.UnescapeDataString(parts[1].Replace("+", " ")));
    }
}
