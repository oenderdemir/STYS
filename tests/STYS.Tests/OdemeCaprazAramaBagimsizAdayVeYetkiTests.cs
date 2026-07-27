using Microsoft.EntityFrameworkCore;
using STYS.AccessScope;
using STYS.Infrastructure.EntityFramework;
using STYS.Muhasebe.BankaHareketleri.Entities;
using STYS.Muhasebe.CariHareketler.Entities;
using STYS.Muhasebe.CariKartlar.Entities;
using STYS.Muhasebe.Common.Constants;
using STYS.Muhasebe.Common.Services;
using STYS.Muhasebe.KasaBankaHesaplari.Entities;
using STYS.Muhasebe.KasaHareketleri.Entities;
using STYS.Muhasebe.MuhasebeFisleri.Entities;
using STYS.Muhasebe.MuhasebeHesapPlanlari.Entities;
using STYS.Muhasebe.OdemeIzleme.Dtos;
using STYS.Muhasebe.OdemeIzleme.Services;
using TOD.Platform.Persistence.Rdbms.Paging;

namespace STYS.Tests;

/// <summary>
/// Capraz-kaynak aramanin GERCEKTEN BAGIMSIZ (KaynakModul==TahsilatOdemeBelgesi'ye baglı OLMAYAN)
/// aday kesfettigini, filtre uygulanamayan kaynaklarin sessizce yaniltici sonuc dondurmedigini ve
/// yetki disi tesise ait kayitlarin sizmadigini KANITLAR.
/// </summary>
[Trait("Category", "Integration")]
public class OdemeCaprazAramaBagimsizAdayVeYetkiTests : IAsyncLifetime
{
    private static readonly string? ConnectionString =
        Environment.GetEnvironmentVariable(IntegrationFactAttribute.ConnectionStringEnvVar);

    private const string TestMarker = "SQLK-971";

    private readonly List<int> _tesisIdler = [];
    private readonly List<int> _kurumIdler = [];
    private readonly List<int> _illIdler = [];
    private readonly List<int> _cariKartIdler = [];
    private readonly List<int> _kasaBankaHesapIdler = [];
    private readonly List<int> _hesapPlaniIdler = [];
    private readonly List<int> _cariHareketIdler = [];
    private readonly List<int> _kasaHareketIdler = [];
    private readonly List<int> _bankaHareketIdler = [];
    private readonly List<int> _fisIdler = [];
    private readonly List<int> _fisSatirIdler = [];

    private static StysAppDbContext CreateDbContext()
    {
        var builder = new DbContextOptionsBuilder<StysAppDbContext>().UseSqlServer(ConnectionString);
        return new StysAppDbContext(builder.Options, new FakeCurrentUserAccessor(), new FakeCurrentTenantAccessor());
    }

    public Task InitializeAsync() => Task.CompletedTask;

