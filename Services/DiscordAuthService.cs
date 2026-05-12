using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DreamLauncher.Models;

namespace DreamLauncher.Services;

public sealed class DiscordAuthService
{
    private const string AuthorizeUrl = "https://discord.com/oauth2/authorize";
    private const string TokenUrl = "https://discord.com/api/oauth2/token";
    private const string CurrentUserUrl = "https://discord.com/api/users/@me";

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
        if (string.IsNullOrWhiteSpace(settings.DiscordClientId))
        {
            throw new InvalidOperationException("Discord client id is required.");
        }

        if (string.IsNullOrWhiteSpace(settings.DiscordClientSecret))
        {
            throw new InvalidOperationException("Discord client secret is required.");
        }

        var state = CreateBase64Url(32);
        var authUrl = BuildAuthorizationUrl(settings, state);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromMinutes(5));

        var callbackTask = WaitForCallbackAsync(settings.DiscordRedirectPort, timeout.Token);
        OpenBrowser(authUrl);

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

        if (!query.TryGetValue("state", out var returnedState) || returnedState != state)
        {
            throw new InvalidOperationException("Discord authorization state mismatch.");
        }

        if (!query.TryGetValue("code", out var code) || string.IsNullOrWhiteSpace(code))
        {
            throw new InvalidOperationException("Discord authorization code was not returned.");
        }

        var token = await ExchangeCodeAsync(settings, code, timeout.Token);
        var user = await GetCurrentUserAsync(token.AccessToken, timeout.Token);

        return new DiscordSession
        {
            AccessToken = token.AccessToken,
            RefreshToken = token.RefreshToken,
            TokenType = token.TokenType,
            Scope = token.Scope,
            ExpiresAtUtc = DateTimeOffset.UtcNow.AddSeconds(token.ExpiresIn),
            User = user
        };
    }

    private static string BuildAuthorizationUrl(LauncherSettings settings, string state)
    {
        var query = new Dictionary<string, string>
        {
            ["response_type"] = "code",
            ["client_id"] = settings.DiscordClientId,
            ["redirect_uri"] = settings.DiscordRedirectUri,
            ["scope"] = settings.DiscordScopes,
            ["state"] = state,
            ["prompt"] = "consent"
        };

        return $"{AuthorizeUrl}?{BuildQuery(query)}";
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

            var parts = requestLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2)
            {
                throw new InvalidOperationException("OAuth callback request was invalid.");
            }

            var body = "<!doctype html><html><head><meta charset=\"utf-8\"><title>Dream Launcher</title></head><body style=\"font-family:Segoe UI,Arial,sans-serif;background:#0b1020;color:#edf2ff\">Discord login finished. You can return to Dream Launcher.</body></html>";
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

    private static async Task<DiscordTokenResponse> ExchangeCodeAsync(
        LauncherSettings settings,
        string code,
        CancellationToken cancellationToken)
    {
        var form = new Dictionary<string, string>
        {
            ["client_id"] = settings.DiscordClientId,
            ["client_secret"] = settings.DiscordClientSecret,
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = settings.DiscordRedirectUri
        };

        using var response = await Http.PostAsync(TokenUrl, new FormUrlEncodedContent(form), cancellationToken);
        var raw = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Discord token exchange failed ({(int)response.StatusCode}): {raw}");
        }

        return JsonSerializer.Deserialize<DiscordTokenResponse>(raw, JsonOptions)
            ?? throw new InvalidOperationException("Discord token response was empty.");
    }

    private static async Task<DiscordUserProfile> GetCurrentUserAsync(string accessToken, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, CurrentUserUrl);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await Http.SendAsync(request, cancellationToken);
        var raw = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Discord user lookup failed ({(int)response.StatusCode}): {raw}");
        }

        return JsonSerializer.Deserialize<DiscordUserProfile>(raw, JsonOptions)
            ?? throw new InvalidOperationException("Discord user response was empty.");
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

    private static string BuildQuery(Dictionary<string, string> values)
    {
        return string.Join("&", values.Select(item =>
            $"{Uri.EscapeDataString(item.Key)}={Uri.EscapeDataString(item.Value)}"));
    }

    private static string CreateBase64Url(int byteCount)
    {
        return Base64Url(RandomNumberGenerator.GetBytes(byteCount));
    }

    private static string Base64Url(byte[] bytes)
    {
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }
}
