using System.Collections.Immutable;
using System.Globalization;
using System.Numerics;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography.Xml;
using System.Xml;

namespace STYS.Muhasebe.SatisBelgeleri.Services;

public sealed record EBelgeXmlImzaDogrulamaSonucu
{
    public bool GecerliMi { get; }

    public string? HataKodu { get; }

    public string? HataMesaji { get; }

    public string? SertifikaSha256ParmakIzi { get; }

    public DateTime? SigningTimeUtc { get; }

    private EBelgeXmlImzaDogrulamaSonucu(bool gecerliMi, string? hataKodu, string? hataMesaji, string? sertifikaSha256ParmakIzi, DateTime? signingTimeUtc)
    {
        GecerliMi = gecerliMi;
        HataKodu = hataKodu;
        HataMesaji = hataMesaji;
        SertifikaSha256ParmakIzi = sertifikaSha256ParmakIzi;
        SigningTimeUtc = signingTimeUtc;
    }

    public static EBelgeXmlImzaDogrulamaSonucu Gecerli(string sertifikaSha256ParmakIzi, DateTime signingTimeUtc) =>
        new(true, null, null, sertifikaSha256ParmakIzi, signingTimeUtc);

    public static EBelgeXmlImzaDogrulamaSonucu Gecersiz(string hataKodu, string hataMesaji) =>
        new(false, hataKodu, hataMesaji, null, null);
}

public interface IEBelgeXmlImzaDogrulayici
{
    Task<EBelgeXmlImzaDogrulamaSonucu> DogrulaAsync(ImmutableArray<byte> signedXmlUtf8, CancellationToken cancellationToken);
}

/// <summary>
/// İmza motorundan (<see cref="EBelgeXmlImzalayici"/>) BAĞIMSIZ bir doğrulayıcı (bkz. Faz 2B.7
/// görev md.11, Faz 2B.7.1 görev md.4) - AYRI bir XML parse, AYRI reference/node çözümlemesi ve
/// AYRI hash hesaplaması kullanır; imzayı üreten kodla AYNI yardımcı metotları PAYLAŞMAZ. Yalnız
/// <see cref="SignedXml.CheckSignature(X509Certificate2, bool)"/> sonucuna GÜVENİLMEZ (bkz. görev
/// md.11, md.27) - bu, aşağıdaki BAĞIMSIZ katmanlardan yalnız BİRİDİR:
///
/// 1. Sertleştirilmiş, bağımsız bir XmlReader ile TAZE bir parse (DTD/external entity KAPALI).
/// 2. Yapısal kontroller: tek ds:Signature (yalnız beklenen ext:ExtensionContent altında), tek
///    xades:QualifyingProperties, tek xades:SignedProperties, QualifyingProperties/@Target'ın
///    GERÇEK ds:Signature/@Id'ye eşitliği, yinelenen "Id" niteliği YOK, beklenen referans
///    sayısı/tipleri/URI'leri/transform sayısı-URI'si/digest algoritması whitelist'i.
/// 3. Tüm belge referansı (URI="") için BAĞIMSIZ, elle yeniden hesaplanmış digest (ds:Signature
///    KALDIRILIP C14N uygulanarak - SignedXml/CheckSignature'a HİÇ İHTİYAÇ DUYULMADAN).
/// 4. xades:SigningCertificate/CertDigest, xades:IssuerSerial VE xades:SignerRole/ds:KeyValue
///    için BAĞIMSIZ, elle yeniden hesaplanmış sertifika hash'i/issuer-serial/public-key (gömülü
///    ds:X509Certificate bytes'ından - imzalayan tarafın kendi nesnesinden DEĞİL).
/// 5. SignedInfo üzerindeki RSA imzasının, gömülü sertifikanın public key'i ile BAĞIMSIZ
///    yeniden doğrulanması (elle C14N + RSA.VerifyData).
/// 6. cac:Signature/cbc:ID (VKN) ile AccountingSupplierParty VKN'si arasındaki bağın VE
///    cac:Signature/cac:DigitalSignatureAttachment/cac:ExternalReference/cbc:URI'nin GERÇEK
///    ds:Signature/@Id'ye işaret ettiğinin bağımsız doğrulanması.
/// 7. EK bir katman olarak .NET'in kendi SignedXml.CheckSignature()'ı - TEK BAŞINA YETERLİ
///    SAYILMAZ, yalnız YUKARIDAKİ bağımsız kontrolleri TAMAMLAR.
/// </summary>
public sealed class EBelgeXmlImzaDogrulayici : IEBelgeXmlImzaDogrulayici
{
    private const string NsDs = "http://www.w3.org/2000/09/xmldsig#";
    private const string NsXades = "http://uri.etsi.org/01903/v1.3.2#";
    private const string NsExt = "urn:oasis:names:specification:ubl:schema:xsd:CommonExtensionComponents-2";
    private const string NsCac = "urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2";
    private const string NsCbc = "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2";

    private static readonly EBelgeXadesProfili Profil = EBelgeXadesProfili.GibUblTr;

