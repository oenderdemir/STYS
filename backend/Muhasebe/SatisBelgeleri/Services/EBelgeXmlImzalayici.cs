using System.Collections.Immutable;
using System.Globalization;
using System.Numerics;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography.Xml;
using System.Text;
using System.Xml;

namespace STYS.Muhasebe.SatisBelgeleri.Services;

/// <summary>Bir XML imzalama talebini taşır - DB entity/DbContext İÇERMEZ (bkz. Faz 2B.7 görev md.3).</summary>
public sealed record EBelgeXmlImzaTalebi
{
    public required int KurumId { get; init; }

    public required ImmutableArray<byte> UnsignedUblUtf8 { get; init; }

    public required string UnsignedUblSha256 { get; init; }

    public required string RuleSetId { get; init; }

    public required string EBelgeUuid { get; init; }

    /// <summary>xades:SigningTime için kullanılır - TimeProvider üzerinden ÇAĞIRAN tarafından sağlanır (bkz. görev md.9, "DateTime.Now/UtcNow kullanılmaz").</summary>
    public required DateTime ImzalamaZamaniUtc { get; init; }
}

public sealed record EBelgeXmlImzaSonucu
{
    public required ImmutableArray<byte> SignedUblUtf8 { get; init; }

    public required string SignedUblSha256 { get; init; }

    public required string KaynakUnsignedUblSha256 { get; init; }

    public required string ImzaProfili { get; init; }

    public required string ImzaAlgoritmasi { get; init; }

    public required string DigestAlgoritmasi { get; init; }

    public required string SertifikaSha256ParmakIzi { get; init; }

    public required DateTime ImzalamaZamaniUtc { get; init; }
}

public interface IEBelgeXmlImzalayici
{
    Task<EBelgeXmlImzaSonucu> ImzalaAsync(EBelgeXmlImzaTalebi talep, CancellationToken cancellationToken);
}

/// <summary>
/// Unsigned UBL XML'i, GİB UBL-TR profiline (bkz. <see cref="EBelgeXadesProfili.GibUblTr"/> XML
/// doc'u - kaynaklar için) uygun, GERÇEK kriptografik XAdES-BES enveloped imza ile imzalar.
/// DB entity/DbContext'e HİÇ ERİŞMEZ (bkz. görev md.3). Sertifika/private key'e YALNIZ
/// <see cref="IEBelgeImzaKimligiSaglayici"/> üzerinden erişir - dosya sistemi/env
/// var/certificate store/repository dosyası OKUMAZ (bkz. görev md.4).
/// </summary>
public sealed class EBelgeXmlImzalayici : IEBelgeXmlImzalayici
{
    private const string NsExt = "urn:oasis:names:specification:ubl:schema:xsd:CommonExtensionComponents-2";
    private const string NsCac = "urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2";
    private const string NsCbc = "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2";
    private const string NsDs = "http://www.w3.org/2000/09/xmldsig#";

    private const string SignatureVknSchemeId = "VKN_TCKN";
    private const int MinRsaKeySizeBits = 2048;

    /// <summary>bkz. `BuildQualifyingProperties` içindeki xades:SignerRole açıklaması - TÜBİTAK KamuSM ESYA SDK dokümantasyonuyla uyumlu, sabit değer.</summary>
    internal const string SignerClaimedRole = "Supplier";

    private readonly IEBelgeImzaKimligiSaglayici _kimlikSaglayici;
    private readonly IEBelgeSertifikaGuvenValidatoru _guvenValidatoru;
    private readonly EBelgeXadesProfili _profil;

    public EBelgeXmlImzalayici(
        IEBelgeImzaKimligiSaglayici kimlikSaglayici,
        IEBelgeSertifikaGuvenValidatoru guvenValidatoru)
    {
        _kimlikSaglayici = kimlikSaglayici;
        _guvenValidatoru = guvenValidatoru;
        _profil = EBelgeXadesProfili.GibUblTr;
    }