    private List<STYS.Tests.TestSupport.CleanupAdimi> OlusturCleanupAdimlari() =>
    [
        new("MuhasebeFisSatirlari silme", async () =>
        {
            if (_fisSatirIdler.Count == 0) return;
            await using var db = CreateDbContext();
            await db.MuhasebeFisSatirlari.IgnoreQueryFilters().Where(x => _fisSatirIdler.Contains(x.Id)).ExecuteDeleteAsync();
        }),
        new("MuhasebeFisler silme", async () =>
        {
            if (_fisIdler.Count == 0) return;
            await using var db = CreateDbContext();
            await db.MuhasebeFisler.IgnoreQueryFilters().Where(x => _fisIdler.Contains(x.Id)).ExecuteDeleteAsync();
        }),
        new("BankaHareketleri silme", async () =>
        {
            if (_bankaHareketIdler.Count == 0) return;
            await using var db = CreateDbContext();
            await db.BankaHareketleri.IgnoreQueryFilters().Where(x => _bankaHareketIdler.Contains(x.Id)).ExecuteDeleteAsync();
        }),
        new("KasaHareketleri silme", async () =>
        {
            if (_kasaHareketIdler.Count == 0) return;
            await using var db = CreateDbContext();
            await db.KasaHareketleri.IgnoreQueryFilters().Where(x => _kasaHareketIdler.Contains(x.Id)).ExecuteDeleteAsync();
        }),
        new("CariHareketler silme", async () =>
        {
            if (_cariHareketIdler.Count == 0) return;
            await using var db = CreateDbContext();
            await db.CariHareketler.IgnoreQueryFilters().Where(x => _cariHareketIdler.Contains(x.Id)).ExecuteDeleteAsync();
        }),
        new("KasaBankaHesaplari silme", async () =>
        {
            if (_kasaBankaHesapIdler.Count == 0) return;
            await using var db = CreateDbContext();
            await db.KasaBankaHesaplari.IgnoreQueryFilters().Where(x => _kasaBankaHesapIdler.Contains(x.Id)).ExecuteDeleteAsync();
        }),
        new("MuhasebeHesapPlanlari silme", async () =>
        {
            if (_hesapPlaniIdler.Count == 0) return;
            await using var db = CreateDbContext();
            await db.MuhasebeHesapPlanlari.IgnoreQueryFilters().Where(x => _hesapPlaniIdler.Contains(x.Id)).ExecuteDeleteAsync();
        }),
        new("CariKartlar silme", async () =>
        {
            if (_cariKartIdler.Count == 0) return;
            await using var db = CreateDbContext();
            await db.CariKartlar.IgnoreQueryFilters().Where(x => _cariKartIdler.Contains(x.Id)).ExecuteDeleteAsync();
        }),
        new("Tesisler silme", async () =>
        {
            if (_tesisIdler.Count == 0) return;
            await using var db = CreateDbContext();
            await db.Tesisler.IgnoreQueryFilters().Where(x => _tesisIdler.Contains(x.Id)).ExecuteDeleteAsync();
        }),
        new("Iller silme", async () =>
        {
            if (_illIdler.Count == 0) return;
            await using var db = CreateDbContext();
            await db.Iller.IgnoreQueryFilters().Where(x => _illIdler.Contains(x.Id)).ExecuteDeleteAsync();
        }),
        new("Kurumlar silme", async () =>
        {
            if (_kurumIdler.Count == 0) return;
            await using var db = CreateDbContext();
            await db.Kurumlar.IgnoreQueryFilters().Where(x => _kurumIdler.Contains(x.Id)).ExecuteDeleteAsync();
        }),
    ];

    public async Task DisposeAsync()
    {
        if (string.IsNullOrWhiteSpace(ConnectionString))
        {
            return;
        }

        var hatalar = (await STYS.Tests.TestSupport.TwoPhaseCleanupRunner.CalistirAsync(OlusturCleanupAdimlari())).ToList();

        await using var db = CreateDbContext();
        var kalan = await db.Kurumlar.IgnoreQueryFilters().CountAsync(x => _kurumIdler.Contains(x.Id));
        if (kalan > 0)
        {
            hatalar.Add(new InvalidOperationException($"Cleanup sonrasi kalinti: Kurumlar={kalan}"));
        }

        if (hatalar.Count > 0)
        {
            throw new AggregateException($"[OdemeCaprazAramaBagimsizAdayVeYetkiTests.DisposeAsync] {hatalar.Count} cleanup hatasi.", hatalar);
        }
    }

    private async Task<int> SeedTesisAsync(StysAppDbContext db, string suffix)
    {
        var kurum = new STYS.Kurumlar.Entities.Kurum { Kod = suffix, Ad = "K " + suffix, AktifMi = true };
        db.Kurumlar.Add(kurum);
        var il = new STYS.Iller.Entities.Il { Ad = "I " + suffix, AktifMi = true };
        db.Iller.Add(il);
        await db.SaveChangesAsync();
        _kurumIdler.Add(kurum.Id);
        _illIdler.Add(il.Id);

        var tesis = new STYS.Tesisler.Entities.Tesis
        {
            KurumId = kurum.Id, IlId = il.Id, Ad = "T " + suffix, Telefon = "0", Adres = "A", AktifMi = true
        };
        db.Tesisler.Add(tesis);
        await db.SaveChangesAsync();
        _tesisIdler.Add(tesis.Id);
        return tesis.Id;
    }

