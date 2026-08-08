namespace STYS.Agent.Modules.Pavo;

public interface IPavoClient
{
    Task<PavoConnectionResult> TestConnectionAsync(string endpoint, int timeoutMs, CancellationToken cancellationToken);
}

public sealed class PavoConnectionResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public long? ResponseTimeMs { get; set; }

    public static PavoConnectionResult Ok(long responseTimeMs) => new() { Success = true, ResponseTimeMs = responseTimeMs };
    public static PavoConnectionResult Fail(string error) => new() { Success = false, ErrorMessage = error };
}

public sealed class PavoHttpClient : IPavoClient
{
    private readonly IHttpClientFactory _httpFactory;

    public PavoHttpClient(IHttpClientFactory httpFactory)
    {
        _httpFactory = httpFactory;
    }

    public async Task<PavoConnectionResult> TestConnectionAsync(string endpoint, int timeoutMs, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(endpoint))
            return PavoConnectionResult.Fail("PAVO endpoint yapılandırılmamış.");

        try
        {
            var client = _httpFactory.CreateClient("PavoClient");
            client.Timeout = TimeSpan.FromMilliseconds(timeoutMs);

            var sw = System.Diagnostics.Stopwatch.StartNew();
            var response = await client.GetAsync(endpoint, cancellationToken);
            sw.Stop();

            return response.IsSuccessStatusCode
                ? PavoConnectionResult.Ok(sw.ElapsedMilliseconds)
                : PavoConnectionResult.Fail($"PAVO endpoint HTTP {(int)response.StatusCode} döndü.");
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return PavoConnectionResult.Fail($"PAVO endpoint zaman aşımı ({timeoutMs}ms).");
        }
        catch (HttpRequestException ex)
        {
            return PavoConnectionResult.Fail($"PAVO bağlantı hatası: {ex.Message}");
        }
        catch (Exception ex)
        {
            return PavoConnectionResult.Fail($"PAVO beklenmeyen hata: {ex.Message}");
        }
    }
}
