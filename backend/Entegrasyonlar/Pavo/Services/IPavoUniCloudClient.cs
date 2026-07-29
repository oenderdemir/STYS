using STYS.Entegrasyonlar.Pos.Entities;

namespace STYS.Entegrasyonlar.Pavo.Services;

public interface IPavoUniCloudClient
{
    Task<PavoPairingResult> PairingRequestAsync(PosTerminal terminal, CancellationToken cancellationToken);
    Task<PavoPairingResult> CheckPairingAsync(PosTerminal terminal, CancellationToken cancellationToken);
    Task<PavoCreateLinkResult> CreateLinkAsync(PosTerminal terminal, string reference, decimal amount, string currency, CancellationToken cancellationToken);
    Task<PavoCheckLinkResult> CheckLinkAsync(PosTerminal terminal, long paymentLinkId, string reference, CancellationToken cancellationToken);
}

public sealed record PavoPairingResult(
    long Id,
    string? PairingCode,
    string? TargetFingerprint,
    bool IsApproved);

public sealed record PavoCreateLinkResult(long Id, int StatusId, string RawJson);

public sealed record PavoCheckLinkResult(
    int StatusId,
    bool Pending,
    bool Successful,
    string RawJson,
    string? ErrorMessage,
    string? RetrievalReferenceNo,
    string? AcquirerReference,
    string? AuthorizationCode);