    private async Task<int> SeedCariKartAsync(StysAppDbContext db, int tesisId, string suffix)
    {
        var cari = new CariKart
        {
            TesisId = tesisId, CariTipi = CariKartTipleri.Musteri,
            CariKodu = suffix, UnvanAdSoyad = "C " + suffix, AktifMi = true
        };
        db.CariKartlar.Add(cari);
        await db.SaveChangesAsync();
        _cariKartIdler.Add(cari.Id);
        return cari.Id;
    }

    private async Task<int> SeedKasaBankaHesapAsync(StysAppDbContext db, int? tesisId, string suffix)
    {
        var hesap = new KasaBankaHesap
        {
            TesisId = tesisId, Tip = KasaBankaHesapTipleri.Banka, Kod = suffix, Ad = "H " + suffix,
            ParaBirimi = "TRY", AktifMi = true
        };
        db.KasaBankaHesaplari.Add(hesap);
        await db.SaveChangesAsync();
        _kasaBankaHesapIdler.Add(hesap.Id);
        return hesap.Id;
    }

    private async Task<int> SeedHesapPlaniAsync(StysAppDbContext db, string suffix)
    {
        var hp = new MuhasebeHesapPlani { Kod = suffix, TamKod = suffix, Ad = "HP " + suffix, SeviyeNo = 1, AktifMi = true };
        db.MuhasebeHesapPlanlari.Add(hp);
        await db.SaveChangesAsync();
        _hesapPlaniIdler.Add(hp.Id);
        return hp.Id;
    }

    private async Task<int> SeedMuhasebeFisAsync(
        StysAppDbContext db, int tesisId, string fisNo, string? kaynakModul, int? kaynakId,
        int maliYil = 2026, int donem = 7, string durum = MuhasebeFisDurumlari.Onayli)
    {
        var fis = new MuhasebeFis
        {
            TesisId = tesisId, MaliYil = maliYil, Donem = donem, FisNo = fisNo, FisTarihi = DateTime.UtcNow.Date,
            FisTipi = MuhasebeFisTipleri.Mahsup, KaynakModul = kaynakModul ?? string.Empty, KaynakId = kaynakId,
            Durum = durum, ToplamBorc = 100m, ToplamAlacak = 100m
        };
        db.MuhasebeFisler.Add(fis);
        await db.SaveChangesAsync();
        _fisIdler.Add(fis.Id);
        return fis.Id;
    }

    [IntegrationFact]
    public async Task Bagimsiz_CariHareket_KaynakModulKisitiOlmadan_Aday_Olarak_Bulunur()
    {
        await using var db = CreateDbContext();
        var suffix = $"{TestMarker}-{Guid.NewGuid():N}"[..20];
        var tesisId = await SeedTesisAsync(db, suffix);
        var cariId = await SeedCariKartAsync(db, tesisId, suffix);

        // KaynakModul BOS/farkli - hicbir odeme belgesine bagli DEGIL.
        var hareket = new CariHareket
        {
            CariKartId = cariId, HareketTarihi = DateTime.UtcNow.Date, BelgeTuru = "Manuel",
            BorcTutari = 250m, AlacakTutari = 0m, ParaBirimi = "TRY", Durum = CariHareketDurumlari.Aktif,
            KaynakModul = null, KaynakId = null
        };
        db.CariHareketler.Add(hareket);
        await db.SaveChangesAsync();
        _cariHareketIdler.Add(hareket.Id);

        var svc = new OdemeCaprazAramaService(db, new FakeMuhasebeTesisScopeService([tesisId]));
        var sonuc = await svc.AraAsync(
            new PagedRequest { PageNumber = 1, PageSize = 20 },
            new OdemeCaprazAramaFilterDto { TesisId = tesisId, CariKartId = cariId });

        var aday = Assert.Single(sonuc.Items);
        Assert.True(aday.BagimsizKayitMi);
        Assert.Equal(OdemeGuvenSeviyeleri.IncelenmesiGereken, aday.GuvenSeviyesi);
        Assert.Contains(OdemeAdayKaynaklari.CariHareket, aday.BulunduguKaynaklar);
        Assert.Equal($"CARIHAREKET:{hareket.Id}", aday.TekillestirmeAnahtari);
    }

