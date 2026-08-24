using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using STYS.AccessScope;
using STYS.Binalar.Entities;
using STYS.Infrastructure.EntityFramework;
using STYS.IsletmeAlanlari.Entities;
using STYS.Muhasebe.Depolar.Entities;
using STYS.Muhasebe.SarfFisleri.Entities;
using STYS.Muhasebe.SarfRaporlari.Dtos;
using STYS.Muhasebe.SarfRaporlari.Services;
using STYS.Muhasebe.StokHareketleri.Entities;
using STYS.Muhasebe.TasinirKartlari.Entities;
using STYS.Muhasebe.TasinirKodlari.Entities;
using STYS.OdaSiniflari.Entities;
using STYS.Odalar.Entities;
using STYS.OdaTipleri.Entities;
using STYS.Tesisler.Entities;
using TOD.Platform.Persistence.Rdbms.Paging;
using TOD.Platform.Security.Auth.Services;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.Tests;

public class SarfTuketimRaporServiceTests
{
    [Fact]
    public async Task GetDetayAsync_VarsayilanOlarakYalnizcaKesinlesenKayitlariVeMaliyetiGetirir()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext);
        await SeedSarfFisiAsync(
            dbContext,
            sarfFisiId: 1000,
            satirId: 1001,
            stokHareketId: 1002,
            durum: SarfFisiDurumlari.Kesinlesti,
            tarih: new DateTime(2026, 8, 24, 9, 0, 0),
            miktar: 3,
            maliyetBirimFiyat: 12.5m,
            maliyetTutari: 37.5m,
            sarfNedeni: "Oda temizligi");
        await SeedSarfFisiAsync(
            dbContext,
            sarfFisiId: 1003,
            satirId: 1004,
            stokHareketId: null,
            durum: SarfFisiDurumlari.Taslak,
            tarih: new DateTime(2026, 8, 24, 10, 0, 0),
            miktar: 5,
            maliyetBirimFiyat: null,
            maliyetTutari: null,
            sarfNedeni: "Taslak");

        var service = CreateService(dbContext);
        var result = await service.GetDetayAsync(
            new PagedRequest { PageNumber = 1, PageSize = 10 },
            new SarfTuketimRaporFilterDto { TesisId = 1 });

        var row = Assert.Single(result.Items);
        Assert.Equal(1000, row.SarfFisiId);
        Assert.Equal(3, row.Miktar);
        Assert.Equal(12.5m, row.MaliyetBirimFiyat);
        Assert.Equal(37.5m, row.ToplamMaliyet);
        Assert.Equal("Oda temizligi", row.SarfNedeni);
    }

    [Fact]
    public async Task GetDetayListAsync_IptalEdildiFiltresindeNetTuketimVeMaliyetSifirlanir()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext);
        await SeedSarfFisiAsync(
            dbContext,
            sarfFisiId: 1010,
            satirId: 1011,
            stokHareketId: 1012,
            durum: SarfFisiDurumlari.IptalEdildi,
            tarih: new DateTime(2026, 8, 24, 11, 0, 0),
            miktar: 4,
            maliyetBirimFiyat: 20m,
            maliyetTutari: 80m,
            sarfNedeni: "Iptal");

        var service = CreateService(dbContext);
        var result = await service.GetDetayListAsync(new SarfTuketimRaporFilterDto
        {
            TesisId = 1,
            Durum = SarfFisiDurumlari.IptalEdildi
        });

        var row = Assert.Single(result);
        Assert.Equal(0, row.Miktar);
        Assert.Equal(0m, row.MaliyetBirimFiyat);
        Assert.Equal(0m, row.ToplamMaliyet);
    }

    [Fact]
    public async Task GetDetayListAsync_SnapshotAlanlariniCurrentEntityDegisseBileKullanir()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext);
        await SeedSarfFisiAsync(
            dbContext,
            sarfFisiId: 1020,
            satirId: 1021,
            stokHareketId: 1022,
            durum: SarfFisiDurumlari.Kesinlesti,
            tarih: new DateTime(2026, 8, 24, 12, 0, 0),
            miktar: 2,
            maliyetBirimFiyat: 15m,
            maliyetTutari: 30m,
            stokKoduSnapshot: "STK-OLD",
            tasinirKartAdSnapshot: "Eski Deterjan",
            isletmeAlaniAdSnapshot: "Kat Ofisi",
            odaNoSnapshot: "101A",
            odaBinaAdiSnapshot: "A Blok");

        var kart = await dbContext.Set<TasinirKart>().SingleAsync(x => x.Id == 100);
        kart.StokKodu = "STK-NEW";
        kart.Ad = "Yeni Deterjan";

        var fis = await dbContext.SarfFisleri.SingleAsync(x => x.Id == 1020);
        fis.IsletmeAlaniAdSnapshot = "Kat Ofisi";
        fis.OdaNoSnapshot = "101A";
        fis.OdaBinaAdiSnapshot = "A Blok";

        var alan = await dbContext.IsletmeAlanlari.SingleAsync(x => x.Id == 30);
        alan.OzelAd = "Yeni Alan";

        var oda = await dbContext.Odalar.SingleAsync(x => x.Id == 40);
        oda.OdaNo = "999";

        var bina = await dbContext.Binalar.SingleAsync(x => x.Id == 20);
        bina.Ad = "Z Blok";
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);
        var row = Assert.Single(await service.GetDetayListAsync(new SarfTuketimRaporFilterDto { TesisId = 1 }));

        Assert.Equal("STK-OLD", row.StokKodu);
        Assert.Equal("Eski Deterjan", row.MalzemeAd);
        Assert.Equal("Kat Ofisi", row.IsletmeAlaniAd);
        Assert.Equal("101A - A Blok", row.OdaAd);
    }

    [Fact]
    public async Task GetMalzemeOzetAsync_AyniMalzemeIcinNetTuketimiToplar()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext);
        await SeedSarfFisiAsync(
            dbContext,
            sarfFisiId: 1030,
            satirId: 1031,
            stokHareketId: 1032,
            durum: SarfFisiDurumlari.Kesinlesti,
            tarih: new DateTime(2026, 8, 24, 9, 0, 0),
            miktar: 3,
            maliyetBirimFiyat: 10m,
            maliyetTutari: 30m);
        await SeedSarfFisiAsync(
            dbContext,
            sarfFisiId: 1033,
            satirId: 1034,
            stokHareketId: 1035,
            durum: SarfFisiDurumlari.Kesinlesti,
            tarih: new DateTime(2026, 8, 24, 10, 0, 0),
            miktar: 2,
            maliyetBirimFiyat: 11m,
            maliyetTutari: 22m);

        var service = CreateService(dbContext);
        var row = Assert.Single(await service.GetMalzemeOzetAsync(new SarfTuketimRaporFilterDto { TesisId = 1 }));

        Assert.Equal(5, row.ToplamTuketimMiktari);
        Assert.Equal(2, row.SarfFisiSayisi);
        Assert.Equal(52m, row.ToplamTuketimMaliyeti);
    }

    [Fact]
    public async Task GetKullanimYeriOzetAsync_OdaVeIsletmeAlaninaGoreGruplar()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext);
        await SeedSarfFisiAsync(
            dbContext,
            sarfFisiId: 1040,
            satirId: 1041,
            stokHareketId: 1042,
            durum: SarfFisiDurumlari.Kesinlesti,
            tarih: new DateTime(2026, 8, 24, 9, 0, 0),
            miktar: 1,
            maliyetBirimFiyat: 10m,
            maliyetTutari: 10m);
        await SeedSarfFisiAsync(
            dbContext,
            sarfFisiId: 1043,
            satirId: 1044,
            stokHareketId: 1045,
            durum: SarfFisiDurumlari.Kesinlesti,
            tarih: new DateTime(2026, 8, 24, 10, 0, 0),
            miktar: 2,
            maliyetBirimFiyat: 10m,
            maliyetTutari: 20m,
            tasinirKartId: 101);

        var service = CreateService(dbContext);
        var row = Assert.Single(await service.GetKullanimYeriOzetAsync(new SarfTuketimRaporFilterDto { TesisId = 1 }));

        Assert.Equal("Kat Ofisi", row.IsletmeAlaniAd);
        Assert.Equal("101 - A Blok", row.OdaAd);
        Assert.Equal(2, row.ToplamSarfSatiriSayisi);
        Assert.Equal(2, row.FarkliMalzemeSayisi);
        Assert.Contains("1.00 Adet", row.ToplamMiktarOzeti);
        Assert.Contains("2.00 Lt", row.ToplamMiktarOzeti);
        Assert.Equal(30m, row.ToplamTuketimMaliyeti);
    }

    [Fact]
    public async Task GetDetayAsync_TesisScopeDisindaIseForbiddenDoner()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext);
        await SeedSarfFisiAsync(
            dbContext,
            sarfFisiId: 1050,
            satirId: 1051,
            stokHareketId: 1052,
            durum: SarfFisiDurumlari.Kesinlesti,
            tarih: new DateTime(2026, 8, 24, 9, 0, 0),
            miktar: 1,
            maliyetBirimFiyat: 5m,
            maliyetTutari: 5m);

        var service = CreateService(dbContext, DomainAccessScope.Scoped([], [999], []));
        var ex = await Assert.ThrowsAsync<BaseException>(() => service.GetDetayAsync(
            new PagedRequest(),
            new SarfTuketimRaporFilterDto { TesisId = 1 }));

        Assert.Equal(403, ex.ErrorCode);
        Assert.Equal("Bu tesis için yetkiniz bulunmuyor.", ex.Message);
    }

    private static StysAppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<StysAppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(x => x.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new StysAppDbContext(
            options,
            new FakeCurrentUserAccessor(),
            new FakeCurrentTenantAccessor());
    }

    private static SarfTuketimRaporService CreateService(StysAppDbContext dbContext, DomainAccessScope? scope = null)
        => new(dbContext, new FakeUserAccessScopeService(scope ?? DomainAccessScope.Scoped([], [1], [])));

    private static async Task SeedBaseAsync(StysAppDbContext dbContext)
    {
        dbContext.Kurumlar.Add(new STYS.Kurumlar.Entities.Kurum
        {
            Id = 1,
            Kod = "KRM1",
            Ad = "Kurum 1"
        });

        dbContext.Iller.Add(new STYS.Iller.Entities.Il
        {
            Id = 1,
            Ad = "Ankara"
        });

        dbContext.Tesisler.Add(new Tesis
        {
            Id = 1,
            KurumId = 1,
            IlId = 1,
            Ad = "Tesis 1",
            Telefon = "5551112233",
            Adres = "Adres"
        });

        dbContext.Depolar.Add(new Depo
        {
            Id = 10,
            TesisId = 1,
            Kod = "D-01",
            Ad = "Merkez Depo"
        });

        dbContext.Binalar.Add(new Bina
        {
            Id = 20,
            TesisId = 1,
            Ad = "A Blok",
            KatSayisi = 3
        });

        dbContext.IsletmeAlaniSiniflari.Add(new IsletmeAlaniSinifi
        {
            Id = 21,
            Kod = "OFS",
            Ad = "Ofis"
        });

        dbContext.IsletmeAlanlari.Add(new IsletmeAlani
        {
            Id = 30,
            BinaId = 20,
            IsletmeAlaniSinifiId = 21,
            OzelAd = "Kat Ofisi"
        });

        dbContext.OdaSiniflari.Add(new OdaSinifi
        {
            Id = 22,
            Kod = "STD",
            Ad = "Standart"
        });

        dbContext.OdaTipleri.Add(new OdaTipi
        {
            Id = 23,
            TesisId = 1,
            OdaSinifiId = 22,
            Ad = "Standart Oda"
        });

        dbContext.Odalar.Add(new Oda
        {
            Id = 40,
            OdaNo = "101",
            BinaId = 20,
            TesisOdaTipiId = 23,
            KatNo = 1
        });

        dbContext.Set<TasinirKod>().Add(new TasinirKod
        {
            Id = 50,
            Kod = "150",
            TamKod = "150.01",
            Ad = "Temizlik Malzemeleri",
            DuzeyNo = 2
        });

        dbContext.Set<TasinirKart>().AddRange(
            new TasinirKart
            {
                Id = 100,
                TesisId = 1,
                TasinirKodId = 50,
                StokKodu = "STK-100",
                Ad = "Deterjan",
                Birim = "Adet",
                KdvOrani = 20
            },
            new TasinirKart
            {
                Id = 101,
                TesisId = 1,
                TasinirKodId = 50,
                StokKodu = "STK-101",
                Ad = "Yuzey Temizleyici",
                Birim = "Lt",
                KdvOrani = 20
            });

        await dbContext.SaveChangesAsync();
    }

    private static async Task SeedSarfFisiAsync(
        StysAppDbContext dbContext,
        int sarfFisiId,
        int satirId,
        int? stokHareketId,
        string durum,
        DateTime tarih,
        decimal miktar,
        decimal? maliyetBirimFiyat,
        decimal? maliyetTutari,
        string sarfNedeni = "Genel temizlik",
        int tasinirKartId = 100,
        string? stokKoduSnapshot = null,
        string? tasinirKartAdSnapshot = null,
        string? isletmeAlaniAdSnapshot = "Kat Ofisi",
        string? odaNoSnapshot = "101",
        string? odaBinaAdiSnapshot = "A Blok")
    {
        var kart = await dbContext.Set<TasinirKart>().AsNoTracking().SingleAsync(x => x.Id == tasinirKartId);
        var fis = new SarfFisi
        {
            Id = sarfFisiId,
            TesisId = 1,
            DepoId = 10,
            SarfTarihi = tarih,
            IsletmeAlaniId = 30,
            OdaId = 40,
            IsletmeAlaniAdSnapshot = isletmeAlaniAdSnapshot,
            OdaNoSnapshot = odaNoSnapshot,
            OdaBinaAdiSnapshot = odaBinaAdiSnapshot,
            SarfNedeni = sarfNedeni,
            Durum = durum
        };

        dbContext.SarfFisleri.Add(fis);

        if (stokHareketId.HasValue)
        {
            dbContext.StokHareketleri.Add(new StokHareket
            {
                Id = stokHareketId.Value,
                DepoId = 10,
                TasinirKartId = tasinirKartId,
                HareketTarihi = tarih,
                HareketTipi = StokHareketTipleri.Sarf,
                Miktar = miktar,
                BirimFiyat = maliyetBirimFiyat ?? 0m,
                Tutar = maliyetTutari ?? 0m,
                MaliyetBirimFiyat = maliyetBirimFiyat,
                MaliyetTutari = maliyetTutari,
                KaynakModul = "SarfFisiSatir",
                KaynakId = satirId,
                Durum = StokHareketDurumlari.Aktif
            });
        }

        dbContext.SarfFisiSatirlari.Add(new SarfFisiSatir
        {
            Id = satirId,
            SarfFisiId = sarfFisiId,
            TasinirKartId = tasinirKartId,
            StokHareketId = stokHareketId,
            TakipTipi = TasinirKartTakipTipleri.Yok,
            StokKodu = stokKoduSnapshot ?? kart.StokKodu,
            TasinirKartAd = tasinirKartAdSnapshot ?? kart.Ad,
            Birim = kart.Birim,
            Miktar = miktar
        });

        await dbContext.SaveChangesAsync();
    }

    private sealed class FakeUserAccessScopeService : IUserAccessScopeService
    {
        private readonly DomainAccessScope _scope;

        public FakeUserAccessScopeService(DomainAccessScope scope) => _scope = scope;

        public Task<DomainAccessScope> GetCurrentScopeAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(_scope);
    }

    private sealed class FakeCurrentUserAccessor : ICurrentUserAccessor
    {
        public string? GetCurrentUserName() => "test";
        public Guid? GetCurrentUserId() => Guid.Parse("11111111-1111-1111-1111-111111111111");
    }

    private sealed class FakeCurrentTenantAccessor : ICurrentTenantAccessor
    {
        public int? GetCurrentKurumId() => 1;
        public IReadOnlyList<int> GetAccessibleKurumIds() => [1];
        public bool IsSuperAdmin() => false;
        public bool IsKurumAdmin() => true;
    }
}
