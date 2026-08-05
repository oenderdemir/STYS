using System.Security.Cryptography.X509Certificates;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.Muhasebe.SatisBelgeleri.Services;

/// <summary>Bir <see cref="IEBelgeImzaKimligiSaglayici"/> implementasyonunun ARKA PLANDA nereden kimlik sağladığını type-safe biçimde işaretler (bkz. Faz 2B.7 görev md.4 - "ileride HSM/PKCS11/CNG/uzak imzalama servisi/mali mühür cihazına uyarlanabilir").</summary>
public enum EBelgeImzaSaglayiciTuru
{
    /// <summary>Yalnız testlerde kullanılır - <see cref="System.Security.Cryptography.X509Certificates.CertificateRequest"/> ile bellekte üretilmiş, self-signed test sertifikası (bkz. görev md.6). ÜRETİMDE GÜVENİLİR bir sertifika zinciri ANLAMINA GELMEZ.</summary>
    Test = 1
}

/// <summary>
/// Bir kurumun imzalama kimliğini (sertifika + private key erişimi) taşır. Private key HİÇBİR
/// ZAMAN byte array olarak dışarı VERİLMEZ, loglanmaz, serialize edilmez, veritabanına
/// yazılmaz - yalnız <see cref="Sertifika"/> (mümkünse non-exportable private-key handle taşıyan
/// bir <see cref="X509Certificate2"/>) üzerinden erişilir (bkz. görev md.4, md.22).
/// </summary>
public sealed class EBelgeImzaKimligi : IDisposable
{
    public required X509Certificate2 Sertifika { get; init; }

    /// <summary>Sağlayıcıya özgü anahtar/sertifika kimliği (ör. HSM key alias, KMS key id) - private key'in KENDİSİ DEĞİL.</summary>
    public required string AnahtarKimligi { get; init; }

    public required EBelgeImzaSaglayiciTuru SaglayiciTuru { get; init; }

    public required string SertifikaSha256ParmakIzi { get; init; }

    public required DateTime GecerlilikBaslangicUtc { get; init; }

    public required DateTime GecerlilikBitisUtc { get; init; }

    public void Dispose() => Sertifika.Dispose();
}

/// <summary>
/// Kurum başına imzalama kimliğini (sertifika + private key erişimi) sağlayan port - imza motoru
/// sertifikayı DOĞRUDAN dosya sisteminden/environment variable'dan/Windows certificate store'dan/
/// repository içindeki bir dosyadan OKUMAZ, yalnız bu arayüz üzerinden erişir (bkz. Faz 2B.7
/// görev md.4). Üretim implementasyonu (<see cref="EBelgeImzaKimligiYapilandirilmadiSaglayici"/>)
/// bu turda KASITLI olarak fail-closed'dır (bkz. görev md.5) - gerçek bir HSM/mali mühür/PKCS11
/// sağlayıcısı bu portun ARKASINA SONRAKİ bir fazda eklenecektir.
/// </summary>
public interface IEBelgeImzaKimligiSaglayici
{
    Task<EBelgeImzaKimligi> GetAsync(int kurumId, CancellationToken cancellationToken);
}

/// <summary>
/// Kalıcı hata: üretim imzalama sertifika sağlayıcısı henüz yapılandırılmadı (bkz. Faz 2B.7
/// görev md.5 - "production için varsayılan implementation EBELGE_SIGNING_PROVIDER_NOT_CONFIGURED
/// hatasıyla fail-closed çalışmalı"). Retry ANLAMSIZDIR - gerçek bir sağlayıcı DI'a kayıt
/// edilmeden bu hata hiçbir zaman kendiliğinden düzelmez.
/// </summary>
public sealed class EBelgeSigningProviderNotConfiguredException : BaseException
{
    public const int HttpStatusCode = 500;
    public const string SafeErrorCode = "EBELGE_SIGNING_PROVIDER_NOT_CONFIGURED";
    public const string SafeMessage = "E-belge imzalama sertifika sağlayıcısı bu ortamda henüz yapılandırılmadı.";

    public string HataKodu { get; } = SafeErrorCode;

    public EBelgeSigningProviderNotConfiguredException()
        : base(SafeMessage, HttpStatusCode)
    {
    }
}

/// <summary>
/// <see cref="IEBelgeImzaKimligiSaglayici"/>'nin ÜRETİM VARSAYILANI - KASITLI OLARAK fail-closed
/// (bkz. görev md.5, md.27 - "sertifika yoksa unsigned XML'i signed kabul etme"). Otomatik
/// self-signed üretim sertifikası ÜRETMEZ, repository'den PFX OKUMAZ, development sertifikasını
/// KULLANMAZ - yalnız <see cref="EBelgeSigningProviderNotConfiguredException"/> fırlatır. Gerçek
/// bir HSM/mali mühür/PKCS11 sağlayıcısı bu turun KAPSAMI DIŞINDADIR (bkz. görev "bu turda
/// gerçek üretim sertifikası bağlama") ve SONRAKİ bir fazda bu implementasyonun YERİNE DI'a
/// kaydedilmelidir.
/// </summary>
public sealed class EBelgeImzaKimligiYapilandirilmadiSaglayici : IEBelgeImzaKimligiSaglayici
{
    public Task<EBelgeImzaKimligi> GetAsync(int kurumId, CancellationToken cancellationToken)
        => throw new EBelgeSigningProviderNotConfiguredException();
}
