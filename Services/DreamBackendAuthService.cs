using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using DreamLauncher.Models;

namespace DreamLauncher.Services;

public sealed class DreamBackendAuthService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromSeconds(15)
    };

    public async Task<DreamExchangeCodeResponse> CreateExchangeCodeAsync(
        LauncherSettings settings,
        DiscordSession session,
        CancellationToken cancellationToken = default)
    {
        var endpoint = new Uri($"{settings.BackendUrl.TrimEnd('/')}/launcher/api/auth/discord/exchange");
        var body = JsonSerializer.Serialize(new { access_token = session.AccessToken });

        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
        request.Content = new StringContent(body, Encoding.UTF8, "application/json");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var response = await Http.SendAsync(request, cancellationToken);
        var raw = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Backend exchange failed ({(int)response.StatusCode}): {raw}");
        }

        return JsonSerializer.Deserialize<DreamExchangeCodeResponse>(raw, JsonOptions)
            ?? throw new InvalidOperationException("Backend exchange response was empty.");
    }
}
