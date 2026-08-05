namespace STYS.Muhasebe.SatisBelgeleri;

/// <summary>
/// GİB UBL-TR e-Arşiv Fatura için kullanılan XAdES/XMLDSig algoritma profilinin type-safe,
/// MERKEZÎ tanımı (bkz. Faz 2B.7/2B.7.1 görev - "algoritma URI'lerini kodun farklı yerlerine
/// dağılmış string sabitleri olarak bırakma"). Kaynaklar, güven seviyeleri ve gerekçe için
/// docs/e-belge-ubl-pdf-eposta-renderer-hazirlik-raporu.md, "Faz 2B.7.1 sonuç bölümü" içindeki
/// kanıt hiyerarşisi tablosuna bakınız - ÖZET (kaynak öncelik sırasıyla):
///
/// 1. GÜNCEL resmî GİB "e-Arşiv Kılavuzu" (Ağustos 2025, v1.18,
///    https://ebelge.gib.gov.tr/dosyalar/kilavuzlar/e-Arsiv_Teknik_Kilavuzu_V.1.18.pdf):
///    - Bölüm 4: e-Arşiv Raporu (aylık/dönemsel özet raporu - AYRI bir belge, İMZALADIĞIMIZ
///      fatura XML'i DEĞİL) "XADES-A standardı kullanılarak ... imzalanarak" gönderilir - bu,
///      dolaylı olarak e-Arşiv Raporu'nun İÇERDİĞİ BELGELERİN (fatura dahil) daha ZAYIF bir
///      seviyede (XAdES-BES) imzalanmış OLABİLECEĞİNİ ima eder (Rapor'un KENDİSİ arşivleme
///      zaman damgası taşıyan -A seviyesindedir, İÇERİĞİ taşımaz).
///    - Bölüm 7/8/9 (Serbest Meslek Makbuzu/Müstahsil Makbuzu/Adisyon - fatura ile YAPISAL
///      olarak ANALOG UBL-TR belgeleri): HER BİRİ AÇIKÇA "Bu veriler XADES-BES standardı
///      kullanılarak mali mühür/NES ile imzalanmalıdır" der - bölüm 6 (e-Arşiv Fatura
///      Standardı) metninde bu cümle BİREBİR tekrarlanmasa da, YAPISAL benzerlik VE ESKİ
///      e-Fatura kılavuzunun DOĞRUDAN ifadesiyle (aşağı) BİRLİKTE XAdES-BES'i güçlü biçimde
///      DOĞRULAR.
///    - Bölüm 5: SOAP/WSS GÜVENLİĞİ (Başkanlığa GÖNDERİM web servisi mesaj imzası) için
///      "canonicalization ... 'http://www.ws.org/2001/10/xml-exc-c14n#' tavsiye edilir" ve
///      "Signature metot algoritması 'http://www.w3.org/2001/04/xmldsig-more#rsa-sha256'
///      olmalıdır" der - **BU, FATURA XML'İNİN KENDİ İÇİNDEKİ XAdES İMZASI İÇİN DEĞİL, AYRI
///      BİR BAĞLAM (SOAP mesaj taşıma güvenliği) İÇİNDİR** - doğrudan delil olarak
///      KULLANILAMAZ, yalnız GİB'in genel ekosisteminde RSA-SHA256'nın YAYGIN kullanıldığına
///      dair DOLAYLI/zayıf bir sinyal olarak not düşülür.
///    - Bölüm 3.3.2.6 `ozetDeger`: "mali mühürle ... imzalanmış faturanın SHA-256 özet değeri
///      yazılacaktır" - faturanın (imzalı halinin) SHA-256 ile özetlendiğini DOĞRUDAN teyit
///      eder; XMLDSig `ds:DigestMethod` için KULLANILAN algoritmanın AYNISI olduğunu KESİN
///      olarak KANITLAMAZ (Rapor'un `ozetDeger`'i farklı bir hash hesaplaması OLABİLİR) ama
///      GİB ekosisteminde SHA-256'nın standart özet algoritması olduğuna dair orta-güçte bir
///      sinyaldir.
/// 2. Vendored, hash doğrulanmış GİB XSD/Schematron kuralları (EBelgeUblKuralSeti/):
///    - RSA-SHA1 YASAK: `schematron/UBL-TR_Common_Schematron.xml`, "SignatureMethodCheck".
///    - Tek `ds:Reference[@URI='']`, en fazla 1 Transform, zorunlu
///      `ds:KeyInfo/ds:X509Data/ds:X509Certificate`, zorunlu `xades:SigningTime`+
///      `xades:SigningCertificate` (v1.3.2, V2 DEĞİL): AYNI dosya, "XadesSignatureCheckForInvoice"/
///      "X509DataCheck" kuralları + `xades` namespace bağlaması (`http://uri.etsi.org/01903/v1.3.2#`).
///    - `cac:Signature/cbc:ID/@schemeID='VKN_TCKN'`, uzunluk 10/11, YORUMA ALINMIŞ ama AÇIKÇA
///      belgelenmiş `cac:DigitalSignatureAttachment/cac:ExternalReference/cbc:URI` deseni:
///      "SignatureCheck" kuralı.
///    - `xsdrt/common/UBL-CommonAggregateComponents-2.1.xsd`: `cac:Signature`'ın GERÇEK
///      `SignatureType`'ı (`cbc:ID`+`cac:SignatoryParty`+`cac:DigitalSignatureAttachment`,
///      ÜÇÜ DE ZORUNLU) - `sac:SignatureInformationType` (`UBL-SignatureAggregateComponents-2.1.xsd`)
///      İLE KARIŞTIRILMAMALIDIR (Faz 2B.7'de düzeltilen araştırma hatası).
///    - "SignatoryPartyPartyIdentificationCheck" (AKTİF, yoruma ALINMAMIŞ): `cac:SignatoryParty`
///      en az bir schemeID=VKN/TCKN `cac:PartyIdentification/cbc:ID` İÇERMELİDİR.
///    - `xsdrt/common/UBL-XAdESv132-2.1.xsd`: `SignedSignaturePropertiesType` sırası
///      (`SigningTime`, `SigningCertificate`, `SignaturePolicyIdentifier?`,
///      `SignatureProductionPlace?`, `SignerRole?` - SONUNCU ÜÇÜ MİNOCCURS=0, YANİ SEÇİMLİ) -
///      `xades:SignerRole`'ün şema düzeyinde SEÇİMLİ olduğunun DOĞRUDAN kanıtı.
///    - `xsdrt/common/UBL-xmldsig-core-schema-2.1.xsd`: `KeyInfoType` içeriği `ds:KeyValue`'yu
///      YALNIZ SEÇİMLİ bir alternatif olarak sunar; `X509DataCheck` schematron kuralı YALNIZ
///      `X509Certificate`'i ZORUNLU kılar, `ds:KeyValue`'dan HİÇ BAHSETMEZ.
/// 3. ESKİ resmî GİB "e-Fatura Uygulaması (Entegrasyon Kılavuzu)" (Haziran 2018, v1.10, s.17):
///    "Belgelerin imzalanmasında ve onaylanmasında en az XAdES-BES standardı ve enveloped
///    tekniği kullanılır." - tek başına en DOĞRUDAN XAdES-BES+enveloped ifadesi (GÜNCEL
///    kılavuzun 7/8/9. bölümleriyle ÇAPRAZ doğrulanmıştır, bkz. madde 1).
/// 4. TÜBİTAK KamuSM (GİB'in mali mühür sertifikaları için BY NAME atıfta bulunduğu resmî
///    e-imza altyapısı sağlayıcısı) ESYA imzalama SDK dokümantasyonu
///    (yazilim.kamusm.gov.tr/esya-api) - `DigestMethod.SHA_256`, `TransformType.ENVELOPED`,
///    boş-URI tüm-belge referansı, `ds:Signature/@Id`'nin faturanın `cbc:URI` değeriyle
///    eşleşmesi - GİB'İN KENDİ METNİ DEĞİLDİR, İKİNCİL/destekleyici kaynak.
/// 5. Yalnız YUKARIDAKİLERİN CEVAPLAMADIĞI noktalar için AÇIKÇA "mühendislik tercihi" (GİB
///    profilini İHLAL ETMEZ, yalnız .NET/W3C C14N'in kendi kısıtlarını çözer - bkz.
///    `SignedPropertiesTransformUri` alanı XML doc'u):
///    - `CanonicalizationAlgorithmUri` (`ds:SignedInfo` için, kapsayıcı/inclusive C14N 1.0):
///      hiçbir kaynakta POZİTİF teyit YOKTUR - W3C XMLDSig Core'un temel/varsayılan
///      algoritması VE .NET `SignedXml`'in varsayılanı olması nedeniyle seçilmiştir.
///    - `SignedPropertiesTransformUri` (Exclusive C14N): GİB kaynaklarının HİÇBİRİ
///      SignedProperties referansının transform algoritmasını AÇIKÇA belirtmez - Exclusive
///      C14N, .NET `SignedXml.AddObject` + kapsayıcı C14N kombinasyonunun ambient ad alanı
///      sızıntısı sorununu çözmek için GEREKLİ bir mühendislik kararıdır (bkz.
///      `EBelgeXmlImzalayici.ImzalaXml` içindeki ayrıntılı açıklama).
///
/// SignerRole/KeyValue KASITLI OLARAK EKLENMEMİŞTİR: `xades:SignerRole` VE `ds:KeyValue`,
/// vendored XSD'de AÇIKÇA SEÇİMLİ (minOccurs=0) olarak tanımlanmıştır; hiçbir schematron
/// kuralı (aktif ya da yoruma alınmış) ikisinden birini ZORUNLU KILMAZ; ne ESKİ ne GÜNCEL
/// resmî GİB kılavuzu `xades:SignerRole`'den veya `ds:KeyValue`'dan hiç BAHSETMEZ - yalnız
/// GİB'İN KENDİSİ OLMAYAN, ÜÇÜNCÜ TARAF bir entegratör örneği (KamuSM ESYA dokümantasyonunun
/// bir e-Fatura örneği) `xades:SignerRole/ClaimedRole=Supplier` KULLANIR, ama bu TEK BAŞINA
/// GİB gereksinimi SAYILMAZ. Sonuç: İKİSİ DE GEREKSİZ - EKLENMEZLER (bkz. görev md.3, "yalnız
/// 'mevcut testler geçti' gerekçesini kullanma" - buradaki gerekçe testler DEĞİL, ŞEMA
/// kardinalitesi + schematron sessizliği + resmî kılavuzların sessizliğidir).
/// </summary>
public sealed record EBelgeXadesProfili
{
    public required string ProfilKimligi { get; init; }

