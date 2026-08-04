using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.Muhasebe.SatisBelgeleri;

/// <summary>
/// Faz 2B.5 (deterministik, imzasız UBL renderer) hata sınıfları. Kalıcı (retriable olmayan) ve
/// düzeltilebilir (yeni denemeye açık) hatalar ayrıdır - bkz. görev sonuç raporu §17 (outbox
/// entegrasyonu). Kalıcı: Scope/SnapshotVersion/Xsd/Schematron/ManifestHashMismatch.
/// Düzeltilebilir: mevcut <see cref="EBelgeUblMonetaryTotalMismatchException"/> (zaten tanımlı).
/// </summary>
public sealed class EBelgeUblRenderScopeUnsupportedException : BaseException
{
    public const int HttpStatusCode = 422;
    public const string SafeErrorCode = "EBELGE_UBL_RENDER_SCOPE_UNSUPPORTED";

    public string HataKodu { get; } = SafeErrorCode;

    public EBelgeUblRenderScopeUnsupportedException(string mesaj)
        : base(mesaj, HttpStatusCode)
    {
    }
}

/// <summary>Snapshot V1 ise veya desteklenen V2 şema sürümü dışında bir SnapshotSchemaVersion taşıyorsa - outbox için kalıcı hata.</summary>
public sealed class EBelgeUblRenderSnapshotVersionUnsupportedException : BaseException
{
    public const int HttpStatusCode = 422;
    public const string SafeErrorCode = "EBELGE_UBL_RENDER_SNAPSHOT_VERSION_UNSUPPORTED";

    public string HataKodu { get; } = SafeErrorCode;

    public EBelgeUblRenderSnapshotVersionUnsupportedException(string mesaj)
        : base(mesaj, HttpStatusCode)
    {
    }
}

/// <summary>Kural seti manifest dosyalarından biri diskte yok veya SHA-256'sı manifest.json'daki kayıtlı değerle eşleşmiyor - kalıcı yapılandırma/artefakt hatası.</summary>
public sealed class EBelgeUblKuralSetiManifestException : BaseException
{
    public const int HttpStatusCode = 500;
    public const string SafeErrorCode = "EBELGE_UBL_KURALSETI_MANIFEST_INVALID";

    public string HataKodu { get; } = SafeErrorCode;

    public EBelgeUblKuralSetiManifestException(string mesaj)
        : base(mesaj, HttpStatusCode)
    {
    }
}

/// <summary>
/// Üretilen XML, sabitlenmiş GİB UBL-Invoice-2.1 XSD setine göre geçersiz. Güvenli/sınırlı mesaj -
/// dosya sistemi yolu, bağlantı dizesi veya ortam bilgisi İÇERMEZ (bkz. görev md.15).
/// </summary>
public sealed class EBelgeUblXsdValidationFailedException : BaseException
{
    public const int HttpStatusCode = 422;
    public const string SafeErrorCode = "EBELGE_UBL_XSD_VALIDATION_FAILED";

    public string HataKodu { get; } = SafeErrorCode;

    public IReadOnlyList<string> Hatalar { get; }

    public EBelgeUblXsdValidationFailedException(IReadOnlyList<string> hatalar)
        : base(BuildMesaj(hatalar), HttpStatusCode)
    {
        Hatalar = hatalar;
    }

    private static string BuildMesaj(IReadOnlyList<string> hatalar) =>
        $"UBL XML, GİB XSD kural setine göre geçersiz: {string.Join(" | ", hatalar)}";
}

/// <summary>
/// Üretilen XML, sabitlenmiş GİB UBL-TR schematron kural setine göre geçersiz. Çoklu ihlaller
/// belirleyici sırayla (schematron dosyası, sonra doküman sırası) raporlanır; liste sınırlıdır ve
/// tam XML veya müşteri verisi İÇERMEZ (bkz. görev md.16, md.19).
/// </summary>
public sealed class EBelgeUblSchematronValidationFailedException : BaseException
{
    public const int HttpStatusCode = 422;
    public const string SafeErrorCode = "EBELGE_UBL_SCHEMATRON_VALIDATION_FAILED";
    public const int MaxRaporlananIhlalSayisi = 20;

    public string HataKodu { get; } = SafeErrorCode;

    public IReadOnlyList<string> Ihlaller { get; }

    public EBelgeUblSchematronValidationFailedException(IReadOnlyList<string> ihlaller)
        : base(BuildMesaj(ihlaller), HttpStatusCode)
    {
        Ihlaller = ihlaller;
    }

    private static string BuildMesaj(IReadOnlyList<string> ihlaller) =>
        $"UBL XML, GİB UBL-TR schematron kural setine göre geçersiz ({ihlaller.Count} ihlal): {string.Join(" | ", ihlaller.Take(MaxRaporlananIhlalSayisi))}";
}

/// <summary>
/// Schematron sidecar'a erişilemiyor veya zaman aşımına uğradı - GEÇİCİ altyapı hatası (bkz.
/// görev md.8). Gerçek bir schematron ihlaliyle ASLA birleştirilmez; retry'a AÇIKTIR (renderer
/// düzeyinde otomatik retry YAPILMAZ, çağıran taraf karar verir).
/// </summary>
public sealed class EBelgeUblSchematronServiceUnavailableException : BaseException
{
    public const int HttpStatusCode = 503;
    public const string SafeErrorCode = "EBELGE_UBL_SCHEMATRON_SERVICE_UNAVAILABLE";

    public string HataKodu { get; } = SafeErrorCode;

    public EBelgeUblSchematronServiceUnavailableException(string mesaj)
        : base(mesaj, HttpStatusCode)
    {
    }
}

/// <summary>Kural seti sidecar'da bulunamıyor, hash uyuşmuyor veya stylesheet derlenemiyor - KALICI yapılandırma hatası.</summary>
public sealed class EBelgeUblRuleSetArtifactInvalidException : BaseException
{
    public const int HttpStatusCode = 500;
    public const string SafeErrorCode = "EBELGE_UBL_RULESET_ARTIFACT_INVALID";

    public string HataKodu { get; } = SafeErrorCode;

    public EBelgeUblRuleSetArtifactInvalidException(string mesaj)
        : base(mesaj, HttpStatusCode)
    {
    }
}

/// <summary>Sidecar'dan beklenmeyen/geçersiz bir yanıt geldi (protokol uyuşmazlığı) - GEÇİCİ, deployment uyumsuzluğu olabilir.</summary>
public sealed class EBelgeUblSchematronProtocolErrorException : BaseException
{
    public const int HttpStatusCode = 502;
    public const string SafeErrorCode = "EBELGE_UBL_SCHEMATRON_PROTOCOL_ERROR";

    public string HataKodu { get; } = SafeErrorCode;

    public EBelgeUblSchematronProtocolErrorException(string mesaj)
        : base(mesaj, HttpStatusCode)
    {
    }
}
