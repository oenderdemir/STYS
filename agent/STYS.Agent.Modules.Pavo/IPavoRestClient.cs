using STYS.Agent.Contracts.Dtos;

namespace STYS.Agent.Modules.Pavo;

public interface IPavoRestClient
{
    Task<PavoPairingResponse> PairingAsync(PavoPairingRequest request, CancellationToken cancellationToken);
    Task<PavoPingResponse> PingAsync(PavoPingRequest request, CancellationToken cancellationToken);
    Task<PavoGetDeviceInfoResponse> GetDeviceInfoAsync(PavoGetDeviceInfoRequest request, CancellationToken cancellationToken);
    Task<PavoStartPaymentResponse> StartPaymentAsync(PavoStartPaymentRequest request, CancellationToken cancellationToken);
    Task<PavoGetPaymentResultResponse> GetPaymentResultAsync(PavoGetPaymentResultRequest request, CancellationToken cancellationToken);
}

public sealed class PavoRestClientException : Exception
{
    public PavoRestClientException(string errorCode, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        ErrorCode = errorCode;
    }

    public string ErrorCode { get; }
}
