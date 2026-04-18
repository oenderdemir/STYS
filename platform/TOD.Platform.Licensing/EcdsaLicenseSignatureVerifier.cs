using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TOD.Platform.Licensing.Abstractions;

namespace TOD.Platform.Licensing;

/// <summary>
/// ECDSA-SHA256 tabanli lisans imza dogrulayicisi.
///
/// Neden RSA yerine ECDSA:
/// - Ayni guvenlik seviyesinde cok daha kisa anahtar boyutu (256-bit ECDSA ~ 3072-bit RSA)
/// - Daha hizli dogrulama
/// - Daha kucuk imza boyutu (lisans dosyasi daha compact)
///
/// Public key, saldirganin degistirme maliyetini artirmak icin
/// parcalanmis ve runtime'da birlestirilmis sekilde tutulur.
///
/// Development override:
/// - <see cref="LicensingOptions.AllowPublicKeyOverride"/> = true ve
///   <see cref="LicensingOptions.PublicKeyOverride"/> dolu ise yapilandirilan key kullanilir.
/// - Bu override yalnizca Development/Staging icindir. Production'da
///   <c>AddTodLicensing</c> extension'i (EnsureProductionSafe) override'i engeller.
/// </summary>
public sealed class EcdsaLicenseSignatureVerifier : ILicenseSignatureVerifier
{
    // Public key parcalanmis olarak saklanir; tek bir string olarak binary'de aranmasini zorlastirir.
    // Gercek projede bu degerler build sirasinda inject edilebilir.
    internal static readonly string[] PublicKeyParts =
    [
        "MFkwEwYHKoZIzj0CAQYIKoZIzj0DAQ",
        "cDQgAE50G686NZJL/kDwoKaz2fnbM4",
        "KsU1my7VHyKubqN0/Ye5SDr6dcf5Au",
        "bvhQmCsuudOuqhCel9L/zlbHI237IM",
        "xQ=="
    ];

    private readonly Lazy<ECDsa> _publicKey;
    private readonly ILogger<EcdsaLicenseSignatureVerifier> _logger;

    public EcdsaLicenseSignatureVerifier(
        IOptions<LicensingOptions> options,
        ILogger<EcdsaLicenseSignatureVerifier> logger)
    {
        _logger = logger;
        var opt = options.Value;

        _publicKey = new Lazy<ECDsa>(() => LoadPublicKey(opt, _logger));
    }

    public bool Verify(LicenseDocument license)
    {
        try
        {
            var payload = LicensePayloadBuilder.BuildCanonicalPayload(license);
            var signatureBytes = Convert.FromBase64String(license.Signature);

            return _publicKey.Value.VerifyData(
                payload,
                signatureBytes,
                HashAlgorithmName.SHA256);
        }
        catch (Exception)
        {
            // Herhangi bir parse/crypto hatasi -> imza gecersiz
            return false;
        }
    }

    private static ECDsa LoadPublicKey(LicensingOptions options, ILogger logger)
    {
        byte[] keyBytes;

        if (options.AllowPublicKeyOverride && !string.IsNullOrWhiteSpace(options.PublicKeyOverride))
        {
            logger.LogWarning(
                "LICENSING: Public key override aktif. Bu mod yalnizca Development icindir. " +
                "Production'da bu ayarin aktif olmasi beklenmez.");
            keyBytes = Convert.FromBase64String(options.PublicKeyOverride);
        }
        else
        {
            // Parcalar runtime'da birlestirilir
            var fullKey = string.Concat(PublicKeyParts);
            keyBytes = Convert.FromBase64String(fullKey);
        }

        var ecdsa = ECDsa.Create();
        ecdsa.ImportSubjectPublicKeyInfo(keyBytes, out _);
        return ecdsa;
    }
}
