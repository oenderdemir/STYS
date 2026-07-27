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
using STYS.Muhasebe.MuhasebeDonemleri.Entities;
using STYS.Muhasebe.MuhasebeFisleri.Entities;
using STYS.Muhasebe.MuhasebeHesapPlanlari.Entities;
using STYS.Muhasebe.OdemeIzleme.Dtos;
using STYS.Muhasebe.OdemeIzleme.Services;
using STYS.Muhasebe.PosTahsilatValorleri.Entities;
using STYS.Muhasebe.TahsilatOdemeBelgeleri.Entities;
using STYS.Rezervasyonlar;
using STYS.Rezervasyonlar.Entities;
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
    private readonly List<int> _donemIdler = [];
    private readonly List<int> _rezervasyonOdemeIdler = [];
    private readonly List<int> _rezervasyonIdler = [];

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

    /// <summary>MaliYil/Donem'i fisTarihi'nden BAGIMSIZ acikca belirleyen varyant - donem
    /// uyumsuzlugu senaryolarini test etmek icin.</summary>
    private async Task<int> YeniFisAsync(StysAppDbContext dbContext, int tesisId, DateTime fisTarihi, string durum, int maliYil, int donem)
    {
        var fis = new MuhasebeFis
        {
            TesisId = tesisId, MaliYil = maliYil, Donem = donem, FisNo = $"{TestMarker}-{Guid.NewGuid():N}"[..20],
            FisTarihi = fisTarihi, FisTipi = MuhasebeFisTipleri.Mahsup, Durum = durum, ToplamBorc = 0, ToplamAlacak = 0
        };
        dbContext.MuhasebeFisler.Add(fis);
        await dbContext.SaveChangesAsync();
        _fisIdler.Add(fis.Id);
        return fis.Id;
    }

    private async Task<int> YeniMuhasebeDonemAsync(
        StysAppDbContext dbContext, int tesisId, int maliYil, int donemNo, DateTime baslangicTarihi, DateTime bitisTarihi)
    {
        var donem = new MuhasebeDonem
        {
            TesisId = tesisId, MaliYil = maliYil, DonemNo = donemNo,
            BaslangicTarihi = baslangicTarihi, BitisTarihi = bitisTarihi, KapaliMi = false
        };
        dbContext.MuhasebeDonemler.Add(donem);
        await dbContext.SaveChangesAsync();
        _donemIdler.Add(donem.Id);
        return donem.Id;
    }

    private async Task YeniRezervasyonOdemeBaglantisiAsync(StysAppDbContext dbContext, int tesisId, string suffix, int belgeId, string referansNo)
    {
        var rezervasyon = new Rezervasyon
        {
            ReferansNo = referansNo, TesisId = tesisId, GirisTarihi = DateTime.UtcNow.Date, CikisTarihi = DateTime.UtcNow.Date.AddDays(1),
            ToplamBazUcret = 100m, ToplamUcret = 100m, ParaBirimi = "TRY",
            MisafirAdiSoyadi = "Test Misafir " + suffix, MisafirTelefon = "0000000000", RezervasyonDurumu = RezervasyonDurumlari.Onayli
        };
        dbContext.Rezervasyonlar.Add(rezervasyon);
        await dbContext.SaveChangesAsync();
        _rezervasyonIdler.Add(rezervasyon.Id);

        var rezervasyonOdeme = new RezervasyonOdeme
        {
            RezervasyonId = rezervasyon.Id, OdemeTutari = 100m, ParaBirimi = "TRY",
            OdemeTipi = OdemeTipleri.Nakit, TahsilatOdemeBelgesiId = belgeId, Durum = RezervasyonOdemeDurumlari.Aktif
        };
        dbContext.RezervasyonOdemeler.Add(rezervasyonOdeme);
        await dbContext.SaveChangesAsync();
        _rezervasyonOdemeIdler.Add(rezervasyonOdeme.Id);
    }

    public Task InitializeAsync() => Task.CompletedTask;

    private List<STYS.Tests.TestSupport.CleanupAdimi> OlusturCleanupAdimlari() =>
    [
        new("MuhasebeDonemler silme", async () =>
        {
            if (_donemIdler.Count == 0) return;
            await using var dbContext = CreateDbContext();
            await dbContext.MuhasebeDonemler.IgnoreQueryFilters().Where(x => _donemIdler.Contains(x.Id)).ExecuteDeleteAsync();
        }),
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
        new("RezervasyonOdemeler silme", async () =>
        {
            if (_rezervasyonOdemeIdler.Count == 0) return;
            await using var dbContext = CreateDbContext();
            await dbContext.RezervasyonOdemeler.IgnoreQueryFilters().Where(x => _rezervasyonOdemeIdler.Contains(x.Id)).ExecuteDeleteAsync();
        }),
        new("Rezervasyonlar silme", async () =>
        {
            if (_rezervasyonIdler.Count == 0) return;
            await using var dbContext = CreateDbContext();
            await dbContext.Rezervasyonlar.IgnoreQueryFilters().Where(x => _rezervasyonIdler.Contains(x.Id)).ExecuteDeleteAsync();
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

        if (_donemIdler.Count > 0) await KontrolEt("MuhasebeDonemler", dbContext.MuhasebeDonemler.IgnoreQueryFilters().Where(x => _donemIdler.Contains(x.Id)));
        if (_fisIdler.Count > 0) await KontrolEt("MuhasebeFisler", dbContext.MuhasebeFisler.IgnoreQueryFilters().Where(x => _fisIdler.Contains(x.Id)));
        if (_valorIdler.Count > 0) await KontrolEt("PosTahsilatValorleri", dbContext.PosTahsilatValorleri.IgnoreQueryFilters().Where(x => _valorIdler.Contains(x.Id)));
        if (_cariHareketIdler.Count > 0) await KontrolEt("CariHareketler", dbContext.CariHareketler.IgnoreQueryFilters().Where(x => _cariHareketIdler.Contains(x.Id)));
        if (_rezervasyonOdemeIdler.Count > 0) await KontrolEt("RezervasyonOdemeler", dbContext.RezervasyonOdemeler.IgnoreQueryFilters().Where(x => _rezervasyonOdemeIdler.Contains(x.Id)));
        if (_rezervasyonIdler.Count > 0) await KontrolEt("Rezervasyonlar", dbContext.Rezervasyonlar.IgnoreQueryFilters().Where(x => _rezervasyonIdler.Contains(x.Id)));
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

    // ─────────────────────────────────────────────────────────────
    // Fis donem dogrulamasinin GERCEK GetDetayAsync akisina baglanmasi (bu turun duzeltmesi) -
    // MuhasebeDonem kaydindan cozulen MaliYil/DonemNo/tarih araligi Degerlendir'e GERCEKTEN
    // gecirildigini kanitlar (yalnizca MuhasebeFisDogrulama birim testi DEGIL).
    // ─────────────────────────────────────────────────────────────

    [IntegrationFact]
    public async Task Detay_FisMaliYiliBeklenenDonemdenFarkli_FisMaliYiliUyumsuzDoner()
    {
        var suffix = YeniSuffix();
        await using var dbContext = CreateDbContext();
        var tesisId = await YeniTesisAsync(dbContext, suffix);
        var cariId = await YeniCariKartAsync(dbContext, tesisId, suffix);
        var bugun = DateTime.UtcNow.Date;

        // Odeme tarihinin GERCEKTEN ait oldugu donem: MaliYil=2026, DonemNo=bugun.Month.
        await YeniMuhasebeDonemAsync(dbContext, tesisId, 2026, bugun.Month,
            new DateTime(bugun.Year, bugun.Month, 1), new DateTime(bugun.Year, bugun.Month, 1).AddMonths(1).AddDays(-1));

        // Fis AYNI donem numarasini ve tarihini tasiyor ama FARKLI bir mali yila (2025) ait.
        var fisId = await YeniFisAsync(dbContext, tesisId, bugun, MuhasebeFisDurumlari.Onayli, maliYil: 2025, donem: bugun.Month);
        var belgeId = await YeniBelgeAsync(dbContext, cariId, 300m, $"{suffix}-A", bugun, OdemeYontemleri.Nakit, muhasebeFisId: fisId);

        var svc = CreateService(dbContext, tesisId);
        var detay = await svc.GetDetayAsync(belgeId);

        Assert.Contains(FisGecersizlikNedenKodlari.FisMaliYiliUyumsuz, detay.BakiyeyeDahilEdilmemeNedenKodlari);
        Assert.Contains(detay.BakiyeyeDahilEdilmemeAciklamalari, a => a.Contains("mali yılı"));
    }

    [IntegrationFact]
    public async Task Detay_FisDonemNoBeklenenDonemdenFarkli_FisDonemNoUyumsuzDoner()
    {
        var suffix = YeniSuffix();
        await using var dbContext = CreateDbContext();
        var tesisId = await YeniTesisAsync(dbContext, suffix);
        var cariId = await YeniCariKartAsync(dbContext, tesisId, suffix);
        var bugun = DateTime.UtcNow.Date;

        await YeniMuhasebeDonemAsync(dbContext, tesisId, 2026, bugun.Month,
            new DateTime(bugun.Year, bugun.Month, 1), new DateTime(bugun.Year, bugun.Month, 1).AddMonths(1).AddDays(-1));

        // Fis AYNI mali yili ve tarihini tasiyor ama FARKLI bir donem no'suna ait.
        var farkliDonemNo = bugun.Month == 1 ? 2 : 1;
        var fisId = await YeniFisAsync(dbContext, tesisId, bugun, MuhasebeFisDurumlari.Onayli, maliYil: 2026, donem: farkliDonemNo);
        var belgeId = await YeniBelgeAsync(dbContext, cariId, 300m, $"{suffix}-A", bugun, OdemeYontemleri.Nakit, muhasebeFisId: fisId);

        var svc = CreateService(dbContext, tesisId);
        var detay = await svc.GetDetayAsync(belgeId);

        Assert.Contains(FisGecersizlikNedenKodlari.FisDonemNoUyumsuz, detay.BakiyeyeDahilEdilmemeNedenKodlari);
        Assert.Contains(detay.BakiyeyeDahilEdilmemeAciklamalari, a => a.Contains("dönemi"));
    }

    [IntegrationFact]
    public async Task Detay_FisMaliYilDonemVeTarihiUyumlu_DonemHatasiUretilmez()
    {
        var suffix = YeniSuffix();
        await using var dbContext = CreateDbContext();
        var tesisId = await YeniTesisAsync(dbContext, suffix);
        var cariId = await YeniCariKartAsync(dbContext, tesisId, suffix);
        var bugun = DateTime.UtcNow.Date;

        await YeniMuhasebeDonemAsync(dbContext, tesisId, 2026, bugun.Month,
            new DateTime(bugun.Year, bugun.Month, 1), new DateTime(bugun.Year, bugun.Month, 1).AddMonths(1).AddDays(-1));

        // Fis mali yili, donemi VE tarihi beklenen donemle TAM uyumlu.
        var fisId = await YeniFisAsync(dbContext, tesisId, bugun, MuhasebeFisDurumlari.Onayli, maliYil: 2026, donem: bugun.Month);
        var belgeId = await YeniBelgeAsync(dbContext, cariId, 300m, $"{suffix}-A", bugun, OdemeYontemleri.Nakit, muhasebeFisId: fisId);

        var svc = CreateService(dbContext, tesisId);
        var detay = await svc.GetDetayAsync(belgeId);

        Assert.DoesNotContain(FisGecersizlikNedenKodlari.FisMaliYiliUyumsuz, detay.BakiyeyeDahilEdilmemeNedenKodlari);
        Assert.DoesNotContain(FisGecersizlikNedenKodlari.FisDonemNoUyumsuz, detay.BakiyeyeDahilEdilmemeNedenKodlari);
        Assert.DoesNotContain(FisGecersizlikNedenKodlari.FisDonemiUyumsuz, detay.BakiyeyeDahilEdilmemeNedenKodlari);
        Assert.DoesNotContain(FisGecersizlikNedenKodlari.FisTarihiYok, detay.BakiyeyeDahilEdilmemeNedenKodlari);
    }

    [IntegrationFact]
    public async Task Detay_OdemeTarihineKarsilikGelenMuhasebeDonemiYok_MuhasebeDonemiBulunamadiDoner()
    {
        var suffix = YeniSuffix();
        await using var dbContext = CreateDbContext();
        var tesisId = await YeniTesisAsync(dbContext, suffix);
        var cariId = await YeniCariKartAsync(dbContext, tesisId, suffix);
        var bugun = DateTime.UtcNow.Date;

        // KASITLI: bu tesis icin HICBIR MuhasebeDonem kaydi olusturulmuyor - odeme tarihine
        // karsilik gelen bir donem tanimi YOK.
        var fisId = await YeniFisAsync(dbContext, tesisId, bugun, MuhasebeFisDurumlari.Onayli, maliYil: 2026, donem: bugun.Month);
        var belgeId = await YeniBelgeAsync(dbContext, cariId, 300m, $"{suffix}-A", bugun, OdemeYontemleri.Nakit, muhasebeFisId: fisId);

        var svc = CreateService(dbContext, tesisId);
        var detay = await svc.GetDetayAsync(belgeId);

        Assert.Contains(BakiyeyeDahilEdilmemeNedenKodlari.MuhasebeDonemiBulunamadi, detay.BakiyeyeDahilEdilmemeNedenKodlari);
        Assert.NotEqual(BakiyeyeDahilEdilmeDurumlari.TamamenDahil, detay.BakiyeyeDahilEdilmeDurumu);
    }

    [IntegrationFact]
    public async Task Detay_OdemeTarihineKarsilikGelenAktifMuhasebeDonemiVar_MuhasebeDonemiBulunamadiUretilmez()
    {
        var suffix = YeniSuffix();
        await using var dbContext = CreateDbContext();
        var tesisId = await YeniTesisAsync(dbContext, suffix);
        var cariId = await YeniCariKartAsync(dbContext, tesisId, suffix);
        var bugun = DateTime.UtcNow.Date;

        await YeniMuhasebeDonemAsync(dbContext, tesisId, 2026, bugun.Month,
            new DateTime(bugun.Year, bugun.Month, 1), new DateTime(bugun.Year, bugun.Month, 1).AddMonths(1).AddDays(-1));

        var fisId = await YeniFisAsync(dbContext, tesisId, bugun, MuhasebeFisDurumlari.Onayli, maliYil: 2026, donem: bugun.Month);
        var belgeId = await YeniBelgeAsync(dbContext, cariId, 300m, $"{suffix}-A", bugun, OdemeYontemleri.Nakit, muhasebeFisId: fisId);

        var svc = CreateService(dbContext, tesisId);
        var detay = await svc.GetDetayAsync(belgeId);

        Assert.DoesNotContain(BakiyeyeDahilEdilmemeNedenKodlari.MuhasebeDonemiBulunamadi, detay.BakiyeyeDahilEdilmemeNedenKodlari);
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

        // Acilis bakiyesinin para birimi guvenilir bicimde bilinmedigi icin (madde 8) TRY
        // VARSAYILMAZ - acilis 1000 "Bilinmiyor" grubuna gider, TRY toplami YALNIZCA aktif
        // hareketten (0 - 400) olusur; iptal edilen 200'luk hareket HESABA KATILMAZ.
        var tryOzet = dokum.ParaBirimiOzetleri.Single(x => x.ParaBirimi == "TRY");
        Assert.Equal(-400m, tryOzet.AciklananKalanBakiye);
        Assert.Equal(200m, tryOzet.IptalEdilmisTutar);
        Assert.True(dokum.Hareketler.Single(h => h.AlacakTutari == 200m).HesaplamaDisiMi);

        var bilinmiyorOzet = dokum.ParaBirimiOzetleri.Single(x => x.ParaBirimi == "Bilinmiyor");
        Assert.Equal(1000m, bilinmiyorOzet.DevredenBakiye);
        Assert.Equal(1000m, bilinmiyorOzet.AciklananKalanBakiye);
    }

    [IntegrationFact]
    public async Task Karsilastir_BelgeNoEslesirseKesinDoner()
    {
        var suffix = YeniSuffix();
        await using var dbContext = CreateDbContext();
        var tesisId = await YeniTesisAsync(dbContext, suffix);
        var cariId = await YeniCariKartAsync(dbContext, tesisId, suffix);
        var bugun = DateTime.UtcNow.Date;
        var belgeNo = $"{suffix}-DEKONT123";
        await YeniBelgeAsync(dbContext, cariId, 450m, belgeNo, bugun);

        var svc = CreateService(dbContext, tesisId);
        // KESIN eslesme icin referansin TAMAMI verilmelidir. Normalizasyon yalnizca ayirici
        // karakterleri/buyuk-kucuk harfi tolere eder, KISMI metni degil.
        var sonuc = await svc.KarsilastirAsync(new BeyanEdilenOdemeKarsilastirmaFilterDto
        {
            TesisId = tesisId, Tarih = DateOnly.FromDateTime(bugun), Tutar = 450m, ParaBirimi = "TRY",
            BelgeNoTahmini = belgeNo.ToLowerInvariant()
        });

        Assert.Single(sonuc);
        Assert.Equal(OdemeGuvenSeviyeleri.Kesin, sonuc[0].GuvenSeviyesi);
        Assert.True(sonuc[0].TarihBirebirMi);
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

    [IntegrationFact]
    public async Task Karsilastir_KismiBelgeNo_KESIN_ESLESME_URETMEZ()
    {
        var suffix = YeniSuffix();
        await using var dbContext = CreateDbContext();
        var tesisId = await YeniTesisAsync(dbContext, suffix);
        var cariId = await YeniCariKartAsync(dbContext, tesisId, suffix);
        var bugun = DateTime.UtcNow.Date;
        await YeniBelgeAsync(dbContext, cariId, 450m, $"{suffix}-DEKONT123456", bugun);

        var svc = CreateService(dbContext, tesisId);
        // Belge no'nun yalnizca BIR PARCASI verildi - eskiden Contains nedeniyle "Kesin" doniyordu.
        var sonuc = await svc.KarsilastirAsync(new BeyanEdilenOdemeKarsilastirmaFilterDto
        {
            TesisId = tesisId, Tarih = DateOnly.FromDateTime(bugun), Tutar = 450m, ParaBirimi = "TRY", BelgeNoTahmini = "DEKONT"
        });

        Assert.Single(sonuc);
        Assert.NotEqual(OdemeGuvenSeviyeleri.Kesin, sonuc[0].GuvenSeviyesi);
        Assert.Contains(sonuc[0].UyusmayanAlanlar, x => x.Contains("Belge", StringComparison.OrdinalIgnoreCase));
    }

    [IntegrationFact]
    public async Task Karsilastir_CokKisaReferans_ValidationHatasiVerir()
    {
        var suffix = YeniSuffix();
        await using var dbContext = CreateDbContext();
        var tesisId = await YeniTesisAsync(dbContext, suffix);
        var svc = CreateService(dbContext, tesisId);

        var ex = await Assert.ThrowsAsync<BaseException>(() => svc.KarsilastirAsync(new BeyanEdilenOdemeKarsilastirmaFilterDto
        {
            TesisId = tesisId, Tarih = DateOnly.FromDateTime(DateTime.UtcNow.Date), Tutar = 100m, ParaBirimi = "TRY", BelgeNoTahmini = "A1"
        }));

        Assert.Equal(400, ex.ErrorCode);
    }

    [IntegrationFact]
    public async Task Karsilastir_ToleransliTarih_BirebirOlarakRaporlanmaz()
    {
        var suffix = YeniSuffix();
        await using var dbContext = CreateDbContext();
        var tesisId = await YeniTesisAsync(dbContext, suffix);
        var cariId = await YeniCariKartAsync(dbContext, tesisId, suffix);
        var bugun = DateTime.UtcNow.Date;
        await YeniBelgeAsync(dbContext, cariId, 300m, $"{suffix}-REF9988", bugun.AddDays(-1));

        var svc = CreateService(dbContext, tesisId);
        var sonuc = await svc.KarsilastirAsync(new BeyanEdilenOdemeKarsilastirmaFilterDto
        {
            TesisId = tesisId, Tarih = DateOnly.FromDateTime(bugun), Tutar = 300m, ParaBirimi = "TRY",
            TarihToleransGun = 2, BelgeNoTahmini = $"{suffix}-REF9988"
        });

        Assert.Single(sonuc);
        Assert.Equal(OdemeGuvenSeviyeleri.Kesin, sonuc[0].GuvenSeviyesi); // referans birebir
        Assert.False(sonuc[0].TarihBirebirMi);                            // ama tarih birebir DEGIL
        Assert.Equal(1, sonuc[0].TarihFarkiGun);
        Assert.Contains("birebir DEĞİL", sonuc[0].Gerekce);
    }

    [IntegrationFact]
    public async Task BakiyeyeDahil_AktifBelgeFakatCariHareketiYok_OtomatikDahilSayilmaz()
    {
        var suffix = YeniSuffix();
        await using var dbContext = CreateDbContext();
        var tesisId = await YeniTesisAsync(dbContext, suffix);
        var cariId = await YeniCariKartAsync(dbContext, tesisId, suffix);
        var hedefHareketId = await YeniCariHareketAsync(dbContext, cariId, borc: 500m, alacak: 0m, tarih: DateTime.UtcNow.Date);

        // Belge bir borcu kapatmak uzere isaretlenmis AMA karsilik gelen kapama hareketi olusmamis.
        var belgeId = await YeniBelgeAsync(dbContext, cariId, 500m, $"{suffix}-A", DateTime.UtcNow.Date,
            OdemeYontemleri.Nakit, kapatilacakCariHareketId: hedefHareketId);

        var svc = CreateService(dbContext, tesisId);
        var detay = await svc.GetDetayAsync(belgeId);

        Assert.False(detay.BakiyeyeDahilMi);
        Assert.Equal(BakiyeyeDahilEdilmeDurumlari.DahilDegil, detay.BakiyeyeDahilEdilmeDurumu);
        Assert.Contains(BakiyeyeDahilEdilmemeNedenKodlari.CariHareketiYok, detay.BakiyeyeDahilEdilmemeNedenKodlari);
        Assert.Contains(BakiyeyeDahilEdilmemeNedenKodlari.ZorunluMuhasebeFisiYok, detay.BakiyeyeDahilEdilmemeNedenKodlari);
    }

    [IntegrationFact]
    public async Task CariDokum_MutabakatVeHataliPos_NormalBekleyenIleBIRLESTIRILMEZ()
    {
        var suffix = YeniSuffix();
        await using var dbContext = CreateDbContext();
        var tesisId = await YeniTesisAsync(dbContext, suffix);
        var cariId = await YeniCariKartAsync(dbContext, tesisId, suffix);
        var hp = await YeniHesapPlaniAsync(dbContext, suffix, "KK");
        var kkId = await YeniKasaBankaHesabiAsync(dbContext, tesisId, KasaBankaHesapTipleri.KrediKarti, suffix, "KK1", hp);
        var bugun = DateOnly.FromDateTime(DateTime.UtcNow.Date);

        var b1 = await YeniBelgeAsync(dbContext, cariId, 100m, $"{suffix}-1", DateTime.UtcNow.Date, OdemeYontemleri.KrediKarti, kkId);
        await YeniValorAsync(dbContext, tesisId, b1, kkId, null, PosTahsilatValorDurumlari.ValorBekliyor, bugun, 100m);
        var b2 = await YeniBelgeAsync(dbContext, cariId, 200m, $"{suffix}-2", DateTime.UtcNow.Date, OdemeYontemleri.KrediKarti, kkId);
        await YeniValorAsync(dbContext, tesisId, b2, kkId, null, PosTahsilatValorDurumlari.MutabakatBekliyor, bugun, 200m);
        var b3 = await YeniBelgeAsync(dbContext, cariId, 400m, $"{suffix}-3", DateTime.UtcNow.Date, OdemeYontemleri.KrediKarti, kkId);
        await YeniValorAsync(dbContext, tesisId, b3, kkId, null, PosTahsilatValorDurumlari.Hata, bugun, 400m);

        var svc = CreateService(dbContext, tesisId);
        var dokum = await svc.GetCariHareketDokumuAsync(new CariHareketDokumFilterDto { CariKartId = cariId });

        var tryOzet = dokum.ParaBirimiOzetleri.Single(x => x.ParaBirimi == "TRY");
        Assert.Equal(100m, tryOzet.NormalAktarilmayiBekleyenPos);
        Assert.Equal(200m, tryOzet.MutabakatBekleyenPos);
        Assert.Equal(400m, tryOzet.HataliPos);
    }

    [IntegrationFact]
    public async Task CariDokum_FarkliParaBirimleri_TekToplamdaBIRLESTIRILMEZ()
    {
        var suffix = YeniSuffix();
        await using var dbContext = CreateDbContext();
        var tesisId = await YeniTesisAsync(dbContext, suffix);
        var cariId = await YeniCariKartAsync(dbContext, tesisId, suffix);

        await YeniCariHareketAsync(dbContext, cariId, borc: 1000m, alacak: 0m, tarih: DateTime.UtcNow.Date);
        var usdHareketId = await YeniCariHareketAsync(dbContext, cariId, borc: 50m, alacak: 0m, tarih: DateTime.UtcNow.Date);
        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE [Muhasebe].[CariHareketler] SET [ParaBirimi] = 'USD' WHERE [Id] = {usdHareketId}");

        var svc = CreateService(dbContext, tesisId);
        var dokum = await svc.GetCariHareketDokumuAsync(new CariHareketDokumFilterDto { CariKartId = cariId });

        Assert.Equal(2, dokum.ParaBirimiOzetleri.Count);
        Assert.Equal(1000m, dokum.ParaBirimiOzetleri.Single(x => x.ParaBirimi == "TRY").AciklananKalanBakiye);
        Assert.Equal(50m, dokum.ParaBirimiOzetleri.Single(x => x.ParaBirimi == "USD").AciklananKalanBakiye);
    }

    // ─────────────────────────────────────────────────────────────
    // Fis dogrulamasi (madde 3) - GERCEK SQL Server
    // ─────────────────────────────────────────────────────────────

    /// <summary>Bu test, "MuhasebeFisId dolu ama fis FIZIKSEL olarak yok" senaryosunun veritabanindaki
    /// gercek FK kisiti (FK_TahsilatOdemeBelgeleri_MuhasebeFisler_MuhasebeFisId) tarafindan zaten
    /// engellendigini KANITLAR. Dolayisiyla pratikte "fis bulunamadi" durumu yalnizca SOFT-DELETE
    /// yoluyla olusabilir (bkz. BakiyeyeDahil_SoftDeleteEdilmisFis_GecerliKabulEdilmez). Uygulama
    /// katmanindaki FisBulunamadi kontrolu yine de korunur - savunma amaclidir ve fisin yetki
    /// kapsami disinda kalmasi gibi durumlari da kapsar.</summary>
    [IntegrationFact]
    public async Task MuhasebeFisIdDangling_DbSeviyesindeZatenEngellenir()
    {
        var suffix = YeniSuffix();
        await using var dbContext = CreateDbContext();
        var tesisId = await YeniTesisAsync(dbContext, suffix);
        var cariId = await YeniCariKartAsync(dbContext, tesisId, suffix);
        var hp = await YeniHesapPlaniAsync(dbContext, suffix, "KASA");
        var kasaId = await YeniKasaBankaHesabiAsync(dbContext, tesisId, KasaBankaHesapTipleri.NakitKasa, suffix, "KASA1", hp);
        var fisId = await YeniFisAsync(dbContext, tesisId, DateTime.UtcNow.Date, MuhasebeFisDurumlari.Onayli);
        var belgeId = await YeniBelgeAsync(dbContext, cariId, 500m, $"{suffix}-A", DateTime.UtcNow.Date,
            OdemeYontemleri.Nakit, kasaId, muhasebeFisId: fisId);

        // Var olmayan bir fis id'sine isaret ettirmeye calis - FK kisiti bunu REDDETMELI.
        var ex = await Assert.ThrowsAsync<Microsoft.Data.SqlClient.SqlException>(() =>
            dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE [Muhasebe].[TahsilatOdemeBelgeleri] SET [MuhasebeFisId] = 999999999 WHERE [Id] = {belgeId}"));

        Assert.Contains("FK_TahsilatOdemeBelgeleri_MuhasebeFisler_MuhasebeFisId", ex.Message);
    }

    [IntegrationFact]
    public async Task BakiyeyeDahil_SoftDeleteEdilmisFis_GecerliKabulEdilmez()
    {
        var suffix = YeniSuffix();
        await using var dbContext = CreateDbContext();
        var tesisId = await YeniTesisAsync(dbContext, suffix);
        var cariId = await YeniCariKartAsync(dbContext, tesisId, suffix);
        var hp = await YeniHesapPlaniAsync(dbContext, suffix, "KASA");
        var kasaId = await YeniKasaBankaHesabiAsync(dbContext, tesisId, KasaBankaHesapTipleri.NakitKasa, suffix, "KASA1", hp);
        var fisId = await YeniFisAsync(dbContext, tesisId, DateTime.UtcNow.Date, MuhasebeFisDurumlari.Onayli);
        var belgeId = await YeniBelgeAsync(dbContext, cariId, 500m, $"{suffix}-A", DateTime.UtcNow.Date,
            OdemeYontemleri.Nakit, kasaId, muhasebeFisId: fisId);

        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE [Muhasebe].[MuhasebeFisler] SET [IsDeleted] = 1 WHERE [Id] = {fisId}");

        var svc = CreateService(dbContext, tesisId);
        var detay = await svc.GetDetayAsync(belgeId);

        Assert.NotEqual(BakiyeyeDahilEdilmeDurumlari.TamamenDahil, detay.BakiyeyeDahilEdilmeDurumu);
        Assert.Contains(FisGecersizlikNedenKodlari.FisSoftDeleteEdilmis, detay.BakiyeyeDahilEdilmemeNedenKodlari);
    }

    [IntegrationFact]
    public async Task BakiyeyeDahil_FisSatirindaOdemeninHesabiEtkilenmemis_GecerliKabulEdilmez()
    {
        var suffix = YeniSuffix();
        await using var dbContext = CreateDbContext();
        var tesisId = await YeniTesisAsync(dbContext, suffix);
        var cariId = await YeniCariKartAsync(dbContext, tesisId, suffix);
        var hp = await YeniHesapPlaniAsync(dbContext, suffix, "KASA");
        var kasaId = await YeniKasaBankaHesabiAsync(dbContext, tesisId, KasaBankaHesapTipleri.NakitKasa, suffix, "KASA1", hp);
        // Fis olusturulur ama HICBIR satiri odemenin kasa hesabini etkilemez.
        var fisId = await YeniFisAsync(dbContext, tesisId, DateTime.UtcNow.Date, MuhasebeFisDurumlari.Onayli);
        var belgeId = await YeniBelgeAsync(dbContext, cariId, 500m, $"{suffix}-A", DateTime.UtcNow.Date,
            OdemeYontemleri.Nakit, kasaId, muhasebeFisId: fisId);

        var svc = CreateService(dbContext, tesisId);
        var detay = await svc.GetDetayAsync(belgeId);

        Assert.Contains(FisGecersizlikNedenKodlari.FisSatirindaBeklenenHesapYok, detay.BakiyeyeDahilEdilmemeNedenKodlari);
    }

    // ─────────────────────────────────────────────────────────────
    // Tarih araligi ve devreden bakiye (madde 6)
    // ─────────────────────────────────────────────────────────────

    [IntegrationFact]
    public async Task CariDokum_BaslangicOncesiHareketler_DevredenBakiyeyeGirer()
    {
        var suffix = YeniSuffix();
        await using var dbContext = CreateDbContext();
        var tesisId = await YeniTesisAsync(dbContext, suffix);
        var cariId = await YeniCariKartAsync(dbContext, tesisId, suffix, acilisBakiyeTutari: 100m, acilisBakiyeYonu: CariKartAcilisBakiyeYonleri.Borc);

        var bugun = DateTime.UtcNow.Date;
        // Donem ONCESI: 500 borc -> devreden bakiyeye girmeli (acilis 100 + 500 = 600)
        await YeniCariHareketAsync(dbContext, cariId, borc: 500m, alacak: 0m, tarih: bugun.AddDays(-30));
        // Donem ICI: 200 alacak
        await YeniCariHareketAsync(dbContext, cariId, borc: 0m, alacak: 200m, tarih: bugun.AddDays(-5));
        // Donem SONRASI: 999 borc -> hicbir toplama girmemeli
        await YeniCariHareketAsync(dbContext, cariId, borc: 999m, alacak: 0m, tarih: bugun.AddDays(30));

        var svc = CreateService(dbContext, tesisId);
        var dokum = await svc.GetCariHareketDokumuAsync(new CariHareketDokumFilterDto
        {
            CariKartId = cariId,
            TarihBaslangic = DateOnly.FromDateTime(bugun.AddDays(-10)),
            TarihBitis = DateOnly.FromDateTime(bugun)
        });

        // Acilis bakiyesi (100) artik TRY VARSAYILMAZ (madde 8) - "Bilinmiyor" grubuna gider.
        // TRY devreden bakiyesi YALNIZCA donem oncesi GERCEK hareketten (500) olusur.
        var tryOzet = dokum.ParaBirimiOzetleri.Single(x => x.ParaBirimi == "TRY");
        Assert.Equal(500m, tryOzet.DevredenBakiye);               // donem oncesi 500 (acilis DAHIL DEGIL)
        Assert.Equal(200m, tryOzet.ToplamAlacak);                 // yalnizca donem ici
        Assert.Equal(0m, tryOzet.ToplamBorc);                     // donem sonrasi 999 GIRMEZ
        Assert.Equal(300m, tryOzet.AciklananKalanBakiye);         // 500 - 200
        Assert.Single(dokum.Hareketler);                          // yalnizca donem ici hareket listelenir

        var bilinmiyorOzet = dokum.ParaBirimiOzetleri.Single(x => x.ParaBirimi == "Bilinmiyor");
        Assert.Equal(100m, bilinmiyorOzet.DevredenBakiye);
        Assert.Equal(100m, bilinmiyorOzet.AciklananKalanBakiye);
    }

    [IntegrationFact]
    public async Task CariDokum_BosParaBirimi_TRYKabulEdilmez()
    {
        var suffix = YeniSuffix();
        await using var dbContext = CreateDbContext();
        var tesisId = await YeniTesisAsync(dbContext, suffix);
        var cariId = await YeniCariKartAsync(dbContext, tesisId, suffix);

        var hareketId = await YeniCariHareketAsync(dbContext, cariId, borc: 250m, alacak: 0m, tarih: DateTime.UtcNow.Date);
        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE [Muhasebe].[CariHareketler] SET [ParaBirimi] = '' WHERE [Id] = {hareketId}");

        var svc = CreateService(dbContext, tesisId);
        var dokum = await svc.GetCariHareketDokumuAsync(new CariHareketDokumFilterDto { CariKartId = cariId });

        // TRY grubuna KARISMAMALI - ayri "Bilinmiyor" grubunda olmali.
        Assert.DoesNotContain(dokum.ParaBirimiOzetleri, x => x.ParaBirimi == "TRY" && x.ToplamBorc == 250m);
        Assert.Contains(dokum.ParaBirimiOzetleri, x => x.ParaBirimi == "Bilinmiyor" && x.ToplamBorc == 250m);
        Assert.Contains(dokum.Uyarilar, u => u.Contains("Para birimi tanımsız"));
    }

    // ─────────────────────────────────────────────────────────────
    // Eslesme tekilligi (madde 8)
    // ─────────────────────────────────────────────────────────────

    [IntegrationFact]
    public async Task Karsilastir_AyniReferansaBirdenFazlaAday_KESIN_URETMEZ()
    {
        var suffix = YeniSuffix();
        await using var dbContext = CreateDbContext();
        var tesisId = await YeniTesisAsync(dbContext, suffix);
        var cariId = await YeniCariKartAsync(dbContext, tesisId, suffix);
        var bugun = DateTime.UtcNow.Date;

        // Normalize edildiginde AYNI degere donusen iki farkli belge no (ayirici karakter farki).
        await YeniBelgeAsync(dbContext, cariId, 700m, $"{suffix}-REF-5001", bugun);
        await YeniBelgeAsync(dbContext, cariId, 700m, $"{suffix}REF5001", bugun);

        var svc = CreateService(dbContext, tesisId);
        var sonuc = await svc.KarsilastirAsync(new BeyanEdilenOdemeKarsilastirmaFilterDto
        {
            TesisId = tesisId, Tarih = DateOnly.FromDateTime(bugun), Tutar = 700m, ParaBirimi = "TRY",
            BelgeNoTahmini = $"{suffix}-REF-5001"
        });

        Assert.NotEmpty(sonuc);
        Assert.DoesNotContain(sonuc, x => x.GuvenSeviyesi == OdemeGuvenSeviyeleri.Kesin);
        Assert.Contains(sonuc, x => x.ReferansTekilMi == false);
    }

    [IntegrationFact]
    public async Task Karsilastir_YontemCeliskisi_KESIN_URETMEZ()
    {
        var suffix = YeniSuffix();
        await using var dbContext = CreateDbContext();
        var tesisId = await YeniTesisAsync(dbContext, suffix);
        var cariId = await YeniCariKartAsync(dbContext, tesisId, suffix);
        var bugun = DateTime.UtcNow.Date;
        var belgeNo = $"{suffix}-TEKREF77";
        await YeniBelgeAsync(dbContext, cariId, 300m, belgeNo, bugun, OdemeYontemleri.Nakit);

        var svc = CreateService(dbContext, tesisId);
        // Referans birebir eslesiyor AMA beyan edilen yontem (HavaleEft) kayitla CELISIYOR.
        var sonuc = await svc.KarsilastirAsync(new BeyanEdilenOdemeKarsilastirmaFilterDto
        {
            TesisId = tesisId, Tarih = DateOnly.FromDateTime(bugun), Tutar = 300m, ParaBirimi = "TRY",
            BelgeNoTahmini = belgeNo, OdemeYontemi = OdemeYontemleri.HavaleEft
        });

        Assert.Single(sonuc);
        Assert.NotEqual(OdemeGuvenSeviyeleri.Kesin, sonuc[0].GuvenSeviyesi);
        Assert.Contains(sonuc[0].UyusmayanAlanlar, x => x.Contains("yöntem", StringComparison.OrdinalIgnoreCase));
    }

    // ─────────────────────────────────────────────────────────────
    // Capraz-kaynak arastirma (madde 2) - GERCEK SQL Server
    // ─────────────────────────────────────────────────────────────

    private static OdemeCaprazAramaService CreateCaprazServis(StysAppDbContext dbContext, params int[] tesisIds) =>
        new(dbContext, new FakeMuhasebeTesisScopeService(tesisIds));

    [IntegrationFact]
    public async Task CaprazArama_OdemeBaglantisiOlmayanMuhasebeFisi_Bulunur()
    {
        var suffix = YeniSuffix();
        await using var dbContext = CreateDbContext();
        var tesisId = await YeniTesisAsync(dbContext, suffix);

        // Tahsilat kaynakli isaretlenmis ama kaynak belgesi OLMAYAN fis.
        var fisId = await YeniFisAsync(dbContext, tesisId, DateTime.UtcNow.Date, MuhasebeFisDurumlari.Onayli);
        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE [Muhasebe].[MuhasebeFisler] SET [KaynakModul] = 'TahsilatOdemeBelgesi', [KaynakId] = 999999999 WHERE [Id] = {fisId}");

        var svc = CreateCaprazServis(dbContext, tesisId);
        var bugun = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        var sonuc = await svc.AraAsync(new PagedRequest(), new OdemeCaprazAramaFilterDto
        {
            TesisId = tesisId, TarihBaslangic = bugun, TarihBitis = bugun, TutarMin = 0m, TutarMax = 1_000_000m
        });

        Assert.Contains(sonuc.Items, x => x.MuhasebeFisId == fisId
            && x.KopuklukKodlari.Contains(OdemeKopuklukTipleri.OdemeBaglantisiOlmayanMuhasebeFisi));
    }

    [IntegrationFact]
    public async Task CaprazArama_MuhasebeFisiOlmayanOdemeBelgesi_Bulunur()
    {
        var suffix = YeniSuffix();
        await using var dbContext = CreateDbContext();
        var tesisId = await YeniTesisAsync(dbContext, suffix);
        var cariId = await YeniCariKartAsync(dbContext, tesisId, suffix);
        var belgeId = await YeniBelgeAsync(dbContext, cariId, 400m, $"{suffix}-A", DateTime.UtcNow.Date, OdemeYontemleri.Nakit);

        var svc = CreateCaprazServis(dbContext, tesisId);
        var sonuc = await svc.AraAsync(new PagedRequest(), new OdemeCaprazAramaFilterDto { TesisId = tesisId, BelgeNo = suffix, BeklenenCariKartId = cariId });

        Assert.Contains(sonuc.Items, x => x.TahsilatOdemeBelgesiId == belgeId
            && x.KopuklukKodlari.Contains(OdemeKopuklukTipleri.MuhasebeFisiOlmayanOdemeBelgesi));
    }

    [IntegrationFact]
    public async Task CaprazArama_ValorKaydiOlmayanPosTahsilati_Bulunur()
    {
        var suffix = YeniSuffix();
        await using var dbContext = CreateDbContext();
        var tesisId = await YeniTesisAsync(dbContext, suffix);
        var cariId = await YeniCariKartAsync(dbContext, tesisId, suffix);
        var hp = await YeniHesapPlaniAsync(dbContext, suffix, "KK");
        var kkId = await YeniKasaBankaHesabiAsync(dbContext, tesisId, KasaBankaHesapTipleri.KrediKarti, suffix, "KK1", hp);
        var belgeId = await YeniBelgeAsync(dbContext, cariId, 600m, $"{suffix}-KK", DateTime.UtcNow.Date, OdemeYontemleri.KrediKarti, kkId);

        var svc = CreateCaprazServis(dbContext, tesisId);
        var sonuc = await svc.AraAsync(new PagedRequest(), new OdemeCaprazAramaFilterDto { TesisId = tesisId, BelgeNo = suffix, BeklenenCariKartId = cariId });

        Assert.Contains(sonuc.Items, x => x.TahsilatOdemeBelgesiId == belgeId
            && x.KopuklukKodlari.Contains(OdemeKopuklukTipleri.ValorKaydiOlmayanPosTahsilati));
    }

    [IntegrationFact]
    public async Task CaprazArama_HedefBankaHesabiOlmayanValor_Bulunur()
    {
        var suffix = YeniSuffix();
        await using var dbContext = CreateDbContext();
        var tesisId = await YeniTesisAsync(dbContext, suffix);
        var cariId = await YeniCariKartAsync(dbContext, tesisId, suffix);
        var hp = await YeniHesapPlaniAsync(dbContext, suffix, "KK");
        var kkId = await YeniKasaBankaHesabiAsync(dbContext, tesisId, KasaBankaHesapTipleri.KrediKarti, suffix, "KK1", hp);
        var belgeId = await YeniBelgeAsync(dbContext, cariId, 600m, $"{suffix}-KK", DateTime.UtcNow.Date, OdemeYontemleri.KrediKarti, kkId);
        // BagliBankaHesapId NULL - hedef banka hesabi yok.
        var valorId = await YeniValorAsync(dbContext, tesisId, belgeId, kkId, null, PosTahsilatValorDurumlari.ValorBekliyor,
            DateOnly.FromDateTime(DateTime.UtcNow.Date), 600m);

        var svc = CreateCaprazServis(dbContext, tesisId);
        var sonuc = await svc.AraAsync(new PagedRequest(), new OdemeCaprazAramaFilterDto { TesisId = tesisId, BelgeNo = suffix, BeklenenCariKartId = cariId });

        Assert.Contains(sonuc.Items, x => x.PosTahsilatValorId == valorId
            && x.KopuklukKodlari.Contains(OdemeKopuklukTipleri.HedefBankaHesabiOlmayanValor));
    }

    [IntegrationFact]
    public async Task CaprazArama_AyniOdemeFarkliKaynaklarda_MUKERRER_SAYILMAZ()
    {
        var suffix = YeniSuffix();
        await using var dbContext = CreateDbContext();
        var tesisId = await YeniTesisAsync(dbContext, suffix);
        var cariId = await YeniCariKartAsync(dbContext, tesisId, suffix);
        var hp = await YeniHesapPlaniAsync(dbContext, suffix, "KK");
        var kkId = await YeniKasaBankaHesabiAsync(dbContext, tesisId, KasaBankaHesapTipleri.KrediKarti, suffix, "KK1", hp);

        // AYNI mali islem: belge + POS valor + cari hareket olarak UC kaynakta bulunur.
        var belgeId = await YeniBelgeAsync(dbContext, cariId, 800m, $"{suffix}-COK", DateTime.UtcNow.Date, OdemeYontemleri.KrediKarti, kkId);
        await YeniValorAsync(dbContext, tesisId, belgeId, kkId, null, PosTahsilatValorDurumlari.ValorBekliyor,
            DateOnly.FromDateTime(DateTime.UtcNow.Date), 800m);
        await YeniCariHareketAsync(dbContext, cariId, borc: 0m, alacak: 800m, tarih: DateTime.UtcNow.Date,
            kaynakModul: MuhasebeKaynakModulleri.TahsilatOdemeBelgesi, kaynakId: belgeId);

        var svc = CreateCaprazServis(dbContext, tesisId);
        var bugun = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        var sonuc = await svc.AraAsync(new PagedRequest(), new OdemeCaprazAramaFilterDto
        {
            TesisId = tesisId, BeklenenCariKartId = cariId,
            TarihBaslangic = bugun, TarihBitis = bugun, TutarMin = 0m, TutarMax = 1_000_000m
        });

        // TEK aday olmali (uc kaynak da ayni tekillestirme anahtarinda birlesir).
        var adaylar = sonuc.Items.Where(x => x.TahsilatOdemeBelgesiId == belgeId).ToList();
        Assert.Single(adaylar);
        Assert.Contains(OdemeAdayKaynaklari.TahsilatOdemeBelgesi, adaylar[0].BulunduguKaynaklar);
        Assert.Contains(OdemeAdayKaynaklari.PosTahsilatValor, adaylar[0].BulunduguKaynaklar);
        Assert.Contains(OdemeAdayKaynaklari.CariHareket, adaylar[0].BulunduguKaynaklar);
    }

    [IntegrationFact]
    public async Task CaprazArama_YetkisizTesisVerisiSizmaz()
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

        // Yalnizca TesisA yetkisi.
        var bugun = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        var svc = CreateCaprazServis(dbContext, tesisA);
        var sonuc = await svc.AraAsync(new PagedRequest(), new OdemeCaprazAramaFilterDto
        {
            TarihBaslangic = bugun, TarihBitis = bugun, TutarMin = 0m, TutarMax = 1_000_000m
        });

        Assert.All(sonuc.Items, x => Assert.NotEqual(tesisB, x.TesisId));
        // totalCount'a da sizmamali.
        Assert.Equal(sonuc.Items.Count, sonuc.TotalCount);

        var ex = await Assert.ThrowsAsync<BaseException>(() =>
            svc.AraAsync(new PagedRequest(), new OdemeCaprazAramaFilterDto
            {
                TesisId = tesisB, TarihBaslangic = bugun, TarihBitis = bugun, TutarMin = 0m, TutarMax = 1_000_000m
            }));
        Assert.Equal(403, ex.ErrorCode);
    }

    [IntegrationFact]
    public async Task CaprazArama_MaksimumPageSizeUygulanir_VeSiralamaKararlidir()
    {
        var suffix = YeniSuffix();
        await using var dbContext = CreateDbContext();
        var tesisId = await YeniTesisAsync(dbContext, suffix);
        var cariId = await YeniCariKartAsync(dbContext, tesisId, suffix);
        for (var i = 0; i < 5; i++)
        {
            await YeniBelgeAsync(dbContext, cariId, 10m + i, $"{suffix}-{i}", DateTime.UtcNow.Date.AddDays(-i));
        }

        var svc = CreateCaprazServis(dbContext, tesisId);

        // Asiri buyuk page size istegi MAKSIMUMA kirpilir.
        var buyuk = await svc.AraAsync(new PagedRequest { PageNumber = 1, PageSize = 100_000 },
            new OdemeCaprazAramaFilterDto { TesisId = tesisId, BelgeNo = suffix, BeklenenCariKartId = cariId });
        Assert.True(buyuk.PageSize <= 200);

        var s1 = await svc.AraAsync(new PagedRequest { PageNumber = 1, PageSize = 2 }, new OdemeCaprazAramaFilterDto { TesisId = tesisId, BelgeNo = suffix, BeklenenCariKartId = cariId });
        var s2 = await svc.AraAsync(new PagedRequest { PageNumber = 2, PageSize = 2 }, new OdemeCaprazAramaFilterDto { TesisId = tesisId, BelgeNo = suffix, BeklenenCariKartId = cariId });

        Assert.Equal(5, s1.TotalCount);
        Assert.Empty(s1.Items.Select(x => x.TekillestirmeAnahtari).Intersect(s2.Items.Select(x => x.TekillestirmeAnahtari)));
    }

    // ─────────────────────────────────────────────────────────────
    // Capraz-tesis hesap/fis baglanti sizintisi (madde 5)
    // ─────────────────────────────────────────────────────────────

    [IntegrationFact]
    public async Task Detay_BagliKasaBankaHesabiBaskaTesisteyse_HesapDetayiSizmaz()
    {
        var suffixA = YeniSuffix();
        var suffixB = YeniSuffix();
        await using var dbContext = CreateDbContext();
        var tesisA = await YeniTesisAsync(dbContext, suffixA);
        var tesisB = await YeniTesisAsync(dbContext, suffixB);
        var cariA = await YeniCariKartAsync(dbContext, tesisA, suffixA);

        var hpB = await YeniHesapPlaniAsync(dbContext, suffixB, "BANKA");
        // Hesap TesisB'ye ait - ancak belge TesisA'da ve BU hesaba isaret ediyor (dangling/yanlis baglanti senaryosu).
        var bankaHesabiTesisB = await YeniKasaBankaHesabiAsync(dbContext, tesisB, KasaBankaHesapTipleri.Banka, suffixB, "BNK", hpB, iban: "TR330006100519786457841326");

        var belgeId = await YeniBelgeAsync(dbContext, cariA, 250m, $"{suffixA}-A", DateTime.UtcNow.Date, OdemeYontemleri.HavaleEft, bankaHesabiTesisB);

        // Kullanici SADECE TesisA'ya yetkili.
        var svc = CreateService(dbContext, tesisA);
        var detay = await svc.GetDetayAsync(belgeId);

        Assert.True(detay.BagliHesapErisimKisitliMi);
        Assert.Contains(OdemeErisimKisitiNedenKodlari.YetkiKapsamiDisindaHesapBaglantisi, detay.ErisimKisitiNedenKodlari);
        // TesisB'ye ait hesap adi/IBAN/kod HICBIR yerde (maskeli bile olsa) gorunmemeli.
        Assert.Null(detay.IbanMaskeli);
        Assert.DoesNotContain(detay.Uyarilar, u => u.Aciklama != null && u.Aciklama.Contains(suffixB));
    }

    [IntegrationFact]
    public async Task Detay_KullaniciHemAHemBTesisineYetkiliyken_ABelgesiBHesabinaBagliysa_TesisUyusmazligiDoner()
    {
        var suffixA = YeniSuffix();
        var suffixB = YeniSuffix();
        await using var dbContext = CreateDbContext();
        var tesisA = await YeniTesisAsync(dbContext, suffixA);
        var tesisB = await YeniTesisAsync(dbContext, suffixB);
        var cariA = await YeniCariKartAsync(dbContext, tesisA, suffixA);

        var hpB = await YeniHesapPlaniAsync(dbContext, suffixB, "BANKA");
        // Hesap TesisB'ye ait - ancak belge TesisA'da ve BU hesaba isaret ediyor.
        var bankaHesabiTesisB = await YeniKasaBankaHesabiAsync(dbContext, tesisB, KasaBankaHesapTipleri.Banka, suffixB, "BNK", hpB, iban: "TR330006100519786457841326");

        var belgeId = await YeniBelgeAsync(dbContext, cariA, 250m, $"{suffixA}-A", DateTime.UtcNow.Date, OdemeYontemleri.HavaleEft, bankaHesabiTesisB);

        // Kullanici HEM TesisA HEM TesisB'ye yetkili - "yetkili herhangi bir tesise ait" YETERLI
        // SAYILMAMALI, hesap MUTLAKA odemenin (TesisA'nin) kendi tesisiyle eslesmelidir.
        var svc = CreateService(dbContext, tesisA, tesisB);
        var detay = await svc.GetDetayAsync(belgeId);

        Assert.True(detay.BagliHesapErisimKisitliMi);
        Assert.Contains(OdemeErisimKisitiNedenKodlari.TesisUyusmazligi, detay.ErisimKisitiNedenKodlari);
        // TesisB'ye ait hesap adi/banka adi/IBAN/kod ID DISINDA HICBIR yerde (maskeli bile olsa) gorunmemeli.
        Assert.Null(detay.KasaBankaHesapAdi);
        Assert.Null(detay.BankaAdi);
        Assert.Null(detay.IbanMaskeli);
        Assert.Null(detay.MuhasebeHesapKodu);
        Assert.DoesNotContain(detay.Uyarilar, u => u.Aciklama != null && u.Aciklama.Contains(suffixB));
    }

    [IntegrationFact]
    public async Task Detay_OdemeVeHesapAyniTesistiyse_HesapDetaylariGosterilirVeTesisUyusmazligiUretilmez()
    {
        var suffix = YeniSuffix();
        await using var dbContext = CreateDbContext();
        var tesisId = await YeniTesisAsync(dbContext, suffix);
        var cariId = await YeniCariKartAsync(dbContext, tesisId, suffix);

        var hp = await YeniHesapPlaniAsync(dbContext, suffix, "BANKA");
        var bankaHesabi = await YeniKasaBankaHesabiAsync(dbContext, tesisId, KasaBankaHesapTipleri.Banka, suffix, "BNK", hp, iban: "TR330006100519786457841326");

        var belgeId = await YeniBelgeAsync(dbContext, cariId, 250m, $"{suffix}-A", DateTime.UtcNow.Date, OdemeYontemleri.HavaleEft, bankaHesabi);

        var svc = CreateService(dbContext, tesisId);
        var detay = await svc.GetDetayAsync(belgeId);

        Assert.False(detay.BagliHesapErisimKisitliMi);
        Assert.DoesNotContain(OdemeErisimKisitiNedenKodlari.TesisUyusmazligi, detay.ErisimKisitiNedenKodlari);
        Assert.Equal("TR33 **** **** **** **26", detay.IbanMaskeli);
    }

    [IntegrationFact]
    public async Task Detay_BagliMuhasebeFisiBaskaTesisteyse_FisDetayiSizmaz()
    {
        var suffixA = YeniSuffix();
        var suffixB = YeniSuffix();
        await using var dbContext = CreateDbContext();
        var tesisA = await YeniTesisAsync(dbContext, suffixA);
        var tesisB = await YeniTesisAsync(dbContext, suffixB);
        var cariA = await YeniCariKartAsync(dbContext, tesisA, suffixA);

        // Fis TesisB'ye ait - belge TesisA'da ve BU fise isaret ediyor.
        var fisTesisB = await YeniFisAsync(dbContext, tesisB, DateTime.UtcNow.Date, MuhasebeFisDurumlari.Onayli);

        var belgeId = await YeniBelgeAsync(dbContext, cariA, 250m, $"{suffixA}-A", DateTime.UtcNow.Date, OdemeYontemleri.Nakit, null, fisTesisB);

        var svc = CreateService(dbContext, tesisA);
        var detay = await svc.GetDetayAsync(belgeId);

        Assert.True(detay.BagliFisErisimKisitliMi);
        Assert.Contains(OdemeErisimKisitiNedenKodlari.YetkiKapsamiDisindaFisBaglantisi, detay.ErisimKisitiNedenKodlari);
        // "Odeme var fis yok" gibi yaniltici bir uyari da URETILMEMELI - fis var ama erisim disinda.
        Assert.DoesNotContain(detay.Uyarilar, u => u.UyariTipi == OdemeUyariTipleri.OdemeVarFisYok);
    }

    [IntegrationFact]
    public async Task Detay_KullaniciHemAHemBTesisineYetkiliyken_ABelgesiBFisineBagliysa_TesisUyusmazligiDoner()
    {
        var suffixA = YeniSuffix();
        var suffixB = YeniSuffix();
        await using var dbContext = CreateDbContext();
        var tesisA = await YeniTesisAsync(dbContext, suffixA);
        var tesisB = await YeniTesisAsync(dbContext, suffixB);
        var cariA = await YeniCariKartAsync(dbContext, tesisA, suffixA);

        // Fis TesisB'ye ait - belge TesisA'da ve BU fise isaret ediyor.
        var fisTesisB = await YeniFisAsync(dbContext, tesisB, DateTime.UtcNow.Date, MuhasebeFisDurumlari.Onayli);

        var belgeId = await YeniBelgeAsync(dbContext, cariA, 250m, $"{suffixA}-A", DateTime.UtcNow.Date, OdemeYontemleri.Nakit, null, fisTesisB);

        // Kullanici HEM TesisA HEM TesisB'ye yetkili - "yetkili herhangi bir tesise ait" YETERLI
        // SAYILMAMALI, fis MUTLAKA odemenin (TesisA'nin) kendi tesisiyle eslesmelidir.
        var svc = CreateService(dbContext, tesisA, tesisB);
        var detay = await svc.GetDetayAsync(belgeId);

        Assert.True(detay.BagliFisErisimKisitliMi);
        Assert.Contains(OdemeErisimKisitiNedenKodlari.TesisUyusmazligi, detay.ErisimKisitiNedenKodlari);
        Assert.Null(detay.MuhasebeFisNo);
        Assert.Null(detay.MuhasebeFisTarihi);
        Assert.Null(detay.MuhasebeFisDurumu);
        Assert.DoesNotContain(detay.Uyarilar, u => u.Aciklama != null && u.Aciklama.Contains(suffixB));
    }

    [IntegrationFact]
    public async Task Detay_OdemeVeFisAyniTesistiyse_FisDetaylariGosterilirVeTesisUyusmazligiUretilmez()
    {
        var suffix = YeniSuffix();
        await using var dbContext = CreateDbContext();
        var tesisId = await YeniTesisAsync(dbContext, suffix);
        var cariId = await YeniCariKartAsync(dbContext, tesisId, suffix);

        var fisId = await YeniFisAsync(dbContext, tesisId, DateTime.UtcNow.Date, MuhasebeFisDurumlari.Onayli);

        var belgeId = await YeniBelgeAsync(dbContext, cariId, 250m, $"{suffix}-A", DateTime.UtcNow.Date, OdemeYontemleri.Nakit, null, fisId);

        var svc = CreateService(dbContext, tesisId);
        var detay = await svc.GetDetayAsync(belgeId);

        Assert.False(detay.BagliFisErisimKisitliMi);
        Assert.DoesNotContain(OdemeErisimKisitiNedenKodlari.TesisUyusmazligi, detay.ErisimKisitiNedenKodlari);
        Assert.NotNull(detay.MuhasebeFisNo);
    }

    [IntegrationFact]
    public async Task Detay_AyniKaynakIdliCariHareketBaskaCariyeAitse_DikkateAlinmazVeCariHareketiYokDoner()
    {
        var suffix = YeniSuffix();
        await using var dbContext = CreateDbContext();
        var tesisId = await YeniTesisAsync(dbContext, suffix);
        var cariA = await YeniCariKartAsync(dbContext, tesisId, suffix + "A");
        var cariB = await YeniCariKartAsync(dbContext, tesisId, suffix + "B");

        var belgeId = await YeniBelgeAsync(dbContext, cariA, 250m, $"{suffix}-A", DateTime.UtcNow.Date, OdemeYontemleri.Nakit);

        // Ayni KaynakId'yi tasiyan ama BASKA cariye (cariB) ait bir hareket - yanlis baglanti senaryosu.
        await YeniCariHareketAsync(dbContext, cariB, 250m, 0m, DateTime.UtcNow.Date,
            kaynakModul: MuhasebeKaynakModulleri.TahsilatOdemeBelgesi, kaynakId: belgeId);

        var svc = CreateService(dbContext, tesisId);
        var detay = await svc.GetDetayAsync(belgeId);

        Assert.False(detay.KapatildiMi);
        Assert.False(detay.BakiyeyeDahilMi);
        Assert.NotEqual(BakiyeyeDahilEdilmeDurumlari.TamamenDahil, detay.BakiyeyeDahilEdilmeDurumu);
        Assert.Contains(BakiyeyeDahilEdilmemeNedenKodlari.CariHareketiYok, detay.BakiyeyeDahilEdilmemeNedenKodlari);
        Assert.Null(detay.EtkiledigiTutar);
        Assert.Null(detay.EtkiledigiParaBirimi);
        Assert.Null(detay.EtkiledigiCariVeyaBorc);
    }

    [IntegrationFact]
    public async Task Detay_OdemeVeCariHareketAyniCariyeAitse_MevcutDavranisKorunur()
    {
        var suffix = YeniSuffix();
        await using var dbContext = CreateDbContext();
        var tesisId = await YeniTesisAsync(dbContext, suffix);
        var cariId = await YeniCariKartAsync(dbContext, tesisId, suffix);

        var belgeId = await YeniBelgeAsync(dbContext, cariId, 250m, $"{suffix}-A", DateTime.UtcNow.Date, OdemeYontemleri.Nakit);

        await YeniCariHareketAsync(dbContext, cariId, 250m, 0m, DateTime.UtcNow.Date,
            kaynakModul: MuhasebeKaynakModulleri.TahsilatOdemeBelgesi, kaynakId: belgeId);

        var svc = CreateService(dbContext, tesisId);
        var detay = await svc.GetDetayAsync(belgeId);

        Assert.DoesNotContain(BakiyeyeDahilEdilmemeNedenKodlari.CariHareketiYok, detay.BakiyeyeDahilEdilmemeNedenKodlari);
    }

    [IntegrationFact]
    public async Task Detay_PosValorKaydiBaskaTesisteyse_DikkateAlinmazVePosValorKaydiYokDoner()
    {
        var suffixA = YeniSuffix();
        var suffixB = YeniSuffix();
        await using var dbContext = CreateDbContext();
        var tesisA = await YeniTesisAsync(dbContext, suffixA);
        var tesisB = await YeniTesisAsync(dbContext, suffixB);
        var cariA = await YeniCariKartAsync(dbContext, tesisA, suffixA);

        var hp = await YeniHesapPlaniAsync(dbContext, suffixA, "KK");
        var kkId = await YeniKasaBankaHesabiAsync(dbContext, tesisA, KasaBankaHesapTipleri.KrediKarti, suffixA, "KK1", hp);
        var belgeId = await YeniBelgeAsync(dbContext, cariA, 600m, $"{suffixA}-KK", DateTime.UtcNow.Date, OdemeYontemleri.KrediKarti, kkId);

        // POS valor kaydi yanlislikla TesisB'ye ait olarak olusturulmus - odeme TesisA'da.
        await YeniValorAsync(dbContext, tesisB, belgeId, kkId, null, PosTahsilatValorDurumlari.Aktarildi,
            DateOnly.FromDateTime(DateTime.UtcNow.Date), 590m);

        var svc = CreateService(dbContext, tesisA, tesisB);
        var detay = await svc.GetDetayAsync(belgeId);

        Assert.Null(detay.PosTahsilatValorId);
        Assert.Null(detay.PosValorDurumu);
        Assert.Null(detay.PosBeklenenValorTarihi);
        Assert.Null(detay.PosNetTutar);
        Assert.Contains(BakiyeyeDahilEdilmemeNedenKodlari.PosValorKaydiYok, detay.BakiyeyeDahilEdilmemeNedenKodlari);
        Assert.NotEqual(BakiyeyeDahilEdilmeDurumlari.TamamenDahil, detay.BakiyeyeDahilEdilmeDurumu);
    }

    [IntegrationFact]
    public async Task Detay_OdemeVePosValorKaydiAyniTesistiyse_MevcutDavranisKorunur()
    {
        var suffix = YeniSuffix();
        await using var dbContext = CreateDbContext();
        var tesisId = await YeniTesisAsync(dbContext, suffix);
        var cariId = await YeniCariKartAsync(dbContext, tesisId, suffix);

        var hp = await YeniHesapPlaniAsync(dbContext, suffix, "KK");
        var kkId = await YeniKasaBankaHesabiAsync(dbContext, tesisId, KasaBankaHesapTipleri.KrediKarti, suffix, "KK1", hp);
        var belgeId = await YeniBelgeAsync(dbContext, cariId, 600m, $"{suffix}-KK", DateTime.UtcNow.Date, OdemeYontemleri.KrediKarti, kkId);

        var valorId = await YeniValorAsync(dbContext, tesisId, belgeId, kkId, null, PosTahsilatValorDurumlari.Aktarildi,
            DateOnly.FromDateTime(DateTime.UtcNow.Date), 590m);

        var svc = CreateService(dbContext, tesisId);
        var detay = await svc.GetDetayAsync(belgeId);

        Assert.Equal(valorId, detay.PosTahsilatValorId);
        Assert.Equal(PosTahsilatValorDurumlari.Aktarildi, detay.PosValorDurumu);
        Assert.DoesNotContain(BakiyeyeDahilEdilmemeNedenKodlari.PosValorKaydiYok, detay.BakiyeyeDahilEdilmemeNedenKodlari);
    }

    [IntegrationFact]
    public async Task Arama_ValorDurumuFiltresi_BaskaTesistekiPosKaydiEslesmez()
    {
        var suffixA = YeniSuffix();
        var suffixB = YeniSuffix();
        await using var dbContext = CreateDbContext();
        var tesisA = await YeniTesisAsync(dbContext, suffixA);
        var tesisB = await YeniTesisAsync(dbContext, suffixB);
        var cariA = await YeniCariKartAsync(dbContext, tesisA, suffixA);

        var hp = await YeniHesapPlaniAsync(dbContext, suffixA, "KK");
        var kkId = await YeniKasaBankaHesabiAsync(dbContext, tesisA, KasaBankaHesapTipleri.KrediKarti, suffixA, "KK1", hp);
        var belgeId = await YeniBelgeAsync(dbContext, cariA, 600m, $"{suffixA}-KK", DateTime.UtcNow.Date, OdemeYontemleri.KrediKarti, kkId);

        // POS valor kaydi yanlislikla TesisB'ye ait olarak olusturulmus - odeme TesisA'da.
        await YeniValorAsync(dbContext, tesisB, belgeId, kkId, null, PosTahsilatValorDurumlari.Aktarildi,
            DateOnly.FromDateTime(DateTime.UtcNow.Date), 590m);

        var svc = CreateService(dbContext, tesisA, tesisB);
        var sonuc = await svc.AraAsync(new PagedRequest(), new OdemeAramaFilterDto { TesisId = tesisA, ValorDurumu = PosTahsilatValorDurumlari.Aktarildi });

        Assert.DoesNotContain(sonuc.Items, x => x.Id == belgeId);
    }

    [IntegrationFact]
    public async Task Arama_BaskaTesistekiPosKaydi_UyariSayisindaPosEksikSayilir()
    {
        var suffixA = YeniSuffix();
        var suffixB = YeniSuffix();
        await using var dbContext = CreateDbContext();
        var tesisA = await YeniTesisAsync(dbContext, suffixA);
        var tesisB = await YeniTesisAsync(dbContext, suffixB);
        var cariA = await YeniCariKartAsync(dbContext, tesisA, suffixA);

        var hp = await YeniHesapPlaniAsync(dbContext, suffixA, "KK");
        var kkId = await YeniKasaBankaHesabiAsync(dbContext, tesisA, KasaBankaHesapTipleri.KrediKarti, suffixA, "KK1", hp);
        var belgeId = await YeniBelgeAsync(dbContext, cariA, 600m, $"{suffixA}-KK", DateTime.UtcNow.Date, OdemeYontemleri.KrediKarti, kkId);

        await YeniValorAsync(dbContext, tesisB, belgeId, kkId, null, PosTahsilatValorDurumlari.Aktarildi,
            DateOnly.FromDateTime(DateTime.UtcNow.Date), 590m);

        var svc = CreateService(dbContext, tesisA, tesisB);
        var sonuc = await svc.AraAsync(new PagedRequest(), new OdemeAramaFilterDto { TesisId = tesisA });

        var satir = Assert.Single(sonuc.Items, x => x.Id == belgeId);
        Assert.True(satir.UyariSayisi >= 1);
    }

    [IntegrationFact]
    public async Task Arama_OdemeVePosValorKaydiAyniTesistiyse_ValorDurumuFiltresiEslesirVeUyariUretilmez()
    {
        var suffix = YeniSuffix();
        await using var dbContext = CreateDbContext();
        var tesisId = await YeniTesisAsync(dbContext, suffix);
        var cariId = await YeniCariKartAsync(dbContext, tesisId, suffix);

        var hp = await YeniHesapPlaniAsync(dbContext, suffix, "KK");
        var kkId = await YeniKasaBankaHesabiAsync(dbContext, tesisId, KasaBankaHesapTipleri.KrediKarti, suffix, "KK1", hp);
        var fisId = await YeniFisAsync(dbContext, tesisId, DateTime.UtcNow.Date, MuhasebeFisDurumlari.Onayli);
        var belgeId = await YeniBelgeAsync(dbContext, cariId, 600m, $"{suffix}-KK", DateTime.UtcNow.Date, OdemeYontemleri.KrediKarti, kkId, fisId);

        await YeniValorAsync(dbContext, tesisId, belgeId, kkId, null, PosTahsilatValorDurumlari.Aktarildi,
            DateOnly.FromDateTime(DateTime.UtcNow.Date), 590m);

        var svc = CreateService(dbContext, tesisId);
        var sonuc = await svc.AraAsync(new PagedRequest(), new OdemeAramaFilterDto { TesisId = tesisId, ValorDurumu = PosTahsilatValorDurumlari.Aktarildi });

        var satir = Assert.Single(sonuc.Items, x => x.Id == belgeId);
        Assert.Equal(0, satir.UyariSayisi);
    }

    [IntegrationFact]
    public async Task Uyari_BaskaTesistekiFarkliParaBirimliPosKaydi_PosVarValorYokUretirParaBirimiTutarsizligiUretmez()
    {
        var suffixA = YeniSuffix();
        var suffixB = YeniSuffix();
        await using var dbContext = CreateDbContext();
        var tesisA = await YeniTesisAsync(dbContext, suffixA);
        var tesisB = await YeniTesisAsync(dbContext, suffixB);
        var cariA = await YeniCariKartAsync(dbContext, tesisA, suffixA);

        var hp = await YeniHesapPlaniAsync(dbContext, suffixA, "KK");
        var kkId = await YeniKasaBankaHesabiAsync(dbContext, tesisA, KasaBankaHesapTipleri.KrediKarti, suffixA, "KK1", hp);
        var belgeId = await YeniBelgeAsync(dbContext, cariA, 600m, $"{suffixA}-KK", DateTime.UtcNow.Date, OdemeYontemleri.KrediKarti, kkId);

        // POS valor kaydi yanlislikla TesisB'ye ait olarak olusturulmus - odeme TesisA'da, para birimleri de farkli.
        await YeniValorAsync(dbContext, tesisB, belgeId, kkId, null, PosTahsilatValorDurumlari.Aktarildi,
            DateOnly.FromDateTime(DateTime.UtcNow.Date), 590m, paraBirimi: "USD");

        var svc = CreateService(dbContext, tesisA, tesisB);
        var detay = await svc.GetDetayAsync(belgeId);

        Assert.Contains(detay.Uyarilar, u => u.UyariTipi == OdemeUyariTipleri.PosVarValorYok);
        Assert.DoesNotContain(detay.Uyarilar, u => u.UyariTipi == OdemeUyariTipleri.ParaBirimiTutarsizligi);
        Assert.DoesNotContain(detay.Uyarilar, u => u.Aciklama != null && u.Aciklama.Contains("USD"));
    }

    [IntegrationFact]
    public async Task Uyari_AyniTesistekiFarkliParaBirimliPosKaydi_ParaBirimiTutarsizligiUretirPosVarValorYokUretmez()
    {
        var suffix = YeniSuffix();
        await using var dbContext = CreateDbContext();
        var tesisId = await YeniTesisAsync(dbContext, suffix);
        var cariId = await YeniCariKartAsync(dbContext, tesisId, suffix);

        var hp = await YeniHesapPlaniAsync(dbContext, suffix, "KK");
        var kkId = await YeniKasaBankaHesabiAsync(dbContext, tesisId, KasaBankaHesapTipleri.KrediKarti, suffix, "KK1", hp);
        var belgeId = await YeniBelgeAsync(dbContext, cariId, 600m, $"{suffix}-KK", DateTime.UtcNow.Date, OdemeYontemleri.KrediKarti, kkId);

        await YeniValorAsync(dbContext, tesisId, belgeId, kkId, null, PosTahsilatValorDurumlari.Aktarildi,
            DateOnly.FromDateTime(DateTime.UtcNow.Date), 590m, paraBirimi: "USD");

        var svc = CreateService(dbContext, tesisId);
        var detay = await svc.GetDetayAsync(belgeId);

        Assert.Contains(detay.Uyarilar, u => u.UyariTipi == OdemeUyariTipleri.ParaBirimiTutarsizligi);
        Assert.DoesNotContain(detay.Uyarilar, u => u.UyariTipi == OdemeUyariTipleri.PosVarValorYok);
    }

    [IntegrationFact]
    public async Task Arama_KullaniciHemAHemBTesisineYetkiliyken_ABelgesiBHesabinaBagliysa_HesapAdiSizmaz()
    {
        var suffixA = YeniSuffix();
        var suffixB = YeniSuffix();
        await using var dbContext = CreateDbContext();
        var tesisA = await YeniTesisAsync(dbContext, suffixA);
        var tesisB = await YeniTesisAsync(dbContext, suffixB);
        var cariA = await YeniCariKartAsync(dbContext, tesisA, suffixA);

        var hpB = await YeniHesapPlaniAsync(dbContext, suffixB, "BANKA");
        var bankaHesabiTesisB = await YeniKasaBankaHesabiAsync(dbContext, tesisB, KasaBankaHesapTipleri.Banka, suffixB, "BNK", hpB);

        var belgeId = await YeniBelgeAsync(dbContext, cariA, 250m, $"{suffixA}-A", DateTime.UtcNow.Date, OdemeYontemleri.HavaleEft, bankaHesabiTesisB);

        var svc = CreateService(dbContext, tesisA, tesisB);
        var sonuc = await svc.AraAsync(new PagedRequest(), new OdemeAramaFilterDto { TesisId = tesisA });

        var satir = Assert.Single(sonuc.Items, x => x.Id == belgeId);
        Assert.Null(satir.KasaBankaHesapAdi);
        Assert.DoesNotContain(sonuc.Items, x => x.KasaBankaHesapAdi != null && x.KasaBankaHesapAdi.Contains(suffixB));
    }

    [IntegrationFact]
    public async Task Arama_OdemeVeHesapAyniTesistiyse_HesapAdiGosterilmeyeDevamEder()
    {
        var suffix = YeniSuffix();
        await using var dbContext = CreateDbContext();
        var tesisId = await YeniTesisAsync(dbContext, suffix);
        var cariId = await YeniCariKartAsync(dbContext, tesisId, suffix);

        var hp = await YeniHesapPlaniAsync(dbContext, suffix, "BANKA");
        var bankaHesabi = await YeniKasaBankaHesabiAsync(dbContext, tesisId, KasaBankaHesapTipleri.Banka, suffix, "BNK", hp);

        var belgeId = await YeniBelgeAsync(dbContext, cariId, 250m, $"{suffix}-A", DateTime.UtcNow.Date, OdemeYontemleri.HavaleEft, bankaHesabi);

        var svc = CreateService(dbContext, tesisId);
        var sonuc = await svc.AraAsync(new PagedRequest(), new OdemeAramaFilterDto { TesisId = tesisId });

        var satir = Assert.Single(sonuc.Items, x => x.Id == belgeId);
        Assert.NotNull(satir.KasaBankaHesapAdi);
        Assert.Contains("BNK", satir.KasaBankaHesapAdi);
    }

    [IntegrationFact]
    public async Task Arama_KasaBankaHesapIdFiltresi_BaskaTesisinHesabiylaEslesmez()
    {
        var suffixA = YeniSuffix();
        var suffixB = YeniSuffix();
        await using var dbContext = CreateDbContext();
        var tesisA = await YeniTesisAsync(dbContext, suffixA);
        var tesisB = await YeniTesisAsync(dbContext, suffixB);
        var cariA = await YeniCariKartAsync(dbContext, tesisA, suffixA);

        var hpB = await YeniHesapPlaniAsync(dbContext, suffixB, "BANKA");
        var bankaHesabiTesisB = await YeniKasaBankaHesabiAsync(dbContext, tesisB, KasaBankaHesapTipleri.Banka, suffixB, "BNK", hpB);

        var belgeId = await YeniBelgeAsync(dbContext, cariA, 250m, $"{suffixA}-A", DateTime.UtcNow.Date, OdemeYontemleri.HavaleEft, bankaHesabiTesisB);

        var svc = CreateService(dbContext, tesisA, tesisB);
        var sonuc = await svc.AraAsync(new PagedRequest(), new OdemeAramaFilterDto { TesisId = tesisA, KasaBankaHesapId = bankaHesabiTesisB });

        Assert.DoesNotContain(sonuc.Items, x => x.Id == belgeId);
    }

    [IntegrationFact]
    public async Task Arama_KasaBankaHesapIdFiltresi_AyniTesisinHesabiylaEslesmeyeDevamEder()
    {
        var suffix = YeniSuffix();
        await using var dbContext = CreateDbContext();
        var tesisId = await YeniTesisAsync(dbContext, suffix);
        var cariId = await YeniCariKartAsync(dbContext, tesisId, suffix);

        var hp = await YeniHesapPlaniAsync(dbContext, suffix, "BANKA");
        var bankaHesabi = await YeniKasaBankaHesabiAsync(dbContext, tesisId, KasaBankaHesapTipleri.Banka, suffix, "BNK", hp);

        var belgeId = await YeniBelgeAsync(dbContext, cariId, 250m, $"{suffix}-A", DateTime.UtcNow.Date, OdemeYontemleri.HavaleEft, bankaHesabi);

        var svc = CreateService(dbContext, tesisId);
        var sonuc = await svc.AraAsync(new PagedRequest(), new OdemeAramaFilterDto { TesisId = tesisId, KasaBankaHesapId = bankaHesabi });

        Assert.Contains(sonuc.Items, x => x.Id == belgeId);
    }

    [IntegrationFact]
    public async Task Arama_IbanFiltresi_BaskaTesisinHesabiylaEslesmez()
    {
        var suffixA = YeniSuffix();
        var suffixB = YeniSuffix();
        await using var dbContext = CreateDbContext();
        var tesisA = await YeniTesisAsync(dbContext, suffixA);
        var tesisB = await YeniTesisAsync(dbContext, suffixB);
        var cariA = await YeniCariKartAsync(dbContext, tesisA, suffixA);

        var hpB = await YeniHesapPlaniAsync(dbContext, suffixB, "BANKA");
        var bankaHesabiTesisB = await YeniKasaBankaHesabiAsync(dbContext, tesisB, KasaBankaHesapTipleri.Banka, suffixB, "BNK", hpB, iban: "TR330006100519786457841326");

        var belgeId = await YeniBelgeAsync(dbContext, cariA, 250m, $"{suffixA}-A", DateTime.UtcNow.Date, OdemeYontemleri.HavaleEft, bankaHesabiTesisB);

        var svc = CreateService(dbContext, tesisA, tesisB);
        var sonuc = await svc.AraAsync(new PagedRequest(), new OdemeAramaFilterDto { TesisId = tesisA, Iban = "TR3300061005197864578413" });

        Assert.DoesNotContain(sonuc.Items, x => x.Id == belgeId);
    }

    [IntegrationFact]
    public async Task Arama_IbanFiltresi_AyniTesisinHesabiylaEslesmeyeDevamEder()
    {
        var suffix = YeniSuffix();
        await using var dbContext = CreateDbContext();
        var tesisId = await YeniTesisAsync(dbContext, suffix);
        var cariId = await YeniCariKartAsync(dbContext, tesisId, suffix);

        var hp = await YeniHesapPlaniAsync(dbContext, suffix, "BANKA");
        var bankaHesabi = await YeniKasaBankaHesabiAsync(dbContext, tesisId, KasaBankaHesapTipleri.Banka, suffix, "BNK", hp, iban: "TR330006100519786457841326");

        var belgeId = await YeniBelgeAsync(dbContext, cariId, 250m, $"{suffix}-A", DateTime.UtcNow.Date, OdemeYontemleri.HavaleEft, bankaHesabi);

        var svc = CreateService(dbContext, tesisId);
        var sonuc = await svc.AraAsync(new PagedRequest(), new OdemeAramaFilterDto { TesisId = tesisId, Iban = "TR3300061005197864578413" });

        Assert.Contains(sonuc.Items, x => x.Id == belgeId);
    }

    [IntegrationFact]
    public async Task Arama_MuhasebeFisNoFiltresi_BaskaTesisinFisiyleEslesmez()
    {
        var suffixA = YeniSuffix();
        var suffixB = YeniSuffix();
        await using var dbContext = CreateDbContext();
        var tesisA = await YeniTesisAsync(dbContext, suffixA);
        var tesisB = await YeniTesisAsync(dbContext, suffixB);
        var cariA = await YeniCariKartAsync(dbContext, tesisA, suffixA);

        var fisTesisB = await YeniFisAsync(dbContext, tesisB, DateTime.UtcNow.Date, MuhasebeFisDurumlari.Onayli);
        var fisNoTesisB = await dbContext.MuhasebeFisler.AsNoTracking().Where(f => f.Id == fisTesisB).Select(f => f.FisNo).SingleAsync();

        var belgeId = await YeniBelgeAsync(dbContext, cariA, 250m, $"{suffixA}-A", DateTime.UtcNow.Date, OdemeYontemleri.Nakit, null, fisTesisB);

        var svc = CreateService(dbContext, tesisA, tesisB);
        var sonuc = await svc.AraAsync(new PagedRequest(), new OdemeAramaFilterDto { TesisId = tesisA, MuhasebeFisNo = fisNoTesisB });

        Assert.DoesNotContain(sonuc.Items, x => x.Id == belgeId);
    }

    [IntegrationFact]
    public async Task Arama_MuhasebeFisNoFiltresi_AyniTesisinFisiyleEslesmeyeDevamEder()
    {
        var suffix = YeniSuffix();
        await using var dbContext = CreateDbContext();
        var tesisId = await YeniTesisAsync(dbContext, suffix);
        var cariId = await YeniCariKartAsync(dbContext, tesisId, suffix);

        var fisId = await YeniFisAsync(dbContext, tesisId, DateTime.UtcNow.Date, MuhasebeFisDurumlari.Onayli);
        var fisNo = await dbContext.MuhasebeFisler.AsNoTracking().Where(f => f.Id == fisId).Select(f => f.FisNo).SingleAsync();

        var belgeId = await YeniBelgeAsync(dbContext, cariId, 250m, $"{suffix}-A", DateTime.UtcNow.Date, OdemeYontemleri.Nakit, null, fisId);

        var svc = CreateService(dbContext, tesisId);
        var sonuc = await svc.AraAsync(new PagedRequest(), new OdemeAramaFilterDto { TesisId = tesisId, MuhasebeFisNo = fisNo });

        Assert.Contains(sonuc.Items, x => x.Id == belgeId);
    }

    [IntegrationFact]
    public async Task Arama_MaliYilFiltresi_BaskaTesisinFisiyleEslesmez()
    {
        var suffixA = YeniSuffix();
        var suffixB = YeniSuffix();
        await using var dbContext = CreateDbContext();
        var tesisA = await YeniTesisAsync(dbContext, suffixA);
        var tesisB = await YeniTesisAsync(dbContext, suffixB);
        var cariA = await YeniCariKartAsync(dbContext, tesisA, suffixA);
        var bugun = DateTime.UtcNow.Date;

        var fisTesisB = await YeniFisAsync(dbContext, tesisB, bugun, MuhasebeFisDurumlari.Onayli, maliYil: 2026, donem: bugun.Month);
        var belgeId = await YeniBelgeAsync(dbContext, cariA, 250m, $"{suffixA}-A", bugun, OdemeYontemleri.Nakit, null, fisTesisB);

        var svc = CreateService(dbContext, tesisA, tesisB);
        var sonuc = await svc.AraAsync(new PagedRequest(), new OdemeAramaFilterDto { TesisId = tesisA, MaliYil = 2026 });

        Assert.DoesNotContain(sonuc.Items, x => x.Id == belgeId);
    }

    [IntegrationFact]
    public async Task Arama_DonemFiltresi_BaskaTesisinFisiyleEslesmez()
    {
        var suffixA = YeniSuffix();
        var suffixB = YeniSuffix();
        await using var dbContext = CreateDbContext();
        var tesisA = await YeniTesisAsync(dbContext, suffixA);
        var tesisB = await YeniTesisAsync(dbContext, suffixB);
        var cariA = await YeniCariKartAsync(dbContext, tesisA, suffixA);
        var bugun = DateTime.UtcNow.Date;

        var fisTesisB = await YeniFisAsync(dbContext, tesisB, bugun, MuhasebeFisDurumlari.Onayli, maliYil: 2026, donem: bugun.Month);
        var belgeId = await YeniBelgeAsync(dbContext, cariA, 250m, $"{suffixA}-A", bugun, OdemeYontemleri.Nakit, null, fisTesisB);

        var svc = CreateService(dbContext, tesisA, tesisB);
        var sonuc = await svc.AraAsync(new PagedRequest(), new OdemeAramaFilterDto { TesisId = tesisA, Donem = bugun.Month });

        Assert.DoesNotContain(sonuc.Items, x => x.Id == belgeId);
    }

    [IntegrationFact]
    public async Task Arama_MaliYilVeDonemFiltresi_AyniTesisinFisiyleEslesmeyeDevamEder()
    {
        var suffix = YeniSuffix();
        await using var dbContext = CreateDbContext();
        var tesisId = await YeniTesisAsync(dbContext, suffix);
        var cariId = await YeniCariKartAsync(dbContext, tesisId, suffix);
        var bugun = DateTime.UtcNow.Date;

        var fisId = await YeniFisAsync(dbContext, tesisId, bugun, MuhasebeFisDurumlari.Onayli, maliYil: 2026, donem: bugun.Month);
        var belgeId = await YeniBelgeAsync(dbContext, cariId, 250m, $"{suffix}-A", bugun, OdemeYontemleri.Nakit, null, fisId);

        var svc = CreateService(dbContext, tesisId);
        var sonuc = await svc.AraAsync(new PagedRequest(), new OdemeAramaFilterDto { TesisId = tesisId, MaliYil = 2026, Donem = bugun.Month });

        Assert.Contains(sonuc.Items, x => x.Id == belgeId);
    }

    [IntegrationFact]
    public async Task Arama_RezervasyonReferansNoFiltresi_BaskaTesisinRezervasyonuylaEslesmez()
    {
        var suffixA = YeniSuffix();
        var suffixB = YeniSuffix();
        await using var dbContext = CreateDbContext();
        var tesisA = await YeniTesisAsync(dbContext, suffixA);
        var tesisB = await YeniTesisAsync(dbContext, suffixB);
        var cariA = await YeniCariKartAsync(dbContext, tesisA, suffixA);

        var belgeId = await YeniBelgeAsync(dbContext, cariA, 100m, $"{suffixA}-A", DateTime.UtcNow.Date);
        var referansNo = $"{suffixB}-REZ";
        await YeniRezervasyonOdemeBaglantisiAsync(dbContext, tesisB, suffixB, belgeId, referansNo);

        var svc = CreateService(dbContext, tesisA, tesisB);
        var sonuc = await svc.AraAsync(new PagedRequest(), new OdemeAramaFilterDto { TesisId = tesisA, RezervasyonReferansNo = referansNo });

        Assert.DoesNotContain(sonuc.Items, x => x.Id == belgeId);
    }

    [IntegrationFact]
    public async Task Arama_RezervasyonReferansNoFiltresi_AyniTesisinRezervasyonuylaEslesmeyeDevamEder()
    {
        var suffix = YeniSuffix();
        await using var dbContext = CreateDbContext();
        var tesisId = await YeniTesisAsync(dbContext, suffix);
        var cariId = await YeniCariKartAsync(dbContext, tesisId, suffix);

        var belgeId = await YeniBelgeAsync(dbContext, cariId, 100m, $"{suffix}-A", DateTime.UtcNow.Date);
        var referansNo = $"{suffix}-REZ";
        await YeniRezervasyonOdemeBaglantisiAsync(dbContext, tesisId, suffix, belgeId, referansNo);

        var svc = CreateService(dbContext, tesisId);
        var sonuc = await svc.AraAsync(new PagedRequest(), new OdemeAramaFilterDto { TesisId = tesisId, RezervasyonReferansNo = referansNo });

        Assert.Contains(sonuc.Items, x => x.Id == belgeId);
    }

    [IntegrationFact]
    public async Task Uyari_BaskaTesistekiFarkliDonemliFis_FarkliMuhasebeDonemineDusmeUretilmez()
    {
        var suffixA = YeniSuffix();
        var suffixB = YeniSuffix();
        await using var dbContext = CreateDbContext();
        var tesisA = await YeniTesisAsync(dbContext, suffixA);
        var tesisB = await YeniTesisAsync(dbContext, suffixB);
        var cariA = await YeniCariKartAsync(dbContext, tesisA, suffixA);
        var bugun = DateTime.UtcNow.Date;
        var farkliDonemNo = bugun.Month == 12 ? 1 : bugun.Month + 1;

        // Fis TesisB'ye ait - donemi de odeme tarihinden farkli.
        var fisTesisB = await YeniFisAsync(dbContext, tesisB, bugun, MuhasebeFisDurumlari.Onayli, maliYil: 2030, donem: farkliDonemNo);

        var belgeId = await YeniBelgeAsync(dbContext, cariA, 250m, $"{suffixA}-A", bugun, OdemeYontemleri.Nakit, null, fisTesisB);

        var svc = CreateService(dbContext, tesisA, tesisB);
        var detay = await svc.GetDetayAsync(belgeId);

        Assert.DoesNotContain(detay.Uyarilar, u => u.UyariTipi == OdemeUyariTipleri.FarkliMuhasebeDonemineDusme);
        Assert.DoesNotContain(detay.Uyarilar, u => u.Aciklama != null && u.Aciklama.Contains("2030"));
    }

    [IntegrationFact]
    public async Task Uyari_AyniTesistekiFarkliDonemliFis_FarkliMuhasebeDonemineDusmeUretilir()
    {
        var suffix = YeniSuffix();
        await using var dbContext = CreateDbContext();
        var tesisId = await YeniTesisAsync(dbContext, suffix);
        var cariId = await YeniCariKartAsync(dbContext, tesisId, suffix);
        var bugun = DateTime.UtcNow.Date;
        var farkliDonemNo = bugun.Month == 12 ? 1 : bugun.Month + 1;

        var fisId = await YeniFisAsync(dbContext, tesisId, bugun, MuhasebeFisDurumlari.Onayli, maliYil: 2030, donem: farkliDonemNo);

        var belgeId = await YeniBelgeAsync(dbContext, cariId, 250m, $"{suffix}-A", bugun, OdemeYontemleri.Nakit, null, fisId);

        var svc = CreateService(dbContext, tesisId);
        var detay = await svc.GetDetayAsync(belgeId);

        Assert.Contains(detay.Uyarilar, u => u.UyariTipi == OdemeUyariTipleri.FarkliMuhasebeDonemineDusme
            && u.Aciklama != null && u.Aciklama.Contains("2030"));
    }

    [IntegrationFact]
    public async Task Uyari_BaskaTesistekiOrtakHesap_AyniTutarAyniTarihFarkliCariUretilmez()
    {
        var suffixA = YeniSuffix();
        var suffixB = YeniSuffix();
        await using var dbContext = CreateDbContext();
        var tesisA = await YeniTesisAsync(dbContext, suffixA);
        var tesisB = await YeniTesisAsync(dbContext, suffixB);
        var cariA1 = await YeniCariKartAsync(dbContext, tesisA, suffixA + "1");
        var cariA2 = await YeniCariKartAsync(dbContext, tesisA, suffixA + "2");
        var bugun = DateTime.UtcNow.Date;

        var hpB = await YeniHesapPlaniAsync(dbContext, suffixB, "BANKA");
        var bankaHesabiTesisB = await YeniKasaBankaHesabiAsync(dbContext, tesisB, KasaBankaHesapTipleri.Banka, suffixB, "BNK", hpB);

        // Iki farkli cariye ait, ayni tarih/tutarli odeme - ikisi de yanlislikla TesisB'nin hesabina bagli.
        var belgeId1 = await YeniBelgeAsync(dbContext, cariA1, 300m, $"{suffixA}-A1", bugun, OdemeYontemleri.HavaleEft, bankaHesabiTesisB);
        var belgeId2 = await YeniBelgeAsync(dbContext, cariA2, 300m, $"{suffixA}-A2", bugun, OdemeYontemleri.HavaleEft, bankaHesabiTesisB);

        var svc = CreateService(dbContext, tesisA, tesisB);
        var detay = await svc.GetDetayAsync(belgeId1);

        Assert.DoesNotContain(detay.Uyarilar, u => u.UyariTipi == OdemeUyariTipleri.AyniTutarAyniTarihFarkliCari);
        Assert.DoesNotContain(detay.Uyarilar, u => u.IliskiliBelgeId == belgeId2);
    }

    [IntegrationFact]
    public async Task Uyari_AyniTesistekiGecerliOrtakHesap_AyniTutarAyniTarihFarkliCariUretilir()
    {
        var suffix = YeniSuffix();
        await using var dbContext = CreateDbContext();
        var tesisId = await YeniTesisAsync(dbContext, suffix);
        var cari1 = await YeniCariKartAsync(dbContext, tesisId, suffix + "1");
        var cari2 = await YeniCariKartAsync(dbContext, tesisId, suffix + "2");
        var bugun = DateTime.UtcNow.Date;

        var hp = await YeniHesapPlaniAsync(dbContext, suffix, "BANKA");
        var bankaHesabi = await YeniKasaBankaHesabiAsync(dbContext, tesisId, KasaBankaHesapTipleri.Banka, suffix, "BNK", hp);

        var belgeId1 = await YeniBelgeAsync(dbContext, cari1, 300m, $"{suffix}-A1", bugun, OdemeYontemleri.HavaleEft, bankaHesabi);
        var belgeId2 = await YeniBelgeAsync(dbContext, cari2, 300m, $"{suffix}-A2", bugun, OdemeYontemleri.HavaleEft, bankaHesabi);

        var svc = CreateService(dbContext, tesisId);
        var detay = await svc.GetDetayAsync(belgeId1);

        Assert.Contains(detay.Uyarilar, u => u.UyariTipi == OdemeUyariTipleri.AyniTutarAyniTarihFarkliCari
            && u.IliskiliBelgeId == belgeId2);
    }

    [IntegrationFact]
    public async Task Detay_KullaniciHemAHemBTesisineYetkiliyken_ABelgesiBRezervasyonunaBagliysa_RezervasyonBilgisiSizmaz()
    {
        var suffixA = YeniSuffix();
        var suffixB = YeniSuffix();
        await using var dbContext = CreateDbContext();
        var tesisA = await YeniTesisAsync(dbContext, suffixA);
        var tesisB = await YeniTesisAsync(dbContext, suffixB);
        var cariA = await YeniCariKartAsync(dbContext, tesisA, suffixA);

        var belgeId = await YeniBelgeAsync(dbContext, cariA, 100m, $"{suffixA}-A", DateTime.UtcNow.Date);
        var referansNo = $"{suffixB}-REZ";
        await YeniRezervasyonOdemeBaglantisiAsync(dbContext, tesisB, suffixB, belgeId, referansNo);

        var svc = CreateService(dbContext, tesisA, tesisB);
        var detay = await svc.GetDetayAsync(belgeId);

        Assert.Null(detay.RezervasyonId);
        Assert.Null(detay.RezervasyonReferansNo);
    }

    [IntegrationFact]
    public async Task Detay_OdemeVeRezervasyonAyniTesistiyse_RezervasyonBilgisiDonmeyeDevamEder()
    {
        var suffix = YeniSuffix();
        await using var dbContext = CreateDbContext();
        var tesisId = await YeniTesisAsync(dbContext, suffix);
        var cariId = await YeniCariKartAsync(dbContext, tesisId, suffix);

        var belgeId = await YeniBelgeAsync(dbContext, cariId, 100m, $"{suffix}-A", DateTime.UtcNow.Date);
        var referansNo = $"{suffix}-REZ";
        await YeniRezervasyonOdemeBaglantisiAsync(dbContext, tesisId, suffix, belgeId, referansNo);

        var svc = CreateService(dbContext, tesisId);
        var detay = await svc.GetDetayAsync(belgeId);

        Assert.NotNull(detay.RezervasyonId);
        Assert.Equal(referansNo, detay.RezervasyonReferansNo);
    }

    [IntegrationFact]
    public async Task CariDokum_BaskaTesistekiPosKaydi_ToplamaEklenmez()
    {
        var suffixA = YeniSuffix();
        var suffixB = YeniSuffix();
        await using var dbContext = CreateDbContext();
        var tesisA = await YeniTesisAsync(dbContext, suffixA);
        var tesisB = await YeniTesisAsync(dbContext, suffixB);
        var cariA = await YeniCariKartAsync(dbContext, tesisA, suffixA);
        var hp = await YeniHesapPlaniAsync(dbContext, suffixA, "KK");
        var kkId = await YeniKasaBankaHesabiAsync(dbContext, tesisA, KasaBankaHesapTipleri.KrediKarti, suffixA, "KK1", hp);
        var bugun = DateOnly.FromDateTime(DateTime.UtcNow.Date);

        var belgeId = await YeniBelgeAsync(dbContext, cariA, 300m, $"{suffixA}-KK", DateTime.UtcNow.Date, OdemeYontemleri.KrediKarti, kkId);
        // POS valor kaydi yanlislikla TesisB'ye ait olarak olusturulmus - odeme/cari TesisA'da.
        await YeniValorAsync(dbContext, tesisB, belgeId, kkId, null, PosTahsilatValorDurumlari.ValorBekliyor, bugun, 300m);

        var svc = CreateService(dbContext, tesisA, tesisB);
        var dokum = await svc.GetCariHareketDokumuAsync(new CariHareketDokumFilterDto { CariKartId = cariA });

        Assert.DoesNotContain(dokum.ParaBirimiOzetleri, x => x.NormalAktarilmayiBekleyenPos > 0);
        Assert.DoesNotContain(dokum.ParaBirimiOzetleri, x => x.MutabakatBekleyenPos > 0);
        Assert.DoesNotContain(dokum.ParaBirimiOzetleri, x => x.HataliPos > 0);
        Assert.DoesNotContain(dokum.ParaBirimiOzetleri, x => x.AktarimSurecindekiPos > 0);
    }

    [IntegrationFact]
    public async Task CariDokum_BelirsizTarihliBelgeninBaskaTesistekiPosKaydi_ToplamaEklenmezUyariUretilmez()
    {
        var suffixA = YeniSuffix();
        var suffixB = YeniSuffix();
        await using var dbContext = CreateDbContext();
        var tesisA = await YeniTesisAsync(dbContext, suffixA);
        var tesisB = await YeniTesisAsync(dbContext, suffixB);
        var cariA = await YeniCariKartAsync(dbContext, tesisA, suffixA);
        var hp = await YeniHesapPlaniAsync(dbContext, suffixA, "KK");
        var kkId = await YeniKasaBankaHesabiAsync(dbContext, tesisA, KasaBankaHesapTipleri.KrediKarti, suffixA, "KK1", hp);
        var bugun = DateTime.UtcNow.Date;

        var belgeId = await YeniBelgeAsync(dbContext, cariA, 300m, $"{suffixA}-KK", bugun, OdemeYontemleri.KrediKarti, kkId);
        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE [Muhasebe].[TahsilatOdemeBelgeleri] SET [BelgeTarihi] = '0001-01-01' WHERE [Id] = {belgeId}");
        // POS valor kaydi yanlislikla TesisB'ye ait olarak olusturulmus.
        await YeniValorAsync(dbContext, tesisB, belgeId, kkId, null, PosTahsilatValorDurumlari.ValorBekliyor,
            DateOnly.FromDateTime(bugun), 300m);

        var svc = CreateService(dbContext, tesisA, tesisB);
        var dokum = await svc.GetCariHareketDokumuAsync(new CariHareketDokumFilterDto
        {
            CariKartId = cariA,
            TarihBaslangic = DateOnly.FromDateTime(bugun.AddDays(-10)),
            TarihBitis = DateOnly.FromDateTime(bugun.AddDays(10))
        });

        Assert.DoesNotContain(dokum.ParaBirimiOzetleri, x => x.DonemeKatilmayanBelirsizTarihliPos > 0);
        Assert.DoesNotContain(dokum.Uyarilar, u => u.Contains("belge tarihi tanımsız"));
    }

    [IntegrationFact]
    public async Task CariDokum_OdemeCariVePosValorKaydiAyniTesistiyse_ToplamaEklenmeyeDevamEder()
    {
        var suffix = YeniSuffix();
        await using var dbContext = CreateDbContext();
        var tesisId = await YeniTesisAsync(dbContext, suffix);
        var cariId = await YeniCariKartAsync(dbContext, tesisId, suffix);
        var hp = await YeniHesapPlaniAsync(dbContext, suffix, "KK");
        var kkId = await YeniKasaBankaHesabiAsync(dbContext, tesisId, KasaBankaHesapTipleri.KrediKarti, suffix, "KK1", hp);
        var bugun = DateOnly.FromDateTime(DateTime.UtcNow.Date);

        var belgeId = await YeniBelgeAsync(dbContext, cariId, 300m, $"{suffix}-KK", DateTime.UtcNow.Date, OdemeYontemleri.KrediKarti, kkId);
        await YeniValorAsync(dbContext, tesisId, belgeId, kkId, null, PosTahsilatValorDurumlari.ValorBekliyor, bugun, 300m);

        var svc = CreateService(dbContext, tesisId);
        var dokum = await svc.GetCariHareketDokumuAsync(new CariHareketDokumFilterDto { CariKartId = cariId });

        var tryOzet = dokum.ParaBirimiOzetleri.Single(x => x.ParaBirimi == "TRY");
        Assert.Equal(300m, tryOzet.NormalAktarilmayiBekleyenPos);
    }

    [IntegrationFact]
    public async Task Karsilastir_BaskaTesisinHesabiEslesmeOlcutuOlarakKullanilmaz()
    {
        var suffixA = YeniSuffix();
        var suffixB = YeniSuffix();
        await using var dbContext = CreateDbContext();
        var tesisA = await YeniTesisAsync(dbContext, suffixA);
        var tesisB = await YeniTesisAsync(dbContext, suffixB);
        var cariA = await YeniCariKartAsync(dbContext, tesisA, suffixA);
        var bugun = DateTime.UtcNow.Date;

        var hpB = await YeniHesapPlaniAsync(dbContext, suffixB, "BANKA");
        var bankaHesabiTesisB = await YeniKasaBankaHesabiAsync(dbContext, tesisB, KasaBankaHesapTipleri.Banka, suffixB, "BNK", hpB);

        await YeniBelgeAsync(dbContext, cariA, 450m, $"{suffixA}-X", bugun, OdemeYontemleri.HavaleEft, bankaHesabiTesisB);

        var svc = CreateService(dbContext, tesisA, tesisB);
        var sonuc = await svc.KarsilastirAsync(new BeyanEdilenOdemeKarsilastirmaFilterDto
        {
            TesisId = tesisA, Tarih = DateOnly.FromDateTime(bugun), Tutar = 450m, ParaBirimi = "TRY", KasaBankaHesapId = bankaHesabiTesisB
        });

        Assert.Single(sonuc);
        Assert.Equal(OdemeGuvenSeviyeleri.IncelenmesiGereken, sonuc[0].GuvenSeviyesi);
        Assert.Contains(sonuc[0].UyusmayanAlanlar, x => x.Contains("Kasa/banka"));
        Assert.DoesNotContain(sonuc[0].EslesenAlanlar, x => x.Contains("Kasa/banka"));
    }

    [IntegrationFact]
    public async Task Karsilastir_AyniTesisinGecerliHesabiEslesmeOlcutuOlarakKullanilir()
    {
        var suffix = YeniSuffix();
        await using var dbContext = CreateDbContext();
        var tesisId = await YeniTesisAsync(dbContext, suffix);
        var cariId = await YeniCariKartAsync(dbContext, tesisId, suffix);
        var bugun = DateTime.UtcNow.Date;

        var hp = await YeniHesapPlaniAsync(dbContext, suffix, "BANKA");
        var bankaHesabi = await YeniKasaBankaHesabiAsync(dbContext, tesisId, KasaBankaHesapTipleri.Banka, suffix, "BNK", hp);

        await YeniBelgeAsync(dbContext, cariId, 450m, $"{suffix}-X", bugun, OdemeYontemleri.HavaleEft, bankaHesabi);

        var svc = CreateService(dbContext, tesisId);
        var sonuc = await svc.KarsilastirAsync(new BeyanEdilenOdemeKarsilastirmaFilterDto
        {
            TesisId = tesisId, Tarih = DateOnly.FromDateTime(bugun), Tutar = 450m, ParaBirimi = "TRY", KasaBankaHesapId = bankaHesabi
        });

        Assert.Single(sonuc);
        Assert.Equal(OdemeGuvenSeviyeleri.YuksekOlasilik, sonuc[0].GuvenSeviyesi);
        Assert.Contains(sonuc[0].EslesenAlanlar, x => x.Contains("Kasa/banka"));
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
