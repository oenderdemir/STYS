using STYS.Muhasebe.Kdv.Enums;
using STYS.Muhasebe.SatisBelgeleri;
using STYS.Muhasebe.SatisBelgeleri.Enums;
using Xunit;

namespace STYS.Tests;

/// <summary>
/// Faz 2B.4.2: IEBelgeUblPreCutValidator'ın tüm kurallarını, veritabanından tamamen bağımsız
/// (saf EBelgeUblPreCutContext) olarak doğrular. Entegrasyon seviyesindeki kanal/V2-üretim/yan
/// etki testleri için bkz. EBelgeUblPreCutIntegrationTests.
/// </summary>
[Trait("Domain", "EBelge")]
[Trait("TestLevel", "Contract")]
public class EBelgeUblPreCutValidatorTests
{
    private static readonly EBelgeUblPreCutSatirContext GecerliSatir = new(
        Birim: "Adet",
        KdvUygulamaTipi: KdvUygulamaTipi.Kdvli,
        KdvIstisnaKodu: null,
        TevkifatPay: null,
        TevkifatPayda: null,
        TevkifatTutari: 0m,
        OtvTutari: 0m,
        OivTutari: 0m,
        KonaklamaVergisiTutari: 0m,
        Matrah: 100m,
        KdvTutari: 18m,
        SatirToplami: 118m);

    private static EBelgeUblPreCutContext GecerliBaglam(
        bool featureEnabled = true,
        DateTime? belgeTarihi = null,
        DateTime? planlananKesimTarihiTrt = null,
        EBelgeKanali eBelgeKanali = EBelgeKanali.EArsiv,
        SatisBelgesiTipi belgeTipi = SatisBelgesiTipi.SatisFaturasi,
        string paraBirimi = "TRY",
        decimal kur = 1m,
        bool iadeSenaryosuVarMi = false,
        bool aliciKurumsalMi = true,
        string? aliciUnvan = "Test Ticaret A.Ş.",
        string? aliciVergiNo = "1234567890",
        string? aliciAd = null,
        string? aliciSoyad = null,
        string? aliciTcKimlikNo = null,
        string? saticiIlce = "Kadıköy",
        string? saticiIl = "İstanbul",
        string? aliciIlce = "Beşiktaş",
        string? aliciIl = "İstanbul",
        IReadOnlyList<EBelgeUblPreCutSatirContext>? aktifSatirlar = null,
        decimal toplamMatrah = 100m,
        decimal toplamKdv = 18m,
        decimal genelToplam = 118m)
    {
        var tarih = new DateTime(2026, 9, 15);
        return new EBelgeUblPreCutContext(
            FeatureEnabled: featureEnabled,
            BelgeTarihi: belgeTarihi ?? tarih,
            PlanlananKesimTarihiTrt: planlananKesimTarihiTrt ?? tarih,
            EBelgeKanali: eBelgeKanali,
            BelgeTipi: belgeTipi,
            ParaBirimi: paraBirimi,
            Kur: kur,
            IadeSenaryosuVarMi: iadeSenaryosuVarMi,
            AliciKurumsalMi: aliciKurumsalMi,
            AliciUnvan: aliciUnvan,
            AliciVergiNo: aliciVergiNo,
            AliciAd: aliciAd,
            AliciSoyad: aliciSoyad,
            AliciTcKimlikNo: aliciTcKimlikNo,
            SaticiIlce: saticiIlce,
            SaticiIl: saticiIl,
            AliciIlce: aliciIlce,
            AliciIl: aliciIl,
            AktifSatirlar: aktifSatirlar ?? [GecerliSatir],
            ToplamMatrah: toplamMatrah,
            ToplamKdv: toplamKdv,
            GenelToplam: genelToplam);
    }

    [Fact]
    public void TumKurallarGecerliyseIstisnaFirlatilmaz()
    {
        var validator = new EBelgeUblPreCutValidator();

        var ex = Record.Exception(() => validator.Validate(GecerliBaglam()));

        Assert.Null(ex);
    }

    [Fact]
    public void FeatureKapaliysa503ReddedilirVeDigerKurallarKontrolEdilmez()
    {
        var validator = new EBelgeUblPreCutValidator();
        // Feature kapalıyken diğer TÜM alanlar da geçersiz olsa dahi ilk kontrol (rule 1) durmalı.
        var context = GecerliBaglam(featureEnabled: false, eBelgeKanali: EBelgeKanali.EFatura);

        var ex = Assert.Throws<EBelgeUblFeatureDisabledException>(() => validator.Validate(context));

        Assert.Equal(EBelgeUblFeatureDisabledException.HttpStatusCode, ex.ErrorCode);
        Assert.Equal(EBelgeUblFeatureDisabledException.SafeErrorCode, ex.HataKodu);
    }

