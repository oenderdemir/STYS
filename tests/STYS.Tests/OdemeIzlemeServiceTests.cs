using Microsoft.EntityFrameworkCore;
using STYS.AccessScope;
using STYS.Infrastructure.EntityFramework;
using STYS.Iller.Entities;
using STYS.Kurumlar.Entities;
using STYS.Muhasebe.CariHareketler.Entities;
using STYS.Muhasebe.CariKartlar.Entities;
using STYS.Muhasebe.Common.Constants;
using STYS.Muhasebe.Common.Services;
using STYS.Muhasebe.KasaBankaHesaplari.Entities;
using STYS.Muhasebe.MuhasebeFisleri.Entities;
using STYS.Muhasebe.MuhasebeHesapPlanlari.Entities;
using STYS.Muhasebe.OdemeIzleme.Dtos;
using STYS.Muhasebe.OdemeIzleme.Services;
using STYS.Muhasebe.PosTahsilatValorleri.Entities;
using STYS.Muhasebe.TahsilatOdemeBelgeleri.Entities;
using STYS.Tesisler.Entities;
using TOD.Platform.Persistence.Rdbms.Paging;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.Tests;

/// <summary>
/// OdemeIzlemeService'in gercek is kurallarini GERCEK SQL Server'a karsi dogrular. Her test KENDI
/// Kurum/Il/Tesis/CariKart/TahsilatOdemeBelgesi/CariHareket/PosTahsilatValor/MuhasebeFis
/// kayitlarini (benzersiz "ODZ-970-{guid}" isaretiyle) olusturur; DisposeAsync
/// TwoPhaseCleanupRunner ile bagimsiz adimlarla temizler ve fiziksel kalinti olmadigini dogrular.
/// </summary>
[Trait("Category", "Integration")]
public class OdemeIzlemeServiceTests : IAsyncLifetime
{
    private static readonly string? ConnectionString =
        Environment.GetEnvironmentVariable(IntegrationFactAttribute.ConnectionStringEnvVar);

    private const string TestMarker = "ODZ-970";

    private readonly List<int> _tesisIdler = [];
    private readonly List<int> _kurumIdler = [];
    private readonly List<int> _illIdler = [];
    private readonly List<int> _cariKartIdler = [];
    private readonly List<int> _hesapPlaniIdler = [];
    private readonly List<int> _kasaBankaHesapIdler = [];
    private readonly List<int> _belgeIdler = [];
    private readonly List<int> _cariHareketIdler = [];
    private readonly List<int> _valorIdler = [];
    private readonly List<int> _fisIdler = [];