    [IntegrationFact]
    public async Task Bagimsiz_MuhasebeFisi_OdemeBelgesineBagliOlmadanBileBulunur()
    {
        await using var db = CreateDbContext();
        var suffix = $"{TestMarker}-{Guid.NewGuid():N}"[..20];
        var tesisId = await SeedTesisAsync(db, suffix);
        var cariId = await SeedCariKartAsync(db, tesisId, suffix);
        var hesapPlaniId = await SeedHesapPlaniAsync(db, suffix);

        var fisId = await SeedMuhasebeFisAsync(db, tesisId, suffix, kaynakModul: null, kaynakId: null);
        var satir = new MuhasebeFisSatir
        {
            MuhasebeFisId = fisId, MuhasebeHesapPlaniId = hesapPlaniId, SiraNo = 1,
            Borc = 100m, Alacak = 0m, ParaBirimi = "TRY", CariKartId = cariId
        };
        db.MuhasebeFisSatirlari.Add(satir);
        await db.SaveChangesAsync();
        _fisSatirIdler.Add(satir.Id);

        var svc = new OdemeCaprazAramaService(db, new FakeMuhasebeTesisScopeService([tesisId]));
        var sonuc = await svc.AraAsync(
            new PagedRequest { PageNumber = 1, PageSize = 20 },
            new OdemeCaprazAramaFilterDto { TesisId = tesisId, CariKartId = cariId });

        var aday = Assert.Single(sonuc.Items);
        Assert.True(aday.BagimsizKayitMi);
        Assert.Equal($"FIS:{fisId}", aday.TekillestirmeAnahtari);
        Assert.Contains(OdemeAdayKaynaklari.MuhasebeFis, aday.BulunduguKaynaklar);
    }

    [IntegrationFact]
    public async Task Bagimsiz_KasaHareketi_ve_BankaHareketi_KaynakModulKisitiOlmadanBulunur()
    {
        await using var db = CreateDbContext();
        var suffix = $"{TestMarker}-{Guid.NewGuid():N}"[..20];
        var tesisId = await SeedTesisAsync(db, suffix);
        var cariId = await SeedCariKartAsync(db, tesisId, suffix);
        var hesapId = await SeedKasaBankaHesapAsync(db, tesisId, suffix);

        var kasa = new KasaHareket
        {
            KasaKodu = suffix, KasaBankaHesapId = hesapId, HareketTarihi = DateTime.UtcNow.Date,
            HareketTipi = KasaHareketTipleri.Tahsilat, Tutar = 75m, ParaBirimi = "TRY",
            CariKartId = cariId, Durum = CariHareketDurumlari.Aktif, KaynakModul = null, KaynakId = null
        };
        db.KasaHareketleri.Add(kasa);
        var banka = new BankaHareket
        {
            BankaAdi = "Test Banka", HesapKoduIban = "TR000", KasaBankaHesapId = hesapId,
            HareketTarihi = DateTime.UtcNow.Date, HareketTipi = KasaHareketTipleri.Tahsilat,
            Tutar = 85m, ParaBirimi = "TRY", CariKartId = cariId, Durum = CariHareketDurumlari.Aktif,
            KaynakModul = null, KaynakId = null
        };
        db.BankaHareketleri.Add(banka);
        await db.SaveChangesAsync();
        _kasaHareketIdler.Add(kasa.Id);
        _bankaHareketIdler.Add(banka.Id);

        var svc = new OdemeCaprazAramaService(db, new FakeMuhasebeTesisScopeService([tesisId]));
        var sonuc = await svc.AraAsync(
            new PagedRequest { PageNumber = 1, PageSize = 20 },
            new OdemeCaprazAramaFilterDto { TesisId = tesisId, CariKartId = cariId });

        Assert.Equal(2, sonuc.Items.Count);
        Assert.Contains(sonuc.Items, a => a.TekillestirmeAnahtari == $"KASAHAREKET:{kasa.Id}" && a.BagimsizKayitMi);
        Assert.Contains(sonuc.Items, a => a.TekillestirmeAnahtari == $"BANKAHAREKET:{banka.Id}" && a.BagimsizKayitMi);
    }