    public async Task<EBelgeXmlImzaSonucu> ImzalaAsync(EBelgeXmlImzaTalebi talep, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(talep);

        // Faz 2B.7.1 görev md.2: kanıtlanmamış/onaysız bir profille ÜRETİM imzalaması SESSİZCE
        // devam ETMEZ - bkz. EBelgeXadesProfili.Onayli XML doc'u.
        if (!_profil.Onayli)
        {
            throw new EBelgeXadesProfiliOnaylanmadiException(_profil.ProfilKimligi);
        }

        // md.15: kaynak bytes'ın hash'i - motor kendi bağımsız garantisini de sağlar (çağıran
        // taraf zaten kontrol etmiş olsa bile).
        var bagimsizKaynakHash = Convert.ToHexString(SHA256.HashData(talep.UnsignedUblUtf8.AsSpan()));
        if (!string.Equals(bagimsizKaynakHash, talep.UnsignedUblSha256, StringComparison.Ordinal))
        {
            throw new EBelgeXmlImzaKaliciHataException(
                EBelgeXmlImzaHataKodlari.KaynakHashUyumsuz,
                "İmzalanacak XML bytes'ın hash'i, talepte beyan edilen hash ile eşleşmiyor.");
        }

        var kimlik = await _kimlikSaglayici.GetAsync(talep.KurumId, cancellationToken);
        try
        {
            ValidateSertifika(kimlik, talep.ImzalamaZamaniUtc);

            var guvenSonucu = await _guvenValidatoru.DogrulaAsync(kimlik.Sertifika, cancellationToken);
            if (!guvenSonucu.GuvenilirMi)
            {
                throw new EBelgeXmlImzaKaliciHataException(
                    EBelgeXmlImzaHataKodlari.SertifikaGuvenilirDegil,
                    $"Sertifika güven doğrulaması başarısız: {guvenSonucu.RedNedeni}");
            }

            var signedXmlBytes = ImzalaXml(talep, kimlik);
            var signedSha256 = Convert.ToHexString(SHA256.HashData(signedXmlBytes));

            return new EBelgeXmlImzaSonucu
            {
                SignedUblUtf8 = ImmutableArray.Create(signedXmlBytes),
                SignedUblSha256 = signedSha256,
                KaynakUnsignedUblSha256 = talep.UnsignedUblSha256,
                ImzaProfili = _profil.ProfilKimligi,
                ImzaAlgoritmasi = _profil.SignatureAlgorithmUri,
                DigestAlgoritmasi = _profil.DigestAlgorithmUri,
                SertifikaSha256ParmakIzi = kimlik.SertifikaSha256ParmakIzi,
                ImzalamaZamaniUtc = talep.ImzalamaZamaniUtc,
            };
        }
        finally
        {
            kimlik.Dispose();
        }
    }

    // ---- md.10: imzalamadan önceki temel sertifika kontrolleri ----