    public Task<EBelgeXmlImzaDogrulamaSonucu> DogrulaAsync(ImmutableArray<byte> signedXmlUtf8, CancellationToken cancellationToken)
    {
        try
        {
            return Task.FromResult(DogrulaCore(signedXmlUtf8));
        }
        catch (EBelgeXmlImzaDogrulamaException ex)
        {
            return Task.FromResult(EBelgeXmlImzaDogrulamaSonucu.Gecersiz(ex.HataKodu, ex.Message));
        }
        catch (Exception ex) when (ex is XmlException or FormatException or CryptographicException or ArgumentException or OverflowException or InvalidOperationException)
        {
            // Faz 2B.7.2 görev md.1: bozuk/kurcalanmış bir imza belgesi (İYİ BİÇİMLİ olmayan XML,
            // geçersiz base64 sertifika/SignatureValue/DigestValue, geçersiz X509 sertifika
            // bytes'ı, hatalı canonicalization girdisi, SignedXml.LoadXml/CheckSignature kaynaklı
            // kriptografik hatalar vb.) BEKLENEN, KALICI bir doğrulama BAŞARISIZLIĞIDIR -
            // programlama hatası DEĞİLDİR. Genel `catch (Exception)` KULLANILMAZ (bkz. görev md.9)
            // - yalnız BU AÇIKÇA SINIFLANDIRILMIŞ, parse/kriptografi katmanından BEKLENEN exception
            // tipleri yakalanır (`OperationCanceledException` bu kümenin DIŞINDADIR - GERÇEK bir
            // iptal isteği varsa normal şekilde dışarı aktarılır). Mesaj KASITLI OLARAK sabittir -
            // hiçbir kişisel veri/XML/sertifika içeriği/SignatureValue/tam digest değeri İÇERMEZ
            // (bkz. görev md.1, md.22).
            return Task.FromResult(EBelgeXmlImzaDogrulamaSonucu.Gecersiz(
                EBelgeXmlImzaHataKodlari.BozukImzaBelgesi,
                "İmzalı belge, ayrıştırma veya kriptografik doğrulama sırasında geçersiz/bozuk bulundu."));
        }
    }