    [IntegrationFact]
    public async Task BaskaTesisinCariHareketi_YetkiDisindaOldugundaHicSizmaz_ve_ToplamaGirmedi()
    {
        await using var db = CreateDbContext();
        var suffix = $"{TestMarker}-{Guid.NewGuid():N}"[..20];
        var yetkiliTesisId = await SeedTesisAsync(db, suffix + "-A");
        var baskaTesisId = await SeedTesisAsync(db, suffix + "-B");

        // Ayni cari kodu, FARKLI tesiste - filtre CariKartId'yi bilmedigi icin tarih araligiyla ariyoruz.
        var cariBaska = await SeedCariKartAsync(db, baskaTesisId, suffix + "-BCARI");
        var hareketBaska = new CariHareket
        {
            CariKartId = cariBaska, HareketTarihi = DateTime.UtcNow.Date, BelgeTuru = "Manuel",
            BorcTutari = 999m, AlacakTutari = 0m, ParaBirimi = "TRY", Durum = CariHareketDurumlari.Aktif,
            KaynakModul = null, KaynakId = null
        };
        db.CariHareketler.Add(hareketBaska);
        await db.SaveChangesAsync();
        _cariHareketIdler.Add(hareketBaska.Id);

        // Kullanici SADECE yetkiliTesisId icin yetkili - baskaTesisId'nin verisi asla dahil edilmemeli.
        var svc = new OdemeCaprazAramaService(db, new FakeMuhasebeTesisScopeService([yetkiliTesisId]));
        var sonuc = await svc.AraAsync(
            new PagedRequest { PageNumber = 1, PageSize = 20 },
            new OdemeCaprazAramaFilterDto
            {
                TesisId = yetkiliTesisId,
                TarihBaslangic = DateOnly.FromDateTime(DateTime.UtcNow.Date),
                TarihBitis = DateOnly.FromDateTime(DateTime.UtcNow.Date)
            });

        Assert.Empty(sonuc.Items);
        Assert.Equal(0, sonuc.TotalCount);
    }

    [IntegrationFact]
    public async Task KasaBankaHesapFiltresi_CariHareketKaynaginda_GuvenilirIliskiOlmadigiIcinSonucDisiBirakilir()
    {
        await using var db = CreateDbContext();
        var suffix = $"{TestMarker}-{Guid.NewGuid():N}"[..20];
        var tesisId = await SeedTesisAsync(db, suffix);
        var cariId = await SeedCariKartAsync(db, tesisId, suffix);
        var hesapId = await SeedKasaBankaHesapAsync(db, tesisId, suffix);

        var hareket = new CariHareket
        {
            CariKartId = cariId, HareketTarihi = DateTime.UtcNow.Date, BelgeTuru = "Manuel",
            BorcTutari = 250m, AlacakTutari = 0m, ParaBirimi = "TRY", Durum = CariHareketDurumlari.Aktif,
            KaynakModul = null, KaynakId = null
        };
        db.CariHareketler.Add(hareket);
        await db.SaveChangesAsync();
        _cariHareketIdler.Add(hareket.Id);

        var svc = new OdemeCaprazAramaService(db, new FakeMuhasebeTesisScopeService([tesisId]));

        // KasaBankaHesapId filtresi CariHareket icin guvenilir bir sekilde uygulanamaz -> bu kaynak
        // SESSIZCE yok sayilmaz, SONUC DISI birakilir (yaniltici "eslesme yok" yerine kaynak disi kalir).
        var sonuc = await svc.AraAsync(
            new PagedRequest { PageNumber = 1, PageSize = 20 },
            new OdemeCaprazAramaFilterDto { TesisId = tesisId, KasaBankaHesapId = hesapId });

        Assert.Empty(sonuc.Items);
    }

