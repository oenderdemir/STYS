using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using STYS.Muhasebe.SatisBelgeleri.Services;

namespace STYS.Tests;

/// <summary>
/// Faz 2B.7 görev md.6 - testlerde GERÇEK kriptografik imza için `CertificateRequest` ile
/// bellekte üretilen, self-signed bir test sertifikası sağlar. RSA en az 2048 bit, Digital
/// Signature key usage taşır, private key YALNIZ bellekte (PFX bytes bir private/readonly
/// alanda tutulur, DİSKE YAZILMAZ) bulunur, test çıktısında loglanmaz. Her <see cref="GetAsync"/>
/// çağrısı, PAYLAŞILAN PFX bytes'ından TAZE, BAĞIMSIZ bir <see cref="X509Certificate2"/> üretir -
/// böylece <see cref="EBelgeImzaKimligi.Dispose"/> (imza motorunun HER çağrıdan sonra çağırdığı)
/// diğer testleri/çağrıları ETKİLEMEZ.
///
/// ÖNEMLİ: Bu, self-signed bir test sertifikasıdır - "üretimde güvenilir bir sertifika zinciri"
/// ANLAMINA GELMEZ (bkz. görev md.6 son cümlesi). Üretim sertifika/güven sağlayıcıları AYRI,
/// KASITLI OLARAK fail-closed implementasyonlardır (bkz. EBelgeImzaKimligiYapilandirilmadiSaglayici,
/// EBelgeSertifikaGuvenValidatoruYapilandirilmadi).
/// </summary>
internal sealed class EBelgeTestSertifikaSaglayici : IEBelgeImzaKimligiSaglayici, IDisposable
{
    private const string TestSubject = "CN=STYS E-Belge Test Sertifikasi, O=STYS Test, C=TR";
    private const int RsaKeySizeBits = 2048;

    private readonly byte[] _pfxBytes;
    private readonly string _pfxPassword;
    private readonly string _sertifikaSha256ParmakIzi;
    private readonly DateTime _gecerlilikBaslangicUtc;
    private readonly DateTime _gecerlilikBitisUtc;
    private readonly string _anahtarKimligi;

    public EBelgeTestSertifikaSaglayici(DateTimeOffset? notBefore = null, DateTimeOffset? notAfter = null, string? anahtarKimligi = null)
    {
        _anahtarKimligi = anahtarKimligi ?? "test-anahtar-1";
        _pfxPassword = Guid.NewGuid().ToString("N");

        using var rsa = RSA.Create(RsaKeySizeBits);
        var request = new CertificateRequest(TestSubject, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, critical: true));

        var nb = notBefore ?? DateTimeOffset.UtcNow.AddDays(-1);
        var na = notAfter ?? DateTimeOffset.UtcNow.AddYears(1);

        using var selfSigned = request.CreateSelfSigned(nb, na);
        _pfxBytes = selfSigned.Export(X509ContentType.Pfx, _pfxPassword);
        _sertifikaSha256ParmakIzi = Convert.ToHexString(SHA256.HashData(selfSigned.RawData));
        _gecerlilikBaslangicUtc = selfSigned.NotBefore.ToUniversalTime();
        _gecerlilikBitisUtc = selfSigned.NotAfter.ToUniversalTime();
    }

    /// <summary>Bu sağlayıcının imzalamada kullandığı sertifikanın PUBLIC-ONLY (private key'siz) bir kopyasını döner - "başka sertifika" / "farklı public key" testleri için.</summary>
    public X509Certificate2 GetPublicOnlyCopy() => X509CertificateLoader.LoadCertificate(GetFreshCertificate().Export(X509ContentType.Cert));

    public Task<EBelgeImzaKimligi> GetAsync(int kurumId, CancellationToken cancellationToken)
    {
        var sertifika = GetFreshCertificate();
        return Task.FromResult(new EBelgeImzaKimligi
        {
            Sertifika = sertifika,
            AnahtarKimligi = _anahtarKimligi,
            SaglayiciTuru = EBelgeImzaSaglayiciTuru.Test,
            SertifikaSha256ParmakIzi = _sertifikaSha256ParmakIzi,
            GecerlilikBaslangicUtc = _gecerlilikBaslangicUtc,
            GecerlilikBitisUtc = _gecerlilikBitisUtc,
        });
    }

    private X509Certificate2 GetFreshCertificate() =>
        X509CertificateLoader.LoadPkcs12(_pfxBytes, _pfxPassword, X509KeyStorageFlags.Exportable);

    public void Dispose()
    {
        Array.Clear(_pfxBytes, 0, _pfxBytes.Length);
    }
}

/// <summary>Private key İÇERMEYEN bir sertifika sağlayan test double'ı - md.10/md.25 "private key içermeyen sertifika reddedilir" testi için.</summary>
internal sealed class EBelgePrivateKeySizSertifikaSaglayici : IEBelgeImzaKimligiSaglayici
{
    private readonly EBelgeTestSertifikaSaglayici _kaynak;

    public EBelgePrivateKeySizSertifikaSaglayici(EBelgeTestSertifikaSaglayici kaynak) => _kaynak = kaynak;

    public Task<EBelgeImzaKimligi> GetAsync(int kurumId, CancellationToken cancellationToken)
    {
        var publicOnly = _kaynak.GetPublicOnlyCopy();
        return Task.FromResult(new EBelgeImzaKimligi
        {
            Sertifika = publicOnly,
            AnahtarKimligi = "test-anahtar-public-only",
            SaglayiciTuru = EBelgeImzaSaglayiciTuru.Test,
            SertifikaSha256ParmakIzi = Convert.ToHexString(SHA256.HashData(publicOnly.RawData)),
            GecerlilikBaslangicUtc = publicOnly.NotBefore.ToUniversalTime(),
            GecerlilikBitisUtc = publicOnly.NotAfter.ToUniversalTime(),
        });
    }
}

/// <summary>Testte KASITLI OLARAK sertifikayı GÜVENİLİR kabul eden, dar kapsamlı bir test trust policy'si - üretim implementasyonu (EBelgeSertifikaGuvenValidatoruYapilandirilmadi) KASITLI OLARAK fail-closed kalır, bu SADECE testler içindir (bkz. Faz 2B.7 görev md.10).</summary>
internal sealed class EBelgeTestSertifikaGuvenPolicy : IEBelgeSertifikaGuvenValidatoru
{
    public Task<EBelgeSertifikaGuvenSonucu> DogrulaAsync(X509Certificate2 sertifika, CancellationToken cancellationToken)
        => Task.FromResult(EBelgeSertifikaGuvenSonucu.Guvenilir());
}
