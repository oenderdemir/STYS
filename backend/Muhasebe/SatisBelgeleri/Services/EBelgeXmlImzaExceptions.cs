using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.Muhasebe.SatisBelgeleri.Services;

/// <summary>
/// Faz 2B.7 imzalama hata kodu sabitleri - merkezî, type-safe tanım (bkz. görev md.2'nin
/// algoritma URI'leri için istediği desenin AYNISI, hata kodları için). Her sabit
/// <see cref="EBelgeXmlImzaKaliciHataException"/>/<see cref="EBelgeXmlImzaGeciciHataException"/>
/// ile birlikte kullanılır - kodun farklı yerlerine dağılmış ham string sabitleri YOKTUR.
/// </summary>
public static class EBelgeXmlImzaHataKodlari
{
    // ---- Kalıcı (bkz. görev md.21 "Kalıcı hatalar") ----
    public const string KaynakHashUyumsuz = "EBELGE_SIGNING_SOURCE_ARTIFACT_HASH_MISMATCH";
    public const string SonucHashUyumsuz = "EBELGE_SIGNING_RESULT_HASH_MISMATCH";
    public const string PrivateKeyYok = "EBELGE_SIGNING_CERTIFICATE_NO_PRIVATE_KEY";
    public const string SertifikaGecersizAralik = "EBELGE_SIGNING_CERTIFICATE_INVALID_PERIOD";
    public const string DesteklenmeyenAnahtarAlgoritmasi = "EBELGE_SIGNING_UNSUPPORTED_KEY_ALGORITHM";
    public const string SertifikaAnahtarEslesmiyor = "EBELGE_SIGNING_CERTIFICATE_KEY_MISMATCH";
    public const string AnahtarKullanimiUygunDegil = "EBELGE_SIGNING_CERTIFICATE_KEY_USAGE_INVALID";
    public const string SertifikaGuvenilirDegil = "EBELGE_SIGNING_CERTIFICATE_NOT_TRUSTED";
    public const string XmlDsigUretimHatasi = "EBELGE_SIGNING_XMLDSIG_GENERATION_FAILED";
    public const string ImzaDogrulamaHatasi = "EBELGE_SIGNING_SIGNATURE_VALIDATION_FAILED";
    public const string YinelenenXmlId = "EBELGE_SIGNING_DUPLICATE_XML_ID";
    public const string ReferansUriCozulemedi = "EBELGE_SIGNING_REFERENCE_URI_UNRESOLVED";
    public const string SignedPropertiesDigestUyumsuz = "EBELGE_SIGNING_SIGNEDPROPERTIES_DIGEST_MISMATCH";
    public const string UnsignedArtifactBulunamadi = "EBELGE_SIGNING_UNSIGNED_ARTIFACT_NOT_FOUND";
    public const string UnsignedArtifactSoftDeleted = "EBELGE_SIGNING_UNSIGNED_ARTIFACT_SOFT_DELETED";
    public const string SignedArtifactIdempotencyConflict = "EBELGE_SIGNED_ARTIFACT_IDEMPOTENCY_CONFLICT";

    // ---- Geçici (bkz. görev md.21 "Geçici hatalar") ----
    public const string SaglayiciGeciciErisilemiyor = "EBELGE_SIGNING_PROVIDER_TEMPORARILY_UNAVAILABLE";
}

/// <summary>Kalıcı (retry YAPILMAYAN) imzalama hatası - bkz. Faz 2B.7 görev md.21.</summary>
public sealed class EBelgeXmlImzaKaliciHataException : BaseException
{
    public const int HttpStatusCode = 422;

    public string HataKodu { get; }

    public EBelgeXmlImzaKaliciHataException(string hataKodu, string mesaj)
        : base(mesaj, HttpStatusCode)
    {
        HataKodu = hataKodu;
    }
}

/// <summary>Geçici (retry policy üzerinden yeniden denenebilir) imzalama hatası - bkz. Faz 2B.7 görev md.21.</summary>
public sealed class EBelgeXmlImzaGeciciHataException : BaseException
{
    public const int HttpStatusCode = 503;

    public string HataKodu { get; }

    public EBelgeXmlImzaGeciciHataException(string hataKodu, string mesaj)
        : base(mesaj, HttpStatusCode)
    {
        HataKodu = hataKodu;
    }
}

/// <summary>
/// Kalıcı, KONFİGÜRASYON sınıfı hata: kullanılmak istenen <see cref="EBelgeXadesProfili"/>
/// production imzalama için ONAYLANMAMIŞ (<c>Onayli = false</c>) - bkz. Faz 2B.7.1 görev md.2,
/// "profil kararının kesinliği yeterli değilse production signing kapalı kalmalı". Retry
/// ANLAMSIZDIR - profil onayı kod/veri değişmeden kendiliğinden değişmez
/// (<see cref="EBelgeSigningProviderNotConfiguredException"/> ile AYNI fail-closed tasarım
/// deseni, farklı KONU).
/// </summary>
public sealed class EBelgeXadesProfiliOnaylanmadiException : BaseException
{
    public const int HttpStatusCode = 500;
    public const string SafeErrorCode = "EBELGE_SIGNING_PROFILE_NOT_APPROVED";

    public string HataKodu { get; } = SafeErrorCode;

    public EBelgeXadesProfiliOnaylanmadiException(string profilKimligi)
        : base($"XAdES imza profili ('{profilKimligi}') production imzalama için onaylanmamış.", HttpStatusCode)
    {
    }
}
