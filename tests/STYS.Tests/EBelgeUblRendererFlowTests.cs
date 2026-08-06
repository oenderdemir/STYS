using System.Collections.Immutable;
using STYS.Muhasebe.SatisBelgeleri;
using Xunit;

namespace STYS.Tests;

/// <summary>Hızlı, ağ bağımsız renderer akış testleri - sahte (stub) IEBelgeSchematronValidator kullanır (gerçek sidecar ile tam akış EBelgeSchematronSidecarIntegrationTests'te ayrıca doğrulanır).</summary>
[Trait("Domain", "EBelge")]
[Trait("TestLevel", "Unit")]
public class EBelgeUblRendererFlowTests
{
    private sealed class SahteSchematronValidator : IEBelgeSchematronValidator
    {
        private readonly Func<ImmutableArray<byte>, string, EBelgeSchematronValidationResult> _yanit;
        public ImmutableArray<byte>? AlinanXmlBytes { get; private set; }
        public string? AlinanRuleSetId { get; private set; }

        public SahteSchematronValidator(Func<ImmutableArray<byte>, string, EBelgeSchematronValidationResult> yanit)
        {
            _yanit = yanit;
        }

        public Task<EBelgeSchematronValidationResult> ValidateAsync(ImmutableArray<byte> xmlBytes, string ruleSetId, CancellationToken cancellationToken)
        {
            AlinanXmlBytes = xmlBytes;
            AlinanRuleSetId = ruleSetId;
            return Task.FromResult(_yanit(xmlBytes, ruleSetId));
        }
    }

    private sealed class HataFirlatanSchematronValidator : IEBelgeSchematronValidator
    {
        private readonly Exception _hata;
        public HataFirlatanSchematronValidator(Exception hata) => _hata = hata;

        public Task<EBelgeSchematronValidationResult> ValidateAsync(ImmutableArray<byte> xmlBytes, string ruleSetId, CancellationToken cancellationToken)
            => throw _hata;
    }

    private static (EBelgeUblRenderer Renderer, SahteSchematronValidator Sahte) CreateRenderer(bool gecerli = true)
    {
        var kuralSeti = EBelgeUblRendererTestVerisi.KuralSetiYukle();
        var xsdValidator = new EBelgeUblXsdValidator(kuralSeti);
        var sahte = new SahteSchematronValidator((_, _) => new EBelgeSchematronValidationResult(
            gecerli,
            gecerli ? Array.Empty<EBelgeSchematronViolation>() : new[] { new EBelgeSchematronViolation("r1", "/Invoice", "test ihlali", "error") }));

        return (new EBelgeUblRenderer(kuralSeti, xsdValidator, sahte), sahte);
    }

    // 29. Aynı snapshot aynı XML byte ve hash üretir.
    [Fact]
    public async Task AyniSnapshotAyniXmlByteVeHashUretir()
    {
        var (renderer1, _) = CreateRenderer();
        var (renderer2, _) = CreateRenderer();
        var snapshot = EBelgeUblRendererTestVerisi.GecerliSnapshot();

        var sonuc1 = await renderer1.RenderAsync(snapshot, CancellationToken.None);
        var sonuc2 = await renderer2.RenderAsync(snapshot, CancellationToken.None);

        Assert.Equal(sonuc1.UnsignedUblSha256, sonuc2.UnsignedUblSha256);
        Assert.True(sonuc1.UnsignedUblUtf8.SequenceEqual(sonuc2.UnsignedUblUtf8));
    }

    // 30. Renderer sidecar'a exact üretilmiş XML byte'larını gönderir.
    [Fact]
    public async Task RendererSidecaraExactUretilenXmlByteGonderir()
    {
        var (renderer, sahte) = CreateRenderer();
        var snapshot = EBelgeUblRendererTestVerisi.GecerliSnapshot();

        var sonuc = await renderer.RenderAsync(snapshot, CancellationToken.None);

        Assert.True(sahte.AlinanXmlBytes!.Value.SequenceEqual(sonuc.UnsignedUblUtf8));
        Assert.Equal(EBelgeSchematronSidecarOptions.SupportedRuleSetId, sahte.AlinanRuleSetId);
    }

    // 31. Schematron başarısızsa başarılı render sonucu dönmez.
    [Fact]
    public async Task SchematronBasarisizsaBasariliSonucDonmez()
    {
        var (renderer, _) = CreateRenderer(gecerli: false);
        var snapshot = EBelgeUblRendererTestVerisi.GecerliSnapshot();

        await Assert.ThrowsAsync<EBelgeUblSchematronValidationFailedException>(
            () => renderer.RenderAsync(snapshot, CancellationToken.None));
    }

    // 32. Sidecar erişilemiyorsa transient hata döner (mali toplam/kapsam hatasıyla ASLA birleşmez).
    [Fact]
    public async Task SidecarErisilemiyorsaTransientHataDoner()
    {
        var kuralSeti = EBelgeUblRendererTestVerisi.KuralSetiYukle();
        var xsdValidator = new EBelgeUblXsdValidator(kuralSeti);
        var hataliValidator = new HataFirlatanSchematronValidator(
            new EBelgeUblSchematronServiceUnavailableException("test: sidecar erişilemiyor"));
        var renderer = new EBelgeUblRenderer(kuralSeti, xsdValidator, hataliValidator);
        var snapshot = EBelgeUblRendererTestVerisi.GecerliSnapshot();

        await Assert.ThrowsAsync<EBelgeUblSchematronServiceUnavailableException>(
            () => renderer.RenderAsync(snapshot, CancellationToken.None));
    }

    // 35. ArtifactStage type-safe biçimde Unsigned olarak belirtilir.
    [Fact]
    public async Task ArtifactStageUnsignedOlarakBelirtilir()
    {
        var (renderer, _) = CreateRenderer();
        var snapshot = EBelgeUblRendererTestVerisi.GecerliSnapshot();

        var sonuc = await renderer.RenderAsync(snapshot, CancellationToken.None);

        Assert.Equal(EBelgeUblArtifactStage.Unsigned, sonuc.ArtifactStage);
    }

    // 36. V1 (desteklenmeyen şema sürümü) snapshot reddedilir.
    [Fact]
    public async Task DesteklenmeyenSnapshotSemaSurumuReddedilir()
    {
        var (renderer, _) = CreateRenderer();
        var snapshot = EBelgeUblRendererTestVerisi.GecerliSnapshot();
        var v1BenzeriSnapshot = snapshot with
        {
            Metadata = snapshot.Metadata with { SnapshotSchemaVersion = "1" },
        };

        await Assert.ThrowsAsync<EBelgeUblRenderSnapshotVersionUnsupportedException>(
            () => renderer.RenderAsync(v1BenzeriSnapshot, CancellationToken.None));
    }
}
