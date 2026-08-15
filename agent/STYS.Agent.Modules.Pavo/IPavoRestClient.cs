using STYS.Agent.Contracts.Dtos;

namespace STYS.Agent.Modules.Pavo;

public interface IPavoRestClient
{
    // --- Operations present in the verified Pavo509.Client reference project ---
    Task<PavoPairingResponse> PairingAsync(PavoPairingRequest request, CancellationToken cancellationToken);
    Task<PavoStartPaymentResponse> StartPaymentAsync(PavoStartPaymentRequest request, CancellationToken cancellationToken);
    Task<PavoPerformEodResponse> PerformEodAsync(PavoPerformEodRequest request, CancellationToken cancellationToken);
    Task<PavoRebootDeviceResponse> RebootDeviceAsync(PavoRebootDeviceRequest request, CancellationToken cancellationToken);
    Task<PavoEnterPinModeResponse> EnterPinModeAsync(PavoEnterPinModeRequest request, CancellationToken cancellationToken);
    Task<PavoExitPinModeResponse> ExitPinModeAsync(PavoExitPinModeRequest request, CancellationToken cancellationToken);

    // --- STYS extensions: NOT part of the reference contract ---
    Task<PavoPingResponse> PingAsync(PavoPingRequest request, CancellationToken cancellationToken);
    Task<PavoGetDeviceInfoResponse> GetDeviceInfoAsync(PavoGetDeviceInfoRequest request, CancellationToken cancellationToken);
    Task<PavoGetPaymentResultResponse> GetPaymentResultAsync(PavoGetPaymentResultRequest request, CancellationToken cancellationToken);
}

public static class PavoTransportErrorCodes
{
    /// <summary>Error codes raised when the request never produced an HTTP response from the device.
    /// The reference client leaves its outgoing transaction sequence untouched in exactly these
    /// cases, so a retry reuses the same sequence number.</summary>
    private static readonly HashSet<string> NoResponseCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        "TIMEOUT",
        "CONNECTION_REFUSED",
        "NETWORK_UNREACHABLE",
        "NETWORK",
        "TLS_CERTIFICATE",
        "INVALID_REQUEST"
    };

    public static bool IsNoResponse(string? errorCode) =>
        !string.IsNullOrWhiteSpace(errorCode) && NoResponseCodes.Contains(errorCode);
}

public sealed class PavoRestClientException : Exception
{
    public PavoRestClientException(string errorCode, string message, bool httpResponseReceived, Exception? innerException = null)
        : base(message, innerException)
    {
        ErrorCode = errorCode;
        HttpResponseReceived = httpResponseReceived;
    }

    public string ErrorCode { get; }

    /// <summary>True when the device actually returned an HTTP response (any status, even an
    /// unparseable body). The reference client advances its outgoing transaction sequence in
    /// exactly this case and leaves it untouched for connection errors and timeouts, so callers
    /// managing sequence state must branch on this flag.</summary>
    public bool HttpResponseReceived { get; }
}