    private static void ValidateSertifika(EBelgeImzaKimligi kimlik, DateTime simdiUtc)
    {
        var sertifika = kimlik.Sertifika;

        if (!sertifika.HasPrivateKey)
        {
            throw new EBelgeXmlImzaKaliciHataException(EBelgeXmlImzaHataKodlari.PrivateKeyYok, "Sertifika private key içermiyor.");
        }

        var gecerliBaslangicUtc = sertifika.NotBefore.ToUniversalTime();
        var gecerliBitisUtc = sertifika.NotAfter.ToUniversalTime();
        if (simdiUtc < gecerliBaslangicUtc)
        {
            throw new EBelgeXmlImzaKaliciHataException(EBelgeXmlImzaHataKodlari.SertifikaGecersizAralik, "Sertifika henüz geçerlilik tarihine ulaşmadı.");
        }

        if (simdiUtc > gecerliBitisUtc)
        {
            throw new EBelgeXmlImzaKaliciHataException(EBelgeXmlImzaHataKodlari.SertifikaGecersizAralik, "Sertifikanın geçerlilik süresi dolmuş.");
        }

        var keyUsageExtension = sertifika.Extensions.OfType<X509KeyUsageExtension>().FirstOrDefault();
        if (keyUsageExtension is not null && !keyUsageExtension.KeyUsages.HasFlag(X509KeyUsageFlags.DigitalSignature))
        {
            throw new EBelgeXmlImzaKaliciHataException(EBelgeXmlImzaHataKodlari.AnahtarKullanimiUygunDegil, "Sertifikanın key usage niteliği dijital imzaya izin vermiyor.");
        }

        using var rsa = sertifika.GetRSAPrivateKey();
        if (rsa is null)
        {
            throw new EBelgeXmlImzaKaliciHataException(EBelgeXmlImzaHataKodlari.DesteklenmeyenAnahtarAlgoritmasi, "Yalnız RSA anahtarlı sertifikalar desteklenir.");
        }

        if (rsa.KeySize < MinRsaKeySizeBits)
        {
            throw new EBelgeXmlImzaKaliciHataException(EBelgeXmlImzaHataKodlari.DesteklenmeyenAnahtarAlgoritmasi, $"RSA anahtar boyutu en az {MinRsaKeySizeBits} bit olmalıdır.");
        }

        // Public/private key eşleşmesi - küçük bir sign+verify round-trip ile bağımsız doğrulanır.
        var probeBytes = "EBelgeXmlImzalayici-anahtar-dogrulama"u8.ToArray();
        var imza = rsa.SignData(probeBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var publicRsa = sertifika.GetRSAPublicKey();
        if (publicRsa is null || !publicRsa.VerifyData(probeBytes, imza, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1))
        {
            throw new EBelgeXmlImzaKaliciHataException(EBelgeXmlImzaHataKodlari.SertifikaAnahtarEslesmiyor, "Sertifikanın public/private anahtar çifti eşleşmiyor.");
        }
    }

    // ---- XML imzalama ----

    private byte[] ImzalaXml(EBelgeXmlImzaTalebi talep, EBelgeImzaKimligi kimlik)
    {
        var doc = LoadUnsignedDocument(talep.UnsignedUblUtf8);
        var nsmgr = CreateNamespaceManager(doc);

        var root = doc.DocumentElement
            ?? throw new EBelgeXmlImzaKaliciHataException(EBelgeXmlImzaHataKodlari.XmlDsigUretimHatasi, "Unsigned XML kök elemanı bulunamadı.");

        EnsureNoExistingIdCollision(doc, nsmgr);

        const string signatureId = "Signature-1";
        const string signedPropertiesId = "SignedProperties-1";

        var extensionContent = InsertUblExtensionsPlaceholder(doc, root);
        InsertCacSignature(doc, nsmgr, root, signatureId);

        var signedXml = new XadesAwareSignedXml(doc)
        {
            SigningKey = kimlik.Sertifika.GetRSAPrivateKey()
                ?? throw new EBelgeXmlImzaKaliciHataException(EBelgeXmlImzaHataKodlari.PrivateKeyYok, "Sertifika private key içermiyor."),
        };
        signedXml.Signature.Id = signatureId;
        signedXml.SignedInfo!.CanonicalizationMethod = _profil.CanonicalizationAlgorithmUri;
        signedXml.SignedInfo.SignatureMethod = _profil.SignatureAlgorithmUri;

        var belgeReferansi = new Reference { Uri = string.Empty, DigestMethod = _profil.DigestAlgorithmUri };
        belgeReferansi.AddTransform(new XmlDsigEnvelopedSignatureTransform());
        signedXml.AddReference(belgeReferansi);

        // ds:KeyInfo/ds:KeyValue (bkz. Faz 2B.7.2 raporu) - AYNI KamuSM ESYA gerekçesiyle
        // (yukarı, xades:SignerRole açıklaması) eklenir; PUBLIC key'i taşır - private key ASLA
        // bu şekilde/başka herhangi bir şekilde XML'e YAZILMAZ, LOGLANMAZ (bkz. görev md.22).
        using var publicRsaForKeyInfo = kimlik.Sertifika.GetRSAPublicKey()
            ?? throw new EBelgeXmlImzaKaliciHataException(EBelgeXmlImzaHataKodlari.DesteklenmeyenAnahtarAlgoritmasi, "Sertifika RSA public key içermiyor.");

        var keyInfo = new KeyInfo();
        keyInfo.AddClause(new KeyInfoX509Data(kimlik.Sertifika));
        keyInfo.AddClause(new RSAKeyValue(publicRsaForKeyInfo));
        signedXml.KeyInfo = keyInfo;

        var qualifyingProperties = BuildQualifyingProperties(doc, talep, kimlik, signatureId, signedPropertiesId);
        var dataObject = new DataObject { Data = WrapAsNodeList(doc, qualifyingProperties) };
        signedXml.AddObject(dataObject);

        var signedPropertiesReferansi = new Reference
        {
            Uri = "#" + signedPropertiesId,
            Type = _profil.SignedPropertiesTypeUri,
            DigestMethod = _profil.DigestAlgorithmUri,
        };

        // xades:SignedProperties, ComputeSignature() sırasında `signedXml.AddObject` ile
        // eklenmiş, belgeye HENÜZ YERLEŞTİRİLMEMİŞ (kopuk/detached) bir alt-ağaçtır - bu yüzden bu
        // referansın canonicalization'ında GERÇEK belge bağlamındaki (cac/cbc/ext kök ad alanları,
        // ds:Signature'ın varsayılan ad alanı vb.) atalardan MİRAS ALINAN ad alanları KULLANILAMAZ
        // (bunlar imzalama anında erişilebilir DEĞİLDİR). Kapsayıcı (inclusive, REC-xml-c14n)
        // canonicalization TÜM kapsam-içi ad alanlarını RENDER ETMEK ZORUNDADIR - bu, imzalama
        // anında kopuk ağaçta hesaplanan digest ile son serialize edilmiş belgede yeniden ayrıştırma
        // sonrası (TAM belge bağlamıyla) bağımsızca yeniden hesaplanan digest'in ASLA
        // eşleşemeyeceği anlamına gelir (bu, .NET SignedXml.AddObject + kapsayıcı C14N
        // kombinasyonunun BİLİNEN bir kısıtıdır). Bu nedenle - yalnız BU referansın transformu için
        // - "yalnız FİİLEN kullanılan" ad alanlarını render eden, gömülme bağlamından BAĞIMSIZ,
        // Exclusive XML Canonicalization (bkz. `_profil.SignedPropertiesTransformUri` - TEK
        // merkezî sabit, hem burada hem `EBelgeXmlImzaDogrulayici`'de kullanılır) kullanılır.
        // ds:SignedInfo/ds:CanonicalizationMethod (bkz. `_profil.CanonicalizationAlgorithmUri`) ve
        // belge referansının (URI="") transformu BUNDAN ETKİLENMEZ - ikisi de araştırmayla
        // doğrulanmış kapsayıcı C14N'de KALIR. GİB kaynaklarının HİÇBİRİ, SignedProperties
        // referansının transform algoritmasını AÇIKÇA belirtmiyor (yalnız "referans başına en fazla
        // bir Transform" ve rsa-sha1 yasağı doğrulanmıştır) - bu yüzden bu seçim GİB profilini
        // İHLAL ETMEZ, yalnız .NET'in kendi kısıtına karşı gerekli bir mühendislik kararıdır (bkz.
        // Faz 2B.7.1 raporu).
        System.Diagnostics.Debug.Assert(
            new XmlDsigExcC14NTransform().Algorithm == _profil.SignedPropertiesTransformUri,
            "Transform URI'si profil sabitiyle eşleşmiyor.");
        signedPropertiesReferansi.AddTransform(new XmlDsigExcC14NTransform());
        signedXml.AddReference(signedPropertiesReferansi);

        try
        {
            signedXml.ComputeSignature();
        }
        catch (CryptographicException ex)
        {
            throw new EBelgeXmlImzaKaliciHataException(EBelgeXmlImzaHataKodlari.XmlDsigUretimHatasi, "XMLDSig imza hesaplaması başarısız oldu: " + ex.Message);
        }

        var signatureElement = signedXml.GetXml();
        var importedSignature = doc.ImportNode(signatureElement, deep: true);
        extensionContent.AppendChild(importedSignature);

        return SerializeDocument(doc);
    }

    private static XmlDocument LoadUnsignedDocument(ImmutableArray<byte> unsignedUblUtf8)
    {
        var doc = new XmlDocument { XmlResolver = null, PreserveWhitespace = false };

        var readerSettings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
        };

        using var memoryStream = new MemoryStream(unsignedUblUtf8.ToArray());
        using var xmlReader = XmlReader.Create(memoryStream, readerSettings);
        doc.Load(xmlReader);
        return doc;
    }

