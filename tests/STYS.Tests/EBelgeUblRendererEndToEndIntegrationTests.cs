using System.Collections.Immutable;
using STYS.Muhasebe.SatisBelgeleri;
using Xunit;

namespace STYS.Tests;

/// <summary>
/// Faz 2B.5 TAM uçtan uca akış: gerçek renderer + gerçek yerel XSD doğrulayıcı + GERÇEK Java
/// Saxon-HE 13.0 sidecar süreci (bkz. SchematronSidecarProcessFixture) + gerçek GİB e-Arşiv
/// Schematron kuralları ($type='earchive' resmî xsl:param bağlaması ile). Sahte/mock hiçbir
/// bileşen YOKTUR (bkz. görev md.14). Gerçek e-Arşiv renderer çıktısı artık SIFIR Schematron
/// ihlaliyle doğrulanır - Faz 2B.5'in tamamlanma kriteri budur.
/// </summary>
[Collection(SchematronSidecarCollection.Name)]
[Trait("Domain", "EBelge")]
[Trait("Dependency", "JavaSidecar")]
public class EBelgeUblRendererEndToEndIntegrationTests
{
    private readonly SchematronSidecarProcessFixture _fixture;

    public EBelgeUblRendererEndToEndIntegrationTests(SchematronSidecarProcessFixture fixture)
    {
        _fixture = fixture;
    }

    private sealed class HttpTabanliTestValidator : IEBelgeSchematronValidator
    {
        private readonly SaxonSidecarEBelgeSchematronValidator _inner;
        public HttpTabanliTestValidator(string baseUrl)
        {
            _inner = new SaxonSidecarEBelgeSchematronValidator(new HttpClient { BaseAddress = new Uri(baseUrl) });
        }

        public Task<EBelgeSchematronValidationResult> ValidateAsync(
            ImmutableArray<byte> xmlBytes, string ruleSetId, CancellationToken cancellationToken)
            => _inner.ValidateAsync(xmlBytes, ruleSetId, cancellationToken);
    }

    private EBelgeUblRenderer CreateRealRenderer(out GibKuralSeti kuralSeti)
    {
        if (_fixture.BaseUrl is null)
        {
            Assert.Fail($"Sidecar ayağa kaldırılamadı: {_fixture.AtlamaNedeni}");
        }

        kuralSeti = EBelgeUblRendererTestVerisi.KuralSetiYukle();
        var xsdValidator = new EBelgeUblXsdValidator(kuralSeti);
        var schematronValidator = new HttpTabanliTestValidator(_fixture.BaseUrl!);
        return new EBelgeUblRenderer(kuralSeti, xsdValidator, schematronValidator);
    }

    // §7: geçerli e-Arşiv snapshot -> XSD (yalnız bilinen bulgu) -> gerçek sidecar -> valid=true, violations=[] -> başarılı sonuç, ArtifactStage=Unsigned.
    [Fact]
    [Trait("TestLevel", "ReleaseGate")]
    public async Task GercekEArsivRendererCiktisiSifirSchematronIhlaliyleBasariylaSonuclanir()
    {
        var renderer = CreateRealRenderer(out _);
        var snapshot = EBelgeUblRendererTestVerisi.GecerliSnapshot();

        var sonuc = await renderer.RenderAsync(snapshot, CancellationToken.None);

        Assert.NotEmpty(sonuc.UnsignedUblUtf8);
        Assert.NotEmpty(sonuc.UnsignedUblSha256);
        Assert.Equal(EBelgeUblArtifactStage.Unsigned, sonuc.ArtifactStage);
        Assert.Equal("EARSIVFATURA", sonuc.KullanilanProfileId);
    }

    // Negatif test 2: ProfileID yanlışsa (e-Arşiv kapsamı dışı bir değer) gerçek Schematron ihlali verir.
    [Fact]
    [Trait("TestLevel", "SidecarIntegration")]
    public async Task YanlisProfileIdGercekSchematronIhlaliUretir()
    {
        if (_fixture.BaseUrl is null)
        {
            Assert.Fail($"Sidecar ayağa kaldırılamadı: {_fixture.AtlamaNedeni}");
        }

        var schematronValidator = new HttpTabanliTestValidator(_fixture.BaseUrl!);
        const string yanlisProfilXml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <Invoice xmlns="urn:oasis:names:specification:ubl:schema:xsd:Invoice-2"
                     xmlns:cbc="urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2">
              <cbc:UBLVersionID>2.1</cbc:UBLVersionID>
              <cbc:CustomizationID>TR1.2</cbc:CustomizationID>
              <cbc:ProfileID>YANLISPROFIL</cbc:ProfileID>
              <cbc:ID>EAR2026000000001</cbc:ID>
              <cbc:CopyIndicator>false</cbc:CopyIndicator>
              <cbc:UUID>a1b2c3d4-e5f6-4789-a012-b3c4d5e6f789</cbc:UUID>
              <cbc:IssueDate>2026-07-01</cbc:IssueDate>
              <cbc:IssueTime>11:00:00</cbc:IssueTime>
              <cbc:InvoiceTypeCode>SATIS</cbc:InvoiceTypeCode>
              <cbc:DocumentCurrencyCode>TRY</cbc:DocumentCurrencyCode>
            </Invoice>
            """;
        var xmlBytes = ImmutableArray.Create(System.Text.Encoding.UTF8.GetBytes(yanlisProfilXml));

        var sonuc = await schematronValidator.ValidateAsync(xmlBytes, EBelgeSchematronSidecarOptions.SupportedRuleSetId, CancellationToken.None);

        Assert.False(sonuc.Valid);
        Assert.Contains(sonuc.Violations, v => v.Message.Contains("ProfileID", StringComparison.Ordinal));
    }

    // Negatif test 4: e-Fatura profil id'si (henüz desteklenmeyen ruleset suffix'i) ilk dalgada reddedilir.
    [Fact]
    [Trait("TestLevel", "SidecarIntegration")]
    public async Task EFaturaRuleSetIdIlkDalgadaReddedilir()
    {
        if (_fixture.BaseUrl is null)
        {
            Assert.Fail($"Sidecar ayağa kaldırılamadı: {_fixture.AtlamaNedeni}");
        }

        var schematronValidator = new HttpTabanliTestValidator(_fixture.BaseUrl!);
        var snapshot = EBelgeUblRendererTestVerisi.GecerliSnapshot();

        await Assert.ThrowsAsync<EBelgeUblRuleSetArtifactInvalidException>(() =>
            schematronValidator.ValidateAsync(
                ImmutableArray.Create(System.Text.Encoding.UTF8.GetBytes("<Invoice/>")),
                "GIB-UBL-TR-1.2.1/2026-09-14/EFATURA",
                CancellationToken.None));
    }
}