    [Fact]
    public void GoLiveOncesiBelgeTarihiReddedilir()
    {
        var validator = new EBelgeUblPreCutValidator();
        var context = GecerliBaglam(belgeTarihi: new DateTime(2026, 9, 13));

        var ex = Assert.Throws<EBelgeInvoiceDateBeforeGoLiveException>(() => validator.Validate(context));

        Assert.Equal(EBelgeInvoiceDateBeforeGoLiveException.HttpStatusCode, ex.ErrorCode);
    }

    [Fact]
    public void EFaturaKanaliReddedilir()
    {
        var validator = new EBelgeUblPreCutValidator();
        var context = GecerliBaglam(eBelgeKanali: EBelgeKanali.EFatura);

        var ex = Assert.Throws<EBelgeUblScopeUnsupportedException>(() => validator.Validate(context));

        Assert.Equal(EBelgeUblScopeUnsupportedException.HttpStatusCode, ex.ErrorCode);
        Assert.Equal(EBelgeUblScopeUnsupportedException.SafeErrorCode, ex.HataKodu);
    }

    [Fact]
    public void SatisFaturasiDisindaBelgeTipiReddedilir()
    {
        var validator = new EBelgeUblPreCutValidator();
        var context = GecerliBaglam(belgeTipi: SatisBelgesiTipi.AlisIadeFaturasi);

        Assert.Throws<EBelgeUblScopeUnsupportedException>(() => validator.Validate(context));
    }

    [Fact]
    public void TryDisindaParaBirimiReddedilir()
    {
        var validator = new EBelgeUblPreCutValidator();
        var context = GecerliBaglam(paraBirimi: "USD");

        Assert.Throws<EBelgeUblScopeUnsupportedException>(() => validator.Validate(context));
    }

    [Fact]
    public void KurBirDisindaReddedilir()
    {
        var validator = new EBelgeUblPreCutValidator();
        var context = GecerliBaglam(kur: 1.25m);

        Assert.Throws<EBelgeUblScopeUnsupportedException>(() => validator.Validate(context));
    }

    [Fact]
    public void SaticiYapisalAdresiEksikseReddedilir()
    {
        var validator = new EBelgeUblPreCutValidator();
        var context = GecerliBaglam(saticiIlce: null);

        var ex = Assert.Throws<EBelgeUblAuthoritativeFieldMissingException>(() => validator.Validate(context));

        Assert.Equal(EBelgeUblAuthoritativeFieldMissingException.HttpStatusCode, ex.ErrorCode);
    }

    [Fact]
    public void KurumsalAlicidaUnvanVeyaVknEksikseReddedilir()
    {
        var validator = new EBelgeUblPreCutValidator();
        var context = GecerliBaglam(aliciKurumsalMi: true, aliciUnvan: null);

        Assert.Throws<EBelgeUblAuthoritativeFieldMissingException>(() => validator.Validate(context));
    }

    [Fact]
    public void GercekKisiAlicidaAdSoyadVeyaTcknEksikseReddedilir()
    {
        var validator = new EBelgeUblPreCutValidator();
        var context = GecerliBaglam(
            aliciKurumsalMi: false, aliciUnvan: null, aliciVergiNo: null,
            aliciAd: "Ayşe", aliciSoyad: null, aliciTcKimlikNo: "11111111110");

        Assert.Throws<EBelgeUblAuthoritativeFieldMissingException>(() => validator.Validate(context));
    }

    [Fact]
    public void GercekKisiAlicidaTumAlanlarDoluysaGecerlidir()
    {
        var validator = new EBelgeUblPreCutValidator();
        var context = GecerliBaglam(
            aliciKurumsalMi: false, aliciUnvan: null, aliciVergiNo: null,
            aliciAd: "Ayşe", aliciSoyad: "Yılmaz", aliciTcKimlikNo: "11111111110");

        var ex = Record.Exception(() => validator.Validate(context));

        Assert.Null(ex);
    }

    [Fact]
    public void AliciYapisalAdresiEksikseReddedilir()
    {
        var validator = new EBelgeUblPreCutValidator();
        var context = GecerliBaglam(aliciIl: null);

        Assert.Throws<EBelgeUblAuthoritativeFieldMissingException>(() => validator.Validate(context));
    }