    private static XmlNamespaceManager CreateNamespaceManager(XmlDocument doc)
    {
        var nsmgr = new XmlNamespaceManager(doc.NameTable);
        nsmgr.AddNamespace("ext", NsExt);
        nsmgr.AddNamespace("cac", NsCac);
        nsmgr.AddNamespace("cbc", NsCbc);
        nsmgr.AddNamespace("ds", NsDs);
        return nsmgr;
    }

    /// <summary>Signature-wrapping sertleştirmesi (bkz. görev md.8): imzalama BAŞLAMADAN önce, kaynak (renderer çıktısı) XML'de ZATEN bir "Id" niteliği bulunmadığından emin olunur - beklenmeyen bir çakışma sessizce ÜZERİNE yazılmaz.</summary>
    private static void EnsureNoExistingIdCollision(XmlDocument doc, XmlNamespaceManager nsmgr)
    {
        var mevcutIdTasiyanlar = doc.SelectNodes("//*[@Id]", nsmgr);
        if (mevcutIdTasiyanlar is { Count: > 0 })
        {
            throw new EBelgeXmlImzaKaliciHataException(
                EBelgeXmlImzaHataKodlari.YinelenenXmlId,
                "Unsigned XML, imzalama öncesinde beklenmeyen bir 'Id' niteliği taşıyor.");
        }
    }