    public required string XadesNamespaceUri { get; init; }

    public required string CanonicalizationAlgorithmUri { get; init; }

    public required string SignatureAlgorithmUri { get; init; }

    public required string DigestAlgorithmUri { get; init; }

    public required string EnvelopedSignatureTransformUri { get; init; }

    /// <summary>
    /// xades:SignedProperties referansının transform URI'si (Exclusive XML Canonicalization) -
    /// bkz. sınıf düzeyi XML doc'unun 5. maddesi ("mühendislik tercihi" bölümü) - GİB tarafından
    /// AÇIKÇA belirtilmemiştir, .NET'in kendi SignedXml.AddObject kısıtına karşı gerekli bir
    /// çözümdür. Hem imzalayıcı (`EBelgeXmlImzalayici`) hem bağımsız doğrulayıcı
    /// (`EBelgeXmlImzaDogrulayici`) BU SABİTİ kullanır - iki yerde ayrı ayrı hardcode EDİLMEZ.
    /// </summary>
    public required string SignedPropertiesTransformUri { get; init; }

    public required string SignedPropertiesTypeUri { get; init; }

    /// <summary>
    /// Bu profilin PRODUCTION imzalama için ONAYLANDIĞINI belirtir (bkz. Faz 2B.7.1 görev md.2 -
    /// "kanıtlanmamış bir değeri sessiz üretim varsayılanı olarak kullanma"). `false` olan bir
    /// profille imzalama denemesi `EBelgeXadesProfiliOnaylanmadiException`
    /// (`EBELGE_SIGNING_PROFILE_NOT_APPROVED`) ile FAIL-CLOSED reddedilir - bu, "profil kararı
    /// yeterince kesin değilse üretim imzalaması sessizce devam ETMEMELİ" gereksinimini,
    /// GELECEKTEKİ bir profil revizyonu/düşürme kararı için de İŞLEVSEL (yalnız yorum satırı
    /// DEĞİL, test edilebilir) bırakır.
    /// </summary>
    public required bool Onayli { get; init; }