    private static StysAppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<StysAppDbContext>().UseSqlServer(ConnectionString).Options;
        return new StysAppDbContext(options, new FakeCurrentUserAccessor(), new FakeCurrentTenantAccessor());
    }

    private static OdemeIzlemeService CreateService(StysAppDbContext dbContext, params int[] erisilebilirTesisIds) =>
        new(dbContext, new FakeMuhasebeTesisScopeService(erisilebilirTesisIds));

    private static string YeniSuffix() => $"{TestMarker}-{Guid.NewGuid():N}"[..20];

    private async Task<int> YeniTesisAsync(StysAppDbContext dbContext, string suffix)
    {
        var kurum = new Kurum { Kod = suffix, Ad = "Test Kurum " + suffix, AktifMi = true };
        dbContext.Kurumlar.Add(kurum);
        var il = new Il { Ad = "Test Il " + suffix, AktifMi = true };
        dbContext.Iller.Add(il);
        await dbContext.SaveChangesAsync();
        _kurumIdler.Add(kurum.Id);
        _illIdler.Add(il.Id);

        var tesis = new Tesis { KurumId = kurum.Id, IlId = il.Id, Ad = "Test Tesis " + suffix, Telefon = "0000", Adres = "Test Adres", AktifMi = true };
        dbContext.Tesisler.Add(tesis);
        await dbContext.SaveChangesAsync();
        _tesisIdler.Add(tesis.Id);
        return tesis.Id;
    }

    private async Task<int> YeniCariKartAsync(StysAppDbContext dbContext, int tesisId, string suffix, decimal? acilisBakiyeTutari = null, string? acilisBakiyeYonu = null)
    {
        var cari = new CariKart
        {
            TesisId = tesisId, CariTipi = CariKartTipleri.Musteri, CariKodu = suffix, UnvanAdSoyad = "Test Musteri " + suffix, AktifMi = true,
            AcilisBakiyeTutari = acilisBakiyeTutari, AcilisBakiyeYonu = acilisBakiyeYonu
        };
        dbContext.CariKartlar.Add(cari);
        await dbContext.SaveChangesAsync();
        _cariKartIdler.Add(cari.Id);
        return cari.Id;
    }

    private async Task<int> YeniHesapPlaniAsync(StysAppDbContext dbContext, string suffix, string etiket)
    {
        var hesap = new MuhasebeHesapPlani
        {
            Kod = $"{suffix}-{etiket}", TamKod = $"1.10.{suffix}-{etiket}", Ad = "Test " + etiket, SeviyeNo = 3,
            HesapTipi = HesapTipi.DetayHesap, AktifMi = true, DetayHesapMi = true, HareketGorebilirMi = true
        };
        dbContext.MuhasebeHesapPlanlari.Add(hesap);
        await dbContext.SaveChangesAsync();
        _hesapPlaniIdler.Add(hesap.Id);
        return hesap.Id;
    }

    private async Task<int> YeniKasaBankaHesabiAsync(StysAppDbContext dbContext, int tesisId, string tip, string suffix, string etiket, int? muhasebeHesapPlaniId, string? iban = null)
    {
        var hesap = new KasaBankaHesap
        {
            TesisId = tesisId, Tip = tip, Kod = $"{suffix}-{etiket}", Ad = "Test " + etiket, ParaBirimi = "TRY", AktifMi = true,
            MuhasebeHesapPlaniId = muhasebeHesapPlaniId, Iban = iban
        };
        dbContext.KasaBankaHesaplari.Add(hesap);
        await dbContext.SaveChangesAsync();
        _kasaBankaHesapIdler.Add(hesap.Id);
        return hesap.Id;
    }

    private async Task<int> YeniBelgeAsync(
        StysAppDbContext dbContext, int cariKartId, decimal tutar, string belgeNo, DateTime belgeTarihi,
        string odemeYontemi = OdemeYontemleri.Nakit, int? kasaBankaHesapId = null, int? muhasebeFisId = null,
        string durum = TahsilatOdemeBelgeDurumlari.Aktif, int? kapatilacakCariHareketId = null)
    {
        var belge = new TahsilatOdemeBelgesi
        {
            BelgeNo = belgeNo, BelgeTarihi = belgeTarihi, BelgeTipi = TahsilatOdemeBelgeTipleri.Tahsilat,
            CariKartId = cariKartId, Tutar = tutar, ParaBirimi = "TRY", OdemeYontemi = odemeYontemi,
            KasaBankaHesapId = kasaBankaHesapId, MuhasebeFisId = muhasebeFisId, Durum = durum,
            KapatilacakCariHareketId = kapatilacakCariHareketId
        };
        dbContext.TahsilatOdemeBelgeleri.Add(belge);
        await dbContext.SaveChangesAsync();
        _belgeIdler.Add(belge.Id);
        return belge.Id;
    }

    private async Task<int> YeniCariHareketAsync(
        StysAppDbContext dbContext, int cariKartId, decimal borc, decimal alacak, DateTime tarih,
        string durum = CariHareketDurumlari.Aktif, string? kaynakModul = null, int? kaynakId = null)
    {
        var hareket = new CariHareket
        {
            CariKartId = cariKartId, HareketTarihi = tarih, BelgeTuru = "Test", BorcTutari = borc, AlacakTutari = alacak,
            KalanTutar = borc - alacak, ParaBirimi = "TRY", Durum = durum, KaynakModul = kaynakModul, KaynakId = kaynakId
        };
        dbContext.CariHareketler.Add(hareket);
        await dbContext.SaveChangesAsync();
        _cariHareketIdler.Add(hareket.Id);
        return hareket.Id;
    }

    private async Task<int> YeniValorAsync(StysAppDbContext dbContext, int tesisId, int belgeId, int krediKartiHesapId, int? bagliBankaHesapId, string durum, DateOnly beklenenValorTarihi, decimal net, string paraBirimi = "TRY")
    {
        var valor = new PosTahsilatValor
        {
            TesisId = tesisId, TahsilatOdemeBelgesiId = belgeId, KrediKartiHesapId = krediKartiHesapId, BagliBankaHesapId = bagliBankaHesapId,
            OdemeTarihi = DateTime.UtcNow.Date, ValorGunSayisi = 0, ValorGunTuru = ValorGunTurleri.TakvimGunu, BeklenenValorTarihi = beklenenValorTarihi,
            OtomatikAktarimMi = false, BrutTutar = net, KomisyonTutari = 0, NetTutar = net, ParaBirimi = paraBirimi, Durum = durum
        };
        dbContext.PosTahsilatValorleri.Add(valor);
        await dbContext.SaveChangesAsync();
        _valorIdler.Add(valor.Id);
        return valor.Id;
    }

    private async Task<int> YeniFisAsync(StysAppDbContext dbContext, int tesisId, DateTime fisTarihi, string durum)
    {
        var fis = new MuhasebeFis
        {
            TesisId = tesisId, MaliYil = fisTarihi.Year, Donem = fisTarihi.Month, FisNo = $"{TestMarker}-{Guid.NewGuid():N}"[..20],
            FisTarihi = fisTarihi, FisTipi = MuhasebeFisTipleri.Mahsup, Durum = durum, ToplamBorc = 0, ToplamAlacak = 0
        };
        dbContext.MuhasebeFisler.Add(fis);
        await dbContext.SaveChangesAsync();
        _fisIdler.Add(fis.Id);
        return fis.Id;
    }

    public Task InitializeAsync() => Task.CompletedTask;

    private List<STYS.Tests.TestSupport.CleanupAdimi> OlusturCleanupAdimlari() =>
    [
        new("MuhasebeFisler silme", async () =>
        {
            if (_fisIdler.Count == 0) return;
            await using var dbContext = CreateDbContext();
            await dbContext.MuhasebeFisler.IgnoreQueryFilters().Where(x => _fisIdler.Contains(x.Id)).ExecuteDeleteAsync();
        }),
        new("PosTahsilatValorleri silme", async () =>
        {
            if (_valorIdler.Count == 0) return;
            await using var dbContext = CreateDbContext();
            await dbContext.PosTahsilatValorleri.IgnoreQueryFilters().Where(x => _valorIdler.Contains(x.Id)).ExecuteDeleteAsync();
        }),
        new("CariHareketler silme", async () =>
        {
            if (_cariHareketIdler.Count == 0) return;
            await using var dbContext = CreateDbContext();
            await dbContext.CariHareketler.IgnoreQueryFilters().Where(x => _cariHareketIdler.Contains(x.Id)).ExecuteDeleteAsync();
        }),
        new("TahsilatOdemeBelgeleri silme", async () =>
        {
            if (_belgeIdler.Count == 0) return;
            await using var dbContext = CreateDbContext();
            await dbContext.TahsilatOdemeBelgeleri.IgnoreQueryFilters().Where(x => _belgeIdler.Contains(x.Id)).ExecuteDeleteAsync();
        }),
        new("KasaBankaHesaplari silme", async () =>
        {
            if (_kasaBankaHesapIdler.Count == 0) return;
            await using var dbContext = CreateDbContext();
            await dbContext.KasaBankaHesaplari.IgnoreQueryFilters().Where(x => _kasaBankaHesapIdler.Contains(x.Id)).ExecuteDeleteAsync();
        }),
        new("CariKartlar silme", async () =>
        {
            if (_cariKartIdler.Count == 0) return;
            await using var dbContext = CreateDbContext();
            await dbContext.CariKartlar.IgnoreQueryFilters().Where(x => _cariKartIdler.Contains(x.Id)).ExecuteDeleteAsync();
        }),
        new("MuhasebeHesapPlanlari silme", async () =>
        {
            if (_hesapPlaniIdler.Count == 0) return;
            await using var dbContext = CreateDbContext();
            await dbContext.MuhasebeHesapPlanlari.IgnoreQueryFilters().Where(x => _hesapPlaniIdler.Contains(x.Id)).ExecuteDeleteAsync();
        }),
        new("Tesisler silme", async () =>
        {
            if (_tesisIdler.Count == 0) return;
            await using var dbContext = CreateDbContext();
            await dbContext.Tesisler.IgnoreQueryFilters().Where(x => _tesisIdler.Contains(x.Id)).ExecuteDeleteAsync();
        }),
        new("Iller silme", async () =>
        {
            if (_illIdler.Count == 0) return;
            await using var dbContext = CreateDbContext();
            await dbContext.Iller.IgnoreQueryFilters().Where(x => _illIdler.Contains(x.Id)).ExecuteDeleteAsync();
        }),
        new("Kurumlar silme", async () =>
        {
            if (_kurumIdler.Count == 0) return;
            await using var dbContext = CreateDbContext();
            await dbContext.Kurumlar.IgnoreQueryFilters().Where(x => _kurumIdler.Contains(x.Id)).ExecuteDeleteAsync();
        }),
    ];

    private async Task<Dictionary<string, int>> DogrulaTemizlikKalintilariAsync()
    {
        await using var dbContext = CreateDbContext();
        var kalanlar = new Dictionary<string, int>();

        async Task KontrolEt<T>(string tabloAdi, IQueryable<T> sorgu)
        {
            var adet = await sorgu.CountAsync();
            if (adet > 0) kalanlar[tabloAdi] = adet;
        }

        if (_fisIdler.Count > 0) await KontrolEt("MuhasebeFisler", dbContext.MuhasebeFisler.IgnoreQueryFilters().Where(x => _fisIdler.Contains(x.Id)));
        if (_valorIdler.Count > 0) await KontrolEt("PosTahsilatValorleri", dbContext.PosTahsilatValorleri.IgnoreQueryFilters().Where(x => _valorIdler.Contains(x.Id)));
        if (_cariHareketIdler.Count > 0) await KontrolEt("CariHareketler", dbContext.CariHareketler.IgnoreQueryFilters().Where(x => _cariHareketIdler.Contains(x.Id)));
        if (_belgeIdler.Count > 0) await KontrolEt("TahsilatOdemeBelgeleri", dbContext.TahsilatOdemeBelgeleri.IgnoreQueryFilters().Where(x => _belgeIdler.Contains(x.Id)));
        if (_kasaBankaHesapIdler.Count > 0) await KontrolEt("KasaBankaHesaplari", dbContext.KasaBankaHesaplari.IgnoreQueryFilters().Where(x => _kasaBankaHesapIdler.Contains(x.Id)));
        if (_cariKartIdler.Count > 0) await KontrolEt("CariKartlar", dbContext.CariKartlar.IgnoreQueryFilters().Where(x => _cariKartIdler.Contains(x.Id)));
        if (_hesapPlaniIdler.Count > 0) await KontrolEt("MuhasebeHesapPlanlari", dbContext.MuhasebeHesapPlanlari.IgnoreQueryFilters().Where(x => _hesapPlaniIdler.Contains(x.Id)));
        if (_tesisIdler.Count > 0) await KontrolEt("Tesisler", dbContext.Tesisler.IgnoreQueryFilters().Where(x => _tesisIdler.Contains(x.Id)));
        if (_illIdler.Count > 0) await KontrolEt("Iller", dbContext.Iller.IgnoreQueryFilters().Where(x => _illIdler.Contains(x.Id)));
        if (_kurumIdler.Count > 0) await KontrolEt("Kurumlar", dbContext.Kurumlar.IgnoreQueryFilters().Where(x => _kurumIdler.Contains(x.Id)));

        return kalanlar;
    }

    public async Task DisposeAsync()
    {
        if (string.IsNullOrWhiteSpace(ConnectionString))
        {
            return;
        }

        var adimlar = OlusturCleanupAdimlari();
        var hatalar = (await STYS.Tests.TestSupport.TwoPhaseCleanupRunner.CalistirAsync(adimlar)).ToList();

        Dictionary<string, int> kalanlar;
        try
        {
            kalanlar = await DogrulaTemizlikKalintilariAsync();
        }
        catch (Exception ex)
        {
            kalanlar = [];
            hatalar.Add(new InvalidOperationException($"[dogrulama sorgusu basarisiz] {ex.GetType().Name} - {ex.Message}", ex));
        }

        if (kalanlar.Count > 0)
        {
            hatalar.Add(new InvalidOperationException(
                "Cleanup sonrasi kalinti kayit tespit edildi: " + string.Join(", ", kalanlar.Select(kv => $"{kv.Key}={kv.Value}"))));
        }

        if (hatalar.Count > 0)
        {
            throw new AggregateException(
                $"[OdemeIzlemeServiceTests.DisposeAsync] {hatalar.Count} cleanup hatasi (kalinti veri olusmus olabilir).", hatalar);
        }
    }

    // ─────────────────────────────────────────────────────────────
    [IntegrationFact]
    public async Task Arama_FiltrelerDogruUygulanir()
    {
        var suffix = YeniSuffix();
        await using var dbContext = CreateDbContext();
        var tesisId = await YeniTesisAsync(dbContext, suffix);
        var cariId = await YeniCariKartAsync(dbContext, tesisId, suffix);
        var bugun = DateTime.UtcNow.Date;

        await YeniBelgeAsync(dbContext, cariId, 100m, $"{suffix}-A", bugun);
        await YeniBelgeAsync(dbContext, cariId, 500m, $"{suffix}-B", bugun.AddDays(-5));

        var svc = CreateService(dbContext, tesisId);
        var sonuc = await svc.AraAsync(new PagedRequest(), new OdemeAramaFilterDto { TesisId = tesisId, TutarMin = 50m, TutarMax = 200m });

        Assert.Single(sonuc.Items);
        Assert.Equal($"{suffix}-A", sonuc.Items[0].BelgeNo);
    }

    [IntegrationFact]
    public async Task Arama_YetkisizTesisVerisiDonmez()
    {
        var suffixA = YeniSuffix();
        var suffixB = YeniSuffix();
        await using var dbContext = CreateDbContext();
        var tesisA = await YeniTesisAsync(dbContext, suffixA);
        var tesisB = await YeniTesisAsync(dbContext, suffixB);
        var cariA = await YeniCariKartAsync(dbContext, tesisA, suffixA);
        var cariB = await YeniCariKartAsync(dbContext, tesisB, suffixB);
        await YeniBelgeAsync(dbContext, cariA, 100m, $"{suffixA}-A", DateTime.UtcNow.Date);
        await YeniBelgeAsync(dbContext, cariB, 100m, $"{suffixB}-B", DateTime.UtcNow.Date);

        // Yalnizca TesisA'ya erisimi olan kullanici - scope'a gore (TesisId verilmeden) sorgular.
        var svc = CreateService(dbContext, tesisA);
        var sonuc = await svc.AraAsync(new PagedRequest(), new OdemeAramaFilterDto());

        Assert.All(sonuc.Items, x => Assert.StartsWith(suffixA, x.BelgeNo));

        var belgeBId = await dbContext.TahsilatOdemeBelgeleri.IgnoreQueryFilters().Where(b => b.BelgeNo == $"{suffixB}-B").Select(b => b.Id).FirstAsync();
        var ex = await Assert.ThrowsAsync<BaseException>(() => svc.GetDetayAsync(belgeBId));
        Assert.Equal(403, ex.ErrorCode);
    }

    [IntegrationFact]
    public async Task Detay_IbanMaskelenir()
    {
        var suffix = YeniSuffix();
        await using var dbContext = CreateDbContext();
        var tesisId = await YeniTesisAsync(dbContext, suffix);
        var cariId = await YeniCariKartAsync(dbContext, tesisId, suffix);
        var hp = await YeniHesapPlaniAsync(dbContext, suffix, "BANKA");
        var bankaId = await YeniKasaBankaHesabiAsync(dbContext, tesisId, KasaBankaHesapTipleri.Banka, suffix, "BNK", hp, iban: "TR330006100519786457841326");
        var belgeId = await YeniBelgeAsync(dbContext, cariId, 250m, $"{suffix}-A", DateTime.UtcNow.Date, OdemeYontemleri.HavaleEft, bankaId);

        var svc = CreateService(dbContext, tesisId);
        var detay = await svc.GetDetayAsync(belgeId);

        Assert.Equal("TR33 **** **** **** **26", detay.IbanMaskeli);
        Assert.DoesNotContain("0006100519786457841", detay.IbanMaskeli);
    }

    [IntegrationFact]
    public async Task Uyari_OdemeVarFisYok_Tespit()
    {
        var suffix = YeniSuffix();
        await using var dbContext = CreateDbContext();
        var tesisId = await YeniTesisAsync(dbContext, suffix);
        var cariId = await YeniCariKartAsync(dbContext, tesisId, suffix);
        var belgeId = await YeniBelgeAsync(dbContext, cariId, 300m, $"{suffix}-A", DateTime.UtcNow.Date, OdemeYontemleri.Nakit);

        var svc = CreateService(dbContext, tesisId);
        var detay = await svc.GetDetayAsync(belgeId);

        Assert.Contains(detay.Uyarilar, u => u.UyariTipi == OdemeUyariTipleri.OdemeVarFisYok && u.GuvenSeviyesi == OdemeGuvenSeviyeleri.Kesin);
    }

    [IntegrationFact]
    public async Task Uyari_PosVarValorYok_Tespit()
    {
        var suffix = YeniSuffix();
        await using var dbContext = CreateDbContext();
        var tesisId = await YeniTesisAsync(dbContext, suffix);
        var cariId = await YeniCariKartAsync(dbContext, tesisId, suffix);
        var belgeId = await YeniBelgeAsync(dbContext, cariId, 300m, $"{suffix}-A", DateTime.UtcNow.Date, OdemeYontemleri.KrediKarti);

        var svc = CreateService(dbContext, tesisId);
        var detay = await svc.GetDetayAsync(belgeId);

        Assert.Contains(detay.Uyarilar, u => u.UyariTipi == OdemeUyariTipleri.PosVarValorYok);
    }

    /// <summary>OdemeUyariTipleri.MukerrerBelgeNo kontrolu, ayni tesiste (aktif) iki farkli odemenin
    /// AYNI BelgeNo'ya sahip olabilecegi varsayimiyla yazilmisti - ANCAK bu test, veritabanindaki
    /// gercek "IX_TahsilatOdemeBelgeleri_BelgeNo" (unique, IsDeleted=0 filtreli, TUM sistem genelinde
    /// - tek bir tesise ozel DEGIL) kisitinin bunu zaten DB SEVIYESINDE imkansiz kildigini kanitlar.
    /// Bu, kontrolun uygulama kodunda GEREKSIZ oldugu anlamina gelmez (soft-delete edilmis bir kaydin
    /// yaninda yeni bir aktif kaydin ayni numarayla olusmasi teorik olarak hala mumkun olabilir),
    /// ancak pratikte AKTIF-AKTIF mukerrerlik senaryosu bu constraint tarafindan onlenir - bu yuzden
    /// UyariTipleri.MukerrerBelgeNo pratikte NEREDEYSE HIC tetiklenmeyecektir; bu davranissal
    /// dogrulama, bu bulguyu ACIKCA belgeler.</summary>
    [IntegrationFact]
    public async Task MukerrerBelgeNo_AktifAktifSenaryosuDbSeviyesindeZatenEngellenir()
    {
        var suffix = YeniSuffix();
        await using var dbContext = CreateDbContext();
        var tesisId = await YeniTesisAsync(dbContext, suffix);
        var cariId = await YeniCariKartAsync(dbContext, tesisId, suffix);
        await YeniBelgeAsync(dbContext, cariId, 100m, $"{suffix}-AYNI", DateTime.UtcNow.Date);

        var ex = await Assert.ThrowsAsync<Microsoft.EntityFrameworkCore.DbUpdateException>(
            () => YeniBelgeAsync(dbContext, cariId, 200m, $"{suffix}-AYNI", DateTime.UtcNow.Date.AddDays(-1)));

        Assert.Contains("IX_TahsilatOdemeBelgeleri_BelgeNo", ex.InnerException?.Message ?? ex.Message);
    }

    [IntegrationFact]
    public async Task Uyari_AyniTutarAyniTarihFarkliCari_Tespit()
    {
        var suffix = YeniSuffix();
        await using var dbContext = CreateDbContext();
        var tesisId = await YeniTesisAsync(dbContext, suffix);
        var cari1 = await YeniCariKartAsync(dbContext, tesisId, suffix + "1");
        var cari2 = await YeniCariKartAsync(dbContext, tesisId, suffix + "2");
        var hp = await YeniHesapPlaniAsync(dbContext, suffix, "KASA");
        var kasaId = await YeniKasaBankaHesabiAsync(dbContext, tesisId, KasaBankaHesapTipleri.NakitKasa, suffix, "KASA1", hp);
        var bugun = DateTime.UtcNow.Date;

        var belgeId1 = await YeniBelgeAsync(dbContext, cari1, 750m, $"{suffix}-A", bugun, OdemeYontemleri.Nakit, kasaId);
        await YeniBelgeAsync(dbContext, cari2, 750m, $"{suffix}-B", bugun, OdemeYontemleri.Nakit, kasaId);

        var svc = CreateService(dbContext, tesisId);
        var detay = await svc.GetDetayAsync(belgeId1);

        Assert.Contains(detay.Uyarilar, u => u.UyariTipi == OdemeUyariTipleri.AyniTutarAyniTarihFarkliCari && u.GuvenSeviyesi == OdemeGuvenSeviyeleri.IncelenmesiGereken);
    }

    [IntegrationFact]
    public async Task CariHareketDokumu_AcilisBakiyesiVeIptalHaricTutmaDogruHesaplanir()
    {
        var suffix = YeniSuffix();
        await using var dbContext = CreateDbContext();
        var tesisId = await YeniTesisAsync(dbContext, suffix);
        var cariId = await YeniCariKartAsync(dbContext, tesisId, suffix, acilisBakiyeTutari: 1000m, acilisBakiyeYonu: CariKartAcilisBakiyeYonleri.Borc);

        await YeniCariHareketAsync(dbContext, cariId, borc: 0, alacak: 400m, tarih: DateTime.UtcNow.Date.AddDays(-2));
        await YeniCariHareketAsync(dbContext, cariId, borc: 0, alacak: 200m, tarih: DateTime.UtcNow.Date.AddDays(-1), durum: CariHareketDurumlari.Iptal);

        var svc = CreateService(dbContext, tesisId);
        var dokum = await svc.GetCariHareketDokumuAsync(new CariHareketDokumFilterDto { CariKartId = cariId });

        // Acilis 1000 (Borc) - 400 (aktif alacak) = 600; iptal edilen 200'luk hareket HESABA KATILMAZ.
        Assert.Equal(600m, dokum.KalanBakiye);
        Assert.Equal(200m, dokum.ToplamIptalEdilmisTutar);
        Assert.True(dokum.Hareketler.Single(h => h.AlacakTutari == 200m).HesaplamaDisiMi);
    }

    [IntegrationFact]
    public async Task Karsilastir_BelgeNoEslesirseKesinDoner()
    {
        var suffix = YeniSuffix();
        await using var dbContext = CreateDbContext();
        var tesisId = await YeniTesisAsync(dbContext, suffix);
        var cariId = await YeniCariKartAsync(dbContext, tesisId, suffix);
        var bugun = DateTime.UtcNow.Date;
        await YeniBelgeAsync(dbContext, cariId, 450m, $"{suffix}-DEKONT123", bugun);

        var svc = CreateService(dbContext, tesisId);
        var sonuc = await svc.KarsilastirAsync(new BeyanEdilenOdemeKarsilastirmaFilterDto
        {
            TesisId = tesisId, Tarih = DateOnly.FromDateTime(bugun), Tutar = 450m, ParaBirimi = "TRY", BelgeNoTahmini = "DEKONT123"
        });

        Assert.Single(sonuc);
        Assert.Equal(OdemeGuvenSeviyeleri.Kesin, sonuc[0].GuvenSeviyesi);
    }

    [IntegrationFact]
    public async Task Karsilastir_YalnizcaTutarTarihEslesirseIncelenmesiGerekenDoner()
    {
        var suffix = YeniSuffix();
        await using var dbContext = CreateDbContext();
        var tesisId = await YeniTesisAsync(dbContext, suffix);
        var cariId = await YeniCariKartAsync(dbContext, tesisId, suffix);
        var bugun = DateTime.UtcNow.Date;
        await YeniBelgeAsync(dbContext, cariId, 999m, $"{suffix}-X", bugun, OdemeYontemleri.HavaleEft);

        var svc = CreateService(dbContext, tesisId);
        var sonuc = await svc.KarsilastirAsync(new BeyanEdilenOdemeKarsilastirmaFilterDto
        {
            TesisId = tesisId, Tarih = DateOnly.FromDateTime(bugun), Tutar = 999m, ParaBirimi = "TRY"
        });

        Assert.Single(sonuc);
        Assert.Equal(OdemeGuvenSeviyeleri.IncelenmesiGereken, sonuc[0].GuvenSeviyesi);
    }

    [IntegrationFact]
    public async Task Sayfalama_DogruCalisir()
    {
        var suffix = YeniSuffix();
        await using var dbContext = CreateDbContext();
        var tesisId = await YeniTesisAsync(dbContext, suffix);
        var cariId = await YeniCariKartAsync(dbContext, tesisId, suffix);
        var bugun = DateTime.UtcNow.Date;

        for (var i = 0; i < 5; i++)
        {
            await YeniBelgeAsync(dbContext, cariId, 10m + i, $"{suffix}-{i}", bugun.AddDays(-i));
        }

        var svc = CreateService(dbContext, tesisId);
        var sayfa1 = await svc.AraAsync(new PagedRequest { PageNumber = 1, PageSize = 2 }, new OdemeAramaFilterDto { TesisId = tesisId });
        var sayfa2 = await svc.AraAsync(new PagedRequest { PageNumber = 2, PageSize = 2 }, new OdemeAramaFilterDto { TesisId = tesisId });

        Assert.Equal(5, sayfa1.TotalCount);
        Assert.Equal(2, sayfa1.Items.Count);
        Assert.Equal(2, sayfa2.Items.Count);
        Assert.Empty(sayfa1.Items.Select(x => x.Id).Intersect(sayfa2.Items.Select(x => x.Id)));
    }

    // ─────────────────────────────────────────────────────────────
    // Fake'ler
    // ─────────────────────────────────────────────────────────────

    private sealed class FakeCurrentUserAccessor : TOD.Platform.Security.Auth.Services.ICurrentUserAccessor
    {
        public string? GetCurrentUserName() => "odeme-izleme-test";
        public Guid? GetCurrentUserId() => Guid.NewGuid();
    }

    private sealed class FakeCurrentTenantAccessor : TOD.Platform.Security.Auth.Services.ICurrentTenantAccessor
    {
        public int? GetCurrentKurumId() => null;
        public IReadOnlyList<int> GetAccessibleKurumIds() => [];
        public bool IsSuperAdmin() => true;
        public bool IsKurumAdmin() => false;
    }

    private sealed class FakeMuhasebeTesisScopeService : IMuhasebeTesisScopeService
    {
        private readonly HashSet<int> _erisilebilirTesisIds;

        public FakeMuhasebeTesisScopeService(IEnumerable<int> erisilebilirTesisIds)
        {
            _erisilebilirTesisIds = erisilebilirTesisIds.ToHashSet();
        }

        public Task<int[]> GetEffectiveTesisIdsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(_erisilebilirTesisIds.ToArray());

        public Task<int[]> GetEffectiveTesisIdsAsync(DomainAccessScope scope, CancellationToken cancellationToken = default) =>
            Task.FromResult(_erisilebilirTesisIds.ToArray());

        public Task EnsureCanAccessTesisAsync(int tesisId, CancellationToken cancellationToken = default)
        {
            if (!_erisilebilirTesisIds.Contains(tesisId))
            {
                throw new BaseException("Bu tesis için yetkiniz bulunmuyor.", 403);
            }
            return Task.CompletedTask;
        }
    }
}
