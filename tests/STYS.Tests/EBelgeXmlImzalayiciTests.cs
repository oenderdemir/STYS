using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Xml;
using Microsoft.Extensions.Logging.Abstractions;
using STYS.Muhasebe.SatisBelgeleri;
using STYS.Muhasebe.SatisBelgeleri.Services;
using Xunit;

namespace STYS.Tests;

/// <summary>
/// Faz 2B.7 - GERÇEK RSA test sertifikasıyla GERÇEK XAdES-BES enveloped imza üretimi ve BAĞIMSIZ
/// doğrulamasını test eder. Gerçek sidecar KULLANILMAZ (yalnız Unsigned UBL örneğini üretmek için
/// GERÇEK renderer kullanılır, bu sınıf başına BİR KEZ - bkz. InitializeAsync); imza motoru/
/// doğrulayıcı testleri DB veya sidecar'a HİÇ İHTİYAÇ DUYMAZ.
/// </summary>
public sealed class EBelgeXmlImzalayiciTests : IAsyncLifetime
{
    private ImmutableArray<byte> _unsignedUblUtf8;
    private string _unsignedUblSha256 = string.Empty;

    public async Task InitializeAsync()
    {
        var kuralSeti = EBelgeUblRendererTestVerisi.KuralSetiYukle();
        var xsdValidator = new EBelgeUblXsdValidator(kuralSeti);
        var schematronValidatorStub = new NeverCalledSchematronValidator();
        var renderer = new EBelgeUblRenderer(kuralSeti, xsdValidator, schematronValidatorStub);

        // Schematron doğrulaması BİLEREK atlanır (bu dosyadaki testler yalnız imza motorunu
        // hedefler) - bu yüzden BuildXml + XSD aşamasını tetikleyip Schematron'dan ÖNCE
        // exception fırlatacak bir stub kullanılır; gerçek unsigned XML'i doğrudan renderer'ın
        // dahili XML üretiminden DEĞİL, önceden bilinen gerçek bir üretimden almak yerine burada
        // NeverCalledSchematronValidator, "her zaman geçerli" dönerek gerçek XML akışını tamamlar.
        var snapshot = EBelgeUblRendererTestVerisi.GecerliSnapshot();
        var sonuc = await renderer.RenderAsync(snapshot, CancellationToken.None);

        _unsignedUblUtf8 = sonuc.UnsignedUblUtf8;
        _unsignedUblSha256 = sonuc.UnsignedUblSha256;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private sealed class NeverCalledSchematronValidator : IEBelgeSchematronValidator
    {
        public Task<EBelgeSchematronValidationResult> ValidateAsync(ImmutableArray<byte> xmlUtf8, string ruleSetId, CancellationToken cancellationToken)
            => Task.FromResult(new EBelgeSchematronValidationResult(true, Array.Empty<EBelgeSchematronViolation>()));
    }

    private static EBelgeXmlImzalayici CreateImzalayici(IEBelgeImzaKimligiSaglayici saglayici, IEBelgeSertifikaGuvenValidatoru? guvenValidatoru = null)
        => new(saglayici, guvenValidatoru ?? new EBelgeTestSertifikaGuvenPolicy());

    private static EBelgeXmlImzaDogrulayici CreateDogrulayici() => new();

    private EBelgeXmlImzaTalebi CreateTalep(DateTime? imzalamaZamaniUtc = null) => new()
    {
        KurumId = 1,
        UnsignedUblUtf8 = _unsignedUblUtf8,
        UnsignedUblSha256 = _unsignedUblSha256,
        RuleSetId = "test-kural-seti",
        EBelgeUuid = Guid.NewGuid().ToString("D"),
        ImzalamaZamaniUtc = imzalamaZamaniUtc ?? DateTime.UtcNow,
    };

    // ---- Profil ve XML yapısı (senaryo 1-8) ----

    [Fact]
    public async Task ImzaliXmlBeklenenNamespaceVeYapilariIcerir()
    {
        using var saglayici = new EBelgeTestSertifikaSaglayici();
        var sonuc = await CreateImzalayici(saglayici).ImzalaAsync(CreateTalep(), CancellationToken.None);

        var doc = LoadXml(sonuc.SignedUblUtf8);
        var nsmgr = CreateNsManager(doc);

        var root = doc.DocumentElement!;
        Assert.Equal("Invoice", root.LocalName);

        // ext:UBLExtensions, KÖK'ün İLK çocuğu olmalıdır (senaryo 2).
        Assert.Equal("UBLExtensions", root.FirstChild!.LocalName);

        var signatureNodes = doc.SelectNodes("//ds:Signature", nsmgr)!;
        Assert.Equal(1, signatureNodes.Count); // senaryo 5

        var qualifyingPropsNodes = doc.SelectNodes("//xades:QualifyingProperties", nsmgr)!;
        Assert.Equal(1, qualifyingPropsNodes.Count); // senaryo 6

        // cac:Signature (senaryo 4) - gerçek imzayı İÇERMEMELİDİR (görev md.7).
        var cacSignature = doc.SelectSingleNode("//cac:Signature", nsmgr);
        Assert.NotNull(cacSignature);
        Assert.Null(cacSignature!.SelectSingleNode("ds:Signature", nsmgr));
        Assert.Equal("VKN_TCKN", cacSignature.SelectSingleNode("cbc:ID/@schemeID", nsmgr)!.Value);

        // Signature/SignedProperties ID-URI bağları doğru olmalıdır (senaryo 7).
        var signatureId = signatureNodes[0]!.Attributes!["Id"]!.Value;
        var signedPropsId = doc.SelectSingleNode("//xades:SignedProperties/@Id", nsmgr)!.Value;
        var target = doc.SelectSingleNode("//xades:QualifyingProperties/@Target", nsmgr)!.Value;
        Assert.Equal("#" + signatureId, target);

        var signedPropsRefUri = doc.SelectSingleNode($"//ds:Reference[@Type]/@URI", nsmgr)!.Value;
        Assert.Equal("#" + signedPropsId, signedPropsRefUri);
    }

    [Fact]
    public async Task YinelenenIdIcerenUnsignedXmlReddedilir()
    {
        // Senaryo 8: signature-wrapping sertleştirmesi - imzalama ÖNCESİNDE zaten bir "Id"
        // niteliği taşıyan XML KASITLI OLARAK üretilir.
        var xmlText = System.Text.Encoding.UTF8.GetString(_unsignedUblUtf8.AsSpan());
        var tamperedXml = xmlText.Replace("<cbc:UBLVersionID>", "<cbc:UBLVersionID Id=\"sahte-id\">");
        var tamperedBytes = ImmutableArray.Create(System.Text.Encoding.UTF8.GetBytes(tamperedXml));
        var tamperedHash = Convert.ToHexString(SHA256.HashData(tamperedBytes.AsSpan()));

        using var saglayici = new EBelgeTestSertifikaSaglayici();
        var talep = CreateTalep() with { UnsignedUblUtf8 = tamperedBytes, UnsignedUblSha256 = tamperedHash };

        var ex = await Assert.ThrowsAsync<EBelgeXmlImzaKaliciHataException>(() => CreateImzalayici(saglayici).ImzalaAsync(talep, CancellationToken.None));
        Assert.Equal(EBelgeXmlImzaHataKodlari.YinelenenXmlId, ex.HataKodu);
    }

    // ---- Gerçek kriptografik imza (senaryo 9-20) ----

    [Fact]
    public async Task GercekSertifikaIleGercekImzaUretilirVeBagimsizDogrulamaBasarili()
    {
        using var saglayici = new EBelgeTestSertifikaSaglayici();
        var sonuc = await CreateImzalayici(saglayici).ImzalaAsync(CreateTalep(), CancellationToken.None);

        var dogrulama = await CreateDogrulayici().DogrulaAsync(sonuc.SignedUblUtf8, CancellationToken.None);

        Assert.True(dogrulama.GecerliMi, $"{dogrulama.HataKodu}: {dogrulama.HataMesaji}");
        Assert.NotNull(dogrulama.SertifikaSha256ParmakIzi);
        Assert.Equal(sonuc.SertifikaSha256ParmakIzi, dogrulama.SertifikaSha256ParmakIzi, ignoreCase: true);
    }

    [Fact]
    public async Task TekByteDegisikligiImzayiBozar()
    {
        using var saglayici = new EBelgeTestSertifikaSaglayici();
        var sonuc = await CreateImzalayici(saglayici).ImzalaAsync(CreateTalep(), CancellationToken.None);

        // ds:SignatureValue İÇERİĞİNDEKİ (saf base64 metin, XML YAPISI RİSKİ TAŞIMAYAN) bir
        // karakteri bozar - rastgele bir byte (ör. bir etiket adı) bozmak XML'i YAPISAL olarak
        // geçersiz kılabilir (ayrı bir XmlException'a yol açar), bu test YALNIZ kriptografik
        // tamper-detection'ı hedefler.
        var xmlText = System.Text.Encoding.UTF8.GetString(sonuc.SignedUblUtf8.AsSpan());
        var match = System.Text.RegularExpressions.Regex.Match(xmlText, "<(?:\\w+:)?SignatureValue>([^<]+)</(?:\\w+:)?SignatureValue>");
        Assert.True(match.Success);
        var orijinalDeger = match.Groups[1].Value;
        var bozukKarakter = orijinalDeger[0] == 'A' ? 'B' : 'A';
        var bozukDeger = bozukKarakter + orijinalDeger[1..];
        var bozulmusXml = xmlText.Replace(match.Value, match.Value.Replace(orijinalDeger, bozukDeger));
        var bozulmusBytes = System.Text.Encoding.UTF8.GetBytes(bozulmusXml);

        var dogrulama = await CreateDogrulayici().DogrulaAsync(ImmutableArray.Create(bozulmusBytes), CancellationToken.None);

        Assert.False(dogrulama.GecerliMi);
    }

    [Fact]
    public async Task SignedPropertiesDegisikligiImzayiBozar()
    {
        using var saglayici = new EBelgeTestSertifikaSaglayici();
        var sonuc = await CreateImzalayici(saglayici).ImzalaAsync(CreateTalep(), CancellationToken.None);

        var xmlText = System.Text.Encoding.UTF8.GetString(sonuc.SignedUblUtf8.AsSpan());
        // SigningTime değerini (SignedProperties'in İÇİNDE) değiştir - digest artık uyuşmayacaktır.
        var tampered = System.Text.RegularExpressions.Regex.Replace(
            xmlText, "<xades:SigningTime>[^<]+</xades:SigningTime>", "<xades:SigningTime>2099-01-01T00:00:00Z</xades:SigningTime>");
        Assert.NotEqual(xmlText, tampered);

        var dogrulama = await CreateDogrulayici().DogrulaAsync(ImmutableArray.Create(System.Text.Encoding.UTF8.GetBytes(tampered)), CancellationToken.None);

        Assert.False(dogrulama.GecerliMi);
    }

    [Fact]
    public async Task BaskaSertifikaPublicKeyIleDogrulamaBasarisizOlur()
    {
        using var saglayiciA = new EBelgeTestSertifikaSaglayici();
        using var saglayiciB = new EBelgeTestSertifikaSaglayici();

        var sonuc = await CreateImzalayici(saglayiciA).ImzalaAsync(CreateTalep(), CancellationToken.None);

        var farkliSertifika = (await saglayiciB.GetAsync(1, CancellationToken.None)).Sertifika;
        var farkliSertifikaBase64 = Convert.ToBase64String(farkliSertifika.RawData);

        var xmlText = System.Text.Encoding.UTF8.GetString(sonuc.SignedUblUtf8.AsSpan());
        var certMatch = System.Text.RegularExpressions.Regex.Match(xmlText, "<(?:\\w+:)?X509Certificate>([^<]+)</(?:\\w+:)?X509Certificate>");
        Assert.True(certMatch.Success);
        var tampered = xmlText.Replace(certMatch.Value, certMatch.Value.Replace(certMatch.Groups[1].Value, farkliSertifikaBase64));

        var dogrulama = await CreateDogrulayici().DogrulaAsync(ImmutableArray.Create(System.Text.Encoding.UTF8.GetBytes(tampered)), CancellationToken.None);

        Assert.False(dogrulama.GecerliMi);
    }

    [Fact]
    public async Task PrivateKeyIcermeyenSertifikaReddedilir()
    {
        using var kaynak = new EBelgeTestSertifikaSaglayici();
        var saglayici = new EBelgePrivateKeySizSertifikaSaglayici(kaynak);

        var ex = await Assert.ThrowsAsync<EBelgeXmlImzaKaliciHataException>(
            () => CreateImzalayici(saglayici).ImzalaAsync(CreateTalep(), CancellationToken.None));

        Assert.Equal(EBelgeXmlImzaHataKodlari.PrivateKeyYok, ex.HataKodu);
    }

    [Fact]
    public async Task SuresiDolmusSertifikaReddedilir()
    {
        using var saglayici = new EBelgeTestSertifikaSaglayici(
            notBefore: DateTimeOffset.UtcNow.AddDays(-30),
            notAfter: DateTimeOffset.UtcNow.AddDays(-1));

        var ex = await Assert.ThrowsAsync<EBelgeXmlImzaKaliciHataException>(
            () => CreateImzalayici(saglayici).ImzalaAsync(CreateTalep(), CancellationToken.None));

        Assert.Equal(EBelgeXmlImzaHataKodlari.SertifikaGecersizAralik, ex.HataKodu);
    }

    [Fact]
    public async Task HenuzGecerliOlmayanSertifikaReddedilir()
    {
        using var saglayici = new EBelgeTestSertifikaSaglayici(
            notBefore: DateTimeOffset.UtcNow.AddDays(10),
            notAfter: DateTimeOffset.UtcNow.AddDays(400));

        var ex = await Assert.ThrowsAsync<EBelgeXmlImzaKaliciHataException>(
            () => CreateImzalayici(saglayici).ImzalaAsync(CreateTalep(), CancellationToken.None));

        Assert.Equal(EBelgeXmlImzaHataKodlari.SertifikaGecersizAralik, ex.HataKodu);
    }

    [Fact]
    public async Task GuvensizSertifikaReddedilir()
    {
        using var saglayici = new EBelgeTestSertifikaSaglayici();
        var alwaysUntrusted = new AlwaysUntrustedPolicy();

        var ex = await Assert.ThrowsAsync<EBelgeXmlImzaKaliciHataException>(
            () => CreateImzalayici(saglayici, alwaysUntrusted).ImzalaAsync(CreateTalep(), CancellationToken.None));

        Assert.Equal(EBelgeXmlImzaHataKodlari.SertifikaGuvenilirDegil, ex.HataKodu);
    }

    private sealed class AlwaysUntrustedPolicy : IEBelgeSertifikaGuvenValidatoru
    {
        public Task<EBelgeSertifikaGuvenSonucu> DogrulaAsync(System.Security.Cryptography.X509Certificates.X509Certificate2 sertifika, CancellationToken cancellationToken)
            => Task.FromResult(EBelgeSertifikaGuvenSonucu.Guvensiz("test-red"));
    }

    // ---- Determinizm ve zaman (senaryo 21-24) ----

    [Fact]
    public async Task AyniGirdilerVeSabitZamanIleSonucByteBirebirAynidir()
    {
        // RSA-PKCS1 imzalama DETERMİNİSTİKTİR (bkz. görev md.21 istisnası - algoritma doğası
        // gereği nondeterministik İSE byte eşitliği zorunlu değildir; RSASignaturePadding.Pkcs1
        // BU İSTİSNAYA GİRMEZ, deterministik bir imzadır).
        using var saglayici = new EBelgeTestSertifikaSaglayici();
        var sabitZaman = DateTime.UtcNow.AddDays(10);

        var sonuc1 = await CreateImzalayici(saglayici).ImzalaAsync(CreateTalep(sabitZaman), CancellationToken.None);
        var sonuc2 = await CreateImzalayici(saglayici).ImzalaAsync(CreateTalep(sabitZaman), CancellationToken.None);

        Assert.True(sonuc1.SignedUblUtf8.AsSpan().SequenceEqual(sonuc2.SignedUblUtf8.AsSpan()));
        Assert.Equal(sonuc1.SignedUblSha256, sonuc2.SignedUblSha256);
    }

    [Fact]
    public async Task SigningTimeTimeProviderdanGelirVeCultureBagimsizYazilir()
    {
        using var saglayici = new EBelgeTestSertifikaSaglayici();
        var sabitZaman = new DateTime(2026, 12, 25, 18, 45, 30, DateTimeKind.Utc);

        var eskiCulture = System.Threading.Thread.CurrentThread.CurrentCulture;
        try
        {
            System.Threading.Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("tr-TR");
            var sonuc = await CreateImzalayici(saglayici).ImzalaAsync(CreateTalep(sabitZaman), CancellationToken.None);

            var doc = LoadXml(sonuc.SignedUblUtf8);
            var nsmgr = CreateNsManager(doc);
            var signingTimeText = doc.SelectSingleNode("//xades:SigningTime", nsmgr)!.InnerText;

            Assert.Equal("2026-12-25T18:45:30Z", signingTimeText);
        }
        finally
        {
            System.Threading.Thread.CurrentThread.CurrentCulture = eskiCulture;
        }
    }

    // ---- Hash zinciri (md.15) ----

    [Fact]
    public async Task KaynakHashUyusmazsaReddedilir()
    {
        using var saglayici = new EBelgeTestSertifikaSaglayici();
        var talep = CreateTalep() with { UnsignedUblSha256 = new string('0', 64) };

        var ex = await Assert.ThrowsAsync<EBelgeXmlImzaKaliciHataException>(() => CreateImzalayici(saglayici).ImzalaAsync(talep, CancellationToken.None));
        Assert.Equal(EBelgeXmlImzaHataKodlari.KaynakHashUyumsuz, ex.HataKodu);
    }

    [Fact]
    public async Task SonucHashBagimsizDogrulanabilir()
    {
        using var saglayici = new EBelgeTestSertifikaSaglayici();
        var sonuc = await CreateImzalayici(saglayici).ImzalaAsync(CreateTalep(), CancellationToken.None);

        var bagimsizHash = Convert.ToHexString(SHA256.HashData(sonuc.SignedUblUtf8.AsSpan()));
        Assert.Equal(bagimsizHash, sonuc.SignedUblSha256, ignoreCase: true);
    }

    // ---- Üretim fail-closed sağlayıcılar (md.5, md.10) ----

    [Fact]
    public async Task YapilandirilmamisUretimSaglayicisiFailClosedFirlatir()
    {
        var saglayici = new EBelgeImzaKimligiYapilandirilmadiSaglayici();
        var imzalayici = new EBelgeXmlImzalayici(saglayici, new EBelgeSertifikaGuvenValidatoruYapilandirilmadi());

        await Assert.ThrowsAsync<EBelgeSigningProviderNotConfiguredException>(
            () => imzalayici.ImzalaAsync(CreateTalep(), CancellationToken.None));
    }

    [Fact]
    public async Task YapilandirilmamisGuvenValidatoruDaimaGuvensizDoner()
    {
        var validator = new EBelgeSertifikaGuvenValidatoruYapilandirilmadi();
        using var saglayici = new EBelgeTestSertifikaSaglayici();
        var kimlik = await saglayici.GetAsync(1, CancellationToken.None);

        var sonuc = await validator.DogrulaAsync(kimlik.Sertifika, CancellationToken.None);

        Assert.False(sonuc.GuvenilirMi);
        kimlik.Dispose();
    }

    // ---- Bağımsız doğrulayıcı - yapısal sertleştirme ----

    [Fact]
    public async Task DuzImzasizXmlBagimsizDogrulamadanGecemez()
    {
        var dogrulama = await CreateDogrulayici().DogrulaAsync(_unsignedUblUtf8, CancellationToken.None);
        Assert.False(dogrulama.GecerliMi);
    }

    // ---- Faz 2B.7.1 görev md.4/md.8: doğrulayıcının genişletilmiş profil/URI kontrolleri ----

    [Fact]
    public async Task QualifyingPropertiesTargetYanlissaDogrulamaBasarisizOlur()
    {
        using var saglayici = new EBelgeTestSertifikaSaglayici();
        var sonuc = await CreateImzalayici(saglayici).ImzalaAsync(CreateTalep(), CancellationToken.None);

        var xmlText = System.Text.Encoding.UTF8.GetString(sonuc.SignedUblUtf8.AsSpan());
        var tampered = System.Text.RegularExpressions.Regex.Replace(xmlText, "Target=\"#[^\"]+\"", "Target=\"#yanlis-id\"");
        Assert.NotEqual(xmlText, tampered);

        var dogrulama = await CreateDogrulayici().DogrulaAsync(ImmutableArray.Create(System.Text.Encoding.UTF8.GetBytes(tampered)), CancellationToken.None);
        Assert.False(dogrulama.GecerliMi);
    }

    [Fact]
    public async Task IkinciQualifyingPropertiesEklenirseReddedilir()
    {
        using var saglayici = new EBelgeTestSertifikaSaglayici();
        var sonuc = await CreateImzalayici(saglayici).ImzalaAsync(CreateTalep(), CancellationToken.None);

        var doc = LoadXml(sonuc.SignedUblUtf8);
        var nsmgr = CreateNsManager(doc);

        var qualifyingProperties = (XmlElement)doc.SelectSingleNode("//xades:QualifyingProperties", nsmgr)!;
        var klon = (XmlElement)qualifyingProperties.CloneNode(deep: true);
        // Klonun kendi xades:SignedProperties/@Id'sini DEĞİŞTİR - aksi halde bu, YinelenenXmlId
        // kontrolünü (daha ÖNCE) tetikler; bu test ÖZELLİKLE QualifyingProperties SAYISINI hedefler.
        var klonSignedProps = (XmlElement)klon.SelectSingleNode("xades:SignedProperties", nsmgr)!;
        klonSignedProps.SetAttribute("Id", "SignedProperties-2");
        qualifyingProperties.ParentNode!.AppendChild(klon);

        var dogrulama = await CreateDogrulayici().DogrulaAsync(ImmutableArray.Create(SaveXmlBytes(doc)), CancellationToken.None);
        Assert.False(dogrulama.GecerliMi);
    }

    [Fact]
    public async Task SignedPropertiesUriYanlisNodeYonelirseReddedilir()
    {
        using var saglayici = new EBelgeTestSertifikaSaglayici();
        var sonuc = await CreateImzalayici(saglayici).ImzalaAsync(CreateTalep(), CancellationToken.None);

        var doc = LoadXml(sonuc.SignedUblUtf8);
        var nsmgr = CreateNsManager(doc);

        var signatureId = ((XmlElement)doc.SelectSingleNode("//ds:Signature", nsmgr)!).GetAttribute("Id");
        // SignedProperties referansını, VAR OLAN ama xades:SignedProperties OLMAYAN bir hedefe
        // (ds:Signature'ın KENDİSİ) yönlendirir.
        var signedPropsReferansi = (XmlElement)doc.SelectSingleNode("//ds:Reference[@Type]", nsmgr)!;
        signedPropsReferansi.SetAttribute("URI", "#" + signatureId);

        var dogrulama = await CreateDogrulayici().DogrulaAsync(ImmutableArray.Create(SaveXmlBytes(doc)), CancellationToken.None);
        Assert.False(dogrulama.GecerliMi);
    }

    [Fact]
    public async Task BelgeReferansiDigestMethodDegistirilirseReddedilir()
    {
        using var saglayici = new EBelgeTestSertifikaSaglayici();
        var sonuc = await CreateImzalayici(saglayici).ImzalaAsync(CreateTalep(), CancellationToken.None);

        var doc = LoadXml(sonuc.SignedUblUtf8);
        var nsmgr = CreateNsManager(doc);

        var digestMethod = (XmlElement)doc.SelectSingleNode("//ds:Reference[@URI='']/ds:DigestMethod", nsmgr)!;
        digestMethod.SetAttribute("Algorithm", "http://www.w3.org/2000/09/xmldsig#sha1");

        var dogrulama = await CreateDogrulayici().DogrulaAsync(ImmutableArray.Create(SaveXmlBytes(doc)), CancellationToken.None);
        Assert.False(dogrulama.GecerliMi);
    }

    [Fact]
    public async Task SignedPropertiesDigestMethodDegistirilirseReddedilir()
    {
        using var saglayici = new EBelgeTestSertifikaSaglayici();
        var sonuc = await CreateImzalayici(saglayici).ImzalaAsync(CreateTalep(), CancellationToken.None);

        var doc = LoadXml(sonuc.SignedUblUtf8);
        var nsmgr = CreateNsManager(doc);

        var digestMethod = (XmlElement)doc.SelectSingleNode("//ds:Reference[@Type]/ds:DigestMethod", nsmgr)!;
        digestMethod.SetAttribute("Algorithm", "http://www.w3.org/2000/09/xmldsig#sha1");

        var dogrulama = await CreateDogrulayici().DogrulaAsync(ImmutableArray.Create(SaveXmlBytes(doc)), CancellationToken.None);
        Assert.False(dogrulama.GecerliMi);
    }

    [Fact]
    public async Task BelgeReferansiTransformUriDegistirilirseReddedilir()
    {
        using var saglayici = new EBelgeTestSertifikaSaglayici();
        var sonuc = await CreateImzalayici(saglayici).ImzalaAsync(CreateTalep(), CancellationToken.None);

        var doc = LoadXml(sonuc.SignedUblUtf8);
        var nsmgr = CreateNsManager(doc);

        var transform = (XmlElement)doc.SelectSingleNode("//ds:Reference[@URI='']/ds:Transforms/ds:Transform", nsmgr)!;
        transform.SetAttribute("Algorithm", "http://www.w3.org/TR/2001/REC-xml-c14n-20010315");

        var dogrulama = await CreateDogrulayici().DogrulaAsync(ImmutableArray.Create(SaveXmlBytes(doc)), CancellationToken.None);
        Assert.False(dogrulama.GecerliMi);
    }

    [Fact]
    public async Task SignedPropertiesReferansiTransformUriDegistirilirseReddedilir()
    {
        using var saglayici = new EBelgeTestSertifikaSaglayici();
        var sonuc = await CreateImzalayici(saglayici).ImzalaAsync(CreateTalep(), CancellationToken.None);

        var doc = LoadXml(sonuc.SignedUblUtf8);
        var nsmgr = CreateNsManager(doc);

        var transform = (XmlElement)doc.SelectSingleNode("//ds:Reference[@Type]/ds:Transforms/ds:Transform", nsmgr)!;
        transform.SetAttribute("Algorithm", "http://www.w3.org/TR/2001/REC-xml-c14n-20010315");

        var dogrulama = await CreateDogrulayici().DogrulaAsync(ImmutableArray.Create(SaveXmlBytes(doc)), CancellationToken.None);
        Assert.False(dogrulama.GecerliMi);
    }

    [Fact]
    public async Task EkTransformEklenirseReddedilir()
    {
        using var saglayici = new EBelgeTestSertifikaSaglayici();
        var sonuc = await CreateImzalayici(saglayici).ImzalaAsync(CreateTalep(), CancellationToken.None);

        var doc = LoadXml(sonuc.SignedUblUtf8);
        var nsmgr = CreateNsManager(doc);

        var transforms = (XmlElement)doc.SelectSingleNode("//ds:Reference[@URI='']/ds:Transforms", nsmgr)!;
        var ekTransform = doc.CreateElement("ds", "Transform", "http://www.w3.org/2000/09/xmldsig#");
        ekTransform.SetAttribute("Algorithm", "http://www.w3.org/TR/2001/REC-xml-c14n-20010315");
        transforms.AppendChild(ekTransform);

        var dogrulama = await CreateDogrulayici().DogrulaAsync(ImmutableArray.Create(SaveXmlBytes(doc)), CancellationToken.None);
        Assert.False(dogrulama.GecerliMi);
    }

    [Fact]
    public async Task IssuerAdiDegistirilirseReddedilir()
    {
        using var saglayici = new EBelgeTestSertifikaSaglayici();
        var sonuc = await CreateImzalayici(saglayici).ImzalaAsync(CreateTalep(), CancellationToken.None);

        var doc = LoadXml(sonuc.SignedUblUtf8);
        var nsmgr = CreateNsManager(doc);

        var issuerName = (XmlElement)doc.SelectSingleNode("//xades:IssuerSerial/ds:X509IssuerName", nsmgr)!;
        issuerName.InnerText = "CN=Baska Bir Issuer, O=Sahte, C=TR";

        var dogrulama = await CreateDogrulayici().DogrulaAsync(ImmutableArray.Create(SaveXmlBytes(doc)), CancellationToken.None);
        Assert.False(dogrulama.GecerliMi);
    }

    [Fact]
    public async Task SerialNumberDegistirilirseReddedilir()
    {
        using var saglayici = new EBelgeTestSertifikaSaglayici();
        var sonuc = await CreateImzalayici(saglayici).ImzalaAsync(CreateTalep(), CancellationToken.None);

        var doc = LoadXml(sonuc.SignedUblUtf8);
        var nsmgr = CreateNsManager(doc);

        var serialNumber = (XmlElement)doc.SelectSingleNode("//xades:IssuerSerial/ds:X509SerialNumber", nsmgr)!;
        serialNumber.InnerText = (System.Numerics.BigInteger.Parse(serialNumber.InnerText) + 1).ToString();

        var dogrulama = await CreateDogrulayici().DogrulaAsync(ImmutableArray.Create(SaveXmlBytes(doc)), CancellationToken.None);
        Assert.False(dogrulama.GecerliMi);
    }

    [Fact]
    public async Task CacSignatureUriYanlisSignatureIdYonelirseReddedilir()
    {
        using var saglayici = new EBelgeTestSertifikaSaglayici();
        var sonuc = await CreateImzalayici(saglayici).ImzalaAsync(CreateTalep(), CancellationToken.None);

        var doc = LoadXml(sonuc.SignedUblUtf8);
        var nsmgr = CreateNsManager(doc);

        var uri = (XmlElement)doc.SelectSingleNode("//cac:Signature/cac:DigitalSignatureAttachment/cac:ExternalReference/cbc:URI", nsmgr)!;
        uri.InnerText = "#yanlis-signature-id";

        var dogrulama = await CreateDogrulayici().DogrulaAsync(ImmutableArray.Create(SaveXmlBytes(doc)), CancellationToken.None);
        Assert.False(dogrulama.GecerliMi);
    }

    [Fact]
    public async Task DsSignatureBeklenenExtensionContentDisindaBulunursaReddedilir()
    {
        using var saglayici = new EBelgeTestSertifikaSaglayici();
        var sonuc = await CreateImzalayici(saglayici).ImzalaAsync(CreateTalep(), CancellationToken.None);

        var doc = LoadXml(sonuc.SignedUblUtf8);
        var nsmgr = CreateNsManager(doc);

        var signature = (XmlElement)doc.SelectSingleNode("//ds:Signature", nsmgr)!;
        signature.ParentNode!.RemoveChild(signature);
        // ds:Signature'ı, beklenen ext:ExtensionContent yerine DOĞRUDAN kök elemana taşır.
        doc.DocumentElement!.AppendChild(signature);

        var dogrulama = await CreateDogrulayici().DogrulaAsync(ImmutableArray.Create(SaveXmlBytes(doc)), CancellationToken.None);
        Assert.False(dogrulama.GecerliMi);
    }

    [Fact]
    public async Task SignerRoleVeKeyValueOlmadanDaImzaGecerliKabulEdilir()
    {
        // Faz 2B.7.1 görev md.3 kararı: xades:SignerRole VE ds:KeyValue, vendored XSD'de SEÇİMLİ
        // (minOccurs=0) olarak tanımlanmıştır; hiçbir aktif/yoruma alınmış schematron kuralı
        // ikisinden birini ZORUNLU KILMAZ; ne eski (2018) ne güncel (Ağustos 2025, v1.18) resmî
        // GİB kılavuzu bunlardan HİÇ BAHSETMEZ - bu yüzden İKİSİ DE EKLENMEZ (bkz.
        // EBelgeXadesProfili.cs sınıf düzeyi XML doc'u). Bu test, GERÇEK üretim çıktısının
        // (SignerRole/KeyValue içermeyen) bağımsız doğrulamadan BAŞARIYLA geçtiğini - yani bu
        // kararın doğrulayıcı tarafında YANLIŞLIKLA bir "zorunlu" varsayımına dönüşmediğini -
        // teyit eder.
        using var saglayici = new EBelgeTestSertifikaSaglayici();
        var sonuc = await CreateImzalayici(saglayici).ImzalaAsync(CreateTalep(), CancellationToken.None);

        var xmlText = System.Text.Encoding.UTF8.GetString(sonuc.SignedUblUtf8.AsSpan());
        Assert.DoesNotContain("SignerRole", xmlText, StringComparison.Ordinal);
        Assert.DoesNotContain("KeyValue", xmlText, StringComparison.Ordinal);

        var dogrulama = await CreateDogrulayici().DogrulaAsync(sonuc.SignedUblUtf8, CancellationToken.None);
        Assert.True(dogrulama.GecerliMi, $"{dogrulama.HataKodu}: {dogrulama.HataMesaji}");
    }

    private static XmlDocument LoadXml(ImmutableArray<byte> xmlUtf8)
    {
        var doc = new XmlDocument { XmlResolver = null };
        using var ms = new MemoryStream(xmlUtf8.ToArray());
        using var reader = XmlReader.Create(ms, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });
        doc.Load(reader);
        return doc;
    }

