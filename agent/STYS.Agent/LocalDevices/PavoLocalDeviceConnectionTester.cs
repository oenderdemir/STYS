using Microsoft.Extensions.Logging;
using STYS.Agent.Modules.Pavo;

namespace STYS.Agent.LocalDevices;

public sealed class PavoLocalDeviceConnectionTester : ILocalDeviceConnectionTester
{
    private readonly IPavoClient _client;
    private readonly ILogger<PavoLocalDeviceConnectionTester> _logger;

    public PavoLocalDeviceConnectionTester(IPavoClient client, ILogger<PavoLocalDeviceConnectionTester> logger)
    {
        _client = client;
        _logger = logger;
    }

    public LocalDeviceProvider Provider => LocalDeviceProvider.Pavo;

    public async Task<LocalDeviceConnectionTestResult> TestAsync(LocalDevice device, CancellationToken cancellationToken)
    {
        var result = new LocalDeviceConnectionTestResult
        {
            DeviceId = device.Id,
            TestedAt = DateTimeOffset.UtcNow
        };

        try
        {
            var endpoint = BuildEndpoint(device);
            var connection = await _client.TestConnectionAsync(endpoint, 5000, cancellationToken);
            if (connection.Success)
            {
                result.Success = true;
                result.Status = LocalDeviceConnectionStatus.Connected;
                result.Message = "Bağlantı başarılı.";
                return result;
            }

            result.Success = false;
            result.Status = MapFailureStatus(connection.ErrorMessage);
            result.Message = connection.ErrorMessage ?? "Bağlantı başarısız.";
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "PAVO local connection test failed.");
            result.Success = false;
            result.Status = LocalDeviceConnectionStatus.ProtocolError;
            result.Message = ex.Message;
            return result;
        }
    }

    private static string BuildEndpoint(LocalDevice device)
    {
        var builder = new UriBuilder(device.Protocol == LocalDeviceProtocol.Https ? Uri.UriSchemeHttps : Uri.UriSchemeHttp, device.Host, ResolvePort(device));
        return builder.Uri.ToString();
    }

    private static int ResolvePort(LocalDevice device) =>
        device.Protocol == LocalDeviceProtocol.Https
            ? (device.HttpsPort > 0 ? device.HttpsPort : 4568)
            : (device.HttpPort > 0 ? device.HttpPort : 4567);

    private static LocalDeviceConnectionStatus MapFailureStatus(string? errorMessage)
    {
        var normalized = (errorMessage ?? string.Empty).ToLowerInvariant();
        if (normalized.Contains("timeout") || normalized.Contains("zaman aş") || normalized.Contains("timed out"))
        {
            return LocalDeviceConnectionStatus.Timeout;
        }

        if (normalized.Contains("tls") || normalized.Contains("certificate") || normalized.Contains("sertifika"))
        {
            return LocalDeviceConnectionStatus.TlsError;
        }

        if (normalized.Contains("http ") || normalized.Contains("protocol"))
        {
            return LocalDeviceConnectionStatus.ProtocolError;
        }

        if (normalized.Contains("bağlantı hatası") || normalized.Contains("network") || normalized.Contains("unreachable") || normalized.Contains("refused") || normalized.Contains("dns"))
        {
            return LocalDeviceConnectionStatus.Unreachable;
        }

        return LocalDeviceConnectionStatus.Unreachable;
    }
}
