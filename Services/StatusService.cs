using System.Diagnostics;
using System.Net.Http;
using System.Net.Sockets;
using System.Text.Json;
using DreamLauncher.Models;

namespace DreamLauncher.Services;

public sealed class StatusService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromSeconds(2.5)
    };

    public async Task<ServiceCheckResult> CheckBackendAsync(string backendUrl)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var endpoint = new Uri($"{backendUrl.TrimEnd('/')}/launcher/api/status");
            using var response = await Http.GetAsync(endpoint);
            stopwatch.Stop();

            var raw = await response.Content.ReadAsStringAsync();
            var status = TryReadStatus(raw);
            var offlineServices = status?.Services
                .Where(service => IsOffline(service.State))
                .ToList() ?? [];

            return new ServiceCheckResult
            {
                State = response.IsSuccessStatusCode && offlineServices.Count == 0
                    ? ServiceState.Online
                    : ServiceState.Offline,
                LatencyMs = stopwatch.ElapsedMilliseconds,
                StatusCode = (int)response.StatusCode,
                Summary = status is null ? null : BuildStatusSummary(status),
                Error = response.IsSuccessStatusCode
                    ? BuildOfflineServiceSummary(offlineServices)
                    : $"HTTP {(int)response.StatusCode}",
                BackendStatus = status
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return new ServiceCheckResult
            {
                State = ServiceState.Offline,
                LatencyMs = stopwatch.ElapsedMilliseconds,
                Error = ex.Message
            };
        }
    }

    public async Task<ServiceCheckResult> CheckTcpAsync(string host, int port)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            using var client = new TcpClient();
            var connectTask = client.ConnectAsync(host, port);
            var completed = await Task.WhenAny(connectTask, Task.Delay(TimeSpan.FromSeconds(2.5)));

            if (completed != connectTask)
            {
                throw new TimeoutException("Connection timed out");
            }

            await connectTask;
            stopwatch.Stop();

            return new ServiceCheckResult
            {
                State = ServiceState.Online,
                LatencyMs = stopwatch.ElapsedMilliseconds
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return new ServiceCheckResult
            {
                State = ServiceState.Offline,
                LatencyMs = stopwatch.ElapsedMilliseconds,
                Error = ex.Message
            };
        }
    }

    private static LauncherStatusResponse? TryReadStatus(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<LauncherStatusResponse>(raw, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string BuildStatusSummary(LauncherStatusResponse status)
    {
        if (status.Services.Count == 0)
        {
            return status.Status;
        }

        var parts = status.Services
            .Take(4)
            .Select(service => $"{service.Label}: {service.State}");

        return string.Join(", ", parts);
    }

    private static string? BuildOfflineServiceSummary(List<LauncherServiceStatus> offlineServices)
    {
        return offlineServices.Count == 0
            ? null
            : string.Join(", ", offlineServices.Select(service => $"{service.Label}: {service.Details ?? service.State}"));
    }

    private static bool IsOffline(string state)
    {
        return state.Equals("offline", StringComparison.OrdinalIgnoreCase);
    }
}
