using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.Muhasebe.SatisBelgeleri;

/// <summary>
/// Aynı benzersiz anahtar (KurumId+EBelgeKaydiId+ArtifactTipi+ArtifactAsamasi) altında FARKLI
/// bir hash'e sahip artefakt zaten var - KALICI bütünlük hatası (bkz. Faz 2B.6 görev md.8).
/// Otomatik retry YAPILMAZ; bu, determinizmin bozulduğunun veya art arda gelen iki farklı
/// render girişiminin işaretidir ve elle incelenmelidir.
/// </summary>
public sealed class EBelgeArtifactIdempotencyConflictException : BaseException
{
    public const int HttpStatusCode = 409;
    public const string SafeErrorCode = "EBELGE_ARTIFACT_IDEMPOTENCY_CONFLICT";

    public string HataKodu { get; } = SafeErrorCode;

    public EBelgeArtifactIdempotencyConflictException(string mesaj)
        : base(mesaj, HttpStatusCode)
    {
    }
}
