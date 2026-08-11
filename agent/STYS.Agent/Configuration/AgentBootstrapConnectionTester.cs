using System.Net.Http.Json;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Text.Json;

namespace STYS.Agent.Configuration;

public sealed class AgentBootstrapConnectionTester : IAgentBootstrapConnectionTester
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IHttpClientFactory _httpClientFactory;

    public AgentBootstrapConnectionTester(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public async Task<AgentBootstrapConnectionTestResult> TestAsync(string baseUrl, int timeoutSeconds, CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var parsed) ||
            (parsed.Scheme != Uri.UriSchemeHttp && parsed.Scheme != Uri.UriSchemeHttps))
        {
            return Failure("invalid-url", "Geçersiz STYS adresi.");
        }

        var normalizedPath = parsed.AbsolutePath.TrimEnd('/');
        var path = normalizedPath.EndsWith("/api", StringComparison.OrdinalIgnoreCase)
            ? "agent/bootstrap/ping"
            : "api/agent/bootstrap/ping";

        var requestUri = new Uri(parsed, path);
        using var client = _httpClientFactory.CreateClient(nameof(AgentBootstrapConnectionTester));
        client.Timeout = Timeout.InfiniteTimeSpan;

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(timeoutSeconds, 1, 120)));

        try
        {
            using var response = await client.GetAsync(requestUri, timeoutCts.Token);
            if (!response.IsSuccessStatusCode)
            {
                return Failure($"http-{(int)response.StatusCode}", $"STYS adresi erişilebilir fakat HTTP {(int)response.StatusCode} döndü.");
            }

            var payload = await response.Content.ReadFromJsonAsync<BootstrapPingResponse>(JsonOptions, cancellationToken);
            return new AgentBootstrapConnectionTestResult
            {
                Success = true,
                Status = "ok",
                Message = "Bağlantı başarılı.",
                ServerTime = payload?.ServerTime,
                Version = payload?.Version
            };
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Failure("timeout", "Timeout.");
        }
        catch (HttpRequestException ex) when (IsDnsFailure(ex))
        {
            return Failure("dns-error", "DNS hatası");
        }
        catch (HttpRequestException ex) when (IsConnectionRefused(ex))
        {
            return Failure("connection-refused", "Connection refused");
        }
        catch (HttpRequestException ex) when (IsTlsFailure(ex))
        {
            return Failure("tls-error", "TLS/certificate hatası");
        }
        catch (HttpRequestException ex)
        {
            return Failure("http-error", ex.Message);
        }
    }

    private static AgentBootstrapConnectionTestResult Failure(string status, string message) =>
        new() { Success = false, Status = status, Message = message };

    private static bool IsDnsFailure(HttpRequestException ex) =>
        ex.InnerException is SocketException socketEx &&
        socketEx.SocketErrorCode is SocketError.HostNotFound or SocketError.TryAgain or SocketError.NoData;

    private static bool IsConnectionRefused(HttpRequestException ex) =>
        ex.InnerException is SocketException socketEx &&
        socketEx.SocketErrorCode is SocketError.ConnectionRefused or SocketError.NetworkUnreachable;

    private static bool IsTlsFailure(HttpRequestException ex) =>
        ex.InnerException is AuthenticationException;

    private sealed class BootstrapPingResponse
    {
        public string? Status { get; set; }
        public string? ServerTime { get; set; }
        public string? Version { get; set; }
    }
}