    /// <summary>ext:UBLExtensions/ext:UBLExtension/ext:ExtensionContent iskeletini kök elemanın İLK çocuğu olarak ekler (bkz. UBL-Invoice-2.1.xsd sıra kuralı) ve doldurulacak ExtensionContent elemanını döner.</summary>
    private static XmlElement InsertUblExtensionsPlaceholder(XmlDocument doc, XmlElement root)
    {
        var ublExtensions = doc.CreateElement("ext", "UBLExtensions", NsExt);
        var ublExtension = doc.CreateElement("ext", "UBLExtension", NsExt);
        var extensionContent = doc.CreateElement("ext", "ExtensionContent", NsExt);

        ublExtension.AppendChild(extensionContent);
        ublExtensions.AppendChild(ublExtension);
        root.InsertBefore(ublExtensions, root.FirstChild);

        return extensionContent;
    }

    /// <summary>
    /// cac:Signature elemanını cac:AccountingSupplierParty'den HEMEN ÖNCE ekler (bkz.
    /// UBL-Invoice-2.1.xsd sıra kuralı). GERÇEK `SignatureType` (bkz.
    /// UBL-CommonAggregateComponents-2.1.xsd - `sac:SignatureInformationType` İLE
    /// KARIŞTIRILMAMALIDIR, bu İKİ AYRI tiptir) `cbc:ID`, `cac:SignatoryParty` VE
    /// `cac:DigitalSignatureAttachment`'ın ÜÇÜNÜ DE ZORUNLU kılar:
    /// - `cbc:ID` (schemeID=VKN_TCKN): bkz. UBL-TR_Common_Schematron.xml "SignatureCheck".
    /// - `cac:SignatoryParty` (PartyType - cac:PartyIdentification VE cac:PostalAddress ZORUNLU):
    ///   schematron "SignatoryPartyPartyIdentificationCheck" en az bir schemeID=VKN/TCKN
    ///   cac:PartyIdentification/cbc:ID bekler - kimlik/adres, belgenin KENDİ, ZATEN doğrulanmış
    ///   AccountingSupplierParty/cac:Party düğümünden KLONLANIR (sertifika subject'inden TAHMİNÎ
    ///   parse EDİLMEZ, yeni iş verisi İCAT EDİLMEZ - bkz. görev md.10).
    /// - `cac:DigitalSignatureAttachment/cac:ExternalReference/cbc:URI`: gömülü ds:Signature'ı
    ///   işaret eder ("#" + signatureId) - bkz. "SignatureCheck"in YORUMA ALINMIŞ (henüz
    ///   zorunlu kılınmamış ama AÇIKÇA belgelenmiş) ExternalReference/URI assert'i, ayrıca KamuSM
    ///   ESYA SDK dokümantasyonuyla örtüşür (bkz. Faz 2B.7 raporu).
    /// Kriptografik imzanın KENDİSİ BURAYA KONULMAZ (bkz. görev md.7) - yalnız REFERANS URI'si.
    /// </summary>
    private static void InsertCacSignature(XmlDocument doc, XmlNamespaceManager nsmgr, XmlElement root, string signatureId)
    {
        var supplierPartyNode = root.SelectSingleNode("cac:AccountingSupplierParty", nsmgr)
            ?? throw new EBelgeXmlImzaKaliciHataException(EBelgeXmlImzaHataKodlari.XmlDsigUretimHatasi, "cac:AccountingSupplierParty bulunamadı.");

        var supplierPartyIdNode = (XmlElement?)root.SelectSingleNode("cac:AccountingSupplierParty/cac:Party/cac:PartyIdentification[cbc:ID/@schemeID='VKN']", nsmgr)
            ?? throw new EBelgeXmlImzaKaliciHataException(EBelgeXmlImzaHataKodlari.XmlDsigUretimHatasi, "Düzenleyen tarafın VKN kimlik bilgisi (cac:PartyIdentification) bulunamadı.");

        var vkn = supplierPartyIdNode.SelectSingleNode("cbc:ID", nsmgr)!.InnerText;
        if (vkn.Length is not (10 or 11))
        {
            throw new EBelgeXmlImzaKaliciHataException(EBelgeXmlImzaHataKodlari.XmlDsigUretimHatasi, "Düzenleyen tarafın kimlik numarası 10 veya 11 haneli olmalıdır.");
        }

        var supplierPostalAddressNode = (XmlElement?)root.SelectSingleNode("cac:AccountingSupplierParty/cac:Party/cac:PostalAddress", nsmgr)
            ?? throw new EBelgeXmlImzaKaliciHataException(EBelgeXmlImzaHataKodlari.XmlDsigUretimHatasi, "Düzenleyen tarafın adres bilgisi (cac:PostalAddress) bulunamadı.");

        var cacSignature = doc.CreateElement("cac", "Signature", NsCac);

        var cbcId = doc.CreateElement("cbc", "ID", NsCbc);
        cbcId.SetAttribute("schemeID", SignatureVknSchemeId);
        cbcId.InnerText = vkn;
        cacSignature.AppendChild(cbcId);

        var signatoryParty = doc.CreateElement("cac", "SignatoryParty", NsCac);
        signatoryParty.AppendChild(supplierPartyIdNode.CloneNode(deep: true));
        signatoryParty.AppendChild(supplierPostalAddressNode.CloneNode(deep: true));
        cacSignature.AppendChild(signatoryParty);

        var digitalSignatureAttachment = doc.CreateElement("cac", "DigitalSignatureAttachment", NsCac);
        var externalReference = doc.CreateElement("cac", "ExternalReference", NsCac);
        var uri = doc.CreateElement("cbc", "URI", NsCbc);
        uri.InnerText = "#" + signatureId;
        externalReference.AppendChild(uri);
        digitalSignatureAttachment.AppendChild(externalReference);
        cacSignature.AppendChild(digitalSignatureAttachment);

        root.InsertBefore(cacSignature, supplierPartyNode);
    }