    [IntegrationFact]
    public async Task AyniOdemeBelgesineBagliCariHareketVeFis_TEK_ADAY_olarak_birlesir()
    {
        await using var db = CreateDbContext();
        var suffix = $"{TestMarker}-{Guid.NewGuid():N}"[..20];
        var tesisId = await SeedTesisAsync(db, suffix);
        var cariId = await SeedCariKartAsync(db, tesisId, suffix);

        var belge = new STYS.Muhasebe.TahsilatOdemeBelgeleri.Entities.TahsilatOdemeBelgesi
        {
            BelgeNo = $"{suffix}-001",
            BelgeTarihi = DateTime.UtcNow.Date,
            BelgeTipi = STYS.Muhasebe.TahsilatOdemeBelgeleri.Entities.TahsilatOdemeBelgeTipleri.Tahsilat,
            CariKartId = cariId,
            Tutar = 300m,
            ParaBirimi = "TRY",
            OdemeYontemi = STYS.Muhasebe.TahsilatOdemeBelgeleri.Entities.OdemeYontemleri.Nakit,
            Durum = STYS.Muhasebe.TahsilatOdemeBelgeleri.Entities.TahsilatOdemeBelgeDurumlari.Aktif
        };
        db.TahsilatOdemeBelgeleri.Add(belge);
        await db.SaveChangesAsync();

        var hareket = new CariHareket
        {
            CariKartId = cariId, HareketTarihi = DateTime.UtcNow.Date, BelgeTuru = "TahsilatOdemeBelgesi",
            BorcTutari = 0m, AlacakTutari = 300m, ParaBirimi = "TRY", Durum = CariHareketDurumlari.Aktif,
            KaynakModul = MuhasebeKaynakModulleri.TahsilatOdemeBelgesi, KaynakId = belge.Id
        };
        db.CariHareketler.Add(hareket);
        await db.SaveChangesAsync();
        _cariHareketIdler.Add(hareket.Id);

        try
        {
            var svc = new OdemeCaprazAramaService(db, new FakeMuhasebeTesisScopeService([tesisId]));
            var sonuc = await svc.AraAsync(
                new PagedRequest { PageNumber = 1, PageSize = 20 },
                new OdemeCaprazAramaFilterDto { TesisId = tesisId, CariKartId = cariId });

            var aday = Assert.Single(sonuc.Items);
            Assert.False(aday.BagimsizKayitMi);
            Assert.Equal($"BELGE:{belge.Id}", aday.TekillestirmeAnahtari);
            Assert.Contains(OdemeAdayKaynaklari.TahsilatOdemeBelgesi, aday.BulunduguKaynaklar);
            Assert.Contains(OdemeAdayKaynaklari.CariHareket, aday.BulunduguKaynaklar);
        }
        finally
        {
            await db.TahsilatOdemeBelgeleri.IgnoreQueryFilters().Where(x => x.Id == belge.Id).ExecuteDeleteAsync();
        }
    }

    // ── Fake'ler ──

    private sealed class FakeCurrentUserAccessor : TOD.Platform.Security.Auth.Services.ICurrentUserAccessor
    {
        public string? GetCurrentUserName() => "bagimsiz-aday-test";
        public Guid? GetCurrentUserId() => Guid.NewGuid();
    }

    private sealed class FakeCurrentTenantAccessor : TOD.Platform.Security.Auth.Services.ICurrentTenantAccessor
    {
        public int? GetCurrentKurumId() => null;
        public IReadOnlyList<int> GetAccessibleKurumIds() => [];
        public bool IsSuperAdmin() => true;
        public bool IsKurumAdmin() => false;
    }

    private sealed class FakeMuhasebeTesisScopeService(IEnumerable<int> tesisIds) : IMuhasebeTesisScopeService
    {
        private readonly HashSet<int> _ids = tesisIds.ToHashSet();

        public Task<int[]> GetEffectiveTesisIdsAsync(CancellationToken cancellationToken = default) => Task.FromResult(_ids.ToArray());
        public Task<int[]> GetEffectiveTesisIdsAsync(DomainAccessScope scope, CancellationToken cancellationToken = default) => Task.FromResult(_ids.ToArray());
        public Task EnsureCanAccessTesisAsync(int tesisId, CancellationToken cancellationToken = default)
        {
            if (!_ids.Contains(tesisId)) throw new TOD.Platform.SharedKernel.Exceptions.BaseException("Bu tesis için yetkiniz bulunmuyor.", 403);
            return Task.CompletedTask;
        }
    }
}
