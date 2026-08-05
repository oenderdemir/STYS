using System.Collections.Immutable;
using System.Globalization;
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
/// görev md.11) - AYRI bir XML parse, AYRI reference/node çözümlemesi ve AYRI hash hesaplaması
/// kullanır; imzayı üreten kodla AYNI yardımcı metotları PAYLAŞMAZ. Yalnız
/// <see cref="SignedXml.CheckSignature(X509Certificate2, bool)"/> sonucuna GÜVENİLMEZ (bkz. görev
/// md.11, md.27) - bu, aşağıdaki BAĞIMSIZ katmanlardan yalnız BİRİDİR:
///
/// 1. Sertleştirilmiş, bağımsız bir XmlReader ile TAZE bir parse (DTD/external entity KAPALI).
/// 2. Yapısal kontroller: tek ds:Signature, yinelenen "Id" niteliği YOK, beklenen referans
///    sayısı/tipleri/URI'leri.
/// 3. Tüm belge referansı (URI="") için BAĞIMSIZ, elle yeniden hesaplanmış digest (ds:Signature
///    KALDIRILIP C14N uygulanarak - SignedXml/CheckSignature'a HİÇ İHTİYAÇ DUYULMADAN).
/// 4. xades:SigningCertificate/CertDigest için BAĞIMSIZ, elle yeniden hesaplanmış sertifika
///    hash'i (gömülü ds:X509Certificate bytes'ından - imzalayan tarafın kendi nesnesinden DEĞİL).
/// 5. SignedInfo üzerindeki RSA imzasının, gömülü sertifikanın public key'i ile BAĞIMSIZ
///    yeniden doğrulanması (elle C14N + RSA.VerifyData).
/// 6. EK bir katman olarak .NET'in kendi SignedXml.CheckSignature()'ı - TEK BAŞINA YETERLİ
///    SAYILMAZ, yalnız YUKARIDAKİ bağımsız kontrolleri TAMAMLAR.
/// </summary>
public sealed class EBelgeXmlImzaDogrulayici : IEBelgeXmlImzaDogrulayici
{
    private const string NsDs = "http://www.w3.org/2000/09/xmldsig#";
    private const string NsXades = "http://uri.etsi.org/01903/v1.3.2#";

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
    }

    private static EBelgeXmlImzaDogrulamaSonucu DogrulaCore(ImmutableArray<byte> signedXmlUtf8)
    {
        var doc = LoadDocumentSecurely(signedXmlUtf8);
        var nsmgr = CreateNamespaceManager(doc);

        // ---- 2. Yapısal kontroller (signature-wrapping sertleştirmesi, bkz. görev md.8) ----
        var signatureNodes = doc.SelectNodes("//ds:Signature", nsmgr)
            ?? throw new EBelgeXmlImzaDogrulamaException(EBelgeXmlImzaHataKodlari.ImzaDogrulamaHatasi, "ds:Signature aranırken sorgu başarısız oldu.");

        if (signatureNodes.Count != 1)
        {
            throw new EBelgeXmlImzaDogrulamaException(EBelgeXmlImzaHataKodlari.ImzaDogrulamaHatasi, $"Tam olarak 1 ds:Signature beklenirken {signatureNodes.Count} bulundu.");
        }

        var signatureElement = (XmlElement)signatureNodes[0]!;

        var idTasiyanlar = doc.SelectNodes("//*[@Id]", nsmgr)!;
        var idDegerleri = idTasiyanlar.Cast<XmlElement>().Select(e => e.GetAttribute("Id")).ToList();
        if (idDegerleri.Distinct(StringComparer.Ordinal).Count() != idDegerleri.Count)
        {
            throw new EBelgeXmlImzaDogrulamaException(EBelgeXmlImzaHataKodlari.YinelenenXmlId, "Belge içinde yinelenen 'Id' niteliği bulundu.");
        }

        var referenceNodes = signatureElement.SelectNodes("ds:SignedInfo/ds:Reference", nsmgr)!;
        if (referenceNodes.Count != 2)
        {
            throw new EBelgeXmlImzaDogrulamaException(EBelgeXmlImzaHataKodlari.ReferansUriCozulemedi, $"Tam olarak 2 ds:Reference (belge + SignedProperties) beklenirken {referenceNodes.Count} bulundu.");
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

        var belgeDigestBeyan = belgeReferansi.SelectSingleNode("ds:DigestValue", nsmgr)?.InnerText
            ?? throw new EBelgeXmlImzaDogrulamaException(EBelgeXmlImzaHataKodlari.ReferansUriCozulemedi, "Belge referansının DigestValue'su bulunamadı.");

        var belgeDigestGercek = Convert.ToBase64String(ComputeEnvelopedDocumentDigest(signedXmlUtf8));
        if (!string.Equals(belgeDigestBeyan, belgeDigestGercek, StringComparison.Ordinal))
        {
            throw new EBelgeXmlImzaDogrulamaException(EBelgeXmlImzaHataKodlari.ImzaDogrulamaHatasi, "Belge referansının digest'i bağımsız hesaplamayla eşleşmiyor - belge iş verileri değiştirilmiş olabilir.");
        }

        // ---- SignedProperties referansı çözümü + Type kontrolü ----
        var signedPropsReferansi = referenceNodes.Cast<XmlElement>().SingleOrDefault(r => r.GetAttribute("URI").StartsWith('#'))
            ?? throw new EBelgeXmlImzaDogrulamaException(EBelgeXmlImzaHataKodlari.ReferansUriCozulemedi, "SignedProperties referansı (# ile başlayan URI) bulunamadı.");

        if (!string.Equals(signedPropsReferansi.GetAttribute("Type"), Profil.SignedPropertiesTypeUri, StringComparison.Ordinal))
        {
            throw new EBelgeXmlImzaDogrulamaException(EBelgeXmlImzaHataKodlari.ReferansUriCozulemedi, "SignedProperties referansının Type niteliği beklenen XAdES URI'siyle eşleşmiyor.");
        }

        var signedPropsId = signedPropsReferansi.GetAttribute("URI").TrimStart('#');
        var signedPropsElemanlari = doc.SelectNodes($"//*[@Id='{signedPropsId}']", nsmgr)!;
        if (signedPropsElemanlari.Count != 1)
        {
            throw new EBelgeXmlImzaDogrulamaException(EBelgeXmlImzaHataKodlari.ReferansUriCozulemedi, $"SignedProperties referansı (#{signedPropsId}) belgede TAM OLARAK bir kez bulunmuyor.");
        }

        var signedPropertiesElement = (XmlElement)signedPropsElemanlari[0]!;

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

        // ---- 4. Gömülü sertifika + CertDigest bağımsız doğrulaması ----
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

        // ---- 6. Ek katman: .NET'in kendi SignedXml.CheckSignature()'ı (TEK BAŞINA yeterli SAYILMAZ) ----
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
    /// Canonicalization (http://www.w3.org/2001/10/xml-exc-c14n#) yalnız alt-ağaç İÇİNDE FİİLEN
    /// KULLANILAN ad alanı öneklerini render eder; gömülü olduğu belgenin GERÇEK atalarından
    /// (ör. kök Invoice'un cac/cbc/ext ad alanları, ds:Signature'ın varsayılan ad alanı) miras
    /// alınan, alt-ağaçla İLGİSİZ ad alanlarını SIZDIRMAZ - bu, imzalama anında KOPUK bir alt-ağaç
    /// olarak hesaplanan digest ile burada TAM belge bağlamında yeniden ayrıştırma sonrası
    /// hesaplanan digest'in HER ZAMAN eşleşmesini SAĞLAR.
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