    private XmlElement BuildQualifyingProperties(
        XmlDocument doc,
        EBelgeXmlImzaTalebi talep,
        EBelgeImzaKimligi kimlik,
        string signatureId,
        string signedPropertiesId)
    {
        var xadesNs = _profil.XadesNamespaceUri;

        var qualifyingProperties = doc.CreateElement("xades", "QualifyingProperties", xadesNs);
        qualifyingProperties.SetAttribute("Target", "#" + signatureId);

        var signedProperties = doc.CreateElement("xades", "SignedProperties", xadesNs);
        signedProperties.SetAttribute("Id", signedPropertiesId);

        var signedSignatureProperties = doc.CreateElement("xades", "SignedSignatureProperties", xadesNs);

        var signingTime = doc.CreateElement("xades", "SigningTime", xadesNs);
        signingTime.InnerText = talep.ImzalamaZamaniUtc.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);
        signedSignatureProperties.AppendChild(signingTime);

        var signingCertificate = doc.CreateElement("xades", "SigningCertificate", xadesNs);
        var cert = doc.CreateElement("xades", "Cert", xadesNs);

        var certDigest = doc.CreateElement("xades", "CertDigest", xadesNs);
        var digestMethod = doc.CreateElement("ds", "DigestMethod", NsDs);
        digestMethod.SetAttribute("Algorithm", _profil.DigestAlgorithmUri);
        certDigest.AppendChild(digestMethod);
        var digestValue = doc.CreateElement("ds", "DigestValue", NsDs);
        digestValue.InnerText = Convert.ToBase64String(kimlik.Sertifika.GetCertHash(HashAlgorithmName.SHA256));
        certDigest.AppendChild(digestValue);
        cert.AppendChild(certDigest);