    private static EBelgeXmlImzaDogrulamaSonucu DogrulaCore(ImmutableArray<byte> signedXmlUtf8)
    {
        var doc = LoadDocumentSecurely(signedXmlUtf8);
        var nsmgr = CreateNamespaceManager(doc);

        // ---- 2. Yapısal kontroller (signature-wrapping sertleştirmesi, bkz. görev md.8, Faz 2B.7.1 md.4) ----
        var signatureNodes = doc.SelectNodes("//ds:Signature", nsmgr)
            ?? throw new EBelgeXmlImzaDogrulamaException(EBelgeXmlImzaHataKodlari.ImzaDogrulamaHatasi, "ds:Signature aranırken sorgu başarısız oldu.");

        if (signatureNodes.Count != 1)
        {
            throw new EBelgeXmlImzaDogrulamaException(EBelgeXmlImzaHataKodlari.ImzaDogrulamaHatasi, $"Tam olarak 1 ds:Signature beklenirken {signatureNodes.Count} bulundu.");
        }

        var signatureElement = (XmlElement)signatureNodes[0]!;

        // ds:Signature, YALNIZ beklenen ext:UBLExtensions/ext:UBLExtension/ext:ExtensionContent
        // altında bulunmalıdır - başka bir konumdaki (ör. doğrudan kök altına eklenmiş) bir
        // ds:Signature KABUL EDİLMEZ (bkz. Faz 2B.7.1 görev md.4, senaryo 13-14).
        var beklenenKonumdakiSignature = doc.SelectNodes("/*/ext:UBLExtensions/ext:UBLExtension/ext:ExtensionContent/ds:Signature", nsmgr)!;
        if (beklenenKonumdakiSignature.Count != 1 || !ReferenceEquals(beklenenKonumdakiSignature[0], signatureElement))
        {
            throw new EBelgeXmlImzaDogrulamaException(EBelgeXmlImzaHataKodlari.ImzaDogrulamaHatasi, "ds:Signature, yalnız ext:UBLExtensions/ext:UBLExtension/ext:ExtensionContent altında bulunmalıdır.");
        }

        var idTasiyanlar = doc.SelectNodes("//*[@Id]", nsmgr)!;
        var idDegerleri = idTasiyanlar.Cast<XmlElement>().Select(e => e.GetAttribute("Id")).ToList();
        if (idDegerleri.Distinct(StringComparer.Ordinal).Count() != idDegerleri.Count)
        {
            throw new EBelgeXmlImzaDogrulamaException(EBelgeXmlImzaHataKodlari.YinelenenXmlId, "Belge içinde yinelenen 'Id' niteliği bulundu.");
        }

        // Tam olarak 1 xades:QualifyingProperties VE 1 xades:SignedProperties (GLOBAL - yalnız
        // referansla hedeflenen elemanın TEKLİĞİ değil, belgedeki TOPLAM sayı) - bkz. Faz 2B.7.1
        // görev md.4, senaryo 1-2.
        var qualifyingPropertiesNodes = doc.SelectNodes("//xades:QualifyingProperties", nsmgr)!;
        if (qualifyingPropertiesNodes.Count != 1)
        {
            throw new EBelgeXmlImzaDogrulamaException(EBelgeXmlImzaHataKodlari.ImzaDogrulamaHatasi, $"Tam olarak 1 xades:QualifyingProperties beklenirken {qualifyingPropertiesNodes.Count} bulundu.");
        }

        var qualifyingPropertiesElement = (XmlElement)qualifyingPropertiesNodes[0]!;

        var signedPropertiesNodesGlobal = doc.SelectNodes("//xades:SignedProperties", nsmgr)!;
        if (signedPropertiesNodesGlobal.Count != 1)
        {
            throw new EBelgeXmlImzaDogrulamaException(EBelgeXmlImzaHataKodlari.ImzaDogrulamaHatasi, $"Tam olarak 1 xades:SignedProperties beklenirken {signedPropertiesNodesGlobal.Count} bulundu.");
        }

        // xades:QualifyingProperties/@Target, GERÇEK ds:Signature/@Id'ye eşit olmalıdır - bkz.
        // Faz 2B.7.1 görev md.4, senaryo 1.
        var gercekSignatureId = signatureElement.GetAttribute("Id");
        var qualifyingTarget = qualifyingPropertiesElement.GetAttribute("Target");
        if (string.IsNullOrEmpty(gercekSignatureId) || !string.Equals(qualifyingTarget, "#" + gercekSignatureId, StringComparison.Ordinal))
        {
            throw new EBelgeXmlImzaDogrulamaException(EBelgeXmlImzaHataKodlari.ImzaDogrulamaHatasi, "xades:QualifyingProperties/@Target, gerçek ds:Signature/@Id ile eşleşmiyor.");
        }

        var referenceNodes = signatureElement.SelectNodes("ds:SignedInfo/ds:Reference", nsmgr)!;
        if (referenceNodes.Count != 2)
        {
            throw new EBelgeXmlImzaDogrulamaException(EBelgeXmlImzaHataKodlari.ReferansUriCozulemedi, $"Tam olarak 2 ds:Reference (belge + SignedProperties) beklenirken {referenceNodes.Count} bulundu.");
        }

        // Her ds:Reference için: digest algoritması whitelist'le TAM eşleşir; transform sayısı
        // TAM OLARAK 1'dir - fazladan/bilinmeyen transform REDDEDİLİR (bkz. Faz 2B.7.1 görev
        // md.4, senaryo 6-8).
        foreach (XmlElement referans in referenceNodes)
        {
            var digestAlg = referans.SelectSingleNode("ds:DigestMethod/@Algorithm", nsmgr)?.Value;
            if (!string.Equals(digestAlg, Profil.DigestAlgorithmUri, StringComparison.Ordinal))
            {
                throw new EBelgeXmlImzaDogrulamaException(EBelgeXmlImzaHataKodlari.ReferansUriCozulemedi, "Bir ds:Reference'ın DigestMethod algoritması izin verilen profille eşleşmiyor.");
            }

            var transformNodes = referans.SelectNodes("ds:Transforms/ds:Transform", nsmgr)!;
            if (transformNodes.Count != 1)
            {
                throw new EBelgeXmlImzaDogrulamaException(EBelgeXmlImzaHataKodlari.ReferansUriCozulemedi, $"Bir ds:Reference için tam olarak 1 ds:Transform beklenirken {transformNodes.Count} bulundu.");
            }
        }

        // ---- Algoritma/profil whitelist ----
        var sigMethod = signatureElement.SelectSingleNode("ds:SignedInfo/ds:SignatureMethod/@Algorithm", nsmgr)?.Value;
        var c14nMethod = signatureElement.SelectSingleNode("ds:SignedInfo/ds:CanonicalizationMethod/@Algorithm", nsmgr)?.Value;
        if (!string.Equals(sigMethod, Profil.SignatureAlgorithmUri, StringComparison.Ordinal) ||
            !string.Equals(c14nMethod, Profil.CanonicalizationAlgorithmUri, StringComparison.Ordinal))
        {
            throw new EBelgeXmlImzaDogrulamaException(EBelgeXmlImzaHataKodlari.ImzaDogrulamaHatasi, "SignatureMethod/CanonicalizationMethod izin verilen profille eşleşmiyor.");
        }

        // ---- 3. Belge referansı (URI="") - BAĞIMSIZ, elle yeniden hesaplanmış digest ----
        var belgeReferansi = referenceNodes.Cast<XmlElement>().SingleOrDefault(r => r.GetAttribute("URI") == string.Empty)
            ?? throw new EBelgeXmlImzaDogrulamaException(EBelgeXmlImzaHataKodlari.ReferansUriCozulemedi, "URI=\"\" (tüm belge) referansı bulunamadı.");

        var belgeReferansiTransformUri = belgeReferansi.SelectSingleNode("ds:Transforms/ds:Transform/@Algorithm", nsmgr)?.Value;
        if (!string.Equals(belgeReferansiTransformUri, Profil.EnvelopedSignatureTransformUri, StringComparison.Ordinal))
        {
            throw new EBelgeXmlImzaDogrulamaException(EBelgeXmlImzaHataKodlari.ReferansUriCozulemedi, "Belge referansının transform algoritması izin verilen profille eşleşmiyor.");
        }

        var belgeDigestBeyan = belgeReferansi.SelectSingleNode("ds:DigestValue", nsmgr)?.InnerText
            ?? throw new EBelgeXmlImzaDogrulamaException(EBelgeXmlImzaHataKodlari.ReferansUriCozulemedi, "Belge referansının DigestValue'su bulunamadı.");

        var belgeDigestGercek = Convert.ToBase64String(ComputeEnvelopedDocumentDigest(signedXmlUtf8));
        if (!string.Equals(belgeDigestBeyan, belgeDigestGercek, StringComparison.Ordinal))
        {
            throw new EBelgeXmlImzaDogrulamaException(EBelgeXmlImzaHataKodlari.ImzaDogrulamaHatasi, "Belge referansının digest'i bağımsız hesaplamayla eşleşmiyor - belge iş verileri değiştirilmiş olabilir.");
        }

        // ---- SignedProperties referansı çözümü + Type/transform kontrolü ----
        var signedPropsReferansi = referenceNodes.Cast<XmlElement>().SingleOrDefault(r => r.GetAttribute("URI").StartsWith('#'))
            ?? throw new EBelgeXmlImzaDogrulamaException(EBelgeXmlImzaHataKodlari.ReferansUriCozulemedi, "SignedProperties referansı (# ile başlayan URI) bulunamadı.");

        if (!string.Equals(signedPropsReferansi.GetAttribute("Type"), Profil.SignedPropertiesTypeUri, StringComparison.Ordinal))
        {
            throw new EBelgeXmlImzaDogrulamaException(EBelgeXmlImzaHataKodlari.ReferansUriCozulemedi, "SignedProperties referansının Type niteliği beklenen XAdES URI'siyle eşleşmiyor.");
        }

        var signedPropsReferansiTransformUri = signedPropsReferansi.SelectSingleNode("ds:Transforms/ds:Transform/@Algorithm", nsmgr)?.Value;
        if (!string.Equals(signedPropsReferansiTransformUri, Profil.SignedPropertiesTransformUri, StringComparison.Ordinal))
        {
            throw new EBelgeXmlImzaDogrulamaException(EBelgeXmlImzaHataKodlari.ReferansUriCozulemedi, "SignedProperties referansının transform algoritması izin verilen profille eşleşmiyor.");
        }

        var signedPropsId = signedPropsReferansi.GetAttribute("URI").TrimStart('#');

        // Güvenli, XPath-enjeksiyonundan ARINDIRILMIŞ ID çözümlemesi (bkz. Faz 2B.7.1 görev
        // md.4, "XPath sorgularında kullanıcı girdisi birleştirme, ID değeri için güvenli node
        // taraması kullan") - signedPropsId, İMZALI (potansiyel olarak KURCALANMIŞ) belgeden
        // okunan bir öznitelik değeridir; ham XPath string birleştirmesi (`$"//*[@Id='{...}']`)
        // İLE sorguya DOĞRUDAN GÖMÜLMEZ - tüm "Id" taşıyan elemanlar (`idTasiyanlar`, YUKARIDA
        // ZATEN toplanmış) üzerinde SADE bir C# karşılaştırmasıyla taranır.
        var signedPropsElemanlari = idTasiyanlar.Cast<XmlElement>().Where(e => string.Equals(e.GetAttribute("Id"), signedPropsId, StringComparison.Ordinal)).ToList();
        if (signedPropsElemanlari.Count != 1)
        {
            throw new EBelgeXmlImzaDogrulamaException(EBelgeXmlImzaHataKodlari.ReferansUriCozulemedi, $"SignedProperties referansı (#{signedPropsId}) belgede TAM OLARAK bir kez bulunmuyor.");
        }

        var signedPropertiesElement = signedPropsElemanlari[0];

        // signedPropsId'nin çözdüğü eleman, GERÇEKTEN xades:SignedProperties olmalıdır (yukarıda
        // sayılan TEK global xades:SignedProperties ile AYNI nesne) - farklı bir Id taşıyan
        // rastgele bir elemana yönlendirme (signature-wrapping türevi) REDDEDİLİR.
        if (!ReferenceEquals(signedPropertiesElement, signedPropertiesNodesGlobal[0]))
        {
            throw new EBelgeXmlImzaDogrulamaException(EBelgeXmlImzaHataKodlari.ReferansUriCozulemedi, "SignedProperties referansı, gerçek xades:SignedProperties elemanına işaret etmiyor.");
        }

        var signedPropsDigestBeyan = signedPropsReferansi.SelectSingleNode("ds:DigestValue", nsmgr)?.InnerText
            ?? throw new EBelgeXmlImzaDogrulamaException(EBelgeXmlImzaHataKodlari.SignedPropertiesDigestUyumsuz, "SignedProperties referansının DigestValue'su bulunamadı.");

        // xades:SignedProperties, imzalama anında `SignedXml.AddObject` ile eklenmiş, belgeye HENÜZ
        // YERLEŞTİRİLMEMİŞ (kopuk) bir alt-ağaç olarak canonicalize edilmiştir - kapsayıcı (inclusive)
        // C14N kullanılsaydı, imzalama anındaki (kopuk) ad alanı bağlamı ile burada (TAM belge
        // bağlamıyla) yeniden ayrıştırma sonrası ad alanı bağlamı ASLA eşleşemezdi. Bu yüzden bu
        // referansın transformu - yalnız BU referans için - Exclusive C14N'dir (bkz. imzalayıcıdaki
        // eşdeğer açıklama, `EBelgeXmlImzalayici.ImzalaXml`).
        var signedPropsDigestGercek = Convert.ToBase64String(SHA256.HashData(CanonicalizeSubtreeExclusive(signedPropertiesElement)));
        if (!string.Equals(signedPropsDigestBeyan, signedPropsDigestGercek, StringComparison.Ordinal))
        {
            throw new EBelgeXmlImzaDogrulamaException(EBelgeXmlImzaHataKodlari.SignedPropertiesDigestUyumsuz, "SignedProperties referansının digest'i bağımsız hesaplamayla eşleşmiyor.");
        }

        // ---- 4. Gömülü sertifika + CertDigest/IssuerSerial bağımsız doğrulaması ----
        var certBase64 = signatureElement.SelectSingleNode("ds:KeyInfo/ds:X509Data/ds:X509Certificate", nsmgr)?.InnerText
            ?? throw new EBelgeXmlImzaDogrulamaException(EBelgeXmlImzaHataKodlari.ImzaDogrulamaHatasi, "ds:KeyInfo/ds:X509Data/ds:X509Certificate bulunamadı.");

        using var cert = X509CertificateLoader.LoadCertificate(Convert.FromBase64String(certBase64));

        var certDigestNode = signedPropertiesElement.SelectSingleNode(
            "xades:SignedSignatureProperties/xades:SigningCertificate/xades:Cert/xades:CertDigest/ds:DigestValue", nsmgr)
            ?? throw new EBelgeXmlImzaDogrulamaException(EBelgeXmlImzaHataKodlari.ImzaDogrulamaHatasi, "xades:CertDigest bulunamadı.");

        var certDigestBeyan = certDigestNode.InnerText;
        var certDigestGercek = Convert.ToBase64String(SHA256.HashData(cert.RawData));
        if (!string.Equals(certDigestBeyan, certDigestGercek, StringComparison.Ordinal))
        {
            throw new EBelgeXmlImzaDogrulamaException(EBelgeXmlImzaHataKodlari.ImzaDogrulamaHatasi, "xades:CertDigest, gömülü sertifikanın gerçek hash'i ile eşleşmiyor.");
        }

        // xades:IssuerSerial, gömülü sertifikanın GERÇEK issuer/serial değerleriyle BAĞIMSIZ
        // karşılaştırılır (bkz. Faz 2B.7.1 görev md.4, senaryo 9-10) - imzalayanın kendi beyanına
        // GÜVENİLMEZ, sertifikanın KENDİSİNDEN (X509IssuerName/GetSerialNumber) yeniden hesaplanır.
        var issuerNameNode = signedPropertiesElement.SelectSingleNode(
            "xades:SignedSignatureProperties/xades:SigningCertificate/xades:Cert/xades:IssuerSerial/ds:X509IssuerName", nsmgr)
            ?? throw new EBelgeXmlImzaDogrulamaException(EBelgeXmlImzaHataKodlari.ImzaDogrulamaHatasi, "xades:IssuerSerial/ds:X509IssuerName bulunamadı.");

        var serialNode = signedPropertiesElement.SelectSingleNode(
            "xades:SignedSignatureProperties/xades:SigningCertificate/xades:Cert/xades:IssuerSerial/ds:X509SerialNumber", nsmgr)
            ?? throw new EBelgeXmlImzaDogrulamaException(EBelgeXmlImzaHataKodlari.ImzaDogrulamaHatasi, "xades:IssuerSerial/ds:X509SerialNumber bulunamadı.");

        if (!string.Equals(issuerNameNode.InnerText, cert.IssuerName.Name, StringComparison.Ordinal))
        {
            throw new EBelgeXmlImzaDogrulamaException(EBelgeXmlImzaHataKodlari.ImzaDogrulamaHatasi, "xades:IssuerSerial/ds:X509IssuerName, gömülü sertifikanın gerçek issuer adıyla eşleşmiyor.");
        }

        if (!string.Equals(serialNode.InnerText, GetSerialNumberDecimalString(cert), StringComparison.Ordinal))
        {
            throw new EBelgeXmlImzaDogrulamaException(EBelgeXmlImzaHataKodlari.ImzaDogrulamaHatasi, "xades:IssuerSerial/ds:X509SerialNumber, gömülü sertifikanın gerçek seri numarasıyla eşleşmiyor.");
        }

        // xades:SignerRole/xades:ClaimedRoles/xades:ClaimedRole (bkz. Faz 2B.7.2 raporu - TÜBİTAK
        // KamuSM ESYA SDK dokümantasyonu, "e-fatura standartlarında GEREKLİ KILINAN imzacı rolü,
        // açık anahtar ve imza zamanı eklenir" ifadesi) - GİB'in KENDİSİ tarafından yayımlanan
        // resmî bir metinle DOĞRUDAN teyit EDİLMEMİŞ olsa da, aksini gösteren hiçbir kanıt
        // BULUNAMADIĞINDAN uyumluluk açısından GÜVENLİ taraf olarak zorunlu kılınır.
        var claimedRoleNode = signedPropertiesElement.SelectSingleNode(
            "xades:SignedSignatureProperties/xades:SignerRole/xades:ClaimedRoles/xades:ClaimedRole", nsmgr)
            ?? throw new EBelgeXmlImzaDogrulamaException(EBelgeXmlImzaHataKodlari.ImzaDogrulamaHatasi, "xades:SignerRole/xades:ClaimedRoles/xades:ClaimedRole bulunamadı.");

        if (!string.Equals(claimedRoleNode.InnerText, EBelgeXmlImzalayici.SignerClaimedRole, StringComparison.Ordinal))
        {
            throw new EBelgeXmlImzaDogrulamaException(EBelgeXmlImzaHataKodlari.ImzaDogrulamaHatasi, "xades:ClaimedRole, beklenen değerle eşleşmiyor.");
        }

        // ds:KeyInfo/ds:KeyValue/ds:RSAKeyValue - gömülü X509 sertifikanın GERÇEK public key'iyle
        // (sertifikanın KENDİSİNDEN BAĞIMSIZ olarak yeniden alınarak) karşılaştırılır.
        var modulusBase64 = signatureElement.SelectSingleNode("ds:KeyInfo/ds:KeyValue/ds:RSAKeyValue/ds:Modulus", nsmgr)?.InnerText
            ?? throw new EBelgeXmlImzaDogrulamaException(EBelgeXmlImzaHataKodlari.ImzaDogrulamaHatasi, "ds:KeyInfo/ds:KeyValue/ds:RSAKeyValue/ds:Modulus bulunamadı.");
        var exponentBase64 = signatureElement.SelectSingleNode("ds:KeyInfo/ds:KeyValue/ds:RSAKeyValue/ds:Exponent", nsmgr)?.InnerText
            ?? throw new EBelgeXmlImzaDogrulamaException(EBelgeXmlImzaHataKodlari.ImzaDogrulamaHatasi, "ds:KeyInfo/ds:KeyValue/ds:RSAKeyValue/ds:Exponent bulunamadı.");

        using (var certPublicRsaForKeyValue = cert.GetRSAPublicKey()
            ?? throw new EBelgeXmlImzaDogrulamaException(EBelgeXmlImzaHataKodlari.DesteklenmeyenAnahtarAlgoritmasi, "Gömülü sertifika RSA public key içermiyor."))
        {
            var gercekParams = certPublicRsaForKeyValue.ExportParameters(false);
            var beyanEdilenModulus = Convert.FromBase64String(modulusBase64);
            var beyanEdilenExponent = Convert.FromBase64String(exponentBase64);

            if (!beyanEdilenModulus.AsSpan().SequenceEqual(gercekParams.Modulus) ||
                !beyanEdilenExponent.AsSpan().SequenceEqual(gercekParams.Exponent))
            {
                throw new EBelgeXmlImzaDogrulamaException(EBelgeXmlImzaHataKodlari.ImzaDogrulamaHatasi, "ds:KeyValue, gömülü sertifikanın gerçek public key'iyle eşleşmiyor.");
            }
        }

        // ---- 5. SignedInfo üzerindeki RSA imzasının bağımsız doğrulanması ----
        var signedInfoElement = (XmlElement)(signatureElement.SelectSingleNode("ds:SignedInfo", nsmgr)
            ?? throw new EBelgeXmlImzaDogrulamaException(EBelgeXmlImzaHataKodlari.ImzaDogrulamaHatasi, "ds:SignedInfo bulunamadı."));

        var signatureValueBase64 = signatureElement.SelectSingleNode("ds:SignatureValue", nsmgr)?.InnerText
            ?? throw new EBelgeXmlImzaDogrulamaException(EBelgeXmlImzaHataKodlari.ImzaDogrulamaHatasi, "ds:SignatureValue bulunamadı.");

        using var publicRsa = cert.GetRSAPublicKey()
            ?? throw new EBelgeXmlImzaDogrulamaException(EBelgeXmlImzaHataKodlari.DesteklenmeyenAnahtarAlgoritmasi, "Gömülü sertifika RSA public key içermiyor.");

        var signedInfoCanonicalBytes = CanonicalizeSubtree(signedInfoElement);
        var imzaGecerliMi = publicRsa.VerifyData(
            signedInfoCanonicalBytes,
            Convert.FromBase64String(signatureValueBase64),
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        if (!imzaGecerliMi)
        {
            throw new EBelgeXmlImzaDogrulamaException(EBelgeXmlImzaHataKodlari.ImzaDogrulamaHatasi, "SignedInfo üzerindeki RSA imzası bağımsız doğrulamadan geçemedi.");
        }

        // ---- 6. cac:Signature bağları (URI + VKN) bağımsız doğrulaması (bkz. Faz 2B.7.1 md.4) ----
        var cacSignatureNodes = doc.SelectNodes("/*/cac:Signature", nsmgr)!;
        if (cacSignatureNodes.Count != 1)
        {
            throw new EBelgeXmlImzaDogrulamaException(EBelgeXmlImzaHataKodlari.ImzaDogrulamaHatasi, $"Tam olarak 1 cac:Signature beklenirken {cacSignatureNodes.Count} bulundu.");
        }

        var cacSignatureElement = (XmlElement)cacSignatureNodes[0]!;

        var digitalSignatureUri = cacSignatureElement.SelectSingleNode("cac:DigitalSignatureAttachment/cac:ExternalReference/cbc:URI", nsmgr)?.InnerText
            ?? throw new EBelgeXmlImzaDogrulamaException(EBelgeXmlImzaHataKodlari.ImzaDogrulamaHatasi, "cac:Signature/cac:DigitalSignatureAttachment/cac:ExternalReference/cbc:URI bulunamadı.");

        if (!string.Equals(digitalSignatureUri, "#" + gercekSignatureId, StringComparison.Ordinal))
        {
            throw new EBelgeXmlImzaDogrulamaException(EBelgeXmlImzaHataKodlari.ImzaDogrulamaHatasi, "cac:Signature/cac:DigitalSignatureAttachment/cac:ExternalReference/cbc:URI, gerçek ds:Signature/@Id ile eşleşmiyor.");
        }

        var cacSignatureVkn = cacSignatureElement.SelectSingleNode("cbc:ID[@schemeID='VKN_TCKN']", nsmgr)?.InnerText
            ?? throw new EBelgeXmlImzaDogrulamaException(EBelgeXmlImzaHataKodlari.ImzaDogrulamaHatasi, "cac:Signature/cbc:ID[@schemeID='VKN_TCKN'] bulunamadı.");

        var supplierVkn = doc.SelectSingleNode("/*/cac:AccountingSupplierParty/cac:Party/cac:PartyIdentification[cbc:ID/@schemeID='VKN']/cbc:ID", nsmgr)?.InnerText
            ?? throw new EBelgeXmlImzaDogrulamaException(EBelgeXmlImzaHataKodlari.ImzaDogrulamaHatasi, "Düzenleyen tarafın VKN kimlik bilgisi bulunamadı.");

        if (!string.Equals(cacSignatureVkn, supplierVkn, StringComparison.Ordinal))
        {
            throw new EBelgeXmlImzaDogrulamaException(EBelgeXmlImzaHataKodlari.ImzaDogrulamaHatasi, "cac:Signature/cbc:ID, düzenleyen tarafın gerçek VKN'siyle eşleşmiyor.");
        }

        // ---- 7. Ek katman: .NET'in kendi SignedXml.CheckSignature()'ı (TEK BAŞINA yeterli SAYILMAZ) ----
        var checkDoc = LoadDocumentSecurely(signedXmlUtf8);
        var checkNsmgr = CreateNamespaceManager(checkDoc);
        var checkSignatureElement = (XmlElement)(checkDoc.SelectSingleNode("//ds:Signature", checkNsmgr)
            ?? throw new EBelgeXmlImzaDogrulamaException(EBelgeXmlImzaHataKodlari.ImzaDogrulamaHatasi, "İkinci (kontrol) parse'da ds:Signature bulunamadı."));

        var checkSignedXml = new SignedXml(checkDoc);
        checkSignedXml.LoadXml(checkSignatureElement);
        if (!checkSignedXml.CheckSignature(cert, verifySignatureOnly: true))
        {
            throw new EBelgeXmlImzaDogrulamaException(EBelgeXmlImzaHataKodlari.ImzaDogrulamaHatasi, "SignedXml.CheckSignature() başarısız oldu.");
        }

        // ---- SigningTime parse edilebilirliği ----
        var signingTimeText = signedPropertiesElement.SelectSingleNode("xades:SignedSignatureProperties/xades:SigningTime", nsmgr)?.InnerText
            ?? throw new EBelgeXmlImzaDogrulamaException(EBelgeXmlImzaHataKodlari.ImzaDogrulamaHatasi, "xades:SigningTime bulunamadı.");

        if (!DateTime.TryParse(
            signingTimeText, CultureInfo.InvariantCulture,
            DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
            out var signingTimeUtc))
        {
            throw new EBelgeXmlImzaDogrulamaException(EBelgeXmlImzaHataKodlari.ImzaDogrulamaHatasi, "xades:SigningTime parse edilemedi.");
        }

        return EBelgeXmlImzaDogrulamaSonucu.Gecerli(Convert.ToHexString(SHA256.HashData(cert.RawData)), signingTimeUtc);
    }

    private static XmlDocument LoadDocumentSecurely(ImmutableArray<byte> xmlUtf8)
    {
        var doc = new XmlDocument { XmlResolver = null, PreserveWhitespace = false };
        var readerSettings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
        };

        using var memoryStream = new MemoryStream(xmlUtf8.ToArray());
        using var xmlReader = XmlReader.Create(memoryStream, readerSettings);
        doc.Load(xmlReader);
        return doc;
    }

