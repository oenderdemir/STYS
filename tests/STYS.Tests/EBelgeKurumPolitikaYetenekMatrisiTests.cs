using STYS.Muhasebe.SatisBelgeleri.Enums;
using STYS.Muhasebe.SatisBelgeleri.Services;
using Xunit;

namespace STYS.Tests;

/// <summary>
/// Faz 2B.10 görev md.7 - production yöntem yetenek matrisinin (<see cref="EBelgeYontemYetenekSaglayici"/>)
/// TEK, merkezi kaynak olduğunu ve görevde verilen TAM matrisle birebir eşleştiğini doğrular.
/// `OzelEntegrator`/`DogrudanGib` gerçek bir adapter EKLENMEDEN `OperasyonelMi=false` DÖNMELİDİR -
/// bu, production'da bu iki yöntemin AKTİF POLİTİKA olarak kabul edilemeyeceğinin temel garantisidir.
/// </summary>
[Trait("Domain", "EBelge")]
[Trait("TestLevel", "Unit")]
public class EBelgeKurumPolitikaYetenekMatrisiTests
{
    private readonly EBelgeYontemYetenekSaglayici _sut = new();

    public static IEnumerable<object[]> ProductionMatrisi() =>
    [
        [EBelgeEntegrasyonYontemi.Kullanilmayacak, new EBelgeYontemYetenekleri(true, false, false, false, false)],
        [EBelgeEntegrasyonYontemi.HariciMuhasebeSistemi, new EBelgeYontemYetenekleri(true, false, false, false, false)],
        [EBelgeEntegrasyonYontemi.GibPortal, new EBelgeYontemYetenekleri(true, true, true, false, false)],
        [EBelgeEntegrasyonYontemi.OzelEntegrator, new EBelgeYontemYetenekleri(false, false, false, false, false)],
        [EBelgeEntegrasyonYontemi.DogrudanGib, new EBelgeYontemYetenekleri(false, false, false, false, false)],
        [EBelgeEntegrasyonYontemi.Yapilandirilmadi, new EBelgeYontemYetenekleri(false, false, false, false, false)],
    ];

    [Theory]
    [MemberData(nameof(ProductionMatrisi))]
    public void HerYontemGorevdeTanimlananTamYetenekPlaniniDoner(EBelgeEntegrasyonYontemi yontem, EBelgeYontemYetenekleri beklenen)
    {
        var sonuc = _sut.Getir(yontem);

        Assert.Equal(beklenen, sonuc);
    }

    [Theory]
    [InlineData(EBelgeEntegrasyonYontemi.OzelEntegrator)]
    [InlineData(EBelgeEntegrasyonYontemi.DogrudanGib)]
    public void GercekAdapterOlmayanYontemlerOperasyonelDegildir(EBelgeEntegrasyonYontemi yontem)
    {
        // md.27 - "gerçek bir adapter/HSM/mali mühür entegrasyonu YAPILMADAN production'da AKTİF
        // POLİTİKA olarak KABUL EDİLEMEZ" garantisinin tek kaynağı budur.
        Assert.False(_sut.Getir(yontem).OperasyonelMi);
    }

    [Fact]
    public void TanimsizEnumDegeriFailClosedDoner()
    {
        var sonuc = _sut.Getir((EBelgeEntegrasyonYontemi)999);

        Assert.Equal(new EBelgeYontemYetenekleri(false, false, false, false, false), sonuc);
    }

    [Fact]
    public void GibPortalYerelImzaVeOtomatikGonderimIcermez()
    {
        // md.11/md.12 - GibPortal yerel snapshot+unsigned UBL üretir ama ASLA imzalamaz/otomatik göndermez.
        var yetenekler = _sut.Getir(EBelgeEntegrasyonYontemi.GibPortal);

        Assert.True(yetenekler.YerelSnapshotOlustur);
        Assert.True(yetenekler.YerelUnsignedUblOlustur);
        Assert.False(yetenekler.YerelImzaOlustur);
        Assert.False(yetenekler.OtomatikGonderimYap);
    }

    [Theory]
    [InlineData(EBelgeEntegrasyonYontemi.Kullanilmayacak)]
    [InlineData(EBelgeEntegrasyonYontemi.HariciMuhasebeSistemi)]
    public void OperasyonelAmaYerelPipelineGerektirmeyenYontemlerHicYerelUretimYapmaz(EBelgeEntegrasyonYontemi yontem)
    {
        var yetenekler = _sut.Getir(yontem);

        Assert.True(yetenekler.OperasyonelMi);
        Assert.False(yetenekler.YerelSnapshotOlustur);
        Assert.False(yetenekler.YerelUnsignedUblOlustur);
        Assert.False(yetenekler.YerelImzaOlustur);
        Assert.False(yetenekler.OtomatikGonderimYap);
    }
}
