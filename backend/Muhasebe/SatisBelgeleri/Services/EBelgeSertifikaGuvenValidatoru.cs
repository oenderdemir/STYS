using System.Security.Cryptography.X509Certificates;

namespace STYS.Muhasebe.SatisBelgeleri.Services;

/// <summary>
/// Bir sertifikanın DERİN güven durumunu (zincir/OCSP/CRL/trust-store) değerlendirir - bu,
/// <see cref="EBelgeXmlImzalayici"/>'nin kendi yaptığı TEMEL sertifika kontrollerinden (mevcut/
/// süresi/anahtar kullanımı/anahtar eşleşmesi - bkz. görev md.10) AYRI, ONLARDAN SONRA çağrılan
/// bağımsız bir port'tur. Bu turda tam zincir/OCSP/CRL doğrulaması UYGULANMAMAKTADIR (bkz. görev
/// md.10 - "üretim sertifika chain/revocation doğrulaması bu turda tam uygulanamıyorsa sahte
/// başarılı doğrulama yazma") - üretim implementasyonu KASITLI OLARAK fail-closed'dır; testler
/// AÇIK, dar kapsamlı bir test policy kullanır (bkz. STYS.Tests, EBelgeTestSertifikaGuvenPolicy).
/// </summary>
public interface IEBelgeSertifikaGuvenValidatoru
{
    Task<EBelgeSertifikaGuvenSonucu> DogrulaAsync(X509Certificate2 sertifika, CancellationToken cancellationToken);
}

public sealed record EBelgeSertifikaGuvenSonucu
{
    public bool GuvenilirMi { get; }

    public string? RedNedeni { get; }

    private EBelgeSertifikaGuvenSonucu(bool guvenilirMi, string? redNedeni)
    {
        GuvenilirMi = guvenilirMi;
        RedNedeni = redNedeni;
    }

    public static EBelgeSertifikaGuvenSonucu Guvenilir() => new(true, null);

    public static EBelgeSertifikaGuvenSonucu Guvensiz(string redNedeni) => new(false, redNedeni);
}

/// <summary>
/// <see cref="IEBelgeSertifikaGuvenValidatoru"/>'nün ÜRETİM VARSAYILANI - tam zincir/OCSP/CRL/
/// trust-store doğrulaması bu turda UYGULANMADIĞINDAN, KASITLI OLARAK HER ZAMAN güvensiz döner
/// (bkz. görev md.10 - "production implementation fail-closed kalsın"). Bu, üretim imzalamanın
/// (zaten fail-closed olan <see cref="EBelgeImzaKimligiYapilandirilmadiSaglayici"/> ile birlikte)
/// gerçek bir OCSP/CRL/trust-store implementasyonu SONRAKİ bir fazda eklenene kadar HİÇBİR
/// zaman "gerçekten güvenilir" bir sonuca ulaşamayacağı anlamına gelir - bu KASITLIDIR, sahte
/// başarılı doğrulama yazmaktan daha güvenlidir.
/// </summary>
public sealed class EBelgeSertifikaGuvenValidatoruYapilandirilmadi : IEBelgeSertifikaGuvenValidatoru
{
    public const string SafeErrorCode = "EBELGE_SIGNING_TRUST_VALIDATOR_NOT_CONFIGURED";

    public Task<EBelgeSertifikaGuvenSonucu> DogrulaAsync(X509Certificate2 sertifika, CancellationToken cancellationToken)
        => Task.FromResult(EBelgeSertifikaGuvenSonucu.Guvensiz(SafeErrorCode));
}