    private static XmlNamespaceManager CreateNamespaceManager(XmlDocument doc)
    {
        var nsmgr = new XmlNamespaceManager(doc.NameTable);
        nsmgr.AddNamespace("ds", NsDs);
        nsmgr.AddNamespace("xades", NsXades);
        nsmgr.AddNamespace("ext", NsExt);
        nsmgr.AddNamespace("cac", NsCac);
        nsmgr.AddNamespace("cbc", NsCbc);
        return nsmgr;
    }

    /// <summary>Enveloped referans (URI="") için: ds:Signature elemanı KALDIRILMIŞ bir KOPYA üzerinde tüm-belge C14N uygulanır ve hash'lenir - alt-ağaç (subtree) namespace bağlamı belirsizliği TAŞIMAZ (bkz. sınıf düzeyi açıklama, md.3).</summary>
    private static byte[] ComputeEnvelopedDocumentDigest(ImmutableArray<byte> signedXmlUtf8)
    {
        var kopya = LoadDocumentSecurely(signedXmlUtf8);
        var nsmgr = CreateNamespaceManager(kopya);
        var signatureNode = kopya.SelectSingleNode("//ds:Signature", nsmgr)
            ?? throw new EBelgeXmlImzaDogrulamaException(EBelgeXmlImzaHataKodlari.ImzaDogrulamaHatasi, "Belge digest'i hesaplanırken ds:Signature bulunamadı.");

        signatureNode.ParentNode!.RemoveChild(signatureNode);

        var transform = new XmlDsigC14NTransform();
        transform.LoadInput(kopya);
        using var output = (Stream)transform.GetOutput(typeof(Stream))!;
        return SHA256.HashData(output);
    }