        var issuerSerial = doc.CreateElement("xades", "IssuerSerial", xadesNs);
        var x509IssuerName = doc.CreateElement("ds", "X509IssuerName", NsDs);
        x509IssuerName.InnerText = kimlik.Sertifika.IssuerName.Name;
        issuerSerial.AppendChild(x509IssuerName);
        var x509SerialNumber = doc.CreateElement("ds", "X509SerialNumber", NsDs);
        x509SerialNumber.InnerText = GetSerialNumberDecimalString(kimlik.Sertifika);
        issuerSerial.AppendChild(x509SerialNumber);
        cert.AppendChild(issuerSerial);

        signingCertificate.AppendChild(cert);
        signedSignatureProperties.AppendChild(signingCertificate);

        // xades:SignerRole (bkz. Faz 2B.7.2 raporu) - TÜBİTAK KamuSM ESYA SDK dokümantasyonu
        // (yazilim.kamusm.gov.tr/esya-api), "e-fatura standartlarında GEREKLİ KILINAN imzacı rolü,
        // açık anahtar ve imza zamanı eklenir" ifadesiyle bunu AÇIKÇA e-Fatura standardının bir
        // gereği olarak belirtir - bu, resmî GİB metninde DOĞRUDAN yazılı olmasa da, GİB'in
        // adlandırdığı akredite e-imza altyapı sağlayıcısından gelen, AKSİNİ gösteren hiçbir kanıt
        // BULUNAMAYAN, uyumluluk açısından güvenli tarafta duran bir karardır (bkz. görev md.6 -
        // "kanıt bulunamıyorsa uyumluluk açısından güvenli olan tercih edilen yaklaşımı uygula").
        var signerRole = doc.CreateElement("xades", "SignerRole", xadesNs);
        var claimedRoles = doc.CreateElement("xades", "ClaimedRoles", xadesNs);
        var claimedRole = doc.CreateElement("xades", "ClaimedRole", xadesNs);
        claimedRole.InnerText = SignerClaimedRole;
        claimedRoles.AppendChild(claimedRole);
        signerRole.AppendChild(claimedRoles);
        signedSignatureProperties.AppendChild(signerRole);