    private static XmlNamespaceManager CreateNsManager(XmlDocument doc)
    {
        var nsmgr = new XmlNamespaceManager(doc.NameTable);
        nsmgr.AddNamespace("ext", "urn:oasis:names:specification:ubl:schema:xsd:CommonExtensionComponents-2");
        nsmgr.AddNamespace("cac", "urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2");
        nsmgr.AddNamespace("cbc", "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2");
        nsmgr.AddNamespace("ds", "http://www.w3.org/2000/09/xmldsig#");
        nsmgr.AddNamespace("xades", "http://uri.etsi.org/01903/v1.3.2#");
        return nsmgr;
    }

    /// <summary>DOM üzerinde kurcalanmış bir belgeyi, imzalayıcının kendi serialize ayarlarıyla (UTF-8, BOM'suz) tutarlı biçimde byte'lara döker - yalnız BU dosyadaki kurcalama testleri İÇİNDİR.</summary>
    private static byte[] SaveXmlBytes(XmlDocument doc)
    {
        using var ms = new MemoryStream();
        var settings = new XmlWriterSettings
        {
            Encoding = new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            Indent = false,
            NewLineHandling = NewLineHandling.None,
            OmitXmlDeclaration = false,
            ConformanceLevel = ConformanceLevel.Document,
        };
        using (var writer = XmlWriter.Create(ms, settings))
        {
            doc.Save(writer);
        }

        return ms.ToArray();
    }
}