    /// <summary>
    /// Bir alt-ağacı (element + TÜM soyundan gelen elemanlar/metin/açıklama/işlem-talimatları,
    /// öznitelikleri VE ad alanı düğümleri) TAM, AÇIK bir W3C node-set olarak C14N ile
    /// canonicalize eder. `XmlDsigC14NTransform.LoadInput(XmlNodeList)`, yalnız KÖK elemanı
    /// içeren bir liste ile beslendiğinde soyundan gelenleri KENDİLİĞİNDEN GENİŞLETMEZ - bu yüzden
    /// node-set burada AÇIKÇA (kök + tüm `.//node()` + tüm `.//@*` + tüm `.//namespace::*`) inşa
    /// edilir (bkz. W3C Canonical XML node-set tanımı). Ad alanı ekseni (`namespace::*`) KRİTİKTİR:
    /// alt-ağaca bir ÜST elemanda bildirilmiş ama alt-ağaç içinde "visibly utilized" olan bir ad
    /// alanı (ör. kök belge üzerinde bildirilmiş `xmlns:cac`/`xmlns:cbc`) varsa, yalnız `.//@*`
    /// bunu YAKALAYAMAZ (bu, öznitelik değil ad alanı düğümüdür) - eksik bırakılırsa imzalama
    /// sırasında SignedXml'in ÜRETTİĞİ digest ile burada BAĞIMSIZ hesaplanan digest UYUŞMAZ.
    /// </summary>
    private static byte[] CanonicalizeSubtree(XmlElement element)
    {
        var nodeList = element.SelectNodes(".|.//node()|.//@*|.//namespace::*")!;
        var transform = new XmlDsigC14NTransform();
        transform.LoadInput(nodeList);
        using var output = (Stream)transform.GetOutput(typeof(Stream))!;
        using var ms = new MemoryStream();
        output.CopyTo(ms);
        return ms.ToArray();
    }