    /// <summary>
    /// GİB UBL-TR e-Arşiv Fatura için kullanılan, sabitlenmiş XAdES-BES profili (bkz. sınıf
    /// düzeyi XML doc'u - kaynaklar, güven seviyeleri ve gerekçe için). Testler HARİCİNDE tek
    /// profil budur; test-only zayıf/eski algoritmalı bir "profil" ASLA üretim kod yolunda
    /// kullanılmaz. `Onayli = true` - yukarıdaki kanıt hiyerarşisi (güncel e-Arşiv Kılavuzu
    /// v1.18 + vendored XSD/Schematron + eski e-Fatura Kılavuzu + KamuSM, ÇAPRAZ doğrulanmış)
    /// production kullanımı için YETERLİ bulunmuştur - bu, bilinçli, belgelenmiş bir
    /// mühendislik/ürün kararıdır (bkz. Faz 2B.7.1 raporu).
    /// </summary>
    public static readonly EBelgeXadesProfili GibUblTr = new()
    {
        ProfilKimligi = "GIB-EARSIV-UBL-TR-XADES-BES/1.18/1.0",
        XadesNamespaceUri = "http://uri.etsi.org/01903/v1.3.2#",
        CanonicalizationAlgorithmUri = "http://www.w3.org/TR/2001/REC-xml-c14n-20010315",
        SignatureAlgorithmUri = "http://www.w3.org/2001/04/xmldsig-more#rsa-sha256",
        DigestAlgorithmUri = "http://www.w3.org/2001/04/xmlenc#sha256",
        EnvelopedSignatureTransformUri = "http://www.w3.org/2000/09/xmldsig#enveloped-signature",
        SignedPropertiesTransformUri = "http://www.w3.org/2001/10/xml-exc-c14n#",
        SignedPropertiesTypeUri = "http://uri.etsi.org/01903#SignedProperties",
        Onayli = true,
    };

    /// <summary>Resmî GİB kural setinin (bkz. UBL-TR_Common_Schematron.xml, "SignatureMethodCheck") KESİNLİKLE yasakladığı tek algoritma URI'si - hiçbir profil bunu ASLA kullanmamalıdır.</summary>
    public const string YasakliRsaSha1Uri = "http://www.w3.org/2000/09/xmldsig#rsa-sha1";
}
