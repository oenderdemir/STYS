using System.Security.Cryptography;
using TOD.Platform.Licensing.Abstractions;

namespace TOD.Platform.Licensing;

/// <summary>
/// ECDSA-SHA256 tabanlı lisans imza doğrulayıcısı.
///
/// Neden RSA yerine ECDSA:
/// - Aynı güvenlik seviyesinde çok daha kısa anahtar boyutu (256-bit ECDSA ≈ 3072-bit RSA)
/// - Daha hızlı doğrulama
/// - Daha küçük imza boyutu (lisans dosyası daha compact)
/// - .NET'in yerleşik ECDsa desteği yeterli ve stabil
///
/// Public key, saldırganın değiştirme maliyetini artırmak için
/// parçalanmış ve runtime'da birleştirilmiş şekilde tutulur.
/// </summary>
public sealed class EcdsaLicenseSignatureVerifier : ILicenseSignatureVerifier
{
    // Public key parçalanmış olarak saklanır.
    // Tek bir string olarak binary'de aranmasını zorlaştırır.
    // Gerçek projede bu değerler build sırasında inject edilebilir.
    private static readonly string[] PublicKeyParts =
    [
        "MFkwEwYHKoZIzj0CAQYIKoZIzj0DAQ",
        "cDQgAE50G686NZJL/kDwoKaz2fnbM4",
        "KsU1my7VHyKubqN0/Ye5SDr6dcf5Au",
        "bvhQmCsuudOuqhCel9L/zlbHI237IM",
        "xQ=="
    ];

    private readonly Lazy<ECDsa> _publicKey;

    public EcdsaLicenseSignatureVerifier()
    {
        _publicKey = new Lazy<ECDsa>(LoadPublicKey);
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
            // Herhangi bir parse/crypto hatası → imza geçersiz
            return false;
        }
    }

    private static ECDsa LoadPublicKey()
    {
        // Parçalar runtime'da birleştirilir
        var fullKey = string.Join("", PublicKeyParts);
        var keyBytes = Convert.FromBase64String(fullKey);

        var ecdsa = ECDsa.Create();
        ecdsa.ImportSubjectPublicKeyInfo(keyBytes, out _);
        return ecdsa;
    }
}
