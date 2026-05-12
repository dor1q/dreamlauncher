using System.Diagnostics;
using System.Net.Http;
using System.Net.Sockets;
using DreamLauncher.Models;

namespace DreamLauncher.Services;

public sealed class StatusService
{
    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromSeconds(2.5)
    };

    public async Task<ServiceCheckResult> CheckBackendAsync(string backendUrl)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            using var response = await Http.GetAsync(backendUrl);
            stopwatch.Stop();

            return new ServiceCheckResult
            {
                State = ServiceState.Online,
                LatencyMs = stopwatch.ElapsedMilliseconds,
                StatusCode = (int)response.StatusCode
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
}
