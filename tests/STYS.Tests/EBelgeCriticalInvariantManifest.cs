namespace STYS.Tests;

/// <summary>
/// Faz 2B.9.1 - kritik e-Belge invariantlarının TEK, merkezi listesi. Bu liste script veya başka
/// bir test dosyasına ELLE KOPYALANMAZ - hem `EBelgeTestMetadataContractTests` (her invariant için
/// en az bir test bulunduğunu doğrular) hem de `release` profilinin dependency-gate adımı
/// (`dotnet test --filter "FullyQualifiedName~EBelgeTestMetadataContractTests"`) bu SINIFI referans
/// alır. Yeni bir kritik invariant eklemek/kaldırmak İÇİN yalnız BU liste güncellenir.
/// </summary>
public static class EBelgeCriticalInvariantManifest
{
    public static readonly IReadOnlyList<string> KnownInvariants =
    [
        "TenantIsolation",
        "StaleWorkerCannotWrite",
        "LeaseTakeover",
        "UnsignedExactByteHash",
        "SignedExactByteHash",
        "SignatureTamperRejected",
        "DuplicateXmlIdRejected",
        "SchematronRealSidecar",
        "ActivationNotBefore20260915",
        "WorkerEndToEndSignedReady",
        "InstitutionPolicyFailClosed",
        "InstitutionPolicyTenantIsolation",
        "PortalRouteNeverSignsLocally",
        "InactivePolicyNeverClaims",
        "PolicyKillSwitchPreventsCommit",
        "NonLocalRouteIsIdempotent",
        "LegacyDecisionNeverProcesses",
        "SigningGatePreventsQueuedSigning",
        "PolicyDecisionVersionIsSerialized",
    ];
}
