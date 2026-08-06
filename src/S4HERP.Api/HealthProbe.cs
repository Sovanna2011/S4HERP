namespace S4HERP.Api;

/// <summary>
/// Minimal in-process HTTP probe used by the container HEALTHCHECK so the
/// runtime image does not need curl or wget installed.
/// </summary>
public static class HealthProbe
{
    public static async Task<int> RunAsync()
    {
        var port = Environment.GetEnvironmentVariable("ASPNETCORE_HTTP_PORTS")?.Split(';')[0] ?? "8080";
        var url = $"http://localhost:{port}/health/live";

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
