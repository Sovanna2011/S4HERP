namespace S4HERP.Host;

/// <summary>
/// Minimal in-process HTTP probe used by the container HEALTHCHECK so the
/// runtime image does not need curl or wget installed.
///
/// It probes <c>/health/ready</c>, not <c>/health/live</c>. Liveness goes green
/// as soon as the process is listening, which in Development is well before
/// migrations and the sample seed have finished — and a container that reports
/// healthy while every request is still answering 403 is worse than one that
/// reports nothing, because <c>depends_on: service_healthy</c> and every "wait
/// for healthy, then call it" script believe it. Docker does not restart a
/// container for failing its health check, so probing readiness costs nothing;
/// the Dockerfile's start period covers the setup window.
/// </summary>
public static class HealthProbe
{
    public static async Task<int> RunAsync()
    {
        var port = Environment.GetEnvironmentVariable("ASPNETCORE_HTTP_PORTS")?.Split(';')[0] ?? "8080";
        var url = $"http://localhost:{port}/health/ready";

        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            using var response = await client.GetAsync(url);
            if (response.IsSuccessStatusCode)
            {
                return 0;
            }

            await Console.Error.WriteLineAsync($"{url} returned {(int)response.StatusCode}.");
            return 1;
        }
        catch (Exception ex)
        {
            await Console.Error.WriteLineAsync($"{url} unreachable: {ex.Message}");
            return 1;
        }
    }
}
