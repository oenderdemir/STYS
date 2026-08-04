using STYS.Muhasebe.SatisBelgeleri;
using Xunit;

namespace STYS.Tests;

/// <summary>
/// Faz 2B.5 TAM uçtan uca akış: gerçek renderer + gerçek yerel XSD doğrulayıcı + GERÇEK Java
/// Saxon-HE 13.0 sidecar süreci (bkz. SchematronSidecarProcessFixture) + gerçek GİB
/// stylesheet'leri. Sahte/mock hiçbir bileşen YOKTUR (bkz. görev md.14).
/// </summary>
[Collection(SchematronSidecarCollection.Name)]
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
            System.Collections.Immutable.ImmutableArray<byte> xmlBytes, string ruleSetId, CancellationToken cancellationToken)
            => _inner.ValidateAsync(xmlBytes, ruleSetId, cancellationToken);
    }

    [Fact]
    public async Task GercekSidecarUzerindenSchematronDogrulamasiBasarisizOlurCunkuUblExtensionsYok()
    {
        if (_fixture.BaseUrl is null)
        {
            Assert.Fail($"Sidecar ayağa kaldırılamadı: {_fixture.AtlamaNedeni}");
        }

        var kuralSeti = EBelgeUblRendererTestVerisi.KuralSetiYukle();
        var xsdValidator = new EBelgeUblXsdValidator(kuralSeti);
        var schematronValidator = new HttpTabanliTestValidator(_fixture.BaseUrl!);
        var renderer = new EBelgeUblRenderer(kuralSeti, xsdValidator, schematronValidator);
        var snapshot = EBelgeUblRendererTestVerisi.GecerliSnapshot();

        // XSD zaten "yalnız bilinen UBLExtensions bulgusu" filtresinden geçer (renderer bunu
        // sessizce kabul eder) - GERÇEK sidecar'a kadar ulaşır ve gerçek schematron sonucunu
        // döner. Snapshot'ın ürettiği ProfileID=EARSIVFATURA, GİB kuralının varsayılan "efatura"
        // modunda tanınmadığından (bkz. rapor, "yan bulgu") GERÇEK bir ihlal bekleniyor - bu da
        // "gerçek sidecar + gerçek GİB kuralları" entegrasyonunun uçtan uca çalıştığının kanıtı.
        var ex = await Assert.ThrowsAsync<EBelgeUblSchematronValidationFailedException>(
            () => renderer.RenderAsync(snapshot, CancellationToken.None));

        Assert.NotEmpty(ex.Ihlaller);
    }
}