    /// <summary>
    /// xades:SignedProperties referansı İÇİN KULLANILIR (bkz. imzalayıcıdaki eşdeğer açıklama,
    /// `EBelgeXmlImzalayici.ImzalaXml`) - kapsayıcı (inclusive) C14N'in aksine, Exclusive XML
    /// Canonicalization (bkz. `EBelgeXadesProfili.SignedPropertiesTransformUri`) yalnız alt-ağaç
    /// İÇİNDE FİİLEN KULLANILAN ad alanı öneklerini render eder; gömülü olduğu belgenin GERÇEK
    /// atalarından (ör. kök Invoice'un cac/cbc/ext ad alanları, ds:Signature'ın varsayılan ad
    /// alanı) miras alınan, alt-ağaçla İLGİSİZ ad alanlarını SIZDIRMAZ - bu, imzalama anında
    /// KOPUK bir alt-ağaç olarak hesaplanan digest ile burada TAM belge bağlamında yeniden
    /// ayrıştırma sonrası hesaplanan digest'in HER ZAMAN eşleşmesini SAĞLAR.
    /// </summary>
    private static byte[] CanonicalizeSubtreeExclusive(XmlElement element)
    {
        var nodeList = element.SelectNodes(".|.//node()|.//@*")!;
        var transform = new XmlDsigExcC14NTransform();
        transform.LoadInput(nodeList);
        using var output = (Stream)transform.GetOutput(typeof(Stream))!;
        using var ms = new MemoryStream();
        output.CopyTo(ms);
        return ms.ToArray();
    }

    /// <summary>Sertifikanın seri numarasını, imzalayıcıdaki (`EBelgeXmlImzalayici.GetSerialNumberDecimalString`) İLE AYNI, standart .NET/BigInteger dönüşümüyle - ama BAĞIMSIZ OLARAK, sertifikanın KENDİSİNDEN - ondalık string'e çevirir (bkz. Faz 2B.7.1 görev md.4, senaryo 10).</summary>
    private static string GetSerialNumberDecimalString(X509Certificate2 cert)
    {
        var littleEndian = cert.GetSerialNumber();
        var bigEndian = (byte[])littleEndian.Clone();
        Array.Reverse(bigEndian);
        var value = new BigInteger(bigEndian, isUnsigned: true, isBigEndian: true);
        return value.ToString(CultureInfo.InvariantCulture);
    }
}

internal sealed class EBelgeXmlImzaDogrulamaException : Exception
{
    public string HataKodu { get; }

    public EBelgeXmlImzaDogrulamaException(string hataKodu, string mesaj)
        : base(mesaj)
    {
        HataKodu = hataKodu;
    }
}
