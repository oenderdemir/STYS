using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using STYS.Agent.Contracts.Dtos;

namespace STYS.Agent.Modules.Pavo;

public sealed class PavoRestClient : IPavoRestClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<PavoRestClient> _logger;

    public PavoRestClient(IHttpClientFactory httpClientFactory, ILogger<PavoRestClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public Task<PavoPairingResponse> PairingAsync(PavoPairingRequest request, CancellationToken cancellationToken) =>
        SendAsync<PavoPairingRequest, PavoPairingResponse>("Pairing", request, cancellationToken);

    public Task<PavoPingResponse> PingAsync(PavoPingRequest request, CancellationToken cancellationToken) =>
        SendAsync<PavoPingRequest, PavoPingResponse>("Ping", request, cancellationToken);

    public Task<PavoGetDeviceInfoResponse> GetDeviceInfoAsync(PavoGetDeviceInfoRequest request, CancellationToken cancellationToken) =>
        SendAsync<PavoGetDeviceInfoRequest, PavoGetDeviceInfoResponse>("GetDeviceInfo", request, cancellationToken);

    private async Task<TResponse> SendAsync<TRequest, TResponse>(string method, TRequest request, CancellationToken cancellationToken)
        where TRequest : PavoDeviceRequestBase
        where TResponse : PavoBaseResponse, new()
    {
        ValidateRequest(request);

        var client = _httpClientFactory.CreateClient("PavoClient");
        client.Timeout = client.Timeout == Timeout.InfiniteTimeSpan ? TimeSpan.FromSeconds(30) : client.Timeout;

        var baseUri = BuildBaseUri(request);
        var uri = new Uri(baseUri, method);

        try
        {
            using var response = await client.PostAsJsonAsync(uri, request, JsonOptions, cancellationToken);
            var result = await ReadResponseAsync<TResponse>(response, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return result;
            }

            if (IsBusinessResponse(result))
            {
                return result;
            }

            throw new PavoRestClientException(
                $"HTTP_{(int)response.StatusCode}",
                BuildHttpErrorMessage(response.StatusCode, result));
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new PavoRestClientException("TIMEOUT", $"PAVO isteği zaman aşımına uğradı ({client.Timeout.TotalSeconds:0}s).", ex);
        }
        catch (HttpRequestException ex) when (ex.InnerException is AuthenticationException or IOException)
        {
            throw new PavoRestClientException("TLS_CERTIFICATE", $"PAVO TLS/sertifika hatası: {ex.Message}", ex);
        }
        catch (HttpRequestException ex) when (ex.InnerException is SocketException socketEx)
        {
            throw new PavoRestClientException(MapSocketError(socketEx), MapSocketErrorMessage(socketEx), ex);
        }
        catch (HttpRequestException ex)
        {
            throw new PavoRestClientException("NETWORK", $"PAVO bağlantı hatası: {ex.Message}", ex);
        }
    }

    private static void ValidateRequest(PavoDeviceRequestBase request)
    {
        if (string.IsNullOrWhiteSpace(request.IpAddress))
            throw new PavoRestClientException("INVALID_REQUEST", "PAVO cihaz IP adresi boş olamaz.");
    }

    private static Uri BuildBaseUri(PavoDeviceRequestBase request)
    {
        var scheme = request.UseHttps || request.HttpsPort.HasValue ? "https" : "http";
        var port = request.UseHttps || request.HttpsPort.HasValue
            ? request.HttpsPort ?? 4568
            : request.HttpPort ?? 4567;

        var builder = new UriBuilder(scheme, request.IpAddress, port);
        return builder.Uri;
    }

    private static async Task<TResponse> ReadResponseAsync<TResponse>(HttpResponseMessage response, CancellationToken cancellationToken)
        where TResponse : PavoBaseResponse, new()
    {
        if (response.Content is null)
        {
            return new TResponse { HasError = !response.IsSuccessStatusCode, Message = response.ReasonPhrase };
        }

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(content))
        {
            return new TResponse { HasError = !response.IsSuccessStatusCode, Message = response.ReasonPhrase };
        }

        try
        {
            var result = JsonSerializer.Deserialize<TResponse>(content, JsonOptions) ?? new TResponse();
            if (!response.IsSuccessStatusCode)
            {
                result.HasError = true;
                result.Message ??= response.ReasonPhrase;
            }

            return result;
        }
        catch (JsonException ex)
        {
            throw new PavoRestClientException("INVALID_RESPONSE", $"PAVO yanıtı ayrıştırılamadı: {ex.Message}", ex);
        }
    }

    private static bool IsBusinessResponse(PavoBaseResponse response) =>
        response.HasError || response.HasAbondon || !string.IsNullOrWhiteSpace(response.ErrorCode);

    private static string BuildHttpErrorMessage(HttpStatusCode statusCode, PavoBaseResponse response)
    {
        var parts = new List<string> { $"PAVO HTTP {(int)statusCode}" };
        if (!string.IsNullOrWhiteSpace(response.ErrorCode))
            parts.Add(response.ErrorCode!);
        if (!string.IsNullOrWhiteSpace(response.Message))
            parts.Add(response.Message!);
        return string.Join(" - ", parts);
    }

    private static string MapSocketError(SocketException ex) => ex.SocketErrorCode switch
    {
        SocketError.ConnectionRefused => "CONNECTION_REFUSED",
        SocketError.HostUnreachable or SocketError.NetworkUnreachable => "NETWORK_UNREACHABLE",
        SocketError.TimedOut => "TIMEOUT",
        _ => "NETWORK"
    };

    private static string MapSocketErrorMessage(SocketException ex) => ex.SocketErrorCode switch
    {
        SocketError.ConnectionRefused => "PAVO bağlantısı reddedildi.",
        SocketError.HostUnreachable or SocketError.NetworkUnreachable => "PAVO ağına erişilemiyor.",
        SocketError.TimedOut => "PAVO bağlantısı zaman aşımına uğradı.",
        _ => $"PAVO ağ hatası: {ex.Message}"
    };
}