        signedProperties.AppendChild(signedSignatureProperties);
        qualifyingProperties.AppendChild(signedProperties);

        return qualifyingProperties;
    }

    private static string GetSerialNumberDecimalString(X509Certificate2 cert)
    {
        var littleEndian = cert.GetSerialNumber();
        var bigEndian = (byte[])littleEndian.Clone();
        Array.Reverse(bigEndian);
        var value = new BigInteger(bigEndian, isUnsigned: true, isBigEndian: true);
        return value.ToString(CultureInfo.InvariantCulture);
    }

    private static XmlNodeList WrapAsNodeList(XmlDocument doc, XmlElement element)
    {
        var wrapper = doc.CreateElement("Wrapper");
        wrapper.AppendChild(element);
        return wrapper.ChildNodes;
    }

    /// <summary>UTF-8, BOM'suz, tek seferlik serialize (bkz. görev md.3 - "signed XML üretildikten sonra yeniden serialize edilmemeli"; bu, o TEK üretim adımıdır).</summary>
    private static byte[] SerializeDocument(XmlDocument doc)
    {
        using var memoryStream = new MemoryStream();
        var settings = new XmlWriterSettings
        {
            Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            Indent = false,
            NewLineHandling = NewLineHandling.None,
            OmitXmlDeclaration = false,
            CloseOutput = true,
            ConformanceLevel = ConformanceLevel.Document,
        };

        using (var writer = XmlWriter.Create(memoryStream, settings))
        {
            doc.Save(writer);
        }

        return memoryStream.ToArray();
    }

    /// <summary>
    /// .NET'in `SignedXml.GetIdElement` TEMEL implementasyonu, yalnız BELGENİN KENDİSİNDEKİ
    /// (`doc.GetElementById`, DTD/şema desteği olmadan HER ZAMAN null döner) veya belgede
    /// DOĞRUDAN bulunan elemanları çözer - `AddObject` ile eklenen, henüz belgeye YERLEŞTİRİLMEMİŞ
    /// bir `DataObject` içindeki `Id` niteliğini (bizim `xades:SignedProperties/@Id`'imiz)
    /// OTOMATİK OLARAK ARAMAZ. Bu, standart .NET XAdES imzalama deseninin BİLİNEN bir
    /// kısıtıdır - çözüm, `Signature.ObjectList` içinde de arama yapan bu ince (override
    /// dışında hiçbir davranışı DEĞİŞTİRMEYEN) alt sınıftır.
    /// </summary>
    private sealed class XadesAwareSignedXml : SignedXml
    {
        public XadesAwareSignedXml(XmlDocument document)
            : base(document)
        {
        }

        public override XmlElement? GetIdElement(XmlDocument? document, string idValue)
        {
            var temelSonuc = base.GetIdElement(document, idValue);
            if (temelSonuc is not null)
            {
                return temelSonuc;
            }

            foreach (var nesne in Signature.ObjectList)
            {
                if (nesne is not DataObject dataObject || dataObject.Data is null)
                {
                    continue;
                }

                foreach (XmlNode node in dataObject.Data)
                {
                    if (node is XmlElement element)
                    {
                        var bulunan = FindElementById(element, idValue);
                        if (bulunan is not null)
                        {
                            return bulunan;
                        }
                    }
                }
            }

            return null;
        }

        private static XmlElement? FindElementById(XmlElement root, string idValue)
        {
            if (string.Equals(root.GetAttribute("Id"), idValue, StringComparison.Ordinal))
            {
                return root;
            }

            foreach (XmlNode child in root.ChildNodes)
            {
                if (child is XmlElement childElement)
                {
                    var bulunan = FindElementById(childElement, idValue);
                    if (bulunan is not null)
                    {
                        return bulunan;
                    }
                }
            }

            return null;
        }
    }
}
