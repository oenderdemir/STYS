namespace STYS.Muhasebe.SatisBelgeleri;

/// <summary>
/// GİB UBL-TR e-Fatura için kullanılan XAdES/XMLDSig algoritma profilinin type-safe, MERKEZÎ
/// tanımı (bkz. Faz 2B.7 görev md.2 - "algoritma URI'lerini kodun farklı yerlerine dağılmış
/// string sabitleri olarak bırakma"). Kaynaklar ve gerekçe için
/// docs/e-belge-ubl-pdf-eposta-renderer-hazirlik-raporu.md, "Faz 2B.7 sonuç bölümü" içindeki
/// "Resmî GİB imza profilinin kanıtları" alt bölümüne bakınız - ÖZET:
///
/// - XAdES-BES + enveloped teknik: resmî GİB "e-Fatura Uygulaması (Entegrasyon Kılavuzu)"
///   (Haziran 2018, v1.10, s.17) - "Belgelerin imzalanmasında ve onaylanmasında en az XAdES-BES
///   standardı ve enveloped tekniği kullanılır."
/// - RSA-SHA1 YASAK: vendored, hash doğrulanmış EBelgeUblKuralSeti/schematron/UBL-TR_Common_Schematron.xml,
///   "SignatureMethodCheck" kuralı.
/// - Tek `ds:Reference[@URI='']`, en fazla 1 Transform, zorunlu `ds:KeyInfo/ds:X509Data/ds:X509Certificate`,
///   zorunlu `xades:SigningTime`+`xades:SigningCertificate` (v1.3.2, V2 DEĞİL): AYNI dosya,
///   "XadesSignatureCheckForInvoice"/"X509DataCheck" kuralları + `xades` namespace bağlaması
///   (`http://uri.etsi.org/01903/v1.3.2#`).
/// - SignatureAlgorithmUri/DigestAlgorithmUri (RSA-SHA256/SHA-256): DOĞRUDAN bir GİB metninde
///   URI olarak YAZILI DEĞİLDİR - SHA-1 yasağından VE TÜBİTAK KamuSM'nin (GİB kılavuzunun
///   kendisinin, mali mühür sertifikaları bağlamında BY NAME atıfta bulunduğu resmî e-imza altyapısı
///   sağlayıcısı) kendi ESYA imzalama SDK dokümantasyonunun (yazilim.kamusm.gov.tr/esya-api)
///   "DigestMethod.SHA_256" kullanımından ÇIKARILMIŞTIR - bu, raporda AÇIKÇA "daha düşük
///   güvenle, ancak gerekçeli" bir karar olarak işaretlenmiştir.
/// - CanonicalizationAlgorithmUri (düz/inclusive C14N 1.0): hiçbir vendored/resmî kaynakta
///   POZİTİF olarak teyit EDİLMEMİŞTİR - W3C XMLDSig Core REC'in TEMEL/varsayılan
///   canonicalization algoritması olması VE .NET'in `SignedXml` sınıfının kendi VARSAYILANI
///   olması nedeniyle seçilmiştir; raporda AYRICA en düşük kanıt güvenilirliğine sahip karar
///   olarak işaretlenmiştir.
/// </summary>
public sealed record EBelgeXadesProfili
{
    public required string ProfilKimligi { get; init; }

    public required string XadesNamespaceUri { get; init; }

    public required string CanonicalizationAlgorithmUri { get; init; }

    public required string SignatureAlgorithmUri { get; init; }

    public required string DigestAlgorithmUri { get; init; }

    public required string EnvelopedSignatureTransformUri { get; init; }

    public required string SignedPropertiesTypeUri { get; init; }

    /// <summary>
    /// GİB UBL-TR e-Fatura için kullanılan, sabitlenmiş XAdES-BES profili (bkz. sınıf düzeyi
    /// XML doc'u - kaynaklar ve gerekçe için). Testler HARİCİNDE tek profil budur; test-only
    /// zayıf/eski algoritmalı bir "profil" ASLA üretim kod yolunda kullanılmaz.
    /// </summary>
    public static readonly EBelgeXadesProfili GibUblTr = new()
    {
        ProfilKimligi = "GIB-UBL-TR-XADES-BES/1.0",
        XadesNamespaceUri = "http://uri.etsi.org/01903/v1.3.2#",
        CanonicalizationAlgorithmUri = "http://www.w3.org/TR/2001/REC-xml-c14n-20010315",
        SignatureAlgorithmUri = "http://www.w3.org/2001/04/xmldsig-more#rsa-sha256",
        DigestAlgorithmUri = "http://www.w3.org/2001/04/xmlenc#sha256",
        EnvelopedSignatureTransformUri = "http://www.w3.org/2000/09/xmldsig#enveloped-signature",
        SignedPropertiesTypeUri = "http://uri.etsi.org/01903#SignedProperties",
    };

    /// <summary>Resmî GİB kural setinin (bkz. UBL-TR_Common_Schematron.xml, "SignatureMethodCheck") KESİNLİKLE yasakladığı tek algoritma URI'si - hiçbir profil bunu ASLA kullanmamalıdır.</summary>
    public const string YasakliRsaSha1Uri = "http://www.w3.org/2000/09/xmldsig#rsa-sha1";
}