    [Fact]
    public void AktifSatirYoksaReddedilir()
    {
        var validator = new EBelgeUblPreCutValidator();
        var context = GecerliBaglam(aktifSatirlar: []);

        Assert.Throws<EBelgeUblAuthoritativeFieldMissingException>(() => validator.Validate(context));
    }

    [Fact]
    public void AdetDisindaBirimReddedilir()
    {
        var validator = new EBelgeUblPreCutValidator();
        var satir = GecerliSatir with { Birim = "Kutu" };
        var context = GecerliBaglam(aktifSatirlar: [satir]);

        Assert.Throws<EBelgeUblScopeUnsupportedException>(() => validator.Validate(context));
    }

    [Fact]
    public void KdvliDisindaUygulamaTipiReddedilir()
    {
        var validator = new EBelgeUblPreCutValidator();
        var satir = GecerliSatir with { KdvUygulamaTipi = KdvUygulamaTipi.TamIstisna };
        var context = GecerliBaglam(aktifSatirlar: [satir]);

        Assert.Throws<EBelgeUblScopeUnsupportedException>(() => validator.Validate(context));
    }

    [Fact]
    public void TevkifatliSatirReddedilir()
    {
        var validator = new EBelgeUblPreCutValidator();
        var satir = GecerliSatir with { TevkifatPay = 9, TevkifatPayda = 10, TevkifatTutari = 16.2m };
        var context = GecerliBaglam(aktifSatirlar: [satir]);

        Assert.Throws<EBelgeUblScopeUnsupportedException>(() => validator.Validate(context));
    }

    [Fact]
    public void KdvIstisnaliSatirReddedilir()
    {
        var validator = new EBelgeUblPreCutValidator();
        var satir = GecerliSatir with { KdvIstisnaKodu = "301" };
        var context = GecerliBaglam(aktifSatirlar: [satir]);

        Assert.Throws<EBelgeUblScopeUnsupportedException>(() => validator.Validate(context));
    }

    [Fact]
    public void OtvIcerenSatirReddedilir()
    {
        var validator = new EBelgeUblPreCutValidator();
        var satir = GecerliSatir with { OtvTutari = 5m };
        var context = GecerliBaglam(aktifSatirlar: [satir]);

        Assert.Throws<EBelgeUblScopeUnsupportedException>(() => validator.Validate(context));
    }

    [Fact]
    public void OivIcerenSatirReddedilir()
    {
        var validator = new EBelgeUblPreCutValidator();
        var satir = GecerliSatir with { OivTutari = 5m };
        var context = GecerliBaglam(aktifSatirlar: [satir]);

        Assert.Throws<EBelgeUblScopeUnsupportedException>(() => validator.Validate(context));
    }

    [Fact]
    public void KonaklamaVergisiIcerenSatirReddedilir()
    {
        var validator = new EBelgeUblPreCutValidator();
        var satir = GecerliSatir with { KonaklamaVergisiTutari = 5m };
        var context = GecerliBaglam(aktifSatirlar: [satir]);

        Assert.Throws<EBelgeUblScopeUnsupportedException>(() => validator.Validate(context));
    }

    [Fact]
    public void IadeSenaryosuVarsaReddedilir()
    {
        var validator = new EBelgeUblPreCutValidator();
        var context = GecerliBaglam(iadeSenaryosuVarMi: true);

        Assert.Throws<EBelgeUblScopeUnsupportedException>(() => validator.Validate(context));
    }

    [Fact]
    public void MaliToplamUyusmazligi422IleReddedilirVeUyusmazlikDetayiTasir()
    {
        var validator = new EBelgeUblPreCutValidator();
        var context = GecerliBaglam(toplamMatrah: 999m);

        var ex = Assert.Throws<EBelgeUblMonetaryTotalMismatchException>(() => validator.Validate(context));

        Assert.Equal(EBelgeUblMonetaryTotalMismatchException.HttpStatusCode, ex.ErrorCode);
        Assert.Equal(EBelgeUblMonetaryTotalMismatchException.SafeErrorCode, ex.HataKodu);
        var uyusmazlik = Assert.Single(ex.Uyusmazliklar);
        Assert.Equal("ToplamMatrah", uyusmazlik.Alan);
        Assert.Equal(100m, uyusmazlik.HesaplananDeger);
        Assert.Equal(999m, uyusmazlik.MevcutDeger);
    }
}
